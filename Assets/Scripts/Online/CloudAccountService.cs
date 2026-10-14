using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using UnityEngine;

/// <summary>
/// Player accounts and cloud progress (Unity Gaming Services).
///   - Guest: Unity Authentication anonymous sign-in. The session token is cached by Unity, so the
///     same guest comes back after a restart on this PC.
///   - Player: Unity Authentication's built-in Username &amp; Password provider (sign up / sign in).
///   - Guest -> account: AddUsernamePasswordAsync links the username to the *current* guest, so the
///     player ID and all cloud data stay the same.
///   - Progress: LocalDataManager is the local cache; this service merges it with Cloud Save
///     (key "progress_v1") and retries uploads while results are pending.
/// One instance lives for the whole session (DontDestroyOnLoad). Passwords are never stored.
/// </summary>
public class CloudAccountService : MonoBehaviour
{
    public enum Mode { None, Guest, Player, Offline }

    const string CloudKey = "progress_v1";
    const string KeyLastProfile = "scamsense_last_profile";
    const string KeyLastName = "scamsense_last_name";
    const string KeySignedOut = "scamsense_signed_out";
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
    public bool ServicesReady { get; private set; }
    public string Username { get; private set; }
    public string LastError { get; private set; }
    public bool SignedIn => ServicesReady && AuthenticationService.Instance.IsSignedIn;
    /// <summary>A previous guest/player session is cached and the player didn't sign out.</summary>
    public bool CanRestore => ServicesReady && AuthenticationService.Instance.SessionTokenExists && PlayerPrefs.GetInt(KeySignedOut, 0) == 0;
    public bool HasOfflineProfile => PlayerPrefs.HasKey(KeyLastProfile);

    public string DisplayName =>
        CurrentMode == Mode.Player ? (string.IsNullOrEmpty(Username) ? "PLAYER" : Username.ToUpperInvariant()) :
        CurrentMode == Mode.Guest ? "GUEST PLAYER" :
        CurrentMode == Mode.Offline ? (PlayerPrefs.GetString(KeyLastName, "") is var n && n != "" ? n.ToUpperInvariant() : "OFFLINE PLAYER") : "";

    public string StatusText =>
        !SignedIn ? "OFFLINE" : (syncing ? "SYNCING" : LocalDataManager.PendingSync ? "SYNC PENDING" : "ONLINE");

