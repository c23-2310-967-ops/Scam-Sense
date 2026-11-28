using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// LEGACY (read-only since the question bank): reads Assets/Editor/CurriculumQuestions.json, the format the Python
/// generator (Tools/build_game_questions.py) writes. Only used by Tools > Scam Sense > Content > Migrate Legacy
/// Content to Bank and by tests; the game itself reads Assets/Resources/Content/question_bank.json.
/// Originally: loaded the 30 validated curriculum questions into the room assets.
/// Each room has its own number of questions (the layout is set by CurriculumRoomLayout: 10 rooms of 3, easiest
/// first). The game shuffles them per attempt; the assets keep the file's order.
/// Replaces each room's scored messages, briefing, objectives, case notes, case files, lessons and the star /
/// unlock scores (worked out from the room's own scoring rules and size). Older inbox emails and scoring rules are kept.
/// Menu: Tools > Scam Sense > Curriculum > Load Questions into Rooms.
/// </summary>
public static class CurriculumQuestionsImporter
{
    public const string JsonPath = "Assets/Editor/CurriculumQuestions.json";
    /// <summary>A room needs at least this many questions (and at most MaxQuestionsPerRoom) to make a real case.</summary>
    public const int MinQuestionsPerRoom = LevelContentRules.MinQuestionsPerRoom, MaxQuestionsPerRoom = LevelContentRules.MaxQuestionsPerRoom;

    /// <summary>See <see cref="LevelContentRules.CorrectNeeded"/>.</summary>
    public static (int oneStar, int unlock) CorrectNeeded(int n) => LevelContentRules.CorrectNeeded(n);

    [Serializable] public class InspectData { public string id, field, match, label, finding, linkAddress, clueId, part; }
    [Serializable] public class ItemData { public string key, role, label, text; }
    [Serializable] public class ActivityData
    {
        public string kind, title, instruction;
        public ItemData[] items;
        public int minClues = 1;
        public string[] outcomes;
    }
    [Serializable] public class EvidenceData { public string id, title, whyItMatters; public bool supportsVerdict; }
    [Serializable] public class QuestionData
    {
        public string id, questionType, difficulty, curriculumRef, explanation, feedback, learningObjective;
        public int number, correctChoice;
        public string[] choices;
        public string verdict, channel, sender, senderDetail, subject, body, link, scenarioContext;
        // Level 1 tutorial only (empty elsewhere): HINT text and underlined clue details
        public string hint;
        public InspectData[] inspectables;
        public EvidenceData[] evidence;
        // Interactive activity (optional): simulated app, clue gate and decision outcomes
        public ActivityData activity;
    }
    [Serializable] public class RoomData
    {
        public int room;
        public string levelAsset, intro;
        public string[] caseNotes, lessons;
        public QuestionData[] questions;
        // Level 1 tutorial only
        public bool tutorial;
        public string firstQuestion, briefingTitle, briefing;
        public string[] objectives;
    }
    [Serializable] public class FileData
    {
        public string source;
        public RoomData[] rooms;
        /// <summary>Validated questions not used in any room right now (kept for future rooms; never loaded into a level).</summary>
        public QuestionData[] reserve;
    }

    public static FileData Load()
    {
        if (!File.Exists(JsonPath)) throw new FileNotFoundException("Curriculum question data not found", JsonPath);
        var data = JsonUtility.FromJson<FileData>(File.ReadAllText(JsonPath));
        if (data == null || data.rooms == null) throw new InvalidDataException(JsonPath + " could not be read");
        return data;
    }

    // The menu items that baked this file into the level assets are gone: the question bank
    // (Assets/Resources/Content/question_bank.json) is now the source, read at runtime. See QuestionBankTools.

