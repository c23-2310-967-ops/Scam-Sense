using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Finished runs waiting to be uploaded to Supabase's game_results table (history only; progress and
/// unlocks live in LocalDataManager). Kept per save profile in PlayerPrefs, so results recorded offline
/// upload later, and each run has its own run_id so an upload that is retried is never stored twice.
/// </summary>
public static class GameResultLog
{
    const string KeyPrefix = "scamsense_pending_results";
    const int MaxPending = 200;   // oldest dropped beyond this (the progress itself is never lost)

    /// <summary>One row of public.game_results (field names match the columns).</summary>
    [Serializable]
    public class Row
    {
        public string user_id;
        public string run_id;
        public string level_id;
        public int score;
        public int max_score;
        public int stars;
        public int correct_answers;
        public int total_questions;
        public bool passed;
        public float total_answer_seconds;
        public string completed_at;   // ISO 8601 UTC
    }

    [Serializable] class Pending { public List<Row> rows = new List<Row>(); }

    static string KeyFor(string profile) => string.IsNullOrEmpty(profile) ? KeyPrefix : KeyPrefix + "_" + profile;

    /// <summary>Queues a finished run for the active save profile.</summary>
    public static void Add(string levelId, int score, int maxScore, int stars, int correct, int total, bool passed, float answerSeconds)
    {
        var p = Read(LocalDataManager.ProfileId);
        p.rows.Add(new Row
        {
            run_id = Guid.NewGuid().ToString(),
            level_id = levelId,
            score = Mathf.Max(0, score),
            max_score = Mathf.Max(0, maxScore),
            stars = Mathf.Clamp(stars, 0, 3),
            correct_answers = Mathf.Clamp(correct, 0, Mathf.Max(0, total)),
            total_questions = Mathf.Max(0, total),
            passed = passed,
            total_answer_seconds = Mathf.Max(0f, answerSeconds),
            completed_at = DateTime.UtcNow.ToString("o"),
        });
        if (p.rows.Count > MaxPending) p.rows.RemoveRange(0, p.rows.Count - MaxPending);
        Write(LocalDataManager.ProfileId, p);
    }

    /// <summary>Runs of <paramref name="profile"/> not uploaded yet (oldest first).</summary>
    public static List<Row> PendingFor(string profile) => Read(profile).rows;

    /// <summary>Removes uploaded runs (matched by run_id; runs added meanwhile are kept).</summary>
    public static void MarkUploaded(string profile, List<Row> uploaded)
    {
        var done = new HashSet<string>();
        foreach (var r in uploaded) done.Add(r.run_id);
        var p = Read(profile);
        p.rows.RemoveAll(r => done.Contains(r.run_id));
        Write(profile, p);
    }

    /// <summary>Moves one profile's queued runs to another (guest progress merged into an account).</summary>
    public static void MoveAll(string fromProfile, string toProfile)
    {
        if (fromProfile == toProfile) return;
        var from = Read(fromProfile);
        if (from.rows.Count == 0) return;
        var to = Read(toProfile);
        to.rows.AddRange(from.rows);
        Write(toProfile, to);
        PlayerPrefs.DeleteKey(KeyFor(fromProfile));
        PlayerPrefs.Save();
    }

    static Pending Read(string profile)
    {
        string json = PlayerPrefs.GetString(KeyFor(profile), "");
        if (string.IsNullOrEmpty(json)) return new Pending();
        try { return JsonUtility.FromJson<Pending>(json) ?? new Pending(); }
        catch { return new Pending(); }
    }

    static void Write(string profile, Pending p)
    {
        if (p.rows.Count == 0) PlayerPrefs.DeleteKey(KeyFor(profile));
        else PlayerPrefs.SetString(KeyFor(profile), JsonUtility.ToJson(p));
        PlayerPrefs.Save();
    }
}
