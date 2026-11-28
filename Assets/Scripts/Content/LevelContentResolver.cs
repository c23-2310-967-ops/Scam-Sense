using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>The level a scene will play, with where its questions came from and any problems found.</summary>
public class ResolvedLevel
{
    /// <summary>A runtime copy of the level asset with the content applied (the asset itself is never changed).</summary>
    public LevelDefinition level;
    public ContentSource source;
    public string contentVersion = "";
    public readonly List<ContentIssue> issues = new List<ContentIssue>();
    /// <summary>False = no valid question anywhere; show the content error screen instead of starting.</summary>
    public bool Playable => level != null && level.MessageCount > 0;
}

/// <summary>
/// Builds the level a scene plays from the question bank:
///   active, valid questions whose levelId is this level, in "order" -> the level's messages;
///   the level record -> briefing, objectives, case notes, lessons, tutorial settings, inbox;
///   star and unlock scores worked out from the level's own scoring rules and question count (LevelContentRules).
/// Fallbacks: no usable bank, no record for this level, or no playable question -> the questions baked into the
/// LevelDefinition asset (kept in step by Tools > Scam Sense > Content > Bake Fallback Content into Levels).
/// Scene, scoring rules, unlock chain, timing and everything else stay as authored in the asset.
/// </summary>
public static class LevelContentResolver
{
    public static ResolvedLevel Resolve(LevelDefinition authored) => Resolve(authored, QuestionBankRepository.Current);

    public static ResolvedLevel Resolve(LevelDefinition authored, LoadedQuestionBank bank)
    {
        var result = new ResolvedLevel();
        if (authored == null) return result;

        var level = Object.Instantiate(authored);
        level.name = authored.name;
        result.level = level;

        var room = bank != null && bank.Usable ? bank.Level(authored.levelId) : null;
        if (room == null)
        {
            result.source = ContentSource.LevelAsset;
            if (bank != null && bank.Usable)
                result.issues.Add(new ContentIssue(ContentSeverity.Warning, authored.levelId, "not in the question bank; using the level's built-in questions"));
            Finish(result, authored);
            return result;
        }

        var messages = new List<ScamMessage>();
        foreach (var q in ActiveQuestions(bank.data, authored.levelId))
        {
            var problems = QuestionBankValidator.ValidateQuestion(q, room.tutorial);
            result.issues.AddRange(problems.Where(p => p.IsError));
            if (problems.Any(p => p.IsError)) continue;
            messages.Add(QuestionMapper.ToMessage(q, room.tutorial));
        }

        if (messages.Count == 0)
        {
            result.source = ContentSource.LevelAsset;
            result.issues.Add(new ContentIssue(ContentSeverity.Warning, authored.levelId, "no playable question in the bank; using the level's built-in questions"));
            Finish(result, authored);
            return result;
        }

        result.source = bank.source;
        result.contentVersion = bank.data.contentVersion ?? "";
        level.messages = messages;
        level.tutorial = room.tutorial;
        level.tutorialFirstQuestion = room.tutorial && messages.Any(m => m.id == room.firstQuestion) ? room.firstQuestion : "";
        if (room.inbox != null && room.inbox.Count > 0)
            level.inboxExtras = room.inbox.Where(m => m != null && QuestionMapper.TryParse(m.messageType, out MessageChannel _))
                                          .Select(m => QuestionMapper.ToMessage(m, false)).ToList();
        LevelContentRules.ApplyThresholds(level, messages.Count);
        LevelContentRules.ApplyTexts(level, room);
        Finish(result, authored);
        return result;
    }

    /// <summary>Active questions assigned to a level, in play order (order, then file order).</summary>
    public static List<QuestionRecord> ActiveQuestions(QuestionBankFile bank, string levelId)
    {
        if (bank?.questions == null) return new List<QuestionRecord>();
        var seen = new HashSet<string>();
        return bank.questions.Select((q, i) => (q, i))
                   .Where(x => x.q != null && x.q.isActive && x.q.levelId == levelId && !string.IsNullOrEmpty(x.q.questionId) && seen.Add(x.q.questionId))
                   .OrderBy(x => x.q.order).ThenBy(x => x.i).Select(x => x.q).ToList();
    }

    /// <summary>How many questions a level will ask (menus, classroom catalog) without building the whole level.</summary>
    public static int QuestionCount(LevelDefinition authored)
    {
        if (authored == null) return 0;
        var bank = QuestionBankRepository.Current;
        var room = bank != null && bank.Usable ? bank.Level(authored.levelId) : null;
        if (room == null) return authored.MessageCount;
        int n = ActiveQuestions(bank.data, authored.levelId).Count(q => QuestionBankValidator.IsPlayable(q, room.tutorial));
        return n > 0 ? n : authored.MessageCount;
    }

    static void Finish(ResolvedLevel r, LevelDefinition authored)
    {
        var level = r.level;
        if (level.messages == null) level.messages = new List<ScamMessage>();
        level.messages.RemoveAll(m => m == null);
        if (level.MessageCount == 0)
            r.issues.Add(new ContentIssue(ContentSeverity.Error, authored.levelId, "this level has no questions to play"));
        else if (level.MessageCount < LevelContentRules.MinQuestionsPerRoom || level.MessageCount > LevelContentRules.MaxQuestionsPerRoom)
            r.issues.Add(new ContentIssue(ContentSeverity.Warning, authored.levelId, $"{level.MessageCount} questions (a room is designed for {LevelContentRules.MinQuestionsPerRoom}-{LevelContentRules.MaxQuestionsPerRoom})"));
    }
}
