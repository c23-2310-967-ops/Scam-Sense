using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using R = CurriculumQuestionsImporter.RoomData;

/// <summary>
/// Tools > Scam Sense > Curriculum > Arrange Rooms
/// The room layout. Level 1 is a short introduction of 4 messages (2 scams from the validated bank, 2 legitimate
/// emails that were already in the room's inbox); rooms 2-5 keep their validated 6 questions, easiest first.
/// Validated questions not placed in a room (E01, E04, E05, E06) are kept in the JSON's "reserve" for future rooms.
/// The question wording, choices and answers are the validated ones and are not changed here. This tool only:
///   - groups the questions into rooms (Assets/Editor/CurriculumQuestions.json) and writes each room's intro
///     ("what you'll learn") and case notes,
///   - adds a one-line HINT to questions that had none (a pointer, never the answer),
///   - adds an English translation under the Taglish text of M04,
///   - sets each room's scoring (gentle in early rooms, time pressure only from room 3),
/// then runs Load Questions into Rooms. Safe to run again (questions are found by id, whatever the current layout).
///
/// More rooms later: add the room's level asset (and scene), add an entry to <see cref="Rooms"/> and move question
/// ids into it - the importer works out each room's star and unlock scores from its own size. This tool never
/// creates level assets.
/// </summary>
public static class CurriculumRoomLayout
{
    public class Room
    {
        public string asset, intro;
        public string[] ids;
        public int basePoints, speedBonus, wrongPenalty, overtimePerStep, maxTimePenalty;
        public float maxReading = 120f;
    }

    /// <summary>The rooms in play order.</summary>
    public static readonly Room[] Rooms =
    {
        // Level 1 (introduction): 4 messages, 2 scams + 2 legitimate. E01, E04, E05 and E06 are kept in the reserve.
        new Room { asset = "Assets/Levels/Level1_TheFirstCase.asset", ids = new[] { "E02", "E03", "L01", "L02" },
                   intro = "In this room: a fake delivery text, a phishing email, and two real emails that are safe.",
                   basePoints = 100, wrongPenalty = 10, maxReading = 150 },
        new Room { asset = "Assets/Levels/Level2_TheInvestigation.asset", ids = new[] { "E07", "E08", "E09", "E10", "M01", "M02" },
                   intro = "In this room: protecting your personal data and accounts, fake virus pop-ups, what to do after a scam, and a message that is real.",
                   basePoints = 150, wrongPenalty = 15, maxReading = 150 },
        new Room { asset = "Assets/Levels/Level3_TheDigitalTrap.asset", ids = new[] { "M03", "M04", "M05", "M06", "M07", "M08" },
                   intro = "In this room: what the padlock means, impostors asking for money, owning up to a mistake, second login checks, fake Wi-Fi and fake bank texts.",
                   basePoints = 200, speedBonus = 20, wrongPenalty = 25, overtimePerStep = 5, maxTimePenalty = 25, maxReading = 140 },
        new Room { asset = "Assets/Levels/Level4_DigitalForensicsLab.asset", ids = new[] { "M09", "M10", "H01", "H02", "H03", "H04" },
                   intro = "In this room: pressure from 'important' people, booby-trapped files, emails written just for you, risky app permissions and which risk to fix first.",
                   basePoints = 250, speedBonus = 25, wrongPenalty = 35, overtimePerStep = 10, maxTimePenalty = 50, maxReading = 150 },
        new Room { asset = "Assets/Levels/Level5_MacLab.asset", ids = new[] { "H05", "H06", "H07", "H08", "H09", "H10" },
                   intro = "In this room: keeping evidence safe, the hidden checks inside an email, identity-theft forms, ransomware, fake QR codes and the right way to report.",
                   basePoints = 300, speedBonus = 30, wrongPenalty = 45, overtimePerStep = 10, maxTimePenalty = 60, maxReading = 150 },
    };

