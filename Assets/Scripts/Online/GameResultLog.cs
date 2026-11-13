using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Finished runs waiting to be uploaded to Supabase's game_results table (history only; progress and
/// unlocks live in LocalDataManager). Kept per save profile in PlayerPrefs, so results recorded offline
/// upload later, and each run has its own run_id so an upload that is retried is never stored twice.
/// Each run keeps the player's answers: the server (submit_game_result) recalculates the score from them with
/// its own answer key, so the stored result never depends on a score the game reports.
/// </summary>
public static class GameResultLog
{
    const string KeyPrefix = "scamsense_pending_results";
    const int MaxPending = 200;   // oldest dropped beyond this (the progress itself is never lost)

    /// <summary>One answer as the server expects it: the first verdict, its reading time and the follow-up re-check, if any.</summary>
    [Serializable]
    public class Answer
    {
        public string id;
        public string verdict;            // "scam" or "legit"
        public float seconds;
        public string recovery_verdict;   // "" when there was no follow-up answer
        public bool recovery_hint;
    }

    /// <summary>A finished run (field names match public.game_results; score and stars are the game's own result).</summary>
    [Serializable]
    public class Row
    {
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
        public List<Answer> answers = new List<Answer>();   // empty for runs queued before the server score check
    }

    /// <summary>Arguments of the submit_game_result database function (names match its parameters).</summary>
    [Serializable]
    public class SubmitRequest
    {
        public string p_run_id;
        public string p_level_id;
        public List<Answer> p_answers;
        public string p_completed_at;
    }

    /// <summary>What submit_game_result returns: the result the server calculated.</summary>
    [Serializable]
    public class ServerResult
    {
        public int score;
        public int max_score;
        public int stars;
        public bool passed;
        public int correct_answers;
        public int total_questions;
    }

    public static string Verdict(MessageVerdict v) => v == MessageVerdict.Scam ? "scam" : "legit";

    /// <summary>The run's answers in the order they were given, with each follow-up re-check.</summary>
    public static List<Answer> AnswersFrom(ScoringSystem scoring)
    {
        var list = new List<Answer>();
        foreach (var r in scoring.Results)
        {
            if (r.message == null) continue;
            bool rechecked = scoring.TryGetRecovery(r.message.id, out var again, out bool hint);
            list.Add(new Answer
            {
                id = r.message.id,
                verdict = Verdict(r.chosen),
                seconds = r.seconds,   // exactly what the game scored with, so the server gets the same points
                recovery_verdict = rechecked ? Verdict(again) : "",
                recovery_hint = rechecked && hint,
            });
        }
        return list;
    }

    public static string SubmitJson(Row row) => JsonUtility.ToJson(new SubmitRequest
    {
        p_run_id = row.run_id, p_level_id = row.level_id, p_answers = row.answers, p_completed_at = row.completed_at
    });

    [Serializable] class Pending { public List<Row> rows = new List<Row>(); }

    static string KeyFor(string profile) => string.IsNullOrEmpty(profile) ? KeyPrefix : KeyPrefix + "_" + profile;

    /// <summary>Queues a finished run for the active save profile.</summary>
    public static void Add(string levelId, int score, int maxScore, int stars, int correct, int total, bool passed, float answerSeconds,
                           List<Answer> answers)
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
            answers = answers ?? new List<Answer>(),
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
