using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Offline save for room progression: best score, best stars, completed and unlocked rooms.
/// Stored as JSON in PlayerPrefs (the same place SettingsMenu keeps its settings).
/// Replaying a level only ever raises the best values - it never deletes progress.
/// </summary>
public static class LocalDataManager
{
    const string SaveKey = "scamsense_progress_v1";

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
    }

    [Serializable]
    class ProgressData
    {
        public List<LevelProgress> levels = new List<LevelProgress>();
    }

    static ProgressData data;

    static ProgressData Data
    {
        get
        {
            if (data == null) Load();
            return data;
        }
    }

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
        Save();
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
        Save();
        return lp;
    }

    /// <summary>Wipes all room progress (settings are not touched). For testing.</summary>
    public static void ResetAll()
    {
        data = new ProgressData();
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
    }

    static void Load()
    {
        data = null;
        string json = PlayerPrefs.GetString(SaveKey, "");
        if (!string.IsNullOrEmpty(json))
        {
            try { data = JsonUtility.FromJson<ProgressData>(json); }
            catch (Exception e) { Debug.LogWarning("LocalDataManager: save data unreadable, starting fresh. " + e.Message); }
        }
        if (data == null) data = new ProgressData();
        if (data.levels == null) data.levels = new List<LevelProgress>();
    }

    static void Save()
    {
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Data));
        PlayerPrefs.Save();
    }

    // Reload from disk on each play session (domain reload may be disabled in the editor).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCache() => data = null;
}
