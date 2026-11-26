using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Loads the 30 validated curriculum questions (Assets/Editor/CurriculumQuestions.json) into the room assets.
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
    public const int MinQuestionsPerRoom = 2, MaxQuestionsPerRoom = 8;

    /// <summary>
    /// Correct answers needed for 1 star and for 2 stars / unlocking, out of <paramref name="n"/> questions:
    /// about half for 1 star, about 60% (never all of them) to unlock. 3 questions: 1 / 2. 6 questions: 3 / 4.
    /// </summary>
    public static (int oneStar, int unlock) CorrectNeeded(int n)
    {
        int unlock = Mathf.Clamp(Mathf.CeilToInt(n * 0.6f), 1, Mathf.Max(1, n - 1));
        int one = Mathf.Clamp(n / 2, 1, unlock);
        return (one, unlock);
    }

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

    [MenuItem("Tools/Scam Sense/Curriculum/Load Questions into Rooms")]
    public static void LoadIntoRooms() => LoadRooms(null);

    /// <summary>Rewrites only Level 1 (the tutorial room). Levels 2-5 are not opened or re-saved.</summary>
    [MenuItem("Tools/Scam Sense/Curriculum/Load Level 1 (Tutorial) Only")]
    public static void LoadLevel1Only() => LoadRooms(1);

    static void LoadRooms(int? onlyRoom)
    {
        var data = Load();
        int total = 0;
        foreach (var room in data.rooms)
        {
            if (onlyRoom.HasValue && room.room != onlyRoom.Value) continue;
            var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(room.levelAsset);
            if (level == null) { Debug.LogError($"Curriculum import: {room.levelAsset} not found"); continue; }
            Undo.RecordObject(level, "Load curriculum questions");
            Apply(level, room);
            EditorUtility.SetDirty(level);
            total += level.MessageCount;
            Debug.Log($"Curriculum import: {level.name} now has {level.MessageCount} questions " +
                      $"(unlock {level.minScoreToUnlockNext}, stars {level.oneStarScore}/{level.twoStarScore}/{level.threeStarScore}).", level);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"Curriculum import: {total} questions loaded into {data.rooms.Length} rooms from {JsonPath}.");
    }

    /// <summary>Writes one room's questions and texts into its level asset (no saving).</summary>
    public static void Apply(LevelDefinition level, RoomData room)
    {
        int n = room.questions != null ? room.questions.Length : 0;
        if (n < MinQuestionsPerRoom || n > MaxQuestionsPerRoom)
            throw new InvalidDataException($"Room {room.room} has {n} questions (a room needs {MinQuestionsPerRoom}-{MaxQuestionsPerRoom})");

        level.messages = new List<ScamMessage>();
        foreach (var q in room.questions) level.messages.Add(ToMessage(q, room.tutorial));
        level.tutorial = room.tutorial;
        level.tutorialFirstQuestion = room.tutorial ? room.firstQuestion ?? "" : "";

        // Scores from the room's own rules and size. Unlocking: about 60% right always unlocks, even if every right
        // answer ran over its reading time (each still earns at least minCorrectPoints) and the misses cost points.
        var s = level.scoring ?? new LevelScoring();
        int b = s.basePoints, p = s.wrongPenalty, slow = Mathf.Min(b, Mathf.Max(0, s.minCorrectPoints));
        var (k1, k2) = CorrectNeeded(n);
        level.oneStarScore = Mathf.Max(1, k1 * slow - (n - k1) * p);
        level.twoStarScore = Mathf.Max(level.oneStarScore, k2 * slow - (n - k2) * p);
        level.threeStarScore = Mathf.Max(level.twoStarScore, n * b);
        level.minScoreToUnlockNext = level.twoStarScore;

        int choiceCount = 0;
        foreach (var q in room.questions) if (q.choices != null && q.choices.Length >= 2) choiceCount++;
        int verdictCount = n - choiceCount;
        // Short and beginner-friendly: what the room teaches, then what kind of questions to expect.
        string mix = verdictCount == 0 ? $"{n} questions with answer choices (A-D)"
                   : choiceCount == 0 ? $"{n} messages to judge as SCAM or LEGITIMATE"
                   : $"{n} questions: {choiceCount} with answer choices (A-D) and {verdictCount} to judge as SCAM or LEGITIMATE";
        level.briefingText = $"{room.intro}\nYou'll get {mix}. Take your time and read each one carefully.";
        bool last = string.IsNullOrEmpty(level.nextLevelId);
        level.objectives = new[]
        {
            $"Answer all {n} questions.",
            "Read the whole situation before you choose. Stuck? Press HINT.",
            last ? $"Score {level.minScoreToUnlockNext}+ to finish the investigation."
                 : $"Score {level.minScoreToUnlockNext}+ to unlock the next room."
        };
        // Tutorial room: its own intro briefing and objectives ({unlock} = the unlock score, {count} = questions).
        if (!string.IsNullOrEmpty(room.briefingTitle)) level.briefingTitle = room.briefingTitle;
        if (!string.IsNullOrEmpty(room.briefing)) level.briefingText = room.briefing;
        if (room.objectives != null && room.objectives.Length > 0)
        {
            level.objectives = new string[room.objectives.Length];
            for (int i = 0; i < room.objectives.Length; i++)
                level.objectives[i] = room.objectives[i].Replace("{unlock}", level.minScoreToUnlockNext.ToString()).Replace("{count}", n.ToString());
        }
        level.caseNotes = room.caseNotes ?? new string[0];
        level.lessons = room.lessons ?? new string[0];
        level.learningSummary = "";

        // Case-files computer: this room's notes (never an answer) and how the room works.
        string notes = level.caseNotes.Length > 0 ? "• " + string.Join("\n• ", level.caseNotes)
                     : "No extra records are needed in this room. Everything you need is in each question.";
        level.caseFiles = new List<CaseFile>
        {
            new CaseFile { title = "Case Notes", body = notes },
            new CaseFile { title = "How This Room Works", body =
                $"1. {n} questions arrive one at a time, in a new order each attempt.\n\n" +
                "2. Case questions: pick the safest, most accurate answer (A-D).\n\n" +
                "3. Messages and web pages: choose SCAM or LEGITIMATE.\n\n" +
                "4. Not sure? Press HINT to see what to look at. After each answer you'll see why it was right or wrong.\n\n" +
                $"Goal: score {level.minScoreToUnlockNext}+ to {(last ? "finish the investigation" : "unlock the next room")}." }
        };
    }

    static string Count(int n) => n switch { 1 => "one", 2 => "two", 3 => "three", 4 => "four", 5 => "five", _ => n.ToString() };

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
