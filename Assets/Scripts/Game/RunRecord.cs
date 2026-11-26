using System;
using System.Collections.Generic;

/// <summary>
/// Turns a finished run's answers (ScoringSystem.Results: the first answer to each question) into the per-question
/// records that are uploaded with the run, so teachers and students can see which topics were missed.
/// No manual collection: GameManager calls this when the level ends.
/// </summary>
public static class RunRecord
{
    /// <param name="firstAnswers">ScoringSystem.Results (recovery answers are not in this list).</param>
    /// <param name="recovered">ScoringSystem.WasRecovered: a wrong first answer fixed in the follow-up.</param>
    /// <param name="hintUsed">ScoringSystem.HintUsedFor.</param>
    public static List<GameResultLog.QuestionAnswer> FromAnswers(IEnumerable<ScoringSystem.AnswerResult> firstAnswers,
                                                                 Func<string, bool> recovered = null, Func<string, bool> hintUsed = null)
    {
        var list = new List<GameResultLog.QuestionAnswer>();
        if (firstAnswers == null) return list;
        var seen = new HashSet<string>();
        foreach (var a in firstAnswers)
        {
            if (a.message == null || string.IsNullOrEmpty(a.message.id) || !seen.Add(a.message.id)) continue;
            string id = a.message.id;
            list.Add(new GameResultLog.QuestionAnswer
            {
                question_id = id,
                correct = a.isCorrect,
                recovered = !a.isCorrect && recovered != null && recovered(id),
                hint_used = (hintUsed != null && hintUsed(id)) || a.hintUsed,
                seconds = a.seconds < 0f ? 0f : a.seconds,
            });
        }
        return list;
    }
}
