using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Room progression: best score, best stars, completed and unlocked rooms.
/// This is the game's single source of progress, and the offline cache for Cloud Save.
/// Stored as JSON in PlayerPrefs (the same place SettingsMenu keeps its settings).
///
/// Profiles: each signed-in Unity player has its own local save (key + "_" + playerId), so a guest
/// and an account on the same PC never mix. Without a profile (never signed in / offline) the
/// original key is used, and that progress is merged into the first profile that signs in.
///
/// Rules that always hold (locally, when merging with the cloud, and when merging profiles):
/// unlocked / completed / passed never go back to false, best score and stars only go up,
/// and the most recent score comes from whichever run was played last.
///
/// The one exception is RESET PROGRESS (ResetProgress). It stamps the save with the time of the reset,
/// and progress older than that stamp is ignored in every merge, so neither Cloud Save nor another local
/// profile can bring the reset progress back. A newer reset found in the player's own cloud save is
/// applied locally as well.
/// </summary>
public static class LocalDataManager
{
    const string BaseKey = "scamsense_progress_v1";
    const string LegacyMigratedKey = "scamsense_legacy_migrated";

    [Serializable]
    public class LevelProgress
    {
        public string levelId;
        public bool unlocked;
        public bool completed;
        public int bestScore;
        public int bestStars;
        public int lastScore;
        public int lastStars;
        public int timesCompleted;
        /// <summary>True once any run reached the level's passing score (kept even after weaker replays).</summary>
        public bool passed;
        /// <summary>UTC ticks of the last finished run (decides which "last score" wins when merging).</summary>
        public long lastPlayedUtc;
        /// <summary>UTC ticks of the last unlock or finished run (0 in saves made before resets existed).</summary>
        public long changedUtc;
    }

    [Serializable]
    class ProgressData
    {
        public List<LevelProgress> levels = new List<LevelProgress>();
        /// <summary>Local changes not yet confirmed by Cloud Save.</summary>
        public bool pendingSync;
        public long updatedUtc;
        /// <summary>UTC ticks of the last RESET PROGRESS (0 = never reset). Older progress is ignored when merging.</summary>
        public long resetUtc;
    }

    static ProgressData data;
    static string profileId;

    /// <summary>Raised after progress changes because of play or RESET PROGRESS (not after a cloud merge).</summary>
    public static event Action Changed;

    /// <summary>The Unity player ID whose save is active, or null for the default (offline) save.</summary>
    public static string ProfileId => profileId;
    /// <summary>True when there are local results Cloud Save hasn't confirmed yet.</summary>
    public static bool PendingSync => Data.pendingSync;
    /// <summary>Increases on every local change; lets the sync tell if something changed mid-upload.</summary>
    public static int Revision { get; private set; }

    static ProgressData Data
    {
        get
        {
            if (data == null) Load();
            return data;
        }
    }

    static string KeyFor(string profile) => string.IsNullOrEmpty(profile) ? BaseKey : BaseKey + "_" + profile;

    // ---------- Profiles ----------

