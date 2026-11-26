using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Finished runs waiting to be uploaded to Supabase (history only; progress and unlocks live in LocalDataManager).
/// Kept per save profile in PlayerPrefs, so results recorded offline upload later, and each run has its own run_id
/// so an upload that is retried is never stored twice (the server answers "duplicate" and nothing changes).
/// Each run also carries its per-question results (question id, first answer right or wrong, recovered, hint),
/// which the server turns into question_results rows for weak-topic reports.
/// Uploads go through the database function submit_game_result (see
/// supabase/migrations/20261126074705_classroom_roles_classes_records.sql), which checks the run, stamps the
/// student's class and attempt number, and never lets a stored result be edited.
/// </summary>
public static class GameResultLog
{
    const string KeyPrefix = "scamsense_pending_results";
    const int MaxPending = 200;   // oldest dropped beyond this (the progress itself is never lost)

    /// <summary>One question of a finished run (field names match what submit_game_result reads).</summary>
    [Serializable]
    public class QuestionAnswer
    {
        public string question_id;
        /// <summary>The first answer was right.</summary>
        public bool correct;
        /// <summary>A wrong first answer was fixed in the follow-up re-check.</summary>
        public bool recovered;
        public bool hint_used;
        public float seconds;
    }

    /// <summary>One finished run (field names match the columns / submit_game_result's JSON).</summary>
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
        public int incorrect_answers;
        public int total_questions;
        public bool passed;
        public bool completed = true;
        public float total_answer_seconds;
        public string completed_at;   // ISO 8601 UTC
        public List<QuestionAnswer> questions = new List<QuestionAnswer>();
    }

    [Serializable] class Pending { public List<Row> rows = new List<Row>(); }

    static string KeyFor(string profile) => string.IsNullOrEmpty(profile) ? KeyPrefix : KeyPrefix + "_" + profile;

    /// <summary>Queues a finished run for the active save profile and returns it.</summary>
    public static Row Add(string levelId, int score, int maxScore, int stars, int correct, int total, bool passed, float answerSeconds,
                          List<QuestionAnswer> questions = null, bool completed = true)
    {
        var row = Make(levelId, score, maxScore, stars, correct, total, passed, answerSeconds, questions, completed, DateTime.UtcNow);
        Enqueue(LocalDataManager.ProfileId, row);
        return row;
    }

    /// <summary>Queues a run for a given save profile (a run already queued - same run_id - is never added twice).</summary>
    public static void Enqueue(string profile, Row row)
    {
        if (row == null || string.IsNullOrEmpty(row.run_id)) return;
        var p = Read(profile);
        if (p.rows.Exists(r => r.run_id == row.run_id)) return;
        p.rows.Add(row);
        if (p.rows.Count > MaxPending) p.rows.RemoveRange(0, p.rows.Count - MaxPending);
        Write(profile, p);
    }

    /// <summary>
    /// Builds a run with every value inside the ranges the server accepts: score 0..max, correct 0..total,
    /// incorrect = total - correct, at most <paramref name="total"/> questions, each question once.
    /// </summary>
    public static Row Make(string levelId, int score, int maxScore, int stars, int correct, int total, bool passed, float answerSeconds,
                           List<QuestionAnswer> questions, bool completed, DateTime finishedUtc)
    {
        total = Mathf.Max(0, total);
        maxScore = Mathf.Max(0, maxScore);
        correct = Mathf.Clamp(correct, 0, total);
        var qs = new List<QuestionAnswer>();
        if (questions != null)
        {
            var seen = new HashSet<string>();
            foreach (var q in questions)
                if (q != null && !string.IsNullOrEmpty(q.question_id) && qs.Count < total && seen.Add(q.question_id))
                    qs.Add(new QuestionAnswer { question_id = q.question_id, correct = q.correct, recovered = q.recovered && !q.correct,
                                                hint_used = q.hint_used, seconds = Mathf.Max(0f, q.seconds) });
        }
        return new Row
        {
            run_id = Guid.NewGuid().ToString(),
            level_id = levelId,
            score = Mathf.Clamp(score, 0, maxScore),
            max_score = maxScore,
            stars = Mathf.Clamp(stars, 0, 3),
            correct_answers = correct,
            incorrect_answers = total - correct,
            total_questions = total,
            passed = passed,
            completed = completed,
            total_answer_seconds = Mathf.Max(0f, answerSeconds),
            completed_at = finishedUtc.ToUniversalTime().ToString("o"),
            questions = qs,
        };
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

    /// <summary>Moves one profile's queued runs to another (guest progress merged into an account). Never duplicates a run.</summary>
    public static void MoveAll(string fromProfile, string toProfile)
    {
        if (fromProfile == toProfile) return;
        var from = Read(fromProfile);
        if (from.rows.Count == 0) return;
        var to = Read(toProfile);
        var have = new HashSet<string>();
        foreach (var r in to.rows) have.Add(r.run_id);
        foreach (var r in from.rows) if (have.Add(r.run_id)) to.rows.Add(r);
        Write(toProfile, to);
        PlayerPrefs.DeleteKey(KeyFor(fromProfile));
        PlayerPrefs.Save();
    }

    /// <summary>Removes every queued run of a profile (tests / RESET PROGRESS of a profile that is never uploaded).</summary>
    public static void Clear(string profile)
    {
        PlayerPrefs.DeleteKey(KeyFor(profile));
        PlayerPrefs.Save();
    }

    // ---------- Upload outcome (what the sync does with each server answer) ----------

    public enum UploadOutcome { Stored, Retry, Rejected }

    /// <summary>
    /// submit_game_result's answer: "recorded" and "duplicate" (already stored by an earlier, retried upload) both mean
    /// the run is safely on the server; "limit" means the player's history is full - nothing to retry either.
    /// </summary>
    public static UploadOutcome OutcomeForStatus(string status) =>
        status == "recorded" || status == "duplicate" || status == "limit" ? UploadOutcome.Stored : UploadOutcome.Retry;

    /// <summary>
    /// A request that failed: network / server problems and expired sessions are retried later; a run the server
    /// refused as invalid (HTTP 400, e.g. a score above the level's maximum) would fail forever, so it is dropped.
    /// </summary>
    public static UploadOutcome OutcomeForError(long httpStatus, bool network)
    {
        if (network || httpStatus == 0 || httpStatus >= 500 || httpStatus == 401 || httpStatus == 403 || httpStatus == 404 || httpStatus == 429)
            return UploadOutcome.Retry;
        return httpStatus == 400 || httpStatus == 409 || httpStatus == 422 ? UploadOutcome.Rejected : UploadOutcome.Retry;
    }

    /// <summary>The JSON body for POST /rest/v1/rpc/submit_game_result.</summary>
    public static string SubmitBody(Row r) => "{\"p_run\":" + JsonUtility.ToJson(r) + "}";

    static Pending Read(string profile)
    {
        string json = PlayerPrefs.GetString(KeyFor(profile), "");
        if (string.IsNullOrEmpty(json)) return new Pending();
        try
        {
            var p = JsonUtility.FromJson<Pending>(json) ?? new Pending();
            foreach (var r in p.rows)
            {
                if (r.questions == null) r.questions = new List<QuestionAnswer>();
                // Runs queued before incorrect_answers existed.
                if (r.incorrect_answers == 0 && r.correct_answers < r.total_questions) r.incorrect_answers = r.total_questions - r.correct_answers;
            }
            return p;
        }
        catch { return new Pending(); }
    }

    static void Write(string profile, Pending p)
    {
        if (p.rows.Count == 0) PlayerPrefs.DeleteKey(KeyFor(profile));
        else PlayerPrefs.SetString(KeyFor(profile), JsonUtility.ToJson(p));
        PlayerPrefs.Save();
    }
}