    /// <summary>One-line hints for questions that had none: where to look, never the answer (checked by LevelDataTests).</summary>
    public static readonly Dictionary<string, string> Hints = new Dictionary<string, string>
    {
        { "E07", "Think about what a raffle really needs from you just to contact a winner." },
        { "E08", "Where do \"Forgot password\" links get sent?" },
        { "E09", "Where do real security warnings come from: a web page, or your own computer?" },
        { "E10", "After a scam, what will the e-wallet company and the police ask you to show them?" },
        { "M01", "Read the case note. Did you ask for this, and does the address match the trusted one?" },
        { "M02", "Read the web address from right to left, up to the first single slash." },
        { "M03", "A padlock is about the connection. Does it tell you who owns the site?" },
        { "M04", "A new number, an emergency, a money request and \"don't call me\". How could you check it's really him?" },
        { "M05", "If your password may be stolen, what makes the stolen one useless, and who needs to know?" },
        { "M06", "The attacker already has the password. What extra check would they still be missing?" },
        { "M07", "Two networks with almost the same name, and a page asking for a social media password. Does real Wi-Fi need that?" },
        { "M08", "Ignore the sender name. Look at the link: is it the bank's trusted address?" },
        { "M09", "A big name plus a rush is an old trick. How can you check who is really calling?" },
        { "M10", "Look at the file ending (.docm). What could \"Enable Content\" let the file do?" },
        { "H01", "Every detail in the email was on a public page. Was it sent to everyone, or written for you?" },
        { "H02", "The sign-in page was real. Now read what the app wants permission to do." },
        { "H03", "If the director's account was taken over, would a reply to that same email reach the real director?" },
        { "H04", "Look for the problem an attacker could use most easily that would also do the most damage." },
        { "H05", "Investigators need evidence that is complete and unchanged." },
        { "H06", "The \"From\" name is just text. Which lines are checks the mail system actually ran?" },
        { "H07", "Look at the PIN request. Does anyone need that to send you money?" },
        { "H08", "First stop it from spreading, and keep the evidence." },
        { "H09", "Look closely at the QR code itself, and think about where a real payment happens." },
        { "H10", "Which choice helps the victims without you touching the stolen data?" },
    };

    // ---------- Level 1's two legitimate messages ----------
    // Both were already in Level 1's computer inbox as unscored "older" emails; they are now judged by the player.
    // They are game examples, not part of the validated question bank (ids L01, L02).

    const string L1Ref = "Information Assurance in Security 1 (BIT1101) — Week 16: Information Security Best Practices (user awareness)";
    const string LibraryObjective = "Recognise a real notice: it matches something you did, gives you time and asks for nothing secret.";
    const string TeamObjective = "Know that not every message is a scam: a real one from someone you know just shares details.";

    static CurriculumQuestionsImporter.InspectData Clue(string id, string field, string match, string label, string finding, string clue) =>
        new CurriculumQuestionsImporter.InspectData { id = id, field = field, match = match, label = label, finding = finding, linkAddress = "", clueId = clue, part = "" };

    static CurriculumQuestionsImporter.EvidenceData Fact(string id, string title, string why) =>
        new CurriculumQuestionsImporter.EvidenceData { id = id, title = title, whyItMatters = why, supportsVerdict = true };

    static CurriculumQuestionsImporter.QuestionData Legit(string id, string objective, string sender, string detail, string subject, string body, string context,
        string hint, string explanation, string feedback, CurriculumQuestionsImporter.InspectData[] clues, CurriculumQuestionsImporter.EvidenceData[] facts) =>
        new CurriculumQuestionsImporter.QuestionData
        {
            id = id, number = 0, questionType = "Scam or Legitimate", difficulty = "Easy", curriculumRef = L1Ref,
            explanation = explanation, feedback = feedback, learningObjective = objective,
            choices = new string[0], correctChoice = 0, verdict = "Legitimate", channel = "Email",
            sender = sender, senderDetail = detail, subject = subject, body = body, link = "", scenarioContext = context,
            hint = hint, inspectables = clues, evidence = facts, activity = new CurriculumQuestionsImporter.ActivityData(),
        };