    /// <summary>
    /// Switches to the save of a Unity player (null = default offline save). The first profile ever
    /// activated on this PC also receives the progress made before accounts existed.
    /// </summary>
    public static void SetProfile(string playerId)
    {
        if (data != null && profileId == playerId) return;
        profileId = playerId;
        Load();

        if (!string.IsNullOrEmpty(playerId) && PlayerPrefs.GetInt(LegacyMigratedKey, 0) == 0)
        {
            var legacy = Read(BaseKey);
            if (legacy != null && legacy.levels.Count > 0)
            {
                MergeInto(data, legacy, adoptReset: false);
                data.pendingSync = true;
                Save(notify: false);
            }
            PlayerPrefs.SetInt(LegacyMigratedKey, 1);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Merges another local profile's progress into the active one (e.g. guest -> signed-in account).</summary>
    public static bool MergeProfileIntoCurrent(string otherProfileId)
    {
        if (otherProfileId == profileId) return false;
        var other = Read(KeyFor(otherProfileId));
        if (other == null || other.levels.Count == 0) return false;
        bool changed = MergeInto(Data, other, adoptReset: false);   // another player's reset never applies here
        if (changed) { data.pendingSync = true; Save(notify: false); }
        return changed;
    }

    // ---------- Progress API (used by the game) ----------

    /// <summary>Returns the saved progress for a level (creates an empty entry if needed).</summary>
    public static LevelProgress Get(string levelId)
    {
        foreach (var lp in Data.levels)
            if (lp.levelId == levelId) return lp;

        var created = new LevelProgress { levelId = levelId };
        Data.levels.Add(created);
        return created;
    }

    /// <summary>Saved progress for a level, or null if there is none (does not create an entry).</summary>
    public static LevelProgress Peek(string levelId)
    {
        foreach (var lp in Data.levels)
            if (lp.levelId == levelId) return lp;
        return null;
    }

    public static bool IsUnlocked(string levelId)
    {
        if (string.IsNullOrEmpty(levelId)) return false;
        foreach (var lp in Data.levels)
            if (lp.levelId == levelId) return lp.unlocked;
        return false;
    }

    public static void Unlock(string levelId)
    {
        if (string.IsNullOrEmpty(levelId)) return;
        var lp = Get(levelId);
        if (lp.unlocked) return;
        lp.unlocked = true;
        lp.changedUtc = DateTime.UtcNow.Ticks;
        MarkChanged();
    }

    /// <summary>Saves a finished run. Best score/stars only go up; unlocked rooms stay unlocked.</summary>
    public static LevelProgress RecordResult(string levelId, int score, int stars, bool passed = false)
    {
        var lp = Get(levelId);
        lp.unlocked = true;
        lp.completed = true;
        lp.passed |= passed;   // never un-pass
        lp.lastScore = score;
        lp.lastStars = stars;
        lp.bestScore = Mathf.Max(lp.bestScore, score);
        lp.bestStars = Mathf.Max(lp.bestStars, stars);
        lp.timesCompleted++;
        lp.lastPlayedUtc = DateTime.UtcNow.Ticks;
        lp.changedUtc = lp.lastPlayedUtc;
        MarkChanged();
        return lp;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>Testing only: makes the next ResetProgress fail as if the save couldn't be written.</summary>
    public static bool FailNextResetForTesting;
#endif

    /// <summary>
    /// RESET PROGRESS: puts the active profile back to a new game (no completed rooms, scores, stars or
    /// unlocks; the first room stays playable through LevelCatalog's own rule). Only this profile's progress
    /// key is written. Settings, account data and other profiles' saves are not touched.
    /// The reset is stamped (see the class notes) and marked for upload, so the player's cloud save receives
    /// it on the next sync instead of restoring the old progress.
    /// Nothing changes unless the new save was written and read back correctly; on failure the previous
    /// save is kept and <paramref name="error"/> explains what happened.
    /// </summary>
    public static bool ResetProgress(out string error)
    {
        error = null;
        string key = KeyFor(profileId);
        bool hadSave = PlayerPrefs.HasKey(key);
        string previous = hadSave ? PlayerPrefs.GetString(key) : null;

        long now = DateTime.UtcNow.Ticks;
        var fresh = new ProgressData
        {
            resetUtc = Math.Max(now, Data.resetUtc + 1),   // always newer than any earlier reset
            pendingSync = true,
            updatedUtc = now,
        };
        string json = JsonUtility.ToJson(fresh);

        try
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (FailNextResetForTesting) { FailNextResetForTesting = false; throw new Exception("simulated save failure"); }
#endif
            PlayerPrefs.SetString(key, json);
            PlayerPrefs.Save();
            if (PlayerPrefs.GetString(key, "") != json) throw new Exception("the new save could not be verified");
        }
        catch (Exception e)
        {
            // Put the previous save back exactly as it was, so the game is never left half reset.
            try
            {
                if (hadSave) PlayerPrefs.SetString(key, previous);
                else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
            catch (Exception restoreError) { Debug.LogError("LocalDataManager: couldn't restore the save after a failed reset. " + restoreError.Message); }
            error = "Your progress couldn't be saved, so nothing was reset. Please try again.";
            Debug.LogWarning("LocalDataManager: reset failed, previous progress kept. " + e.Message);
            return false;
        }

        data = fresh;
        Revision++;
        Changed?.Invoke();   // refreshes listeners and starts a cloud sync when signed in
        return true;
    }

    /// <summary>Wipes the active profile's room progress (settings are not touched). For testing.</summary>
    public static void ResetAll()
    {
        data = new ProgressData();
        PlayerPrefs.DeleteKey(KeyFor(profileId));
        PlayerPrefs.Save();
    }

    // ---------- Cloud sync support ----------

    /// <summary>The active profile's progress as JSON (what Cloud Save stores).</summary>
    public static string ExportJson() => JsonUtility.ToJson(Data);

    /// <summary>
    /// Merges progress downloaded from Cloud Save into the local save using the rules above.
    /// Returns true if anything local changed. Unreadable cloud data is ignored, never applied.
    /// </summary>
    public static bool MergeFromJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return false;
        ProgressData remote;
        try { remote = JsonUtility.FromJson<ProgressData>(json); }
        catch (Exception e) { Debug.LogWarning("LocalDataManager: cloud progress unreadable, keeping local. " + e.Message); return false; }
        if (remote?.levels == null) return false;

        bool changed = MergeInto(Data, remote, adoptReset: true);   // same player: their newer reset applies here too
        if (changed) Save(notify: false);
        return changed;
    }

    /// <summary>Clears the pending flag, but only if nothing changed since <paramref name="revisionUploaded"/>.</summary>
    public static void MarkSynced(int revisionUploaded)
    {
        if (revisionUploaded != Revision || !Data.pendingSync) return;
        data.pendingSync = false;
        Save(notify: false);
    }

    // ---------- Internals ----------

    /// <summary>When an entry last changed (older saves have no changedUtc; their last run is the best guess).</summary>
    static long Stamp(LevelProgress lp) => Math.Max(lp.changedUtc, lp.lastPlayedUtc);

    /// <param name="adoptReset">True only for the same player's own data (Cloud Save): a newer reset in
    /// <paramref name="source"/> clears the older entries in <paramref name="target"/>.</param>
    static bool MergeInto(ProgressData target, ProgressData source, bool adoptReset)
    {
        bool changed = false;
        if (adoptReset && source.resetUtc > target.resetUtc)
        {
            target.levels.RemoveAll(lp => lp == null || Stamp(lp) <= source.resetUtc);
            target.resetUtc = source.resetUtc;
            changed = true;
        }

        foreach (var src in source.levels)
        {
            if (src == null || string.IsNullOrEmpty(src.levelId)) continue;
            if (target.resetUtc > 0 && Stamp(src) <= target.resetUtc) continue;   // from before this save was reset
            LevelProgress dst = null;
            foreach (var lp in target.levels) if (lp.levelId == src.levelId) { dst = lp; break; }
            if (dst == null)
            {
                target.levels.Add(JsonUtility.FromJson<LevelProgress>(JsonUtility.ToJson(src)));
                changed = true;
                continue;
            }

            var before = JsonUtility.ToJson(dst);
            dst.unlocked |= src.unlocked;
            dst.completed |= src.completed;
            dst.passed |= src.passed;
            dst.bestScore = Mathf.Max(dst.bestScore, src.bestScore);
            dst.bestStars = Mathf.Max(dst.bestStars, src.bestStars);
            dst.timesCompleted = Mathf.Max(dst.timesCompleted, src.timesCompleted);
            dst.changedUtc = Math.Max(dst.changedUtc, src.changedUtc);
            if (src.lastPlayedUtc > dst.lastPlayedUtc)
            {
                dst.lastScore = src.lastScore;
                dst.lastStars = src.lastStars;
                dst.lastPlayedUtc = src.lastPlayedUtc;
            }
            if (JsonUtility.ToJson(dst) != before) changed = true;
        }
        return changed;
    }

    static void MarkChanged()
    {
        Data.pendingSync = true;
        Revision++;
        Save(notify: true);
    }

    static ProgressData Read(string key)
    {
        string json = PlayerPrefs.GetString(key, "");
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            var d = JsonUtility.FromJson<ProgressData>(json);
            if (d != null && d.levels == null) d.levels = new List<LevelProgress>();
            return d;
        }
        catch (Exception e)
        {
            Debug.LogWarning("LocalDataManager: save data unreadable (" + key + "). " + e.Message);
            return null;
        }
    }

    static void Load()
    {
        data = Read(KeyFor(profileId)) ?? new ProgressData();
        if (data.levels == null) data.levels = new List<LevelProgress>();
    }

    static void Save(bool notify)
    {
        Data.updatedUtc = DateTime.UtcNow.Ticks;
        PlayerPrefs.SetString(KeyFor(profileId), JsonUtility.ToJson(Data));
        PlayerPrefs.Save();
        if (notify) Changed?.Invoke();
    }

    // Reload from disk on each play session (domain reload may be disabled in the editor).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCache()
    {
        data = null;
        Revision = 0;
        Changed = null;
        // Keep the last profile across scene loads within a session; restore it at startup.
        profileId = PlayerPrefs.GetString("scamsense_active_profile", "");
        if (profileId == "") profileId = null;
    }

    /// <summary>Remembers the active profile so a restart (or offline launch) opens the same save.</summary>
    public static void RememberActiveProfile()
    {
        if (string.IsNullOrEmpty(profileId)) PlayerPrefs.DeleteKey("scamsense_active_profile");
        else PlayerPrefs.SetString("scamsense_active_profile", profileId);
        PlayerPrefs.Save();
    }
}
