using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Player accounts and cloud progress (Supabase).
///   - Guest: a Supabase anonymous user. Its refresh token is kept on this PC, so the same guest comes back
///     after a restart and after signing out (PLAY AS GUEST).
///   - Player: Supabase email + password account.
///   - Guest -> account: the email + password are added to the current guest user, so the user id and all
///     cloud data stay the same.
///   - Progress: LocalDataManager is the local save and its merge rules decide everything; this service
///     downloads the player's rows (profiles, level_progress, achievements), merges them, uploads the result
///     and the queued finished runs (game_results). Row Level Security on every table limits a player to
///     their own rows.
/// One instance lives for the whole session (DontDestroyOnLoad). Passwords are never stored: only Supabase's
/// refresh token is kept, like any signed-in app. Without Assets/Resources/SupabaseSettings.json the game
/// runs offline only.
/// </summary>
public class CloudAccountService : MonoBehaviour
{
    public enum Mode { None, Guest, Player, Offline }

    const string KeySession = "scamsense_sb_session";              // the current session (refresh token + user)
    const string KeyGuestSession = "scamsense_sb_guest_session";   // this PC's guest, kept while an account is used
    const string KeyLastProfile = "scamsense_last_profile";
    const string KeyLastName = "scamsense_last_name";
    const string KeySignedOut = "scamsense_signed_out";
    /// <summary>This PC's guest save (what PLAY OFFLINE uses after an account signs out).</summary>
    const string KeyLastGuestProfile = "scamsense_last_guest_profile";
    const float RetrySeconds = 20f;