    public static readonly CurriculumQuestionsImporter.QuestionData[] ExtraQuestions =
    {
        Legit("L01", LibraryObjective, "Riverside Public Library", "notices@riversidelibrary.example", "Your reserved book is ready for pickup",
            "The book you reserved is now available at the front desk. It will be held for 7 days. Log in to your library account if you need more time.",
            "You reserved a book at Riverside Public Library on Monday.",
            "Check your case notes: did you do something that explains this email? Then look at what it asks you to do.",
            "You reserved a book this week, so this email is expected. It comes from the library's own address, gives you 7 days, and only asks you to pick the book up at the front desk. It doesn't ask for a password, money or a code, and there's no link to click.",
            "Legitimate! It matches what you did, there's no rush, and it asks for nothing secret.",
            new[]
            {
                Clue("sender", "SenderDetail", "", "Sender address", "The address ends in the library's own name: riversidelibrary.example.", "sender"),
                Clue("claim", "Body", "The book you reserved is now available", "The news", "Your case notes say you reserved a book this week. This is exactly what you were waiting for.", "expected"),
                Clue("pickup", "Body", "at the front desk", "What it asks", "You pick the book up in person. No link, no payment, no password.", "noask"),
                Clue("login", "Body", "Log in to your library account", "Account", "It tells you to use your own account if you need more time. It doesn't send you a login link.", "noask"),
                Clue("time", "Body", "held for 7 days", "Time", "Seven days is plenty of time. Nothing pushes you to act right now.", "calm"),
            },
            new[]
            {
                Fact("sender", "The library's own address", "The sender matches the library you used."),
                Fact("expected", "You were expecting it", "You reserved a book, so this news makes sense."),
                Fact("noask", "Asks for nothing secret", "No link, money, password or code is requested."),
                Fact("calm", "No pressure", "Real notices give you time instead of rushing you."),
            }),
        Legit("L02", TeamObjective, "Sam (Volunteer Team)", "sam@greenparkvolunteers.example", "Saturday clean-up: meeting point",
            "Hi everyone, we'll meet at the park entrance at 9:00 on Saturday. Bring gloves and water. See you there!",
            "You signed up for Saturday's park clean-up. Sam is the volunteer team's organiser.",
            "Who is Sam? Check your case notes, then look at what the email asks you to do.",
            "You signed up for this clean-up, and Sam is the organiser. The email only gives the meeting time and place and what to bring. There is no link, no payment, no password and no pressure. Not every message is a scam: real ones match what you expect and ask for nothing risky.",
            "Legitimate! It's from the organiser you know and only shares the meeting details.",
            new[]
            {
                Clue("sender", "Sender", "", "Sender", "Sam is the organiser of the clean-up you joined (see your case notes).", "known"),
                Clue("group", "Body", "Hi everyone", "Greeting", "It's written to the whole team, like a normal group update.", "known"),
                Clue("details", "Body", "we'll meet at the park entrance at 9:00 on Saturday", "The details", "The time and place of the event you signed up for.", "expected"),
                Clue("bring", "Body", "Bring gloves and water", "What it asks", "Harmless, normal instructions. It asks for no money, password or code, and has no link.", "noask"),
            },
            new[]
            {
                Fact("known", "From someone you know", "Sam is the organiser of the clean-up you joined."),
                Fact("expected", "You were expecting it", "It's about the event you signed up for."),
                Fact("noask", "Asks for nothing secret", "No link, money, password or code is requested."),
            }),
    };

    /// <summary>Level 1's older inbox emails: two became the legitimate messages above; the receipt contradicted the room's
    /// case note ("You have not ordered anything this month").</summary>
    static readonly string[] Level1InboxRemoved = { "old_library", "old_team", "old_receipt" };

    const string M04Translation =
        "\n<i>In English: \"Mom, it's Migs. My phone broke, this is my new number. Please send ₱3,000 to PayLite 0917 555 0188, " +
        "it's an emergency. Don't call yet, I have no load.\"</i>";