    Task initTask;
    bool syncing, subscribed;
    float retryTimer;

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
        if (SignedIn && LocalDataManager.PendingSync) _ = SyncAsync();
        else if (CurrentMode == Mode.Offline) _ = TryReconnectAsync();
    }

    // ---------- Startup ----------

    public Task EnsureInitializedAsync()
    {
        if (initTask == null || initTask.IsFaulted) initTask = InitCoreAsync();
        return initTask;
    }

    async Task InitCoreAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();
        ServicesReady = true;
        if (!subscribed)
        {
            AuthenticationService.Instance.Expired += OnSessionExpired;
            subscribed = true;
        }
    }

    /// <summary>
    /// Signs the returning player back in from Unity's cached session (guest or account).
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
            await AuthenticationService.Instance.SignInAnonymouslyAsync();   // re-uses the cached session token
            await AfterSignInAsync(null);
            return true;
        }
        catch (Exception e)
        {
            LastError = Friendly(e);
            if (IsNetworkError(e) && HasOfflineProfile) { EnterOffline(); return true; }
            return false;
        }
        finally { SetBusy(false); }
    }

    // ---------- Buttons ----------

    /// <summary>PLAY AS GUEST: anonymous sign-in (the same guest as before, if this PC has one).</summary>
    public async Task<bool> PlayAsGuestAsync()
    {
        if (Busy) return false;
        SetBusy(true);
        try
        {
            await EnsureInitializedAsync();
            if (!SignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
            await AfterSignInAsync(null);
            return true;
        }
        catch (Exception e) { LastError = Friendly(e); return false; }
        finally { SetBusy(false); }
    }

    /// <summary>
    /// PLAYER SIGN IN with username + password. If a guest is signed in, the guest's local progress is
    /// merged into the account being signed in to (nothing is discarded).
    /// </summary>
    public async Task<bool> SignInAsync(string username, string password)
    {
        if (Busy) return false;
        string problem = ValidateUsername(username) ?? (string.IsNullOrEmpty(password) ? "Enter your password." : null);
        if (problem != null) { LastError = problem; return false; }

        SetBusy(true);
        string previousProfile = null;
        Mode previousMode = CurrentMode;
        try
        {
            await EnsureInitializedAsync();
            if (SignedIn)
            {
                previousProfile = AuthenticationService.Instance.PlayerId;
                AuthenticationService.Instance.SignOut(false);   // keeps the cached session in case sign-in fails
            }
            await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(username.Trim(), password);
            await AfterSignInAsync(previousMode == Mode.Guest ? previousProfile : null);
            return true;
        }
        catch (Exception e)
        {
            LastError = Friendly(e);
            // Failed: put the previous guest back instead of leaving the player signed out.
            if (previousProfile != null && !SignedIn)
            {
                try { await AuthenticationService.Instance.SignInAnonymouslyAsync(); }
                catch { CurrentMode = Mode.None; }
            }
            return false;
        }
        finally { SetBusy(false); }
    }

    /// <summary>
    /// CREATE ACCOUNT. Signed in as a guest: links the username to the guest (same player ID and
    /// cloud progress). Otherwise: signs up a new player (local progress on this PC is merged in).
    /// </summary>
    public async Task<bool> CreateAccountAsync(string username, string password)
    {
        if (Busy) return false;
        string problem = ValidateUsername(username) ?? ValidatePassword(password);
        if (problem != null) { LastError = problem; return false; }

        SetBusy(true);
        try
        {
            await EnsureInitializedAsync();
            if (SignedIn && CurrentMode == Mode.Guest)
            {
                await AuthenticationService.Instance.AddUsernamePasswordAsync(username.Trim(), password);
                await AfterSignInAsync(null);   // same player: refreshes name + syncs
            }
            else
            {
                if (SignedIn) AuthenticationService.Instance.SignOut(false);
                await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(username.Trim(), password);
                await AfterSignInAsync(null);
            }
            return true;
        }
        catch (Exception e) { LastError = Friendly(e); return false; }
        finally { SetBusy(false); }
    }

    /// <summary>
    /// SIGN OUT. Progress is never deleted. A guest keeps Unity's cached session so PLAY AS GUEST
    /// returns to the same guest; an account clears it (the password is needed next time).
    /// </summary>
    public void SignOut()
    {
        if (ServicesReady && AuthenticationService.Instance.IsSignedIn)
            AuthenticationService.Instance.SignOut(clearCredentials: CurrentMode == Mode.Player);
        PlayerPrefs.SetInt(KeySignedOut, 1);
        PlayerPrefs.Save();
        CurrentMode = Mode.None;
        Username = null;
        Notify();
    }

    /// <summary>PLAY OFFLINE: uses this PC's last save; results upload later when signed in again.</summary>
    public void PlayOffline() => EnterOffline();

    public Task SyncNowAsync() => SyncAsync();

    // ---------- Sign-in completion & sync ----------

    async Task AfterSignInAsync(string mergeFromProfile)
    {
        string id = AuthenticationService.Instance.PlayerId;
        LocalDataManager.SetProfile(id);
        if (!string.IsNullOrEmpty(mergeFromProfile)) LocalDataManager.MergeProfileIntoCurrent(mergeFromProfile);
        LocalDataManager.RememberActiveProfile();

        Username = null;
        try
        {
            var info = await AuthenticationService.Instance.GetPlayerInfoAsync();
            Username = info?.Username;
        }
        catch (Exception e) { Debug.LogWarning("CloudAccountService: couldn't read player info. " + e.Message); }

        CurrentMode = string.IsNullOrEmpty(Username) ? Mode.Guest : Mode.Player;
        PlayerPrefs.SetString(KeyLastProfile, id);
        PlayerPrefs.SetString(KeyLastName, CurrentMode == Mode.Player ? Username : "Guest player");
        PlayerPrefs.DeleteKey(KeySignedOut);
        PlayerPrefs.Save();
        LastError = null;
        Notify();
        await SyncAsync();
    }

    /// <summary>Download -> merge (keeps the best of both) -> upload. Safe to repeat.</summary>
    async Task SyncAsync()
    {
        if (!SignedIn || syncing) return;
        syncing = true;
        Notify();
        try
        {
            var player = CloudSaveService.Instance.Data.Player;
            var loaded = await player.LoadAsync(new HashSet<string> { CloudKey },
                new Unity.Services.CloudSave.Models.Data.Player.LoadOptions());
            if (loaded.TryGetValue(CloudKey, out var item) && item?.Value != null)
                LocalDataManager.MergeFromJson(item.Value.GetAs<string>());

            int revision = LocalDataManager.Revision;
            await player.SaveAsync(new Dictionary<string, object> { { CloudKey, LocalDataManager.ExportJson() } },
                new Unity.Services.CloudSave.Models.Data.Player.SaveOptions());
            LocalDataManager.MarkSynced(revision);
        }
        catch (Exception e)
        {
            LastError = Friendly(e);
            Debug.LogWarning("CloudAccountService: sync failed, will retry. " + e.Message);
        }
        finally
        {
            syncing = false;
            Notify();
        }
    }

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
            if (!CanRestore) return;
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            // Only continue the same save; a different player would need an explicit sign-in.
            if (AuthenticationService.Instance.PlayerId == PlayerPrefs.GetString(KeyLastProfile, ""))
                await AfterSignInAsync(null);
            else AuthenticationService.Instance.SignOut(false);
        }
        catch { /* still offline - try again later */ }
    }

    async void OnSessionExpired()
    {
        try { await AuthenticationService.Instance.SignInAnonymouslyAsync(); }
        catch { EnterOffline(); }
    }

    void EnterOffline()
    {
        string last = PlayerPrefs.GetString(KeyLastProfile, "");
        LocalDataManager.SetProfile(last == "" ? null : last);
        LocalDataManager.RememberActiveProfile();
        CurrentMode = Mode.Offline;
        Notify();
    }

    void SetBusy(bool busy) { Busy = busy; Notify(); }
    void Notify() => StateChanged?.Invoke();

    // ---------- Validation & messages ----------

    static readonly Regex UsernamePattern = new Regex(@"^[A-Za-z0-9.\-@_]{3,20}$");

    /// <summary>Unity's username rules: 3-20 characters; letters, digits and . - @ _ only.</summary>
    public static string ValidateUsername(string u)
    {
        if (string.IsNullOrWhiteSpace(u)) return "Enter a username.";
        u = u.Trim();
        if (u.Length < 3 || u.Length > 20) return "Usernames are 3 to 20 characters long.";
        if (!UsernamePattern.IsMatch(u)) return "Usernames can only use letters, numbers and . - @ _";
        return null;
    }

    /// <summary>Unity's password rules: 8-30 characters with upper case, lower case, a number and a symbol.</summary>
    public static string ValidatePassword(string p)
    {
        if (string.IsNullOrEmpty(p)) return "Enter a password.";
        if (p.Length < 8 || p.Length > 30) return "Passwords are 8 to 30 characters long.";
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

    static bool IsNetworkError(Exception e) =>
        e is RequestFailedException rf &&
        (rf.ErrorCode == CommonErrorCodes.TransportError || rf.ErrorCode == CommonErrorCodes.Timeout ||
         rf.ErrorCode == CommonErrorCodes.ServiceUnavailable);

    static string Friendly(Exception e)
    {
        // Checked first: the server reports a disabled provider as 401 / InvalidParameters.
        string raw = ((e.Message ?? "") + " " + (e.InnerException?.Message ?? "")).ToLowerInvariant();
        if (raw.Contains("provider is not available") || raw.Contains("permission_denied"))
            return "Username sign-in isn't switched on for this game yet. (Developer: Unity Dashboard > Player Authentication > Identity providers > add Username & Password.)";
        if (raw.Contains("wrong_username_password") || raw.Contains("invalid username or password") || raw.Contains("wrong username"))
            return "Wrong username or password.";
        if (raw.Contains("username") && (raw.Contains("exist") || raw.Contains("taken") || raw.Contains("conflict") || raw.Contains("already")))
            return "That username is already taken.";

        if (e is AuthenticationException ae)
        {
            if (ae.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
                return "That username already belongs to another account. Use PLAYER SIGN IN instead - your guest progress will be merged into it.";
            if (ae.ErrorCode == AuthenticationErrorCodes.AccountLinkLimitExceeded)
                return "This guest already has a username. Sign in with it instead.";
            if (ae.ErrorCode == AuthenticationErrorCodes.InvalidParameters)
                return "The username or password doesn't meet the rules.";
            if (ae.ErrorCode == AuthenticationErrorCodes.BannedUser)
                return "This account has been suspended.";
            if (ae.ErrorCode == AuthenticationErrorCodes.InvalidSessionToken)
                return "Your saved session expired. Please sign in again.";
        }
        if (e is RequestFailedException rf)
        {
            if (IsNetworkError(e)) return "Can't reach the server. Check your internet connection, or play offline.";
            if (rf.ErrorCode == CommonErrorCodes.TooManyRequests) return "Too many attempts. Wait a moment and try again.";
            string m = (rf.Message ?? "").ToLowerInvariant();
            if (m.Contains("exist") || m.Contains("taken") || m.Contains("conflict")) return "That username is already taken.";
            if (m.Contains("wrong") || m.Contains("invalid username or password") || m.Contains("unauthorized") || m.Contains("credentials"))
                return "Wrong username or password.";
            if (m.Contains("not enabled") || m.Contains("provider") || m.Contains("disabled"))
                return "Username sign-in isn't switched on for this game yet (Unity Dashboard > Authentication > Identity providers).";
            return "Sign-in failed: " + rf.Message;
        }
        if (e is ServicesInitializationException) return "Online services couldn't start. You can still play offline.";
        return "Something went wrong: " + e.Message;
    }
}
