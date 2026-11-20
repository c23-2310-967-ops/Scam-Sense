using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Loads the 30 validated curriculum questions (Assets/Editor/CurriculumQuestions.json, generated from
/// Docs/Curriculum_Question_Bank.md) into the five room assets: six questions per room, in the bank's order
/// (Room 1 = Q1-Q6 ... Room 5 = Q25-Q30). The game shuffles them per attempt; the assets keep the bank order.
/// Replaces each room's scored messages, briefing, objectives, case notes, case files, lessons and the star /
/// unlock scores (worked out from the room's own scoring rules). Older inbox emails and scoring rules are kept.
/// Menu: Tools > Scam Sense > Curriculum > Load Questions into Rooms.
/// </summary>
public static class CurriculumQuestionsImporter
{
    public const string JsonPath = "Assets/Editor/CurriculumQuestions.json";
    public const int QuestionsPerRoom = 6;

    [Serializable] public class QuestionData
    {
        public string id, questionType, difficulty, curriculumRef, explanation, feedback, learningObjective;
        public int number, correctChoice;
        public string[] choices;
        public string verdict, channel, sender, senderDetail, subject, body, link, scenarioContext;
    }
    [Serializable] public class RoomData
    {
        public int room;
        public string levelAsset, intro;
        public string[] caseNotes, lessons;
        public QuestionData[] questions;
    }
    [Serializable] public class FileData { public string source; public RoomData[] rooms; }

    public static FileData Load()
    {
        if (!File.Exists(JsonPath)) throw new FileNotFoundException("Curriculum question data not found", JsonPath);
        var data = JsonUtility.FromJson<FileData>(File.ReadAllText(JsonPath));
        if (data == null || data.rooms == null) throw new InvalidDataException(JsonPath + " could not be read");
        return data;
    }

    [MenuItem("Tools/Scam Sense/Curriculum/Load Questions into Rooms")]
    public static void LoadIntoRooms()
    {
        var data = Load();
        int total = 0;
        foreach (var room in data.rooms)
        {
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
        if (room.questions == null || room.questions.Length != QuestionsPerRoom)
            throw new InvalidDataException($"Room {room.room} must have exactly {QuestionsPerRoom} questions");

        level.messages = new List<ScamMessage>();
        foreach (var q in room.questions) level.messages.Add(ToMessage(q));

        // Scores from the room's own rules: 3 / 4 / 6 correct answers (allowing the penalties of the misses).
        var s = level.scoring ?? new LevelScoring();
        int b = s.basePoints, p = s.wrongPenalty;
        level.oneStarScore = Mathf.Max(1, 3 * b - 3 * p);
        level.twoStarScore = Mathf.Max(level.oneStarScore, 4 * b - 2 * p);
        level.threeStarScore = Mathf.Max(level.twoStarScore, QuestionsPerRoom * b);
        level.minScoreToUnlockNext = level.twoStarScore;

        int choiceCount = 0;
        foreach (var q in room.questions) if (q.choices != null && q.choices.Length >= 2) choiceCount++;
        int verdictCount = QuestionsPerRoom - choiceCount;
        string mix = verdictCount == 0 ? "six case questions, each with four answer choices"
                   : $"{Count(choiceCount)} case question{(choiceCount == 1 ? "" : "s")} with four answer choices and " +
                     $"{Count(verdictCount)} message{(verdictCount == 1 ? "" : "s")} or web page{(verdictCount == 1 ? "" : "s")} to judge as SCAM or LEGITIMATE";
        level.briefingText = $"{room.intro} This room has six questions: {mix}. They arrive in a different order on every attempt, " +
                             "and the answer choices are shuffled too, so read each one carefully instead of remembering positions.";
        bool last = string.IsNullOrEmpty(level.nextLevelId);
        level.objectives = new[]
        {
            "Answer all six questions in this room.",
            "Read the whole situation before you choose.",
            last ? $"Score {level.minScoreToUnlockNext} or more to complete the investigation."
                 : $"Score {level.minScoreToUnlockNext} or more to unlock the next room."
        };
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
                "Six questions are waiting in this room. They arrive one at a time, in a different order on every attempt.\n\n" +
                "Case questions have four answer choices, shuffled each attempt. Pick the safest and most accurate one.\n\n" +
                "Messages and web pages are judged as SCAM or LEGITIMATE.\n\n" +
                $"Score {level.minScoreToUnlockNext} or more to {(last ? "complete the investigation" : "unlock the next room")}." }
        };
    }

    static string Count(int n) => n switch { 1 => "one", 2 => "two", 3 => "three", 4 => "four", 5 => "five", _ => n.ToString() };

    public static ScamMessage ToMessage(QuestionData q)
    {
        var channel = (MessageChannel)Enum.Parse(typeof(MessageChannel), q.channel);
        bool choice = q.choices != null && q.choices.Length >= 2;
        return new ScamMessage
        {
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
            // No clickable investigation data in the question bank, so no follow-up re-check; wrong answers get the explanation.
            consequence = new WrongAnswerConsequence { kind = choice ? ConsequenceKind.FeedbackOnly : ConsequenceKind.Auto, followUp = FollowUpMode.None },
        };
    }
}
