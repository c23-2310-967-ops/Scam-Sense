using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor tools for the question bank (Assets/Resources/Content/question_bank.json). Menu: Tools > Scam Sense > Content.
///   Validate Question Bank            - lists every error / warning in the Console (nothing is changed)
///   Bake Fallback Content into Levels - copies the bank into the LevelDefinition assets, the offline last resort
///   Migrate Legacy Content to Bank    - one-time: builds the bank from CurriculumQuestions.json, the level assets
///                                       and the old Level 3-5 builders (kept inactive). Overwrites the bank file.
/// The game reads the bank at runtime (LevelContentResolver), so editing the JSON is enough to change what is played.
/// </summary>
public static class QuestionBankTools
{
    public const string BankPath = QuestionBankRepository.BundledAssetPath;
    const string CatalogPath = "Assets/Levels/LevelCatalog.asset";

    public static readonly string[] DefaultCategories =
    {
        "Phishing", "Smishing", "Vishing", "Fake Websites", "Online Scams", "Social Engineering",
        "Malware", "Account Security", "Data Privacy", "Network Security", "Incident Response"
    };

    /// <summary>Category of each migrated question (from its topic and learning objective).</summary>
    public static readonly Dictionary<string, string> MigratedCategories = new Dictionary<string, string>
    {
        { "E01", "Account Security" }, { "E02", "Smishing" }, { "E03", "Phishing" }, { "E04", "Vishing" }, { "E05", "Malware" },
        { "E06", "Social Engineering" }, { "E07", "Data Privacy" }, { "E08", "Account Security" }, { "E09", "Fake Websites" },
        { "E10", "Online Scams" }, { "L01", "Phishing" }, { "L02", "Phishing" },
        { "M01", "Phishing" }, { "M02", "Fake Websites" }, { "M03", "Fake Websites" }, { "M04", "Smishing" }, { "M05", "Incident Response" },
        { "M06", "Account Security" }, { "M07", "Network Security" }, { "M08", "Smishing" }, { "M09", "Vishing" }, { "M10", "Malware" },
        { "H01", "Phishing" }, { "H02", "Phishing" }, { "H03", "Phishing" }, { "H04", "Incident Response" }, { "H05", "Incident Response" },
        { "H06", "Phishing" }, { "H07", "Data Privacy" }, { "H08", "Malware" }, { "H09", "Fake Websites" }, { "H10", "Incident Response" },
    };