    // LEGACY: no menu item any more. Rooms are arranged by each question's levelId / order in the question bank
    // (Assets/Resources/Content/question_bank.json, see Docs/ContentManagement.md). Running this would rewrite the old
    // CurriculumQuestions.json and the level assets, not the bank the game plays.
    public static void Arrange()
    {
        var data = CurriculumQuestionsImporter.Load();
        var byId = data.rooms.SelectMany(r => r.questions).Concat(data.reserve ?? new CurriculumQuestionsImporter.QuestionData[0])
                       .ToDictionary(q => q.id, q => q);
        foreach (var extra in ExtraQuestions) byId[extra.id] = extra;   // always from code (the source of these two)
        var tutorial = data.rooms.First(r => r.tutorial);
        var placed = Rooms.SelectMany(r => r.ids).ToList();
        if (placed.Distinct().Count() != placed.Count || placed.Any(id => !byId.ContainsKey(id)))
            throw new InvalidDataException("The room layout must place each question at most once, and only known questions.");
        // Validated questions not placed in a room are kept in the reserve (never lost).
        data.reserve = byId.Values.Where(q => !placed.Contains(q.id) && !ExtraQuestions.Any(e => e.id == q.id))
                                  .OrderBy(q => q.number).ToArray();
        foreach (var spec in Rooms)
            if (AssetDatabase.LoadAssetAtPath<LevelDefinition>(spec.asset) == null)
                throw new FileNotFoundException("Add the room's level asset first (this tool never creates rooms).", spec.asset);

        // Hints and the M04 translation (the validated wording itself is unchanged).
        foreach (var kv in Hints)
            if (string.IsNullOrEmpty(byId[kv.Key].hint)) byId[kv.Key].hint = kv.Value;
        if (!byId["M04"].body.Contains("In English:")) byId["M04"].body += M04Translation;

        var rooms = new List<R>();
        for (int i = 0; i < Rooms.Length; i++)
        {
            var spec = Rooms[i];
            var qs = spec.ids.Select(id => byId[id]).ToArray();
            var room = new R
            {
                room = i + 1, levelAsset = spec.asset, intro = spec.intro, questions = qs,
                lessons = qs.Select(q => q.learningObjective).ToArray(),
                // Case notes: the background of this room's SCAM / LEGITIMATE messages ("Is this..." prompts are not notes).
                caseNotes = qs.Where(q => (q.choices == null || q.choices.Length < 2) && !string.IsNullOrEmpty(q.scenarioContext)
                                          && !q.scenarioContext.StartsWith("Is this")).Select(q => q.scenarioContext).ToArray(),
            };
            if (i == 0)
            {
                room.tutorial = true;
                room.firstQuestion = tutorial.firstQuestion;
                room.briefingTitle = tutorial.briefingTitle;
                room.briefing = tutorial.briefing;
                room.objectives = new[] { "Follow the guide on your first question.", "Answer all {count} questions.", "Score {unlock}+ to unlock the next room." };
            }
            else { room.firstQuestion = room.briefingTitle = room.briefing = ""; room.objectives = new string[0]; }
            rooms.Add(room);
        }
        data.rooms = rooms.ToArray();
        File.WriteAllText(CurriculumQuestionsImporter.JsonPath, JsonUtility.ToJson(data, true));
        AssetDatabase.ImportAsset(CurriculumQuestionsImporter.JsonPath);

        ApplyScoring();
        var level1 = AssetDatabase.LoadAssetAtPath<LevelDefinition>(Rooms[0].asset);
        if (level1.inboxExtras != null && level1.inboxExtras.RemoveAll(m => m != null && Level1InboxRemoved.Contains(m.id)) > 0)
            EditorUtility.SetDirty(level1);
        foreach (var room in data.rooms)
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(room.levelAsset);
            CurriculumQuestionsImporter.Apply(level, room);
            EditorUtility.SetDirty(level);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"Curriculum: {data.rooms.Length} rooms arranged ({string.Join(" / ", data.rooms.Select(r => r.questions.Length))} questions), " +
                  $"{data.reserve.Length} validated questions in reserve ({string.Join(", ", data.reserve.Select(q => q.id))}).");
    }

    /// <summary>Each room's points, penalties and reading time: forgiving at the start, a little stricter in later rooms.</summary>
    static void ApplyScoring()
    {
        foreach (var spec in Rooms)
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(spec.asset);
            Undo.RecordObject(level, "Arrange rooms");
            level.description = spec.intro.Replace("In this room: ", "");
            level.description = char.ToUpperInvariant(level.description[0]) + level.description.Substring(1);
            var s = level.scoring ?? (level.scoring = new LevelScoring());
            s.basePoints = spec.basePoints;
            s.speedBonus = spec.speedBonus;
            s.wrongPenalty = spec.wrongPenalty;
            s.recoveryPoints = Mathf.RoundToInt(spec.basePoints * 0.25f);
            s.minCorrectPoints = Mathf.RoundToInt(spec.basePoints * 0.8f);
            s.overtimePenaltyPerStep = spec.overtimePerStep;
            s.maxTimePenalty = spec.maxTimePenalty;
            s.maxReadingSeconds = spec.maxReading;
            s.minReadingSeconds = Mathf.Max(s.minReadingSeconds, 45f);
            EditorUtility.SetDirty(level);
        }
        AssetDatabase.SaveAssets();
    }
}