    /// <summary>Writes one room's questions and texts into a level (no saving). Same rules as the runtime resolver.</summary>
    public static void Apply(LevelDefinition level, RoomData room)
    {
        int n = room.questions != null ? room.questions.Length : 0;
        if (n < MinQuestionsPerRoom || n > MaxQuestionsPerRoom)
            throw new InvalidDataException($"Room {room.room} has {n} questions (a room needs {MinQuestionsPerRoom}-{MaxQuestionsPerRoom})");

        level.messages = new List<ScamMessage>();
        foreach (var q in room.questions) level.messages.Add(ToMessage(q, room.tutorial));
        level.tutorial = room.tutorial;
        level.tutorialFirstQuestion = room.tutorial ? room.firstQuestion ?? "" : "";
        LevelContentRules.ApplyThresholds(level, n);
        LevelContentRules.ApplyTexts(level, new LevelContentRecord
        {
            levelId = level.levelId, intro = room.intro, briefingTitle = room.briefingTitle, briefing = room.briefing,
            objectives = room.objectives, caseNotes = room.caseNotes, lessons = room.lessons, learningSummary = ""
        });
    }

    /// <summary>The question's interactive activity (Kind None when it has none).</summary>
    public static MessageActivity ToActivity(ActivityData a)
    {
        if (a == null || string.IsNullOrEmpty(a.kind) || a.items == null || a.items.Length == 0) return new MessageActivity();
        var act = new MessageActivity
        {
            kind = (ActivityKind)Enum.Parse(typeof(ActivityKind), a.kind),
            title = a.title ?? "",
            instruction = a.instruction ?? "",
            minClues = Mathf.Max(0, a.minClues),
            outcomes = a.outcomes ?? new string[0],
        };
        foreach (var i in a.items)
            act.items.Add(new ActivityItem
            {
                key = i.key ?? "", label = i.label ?? "", text = i.text ?? "",
                role = string.IsNullOrEmpty(i.role) ? ActivityRole.Text : (ActivityRole)Enum.Parse(typeof(ActivityRole), i.role)
            });
        return act;
    }

    public static ScamMessage ToMessage(QuestionData q, bool tutorial = false)
    {
        var channel = (MessageChannel)Enum.Parse(typeof(MessageChannel), q.channel);
        bool choice = q.choices != null && q.choices.Length >= 2;
        var inspectables = new List<MessageInspectable>();
        if (q.inspectables != null)
            foreach (var i in q.inspectables)
                inspectables.Add(new MessageInspectable
                {
                    id = i.id, field = (InspectField)Enum.Parse(typeof(InspectField), i.field), match = i.match ?? "",
                    label = i.label ?? "", finding = i.finding ?? "", linkAddress = i.linkAddress ?? "", clueId = i.clueId ?? "",
                    part = i.part ?? ""
                });
        var evidence = new List<MessageEvidence>();
        if (q.evidence != null)
            foreach (var e in q.evidence)
                evidence.Add(new MessageEvidence { id = e.id, title = e.title, whyItMatters = e.whyItMatters, supportsVerdict = e.supportsVerdict });
        return new ScamMessage
        {
            activity = ToActivity(q.activity),
            hint = q.hint ?? "",
            inspectables = inspectables,
            evidence = evidence,
            id = q.id,
            channel = channel,
            sender = q.sender ?? "",
            senderDetail = q.senderDetail ?? "",
            subject = q.subject ?? "",
            body = q.body ?? "",
            link = q.link ?? "",
            correctVerdict = q.verdict == "Legitimate" ? MessageVerdict.Legitimate : MessageVerdict.Scam,
            scenarioContext = q.scenarioContext ?? "",
            explanation = q.explanation ?? "",
            feedback = q.feedback ?? "",
            curriculumRef = q.curriculumRef ?? "",
            choices = choice ? q.choices : new string[0],
            correctChoice = choice ? q.correctChoice : 0,
            trigger = channel == MessageChannel.SMS || channel == MessageChannel.Chat ? MessageTrigger.NearPhone
                    : MessageChannels.OnComputer(channel) ? MessageTrigger.NearComputer : MessageTrigger.TimeOnly,
            // Wrong answers get the explanation. The tutorial room's SCAM / LEGITIMATE messages also offer the existing
            // follow-up re-check (inspect the key clues again, HINT, answer again for recovery points); choice questions don't.
            consequence = new WrongAnswerConsequence
            {
                kind = choice ? ConsequenceKind.FeedbackOnly : ConsequenceKind.Auto,
                followUp = tutorial && !choice ? FollowUpMode.Auto : FollowUpMode.None
            },
        };
    }
}