    static LevelCatalog Catalog => AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);

    public static QuestionBankFile LoadBankFile()
    {
        if (!File.Exists(BankPath)) throw new FileNotFoundException("Question bank not found", BankPath);
        return JsonUtility.FromJson<QuestionBankFile>(File.ReadAllText(BankPath));
    }

    public static void SaveBankFile(QuestionBankFile bank)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(BankPath));
        File.WriteAllText(BankPath, JsonUtility.ToJson(bank, true));
        AssetDatabase.ImportAsset(BankPath);
        QuestionBankRepository.Reload();
    }

    // ---------- Validate ----------

    [MenuItem("Tools/Scam Sense/Content/Validate Question Bank", priority = 10)]
    public static void ValidateMenu()
    {
        var bank = QuestionBankRepository.Parse(File.Exists(BankPath) ? File.ReadAllText(BankPath) : "", ContentSource.Bundled, BankPath);
        var known = Catalog != null ? Catalog.levels.Where(l => l != null).Select(l => l.levelId).ToList() : null;
        var issues = bank.data != null ? QuestionBankValidator.Validate(bank.data, known) : bank.issues;
        int errors = issues.Count(i => i.IsError), warnings = issues.Count - errors;
        foreach (var i in issues) if (i.IsError) Debug.LogError("Question bank: " + i); else Debug.LogWarning("Question bank: " + i);
        if (bank.data != null && Catalog != null)
            foreach (var l in Catalog.levels.Where(l => l != null))
            {
                var r = LevelContentResolver.Resolve(l, bank);
                Debug.Log($"Question bank: {l.levelId} plays {r.level.MessageCount} questions from {r.source} (unlock {r.level.minScoreToUnlockNext}).");
                UnityEngine.Object.DestroyImmediate(r.level);
            }
        Debug.Log($"Question bank: {errors} errors, {warnings} warnings in {BankPath}.");
    }

    // ---------- Bake fallback ----------

    [MenuItem("Tools/Scam Sense/Content/Bake Fallback Content into Levels", priority = 11)]
    public static void BakeFallback()
    {
        var bank = QuestionBankRepository.Parse(File.ReadAllText(BankPath), ContentSource.Bundled, BankPath);
        if (!bank.Usable) { Debug.LogError("Bake: the question bank has file-level errors:\n" + string.Join("\n", bank.issues)); return; }
        foreach (var authored in Catalog.levels.Where(l => l != null))
        {
            var r = LevelContentResolver.Resolve(authored, bank);
            if (r.source == ContentSource.LevelAsset) { UnityEngine.Object.DestroyImmediate(r.level); continue; }
            Undo.RecordObject(authored, "Bake question bank");
            CopyContent(r.level, authored);
            EditorUtility.SetDirty(authored);
            UnityEngine.Object.DestroyImmediate(r.level);
            Debug.Log($"Bake: {authored.name} now has {authored.MessageCount} built-in questions.", authored);
        }
        AssetDatabase.SaveAssets();
    }

    /// <summary>The content fields only (everything LevelContentResolver changes).</summary>
    public static void CopyContent(LevelDefinition from, LevelDefinition to)
    {
        to.messages = from.messages;
        to.inboxExtras = from.inboxExtras;
        to.tutorial = from.tutorial;
        to.tutorialFirstQuestion = from.tutorialFirstQuestion;
        to.briefingTitle = from.briefingTitle;
        to.briefingText = from.briefingText;
        to.objectives = from.objectives;
        to.caseNotes = from.caseNotes;
        to.lessons = from.lessons;
        to.learningSummary = from.learningSummary;
        to.caseFiles = from.caseFiles;
        to.oneStarScore = from.oneStarScore;
        to.twoStarScore = from.twoStarScore;
        to.threeStarScore = from.threeStarScore;
        to.minScoreToUnlockNext = from.minScoreToUnlockNext;
    }

    // ---------- Supabase seed ----------

    public const string SchemaPath = "supabase/migrations/20261128004018_question_bank.sql";
    public const string SeedPath = "supabase/migrations/20261128004100_question_bank_seed.sql";
    static readonly string[] ColumnFields = { "questionId", "levelId", "order", "isActive", "category", "difficulty", "questionType", "messageType" };

    /// <summary>
    /// Writes the bank as SQL that fills the Supabase question bank tables (run after 20261128004018_question_bank.sql).
    /// Rows that already exist are left alone, so running it again never overwrites edits made on the server.
    /// </summary>
    [MenuItem("Tools/Scam Sense/Content/Export Supabase Seed", priority = 20)]
    public static void ExportSeedMenu()
    {
        File.WriteAllText(SeedPath, BuildSeedSql(LoadBankFile(), Catalog));
        Debug.Log("Question bank: wrote " + SeedPath + " (review it, then apply it to the Supabase project).");
    }

    public static string BuildSeedSql(QuestionBankFile bank, LevelCatalog catalog)
    {
        const string Tag = "$qb$";
        string Quote(string json)
        {
            if (json.Contains(Tag)) throw new InvalidDataException("The content contains " + Tag + ", which the seed uses as its quote.");
            return Tag + json + Tag + "::jsonb";
        }
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("-- GENERATED by Tools > Scam Sense > Content > Export Supabase Seed from " + BankPath);
        sb.AppendLine("-- Content version " + bank.contentVersion + ". Do not edit by hand: edit the bank, then export again.");
        sb.AppendLine("-- Fills empty tables only (on conflict do nothing): never overwrites edits made on the server.");
        sb.AppendLine();
        sb.AppendLine("insert into public.content_categories (name, sort_order)");
        sb.AppendLine("select value, ordinality::integer from jsonb_array_elements_text(" + Quote(JsonArray(bank.categories)) + ") with ordinality");
        sb.AppendLine("on conflict (name) do nothing;");
        sb.AppendLine();
        sb.AppendLine("insert into public.content_levels (level_id, points_per_question, base_points, min_correct_points, wrong_penalty, content) values");
        var rows = new List<string>();
        foreach (var rec in bank.levels)
        {
            var level = catalog.levels.First(l => l != null && l.levelId == rec.levelId);
            var s = level.scoring;
            rows.Add($"  ('{rec.levelId}', {s.MaxPointsPerMessage}, {s.basePoints}, {s.minCorrectPoints}, {s.wrongPenalty}, {Quote(Compact(JsonUtility.ToJson(rec)))})");
        }
        sb.AppendLine(string.Join(",\n", rows));
        sb.AppendLine("on conflict (level_id) do nothing;");
        sb.AppendLine();
        sb.AppendLine("insert into public.question_bank (question_id, level_id, sort_order, is_active, category, difficulty, question_type, message_type, content)");
        // Empty members are left out of the JSON (Compact), so every column falls back to the record's default.
        sb.AppendLine("select q ->> 'questionId', nullif(q ->> 'levelId', ''), coalesce((q ->> 'order')::integer, 0), coalesce((q ->> 'isActive')::boolean, true),");
        sb.AppendLine("       coalesce(q ->> 'category', ''), coalesce(q ->> 'difficulty', 'Easy'), coalesce(q ->> 'questionType', ''), coalesce(q ->> 'messageType', 'SMS'),");
        sb.AppendLine("       q - array[" + string.Join(", ", ColumnFields.Select(f => "'" + f + "'")) + "]");
        sb.AppendLine("  from jsonb_array_elements(" + Quote(QuestionsArray(bank)) + ") q");
        sb.AppendLine("on conflict (question_id) do nothing;");
        return sb.ToString();
    }

    [Serializable] class StringList { public string[] items; }
    [Serializable] class QuestionList { public List<QuestionRecord> items; }

    static string JsonArray(string[] a) { string j = JsonUtility.ToJson(new StringList { items = a ?? new string[0] }); return j.Substring(j.IndexOf('[')).TrimEnd('}'); }
    static string QuestionsArray(QuestionBankFile b) { string j = JsonUtility.ToJson(new QuestionList { items = b.questions }); return Compact(j.Substring(j.IndexOf('[')).TrimEnd('}')); }

    /// <summary>
    /// Drops "key":"" and "key":[] members (their defaults are the same empty values when the game reads the record),
    /// which roughly halves the seed. Quotes inside text are escaped (\"), so text is never matched.
    /// </summary>
    public static string Compact(string json)
    {
        // Fields whose default is not empty (difficulty "Easy", messageType "SMS", field "Body", role "Text") are kept.
        const string Empty = @"""(?!(?:difficulty|messageType|field|role)"")\w+"":(?:""""|\[\])";
        string prev;
        do
        {
            prev = json;
            json = System.Text.RegularExpressions.Regex.Replace(json, "," + Empty + "(?=[,}])", "");   // ,"k":""  (not first)
            json = System.Text.RegularExpressions.Regex.Replace(json, "{" + Empty + ",", "{");        // {"k":"",
            json = System.Text.RegularExpressions.Regex.Replace(json, "{" + Empty + "}", "{}");       // {"k":""}
        } while (json != prev);
        return json;
    }
    // ---------- One-time migration ----------

    [MenuItem("Tools/Scam Sense/Content/Migrate Legacy Content to Bank", priority = 30)]
    public static void MigrateMenu()
    {
        if (File.Exists(BankPath) && !EditorUtility.DisplayDialog("Migrate legacy content",
                $"{BankPath} already exists and will be rebuilt from the legacy sources. Edits made in it since the last migration are lost.", "Rebuild", "Cancel"))
            return;
        var bank = BuildFromLegacy();
        SaveBankFile(bank);
        Debug.Log($"Migration: wrote {bank.questions.Count} questions and {bank.levels.Count} levels to {BankPath}.");
    }

    /// <summary>
    /// Everything content-related from the old sources, nothing lost:
    ///   * room texts and the 28 room questions + 4 reserve questions (CurriculumQuestions.json, via the old importer's mapping)
    ///   * each level's older inbox emails (from the level assets)
    ///   * the pre-curriculum Level 3-5 messages from the old C# builders, kept as inactive questions of their room
    /// </summary>
    public static QuestionBankFile BuildFromLegacy()
    {
        var legacy = CurriculumQuestionsImporter.Load();
        var bank = new QuestionBankFile
        {
            contentVersion = DateTime.Now.ToString("yyyy-MM-dd") + ".1",
            source = "Migrated from Assets/Editor/CurriculumQuestions.json (" + legacy.source + "), the level assets and the Level 3-5 content builders",
            categories = DefaultCategories,
        };
        var levelByAsset = Catalog.levels.Where(l => l != null).ToDictionary(l => AssetDatabase.GetAssetPath(l));

        foreach (var room in legacy.rooms)
        {
            var asset = levelByAsset[room.levelAsset];
            var rec = new LevelContentRecord
            {
                levelId = asset.levelId, intro = room.intro ?? "", briefingTitle = room.briefingTitle ?? "", briefing = room.briefing ?? "",
                objectives = room.objectives ?? new string[0], caseNotes = room.caseNotes ?? new string[0], lessons = room.lessons ?? new string[0],
                learningSummary = "", tutorial = room.tutorial, firstQuestion = room.tutorial ? room.firstQuestion ?? "" : "",
                inbox = (asset.inboxExtras ?? new List<ScamMessage>()).Select((m, i) => Inbox(m, asset.levelId, i)).ToList(),
            };
            bank.levels.Add(rec);
            int order = 0;
            foreach (var q in room.questions)
                bank.questions.Add(FromLegacy(q, asset.levelId, (order += 10), room.tutorial));
        }
        int r = 0;
        foreach (var q in legacy.reserve ?? new CurriculumQuestionsImporter.QuestionData[0])
        {
            var rec = FromLegacy(q, "", (r += 10), false);
            rec.notes = "Validated reserve question: set levelId to use it in a room.";
            bank.questions.Add(rec);
        }

        // Pre-curriculum messages from the old builders: inactive, so nothing changes in play.
        foreach (var (levelId, fill) in new (string, Action<LevelDefinition>)[]
                 { ("level_3", Level3Content.Fill), ("level_4", Level4Content.Fill), ("level_5", Level5Content.Fill) })
        {
            var temp = ScriptableObject.CreateInstance<LevelDefinition>();
            try
            {
                fill(temp);
                int o = 500;
                foreach (var m in temp.messages)
                {
                    if (bank.questions.Any(x => x.questionId == m.id)) continue;
                    var rec = QuestionMapper.ToRecord(m, levelId, (o += 10), false);
                    rec.isActive = false;
                    rec.category = LegacyCategory(m);
                    rec.difficulty = levelId == "level_3" ? "Medium" : "Hard";
                    rec.questionType = m.channel == MessageChannel.Case ? "Final Case" : "Scam or Legitimate";
                    rec.notes = "Pre-curriculum message from the old " + levelId + " content builder. Inactive: review it before turning it on.";
                    bank.questions.Add(rec);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(temp); }
        }
        return bank;
    }

    static QuestionRecord Inbox(ScamMessage m, string levelId, int i)
    {
        var rec = QuestionMapper.ToRecord(m, levelId, i, false);
        rec.questionType = "Inbox (not scored)";
        rec.category = "";
        rec.difficulty = "";
        return rec;
    }

    static QuestionRecord FromLegacy(CurriculumQuestionsImporter.QuestionData q, string levelId, int order, bool tutorial)
    {
        // The old importer's mapping is the reference: same message, then the extra management fields.
        var m = CurriculumQuestionsImporter.ToMessage(q, tutorial);
        var rec = QuestionMapper.ToRecord(m, levelId, order, tutorial);
        rec.difficulty = q.difficulty ?? "";
        rec.questionType = q.questionType ?? "";
        rec.learningObjective = q.learningObjective ?? "";
        rec.category = MigratedCategories.TryGetValue(q.id, out var c) ? c : "";
        return rec;
    }

    static string LegacyCategory(ScamMessage m) =>
        m.channel == MessageChannel.SMS ? "Smishing" : m.channel == MessageChannel.Website ? "Fake Websites"
        : m.channel == MessageChannel.Chat ? "Social Engineering" : m.channel == MessageChannel.Case ? "Incident Response" : "Phishing";
}