    static CloudAccountService instance;
    public static CloudAccountService Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("CloudAccountService");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<CloudAccountService>();
            }
            return instance;
        }
    }
    public static bool Exists => instance != null;

    /// <summary>Raised when the mode, status or busy state changes (UI refreshes on it).</summary>
    public event Action StateChanged;

    public Mode CurrentMode { get; private set; } = Mode.None;
    public bool Busy { get; private set; }
    /// <summary>True when Supabase is configured for this build (SupabaseSettings.json present and valid).</summary>
    public bool ServicesReady => SupabaseSettings.Configured;
    /// <summary>The player's display name (accounts); null for guests.</summary>
    public string Username { get; private set; }
    public string Email => client != null && client.SignedIn ? client.Session.user.email : null;
    public string LastError { get; private set; }
    /// <summary>Set after CREATE ACCOUNT when Supabase is waiting for the player to confirm their email.</summary>
    public string LastNotice { get; private set; }
    /// <summary>Why the last cloud sync failed (null after a successful sync). Sync retries automatically.</summary>
    public string LastSyncError { get; private set; }
    public bool Syncing => syncing;
    public bool SignedIn => client != null && client.SignedIn;
    /// <summary>True when the active local save is the signed-in player's, i.e. the one SyncAsync uploads.</summary>
    public bool SyncsActiveSave => SignedIn && client.UserId == LocalDataManager.ProfileId;
    /// <summary>A previous guest/player session is stored and the player didn't sign out.</summary>
    public bool CanRestore => ServicesReady && StoredSession(KeySession) != null && PlayerPrefs.GetInt(KeySignedOut, 0) == 0;
    public bool HasOfflineProfile => PlayerPrefs.HasKey(KeyLastProfile);

    public string DisplayName =>
        CurrentMode == Mode.Player ? (string.IsNullOrEmpty(Username) ? "PLAYER" : Username.ToUpperInvariant()) :
        CurrentMode == Mode.Guest ? "GUEST PLAYER" :
        CurrentMode == Mode.Offline ? (PlayerPrefs.GetString(KeyLastName, "") is var n && n != "" ? n.ToUpperInvariant() : "OFFLINE PLAYER") : "";

    public string StatusText =>
        !SignedIn ? "OFFLINE" : (syncing ? "SYNCING" : LocalDataManager.PendingSync || GameResultLog.PendingFor(LocalDataManager.ProfileId).Count > 0 ? "SYNC PENDING" : "ONLINE");

    SupabaseClient client;
    bool syncing;
    float retryTimer;

    /// <summary>What is kept on disk for a session: never the password, never the access token.</summary>
    [Serializable] class StoredTokens { public string refresh_token; public string user_id; public bool anonymous; }

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
    }

    void OnEnable() => LocalDataManager.Changed += OnLocalProgressChanged;
    void OnDisable() => LocalDataManager.Changed -= OnLocalProgressChanged;

    void Update()
    {
        // Retry: pending uploads while signed in, or a quiet reconnect while playing offline.
        retryTimer += Time.unscaledDeltaTime;
        if (retryTimer < RetrySeconds || Busy || syncing) return;
        retryTimer = 0f;
        if (SignedIn && (LocalDataManager.PendingSync || GameResultLog.PendingFor(LocalDataManager.ProfileId).Count > 0)) _ = SyncAsync();
        else if (CurrentMode == Mode.Offline) _ = TryReconnectAsync();
    }

    // ---------- Startup ----------

    /// <summary>Creates the Supabase client (no network). Throws if Supabase isn't configured for this build.</summary>
    public Task EnsureInitializedAsync()
    {
        if (client != null) return Task.CompletedTask;
        var settings = SupabaseSettings.Current;
        if (settings == null) throw new SupabaseException("Online accounts aren't set up in this build.");
        client = new SupabaseClient(settings);
        client.SessionChanged += OnSessionChanged;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Signs the returning player back in from the stored session (guest or account).
    /// Returns false if there is nothing to restore. Throws nothing: offline falls back to the local save.
    /// </summary>
    public async Task<bool> TryRestoreAsync()
    {
        if (Busy) return false;
        SetBusy(true);
        try
        {
            await EnsureInitializedAsync();
            if (SignedIn) { await AfterSignInAsync(null); return true; }
            if (!CanRestore) return false;
            await client.RefreshAsync(StoredSession(KeySession).refresh_token);
            await AfterSignInAsync(null);
            return true;
        }
        catch (Exception e)
        {
            LastError = Friendly(e);
            if (IsNetworkError(e) && HasOfflineProfile) { EnterOffline(); return true; }
            if (e is SupabaseException se && !se.IsNetwork) PlayerPrefs.DeleteKey(KeySession);   // expired / revoked: sign in again
            return false;
        }
        finally { SetBusy(false); }
    }

    // ---------- Buttons ----------

    /// <summary>PLAY AS GUEST: continues this PC's guest if it has one, otherwise creates a new anonymous user.</summary>
    public async Task<bool> PlayAsGuestAsync()
    {
        if (Busy) return false;
        SetBusy(true);
        try
        {
            await EnsureInitializedAsync();
            if (!SignedIn)
            {
                var guest = StoredSession(KeyGuestSession);
                bool restored = false;
                if (guest != null)
                {
                    try { await client.RefreshAsync(guest.refresh_token); restored = true; }
                    catch (SupabaseException se) when (!se.IsNetwork) { PlayerPrefs.DeleteKey(KeyGuestSession); }   // gone: new guest
                }
                if (!restored) await client.SignInAnonymouslyAsync();
            }
            await AfterSignInAsync(null);
            return true;
        }
        catch (Exception e) { LastError = Friendly(e); return false; }
        finally { SetBusy(false); }
    }

    /// <summary>
    /// PLAYER SIGN IN with email + password. If a guest is signed in, the guest's local progress is merged
    /// into the account being signed in to (nothing is discarded), and the guest can still be resumed later.
    /// </summary>
    public async Task<bool> SignInAsync(string email, string password)
    {
        if (Busy) return false;
        string problem = ValidateEmail(email) ?? (string.IsNullOrEmpty(password) ? "Enter your password." : null);
        if (problem != null) { LastError = problem; return false; }

        SetBusy(true);
        string previousProfile = null;
        Mode previousMode = CurrentMode;
        try
        {
            await EnsureInitializedAsync();
            if (SignedIn)
            {
                previousProfile = client.UserId;
                if (previousMode == Mode.Guest) KeepGuestSession();
                client.ForgetSession();   // the old session stays valid on the server in case sign-in fails
            }
            await client.SignInWithPasswordAsync(email.Trim(), password);
            await AfterSignInAsync(previousMode == Mode.Guest ? previousProfile : null);
            return true;
        }
        catch (Exception e)
        {
            LastError = Friendly(e);
            // Failed: put the previous guest back instead of leaving the player signed out.
            if (previousMode == Mode.Guest && !SignedIn && StoredSession(KeyGuestSession) is StoredTokens g)
            {
                try { await client.RefreshAsync(g.refresh_token); CurrentMode = Mode.Guest; }
                catch { CurrentMode = Mode.None; }
            }
            return false;
        }
        finally { SetBusy(false); }
    }

    /// <summary>
    /// CREATE ACCOUNT. Signed in as a guest: adds the email + password to the guest (same user id and cloud
    /// progress, nothing is copied or reset). Otherwise: signs up a new player with its own new save.
    /// If the Supabase project requires email confirmation, the player is told to confirm, then sign in.
    /// </summary>
    public async Task<bool> CreateAccountAsync(string email, string password, string displayName)
    {
        if (Busy) return false;
        string problem = ValidateDisplayName(displayName) ?? ValidateEmail(email) ?? ValidatePassword(password);
        if (problem != null) { LastError = problem; return false; }

        SetBusy(true);
        LastNotice = null;
        email = email.Trim();
        displayName = displayName.Trim();
        try
        {
            await EnsureInitializedAsync();
            if (SignedIn && CurrentMode == Mode.Guest)
            {
                var user = await client.UpdateUserAsync(email, password, displayName);
                // The guest's access token still says is_anonymous = true. Once the user is permanent, get a
                // new token so the database treats it as an account (guests can't change the display name).
                if (user != null && !user.is_anonymous) await client.RefreshAsync(client.Session.refresh_token);
                await SaveDisplayNameAsync(displayName);
                if (user != null && !string.IsNullOrEmpty(user.new_email) && string.IsNullOrEmpty(user.email))
                    LastNotice = $"Almost done: confirm the email sent to {email}. Until then you keep playing as this guest - your progress is safe.";
                await AfterSignInAsync(null);   // same user: refreshes name + syncs
            }
            else
            {
                if (SignedIn) client.ForgetSession();
                bool signedIn = await client.SignUpAsync(email, password, displayName);
                if (!signedIn)
                {
                    LastNotice = $"Account created. Confirm the email sent to {email}, then use PLAYER SIGN IN.";
                    LastError = LastNotice;
                    return false;
                }
                await AfterSignInAsync(null);
            }
            return true;
        }
        catch (Exception e) { LastError = Friendly(e); return false; }
        finally { SetBusy(false); }
    }

    /// <summary>
    /// SIGN OUT. Progress is never deleted: it stays in the player's local save and in Supabase.
    /// A guest keeps its stored session so PLAY AS GUEST returns to the same guest, and the guest's save stays
    /// active. An account ends its session (the password is needed next time) and its save is put away: the
    /// menu and PLAY OFFLINE go back to this PC's guest save, so whoever plays next can't add results to it.
    /// </summary>
    /// <returns>False if an account operation is still running (nothing was changed).</returns>
    public bool SignOut()
    {
        if (Busy) return false;
        bool wasAccount = CurrentMode == Mode.Player;
        if (SignedIn)
        {
            if (wasAccount) _ = client.SignOutAsync();   // ends the session on the server too (best effort)
            else { KeepGuestSession(); client.ForgetSession(); }
        }
        PlayerPrefs.DeleteKey(KeySession);
        PlayerPrefs.SetInt(KeySignedOut, 1);
        if (wasAccount)
        {
            string guest = PlayerPrefs.GetString(KeyLastGuestProfile, "");
            if (guest == "") PlayerPrefs.DeleteKey(KeyLastProfile);
            else PlayerPrefs.SetString(KeyLastProfile, guest);
            PlayerPrefs.SetString(KeyLastName, guest == "" ? "" : "Guest player");
            LocalDataManager.SetProfile(guest == "" ? null : guest);
            LocalDataManager.RememberActiveProfile();
        }
        PlayerPrefs.Save();
        CurrentMode = Mode.None;
        Username = null;
        Notify();
        return true;
    }

    /// <summary>PLAY OFFLINE: uses this PC's last save; results upload later when signed in again.</summary>
    public void PlayOffline() => EnterOffline();

    public Task SyncNowAsync() => SyncAsync();

    // ---------- Sign-in completion & sync ----------

    async Task AfterSignInAsync(string mergeFromProfile)
    {
        string id = client.UserId;
        bool anonymous = client.Session.user.is_anonymous;
        LocalDataManager.SetProfile(id);
        if (!string.IsNullOrEmpty(mergeFromProfile))
        {
            LocalDataManager.MergeProfileIntoCurrent(mergeFromProfile);
            GameResultLog.MoveAll(mergeFromProfile, id);
        }
        LocalDataManager.RememberActiveProfile();

        Username = anonymous ? null : await LoadDisplayNameAsync();
        CurrentMode = anonymous ? Mode.Guest : Mode.Player;
        if (CurrentMode == Mode.Guest) PlayerPrefs.SetString(KeyLastGuestProfile, id);
        else if (PlayerPrefs.GetString(KeyLastGuestProfile, "") == id)   // this guest became an account
        {
            PlayerPrefs.DeleteKey(KeyLastGuestProfile);
            PlayerPrefs.DeleteKey(KeyGuestSession);
        }
        PlayerPrefs.SetString(KeyLastProfile, id);
        PlayerPrefs.SetString(KeyLastName, CurrentMode == Mode.Player ? Username : "Guest player");
        PlayerPrefs.DeleteKey(KeySignedOut);
        PlayerPrefs.Save();
        LastError = null;
        Notify();
        await SyncAsync();
    }

    [Serializable] class ProfileRow { public string id; public string display_name; public long progress_reset_utc; }

    /// <summary>The account's display name from its profile row (created by the database trigger, or here if missing).</summary>
    async Task<string> LoadDisplayNameAsync()
    {
        string fallback = client.Session.user.user_metadata != null && !string.IsNullOrEmpty(client.Session.user.user_metadata.display_name)
            ? client.Session.user.user_metadata.display_name
            : (client.Session.user.email ?? "Player").Split('@')[0];
        try
        {
            var rows = SupabaseClient.ParseArray<ProfileRow>(await client.SelectAsync($"profiles?id=eq.{client.UserId}&select=id,display_name,progress_reset_utc"));
            if (rows.Count > 0 && !string.IsNullOrEmpty(rows[0].display_name)) return rows[0].display_name;
            await client.UpsertAsync("profiles", SupabaseClient.ToJsonArray(new List<ProfileRow> { new ProfileRow { id = client.UserId, display_name = Clip(fallback, 32) } }),
                                     "id", ignoreDuplicates: true);
        }
        catch (Exception e) { Debug.LogWarning("CloudAccountService: couldn't read the profile. " + e.Message); }
        return fallback;
    }

    [Serializable] class NameUpdate { public string display_name; }

    async Task SaveDisplayNameAsync(string displayName)
    {
        try { await client.UpdateAsync($"profiles?id=eq.{client.UserId}", JsonUtility.ToJson(new NameUpdate { display_name = Clip(displayName, 32) })); }
        catch (Exception e) { Debug.LogWarning("CloudAccountService: couldn't save the display name. " + e.Message); }
    }

    // Rows exactly as the tables store them (snake_case = column names).
    [Serializable]
    class LevelRow
    {
        public string user_id, level_id;
        public bool unlocked, completed, passed, perfect;
        public int best_score, best_stars, last_score, last_stars, times_completed, best_xp, best_correct, message_count;
        public long last_played_utc, changed_utc;
    }

    [Serializable] class AchievementRow { public string user_id, achievement_id; public long unlocked_utc; }

    /// <summary>The fields of LocalDataManager's save that travel to and from the cloud (same JSON names).</summary>
    [Serializable]
    class CloudProgress
    {
        public List<LocalDataManager.LevelProgress> levels = new List<LocalDataManager.LevelProgress>();
        public long resetUtc;
        public List<LocalDataManager.AchievementRecord> achievements = new List<LocalDataManager.AchievementRecord>();
    }

    /// <summary>
    /// Download -> merge (keeps the best of both, LocalDataManager's rules) -> upload. Safe to repeat.
    /// Only ever syncs the local save that belongs to the signed-in player, and abandons the sync if the
    /// player changes while a request is in flight (sign out / switch account), so one player's cloud
    /// progress can never be merged into, or uploaded as, another player's save.
    /// </summary>
    async Task SyncAsync()
    {
        if (!SignedIn || syncing) return;
        string uid = client.UserId;
        if (string.IsNullOrEmpty(uid) || uid != LocalDataManager.ProfileId) return;

        syncing = true;
        bool abandoned = false;
        Notify();
        try
        {
            // 1) Download this player's rows (RLS returns only their own; the filter makes it explicit).
            var profile = SupabaseClient.ParseArray<ProfileRow>(await client.SelectAsync($"profiles?id=eq.{uid}&select=id,display_name,progress_reset_utc"));
            var levels = SupabaseClient.ParseArray<LevelRow>(await client.SelectAsync($"level_progress?user_id=eq.{uid}&select=*"));
            var achievements = SupabaseClient.ParseArray<AchievementRow>(await client.SelectAsync($"achievements?user_id=eq.{uid}&select=*"));
            if (!SamePlayer(uid)) { abandoned = true; return; }
            long remoteReset = profile.Count > 0 ? profile[0].progress_reset_utc : 0;

            // 2) Merge into the local save.
            var remote = new CloudProgress { resetUtc = remoteReset };
            foreach (var r in levels) remote.levels.Add(ToLocal(r));
            foreach (var a in achievements) remote.achievements.Add(new LocalDataManager.AchievementRecord { id = a.achievement_id, unlockedUtc = a.unlocked_utc });
            LocalDataManager.MergeFromJson(JsonUtility.ToJson(remote));

            // 3) Upload the merged result.
            int revision = LocalDataManager.Revision;
            var local = JsonUtility.FromJson<CloudProgress>(LocalDataManager.ExportJson());
            if (local.resetUtc > remoteReset)
            {
                // RESET PROGRESS on this PC: record it, then remove the cloud rows from before the reset.
                long r = local.resetUtc;
                if (profile.Count == 0)
                    await client.UpsertAsync("profiles", SupabaseClient.ToJsonArray(new List<ProfileRow> { new ProfileRow { id = uid, display_name = Clip(Username ?? "Guest", 32), progress_reset_utc = r } }), "id");
                else
                    await client.UpdateAsync($"profiles?id=eq.{uid}", "{\"progress_reset_utc\":" + r + "}");
                await client.DeleteAsync($"level_progress?user_id=eq.{uid}&changed_utc=lte.{r}&last_played_utc=lte.{r}");
                await client.DeleteAsync($"achievements?user_id=eq.{uid}&unlocked_utc=lte.{r}");
            }
            if (local.levels.Count > 0)
            {
                var rows = new List<LevelRow>();
                foreach (var lp in local.levels) if (lp != null && !string.IsNullOrEmpty(lp.levelId)) rows.Add(ToRow(uid, lp));
                await client.UpsertAsync("level_progress", SupabaseClient.ToJsonArray(rows), "user_id,level_id");
            }
            if (local.achievements.Count > 0)
            {
                var rows = new List<AchievementRow>();
                foreach (var a in local.achievements)
                    if (a != null && !string.IsNullOrEmpty(a.id)) rows.Add(new AchievementRow { user_id = uid, achievement_id = a.id, unlocked_utc = a.unlockedUtc });
                await client.UpsertAsync("achievements", SupabaseClient.ToJsonArray(rows), "user_id,achievement_id", ignoreDuplicates: true);
            }
            if (!SamePlayer(uid)) { abandoned = true; return; }
            LocalDataManager.MarkSynced(revision);

            // 4) Finished runs (history). run_id makes a retried upload harmless.
            var pending = new List<GameResultLog.Row>(GameResultLog.PendingFor(uid));
            if (pending.Count > 0)
            {
                foreach (var p in pending) p.user_id = uid;
                await client.UpsertAsync("game_results", SupabaseClient.ToJsonArray(pending), "user_id,run_id", ignoreDuplicates: true);
                GameResultLog.MarkUploaded(uid, pending);
            }
            LastSyncError = null;
        }
        catch (Exception e)
        {
            LastSyncError = Friendly(e);
            Debug.LogWarning("CloudAccountService: sync failed, will retry. " + e.Message);
            if (e is SupabaseException se && se.IsAuth && !se.IsNetwork) OnSessionLost();
        }
        finally
        {
            syncing = false;
            Notify();
        }

        // The player changed mid-sync: sync the new player's own save instead (never the old one's data).
        if (abandoned && SignedIn) _ = SyncAsync();
    }

    static LocalDataManager.LevelProgress ToLocal(LevelRow r) => new LocalDataManager.LevelProgress
    {
        levelId = r.level_id, unlocked = r.unlocked, completed = r.completed, passed = r.passed,
        bestScore = r.best_score, bestStars = r.best_stars, lastScore = r.last_score, lastStars = r.last_stars,
        timesCompleted = r.times_completed, lastPlayedUtc = r.last_played_utc, changedUtc = r.changed_utc,
        bestXp = r.best_xp, bestCorrect = r.best_correct, messageCount = r.message_count, perfect = r.perfect,
    };

    static LevelRow ToRow(string uid, LocalDataManager.LevelProgress lp) => new LevelRow
    {
        user_id = uid, level_id = lp.levelId, unlocked = lp.unlocked, completed = lp.completed, passed = lp.passed,
        best_score = Mathf.Max(0, lp.bestScore), best_stars = Mathf.Clamp(lp.bestStars, 0, 3),
        last_score = Mathf.Max(0, lp.lastScore), last_stars = Mathf.Clamp(lp.lastStars, 0, 3),
        times_completed = Mathf.Max(0, lp.timesCompleted), last_played_utc = lp.lastPlayedUtc, changed_utc = lp.changedUtc,
        best_xp = Mathf.Max(0, lp.bestXp), best_correct = Mathf.Max(0, lp.bestCorrect), message_count = Mathf.Max(0, lp.messageCount), perfect = lp.perfect,
    };

    /// <summary>True if the same player is still signed in and their save is still the active one.</summary>
    bool SamePlayer(string uid) => SignedIn && client.UserId == uid && LocalDataManager.ProfileId == uid;

    void OnLocalProgressChanged()
    {
        Notify();
        if (SignedIn) _ = SyncAsync();
    }

    async Task TryReconnectAsync()
    {
        try
        {
            await EnsureInitializedAsync();
            var stored = StoredSession(KeySession);
            if (stored == null || PlayerPrefs.GetInt(KeySignedOut, 0) == 1) return;
            // Only continue the same save; a different player would need an explicit sign-in.
            if (stored.user_id != PlayerPrefs.GetString(KeyLastProfile, "")) return;
            await client.RefreshAsync(stored.refresh_token);
            await AfterSignInAsync(null);
        }
        catch (SupabaseException se) when (!se.IsNetwork)
        {
            PlayerPrefs.DeleteKey(KeySession);   // expired or revoked: the player signs in again from the menu
        }
        catch { /* still offline - try again later */ }
    }

    /// <summary>The session can't be renewed (revoked, expired too long): keep playing offline on the local save.</summary>
    void OnSessionLost()
    {
        if (CurrentMode == Mode.Guest) KeepGuestSession();
        client?.ForgetSession();
        PlayerPrefs.DeleteKey(KeySession);
        LastError = "Your session expired. Sign in again to sync - your progress is safe on this PC.";
        EnterOffline();
    }

    void EnterOffline()
    {
        string last = PlayerPrefs.GetString(KeyLastProfile, "");
        LocalDataManager.SetProfile(last == "" ? null : last);
        LocalDataManager.RememberActiveProfile();
        CurrentMode = Mode.Offline;
        Notify();
    }

    // ---------- Stored sessions ----------

    void OnSessionChanged(SupabaseSession s)
    {
        if (s == null || !s.HasTokens) return;   // forgetting a session never deletes what is stored (sign out decides)
        Store(KeySession, s);
        if (s.user.is_anonymous && PlayerPrefs.GetString(KeyLastGuestProfile, "") == s.user.id) Store(KeyGuestSession, s);
    }

    void KeepGuestSession()
    {
        if (SignedIn && client.Session.user.is_anonymous) Store(KeyGuestSession, client.Session);
    }

    static void Store(string key, SupabaseSession s)
    {
        PlayerPrefs.SetString(key, JsonUtility.ToJson(new StoredTokens { refresh_token = s.refresh_token, user_id = s.user.id, anonymous = s.user.is_anonymous }));
        PlayerPrefs.Save();
    }

    static StoredTokens StoredSession(string key)
    {
        string json = PlayerPrefs.GetString(key, "");
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            var t = JsonUtility.FromJson<StoredTokens>(json);
            return t != null && !string.IsNullOrEmpty(t.refresh_token) ? t : null;
        }
        catch { return null; }
    }

    void SetBusy(bool busy) { Busy = busy; Notify(); }
    void Notify() => StateChanged?.Invoke();

    static string Clip(string s, int max) => string.IsNullOrEmpty(s) ? s : s.Length <= max ? s : s.Substring(0, max);

    // ---------- Validation & messages ----------

    static readonly Regex EmailPattern = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$");

    public static string ValidateEmail(string e)
    {
        if (string.IsNullOrWhiteSpace(e)) return "Enter your email address.";
        e = e.Trim();
        if (e.Length > 254 || !EmailPattern.IsMatch(e)) return "That doesn't look like an email address.";
        return null;
    }

    public static string ValidateDisplayName(string n)
    {
        if (string.IsNullOrWhiteSpace(n)) return "Enter a display name.";
        n = n.Trim();
        if (n.Length < 2 || n.Length > 20) return "Display names are 2 to 20 characters long.";
        foreach (char c in n) if (char.IsControl(c) || c == '<' || c == '>') return "Display names can't use < > or control characters.";
        return null;
    }

    /// <summary>8-72 characters with upper case, lower case, a number and a symbol (Supabase allows up to 72).</summary>
    public static string ValidatePassword(string p)
    {
        if (string.IsNullOrEmpty(p)) return "Enter a password.";
        if (p.Length < 8 || p.Length > 72) return "Passwords are 8 to 72 characters long.";
        bool upper = false, lower = false, digit = false, symbol = false;
        foreach (char c in p)
        {
            if (char.IsUpper(c)) upper = true;
            else if (char.IsLower(c)) lower = true;
            else if (char.IsDigit(c)) digit = true;
            else symbol = true;
        }
        if (!(upper && lower && digit && symbol)) return "Use at least one upper-case letter, one lower-case letter, one number and one symbol.";
        return null;
    }

    static bool IsNetworkError(Exception e) => e is SupabaseException se && se.IsNetwork;

    static string Friendly(Exception e)
    {
        if (!(e is SupabaseException se)) return "Something went wrong: " + e.Message;
        if (se.IsNetwork) return "Can't reach the server. Check your internet connection, or play offline.";
        string code = (se.Code ?? "").ToLowerInvariant(), msg = (se.Message ?? "").ToLowerInvariant();
        bool Has(string s) => code.Contains(s) || msg.Contains(s);

        if (Has("not set up")) return se.Message;
        if (Has("invalid_credentials") || Has("invalid login")) return "Wrong email or password.";
        if (Has("email_not_confirmed") || Has("not confirmed")) return "Confirm your email first (check your inbox), then sign in.";
        if (Has("user_already_exists") || Has("email_exists") || Has("already registered") || Has("already been registered"))
            return "That email already has an account. Use PLAYER SIGN IN instead - guest progress is merged into it.";
        if (Has("weak_password") || Has("password should")) return "That password is too weak. " + se.Message;
        if (Has("anonymous_provider_disabled") || Has("anonymous sign-ins are disabled"))
            return "Guest play isn't switched on for this game yet. (Developer: Supabase > Authentication > Sign In / Providers > Allow anonymous sign-ins.)";
        if (Has("signup_disabled") || Has("signups not allowed")) return "New accounts are switched off for this game right now.";
        if (Has("email_address_invalid") || Has("invalid format")) return "That email address isn't accepted.";
        if (Has("rate_limit") || se.Status == 429) return "Too many attempts. Wait a minute and try again.";
        if (Has("refresh_token") || Has("session_not_found") || Has("bad_jwt")) return "Your saved session expired. Please sign in again.";
        if (se.Status == 404 || Has("pgrst205") || Has("relation") || Has("schema cache"))
            return "The game's database tables aren't set up yet. (Developer: run supabase/migrations/20261020022638_scam_sense_schema.sql.)";
        if (Has("42501") || Has("row-level security")) return "The server refused to save this data (permission). Please sign in again.";
        return "Request failed: " + se.Message;
    }
}
