using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>A question bank that was read and checked, and where it came from.</summary>
public class LoadedQuestionBank
{
    public QuestionBankFile data;
    public ContentSource source;
    /// <summary>File path or resource name it was read from.</summary>
    public string origin = "";
    public List<ContentIssue> issues = new List<ContentIssue>();

    public bool Usable => data != null && !issues.Any(i => i.IsError && i.where.Length == 0);
    public LevelContentRecord Level(string levelId) => data?.levels?.FirstOrDefault(l => l != null && l.levelId == levelId);
}

/// <summary>
/// Finds the question bank to play. Supabase is the content source; the bundled file is the offline copy.
/// Sources, first usable one wins:
///   1. Cloud cache     - {persistentDataPath}/content/question_bank_cloud.json (last Supabase download, see CloudContentSync)
///   2. Local override  - {persistentDataPath}/content/question_bank_override.json (testing an edit on a PC that has no download)
///   3. Bundled         - Assets/Resources/Content/question_bank.json (ships with every build: first run, offline, fallback)
/// A file that is missing, isn't valid JSON or fails a file-level check is skipped (logged), never fatal.
/// If none is usable, levels play the questions baked into their LevelDefinition assets.
/// Loaded once and reused; <see cref="Reload"/> after a sync or an edit.
/// </summary>
public static class QuestionBankRepository
{
    public const string BundledResource = "Content/question_bank";
    public const string BundledAssetPath = "Assets/Resources/Content/question_bank.json";

    public static string ContentFolder => Path.Combine(Application.persistentDataPath, "content");
    public static string OverridePath => Path.Combine(ContentFolder, "question_bank_override.json");
    public static string CloudCachePath => Path.Combine(ContentFolder, "question_bank_cloud.json");

    static LoadedQuestionBank current;
    static LoadedQuestionBank injected;
    static bool loaded;

    /// <summary>Raised after <see cref="Reload"/> picks a bank (e.g. after a cloud download).</summary>
    public static event Action<LoadedQuestionBank> Changed;

    /// <summary>The bank in use, or null if no source is usable (levels then use their built-in questions).</summary>
    public static LoadedQuestionBank Current
    {
        get
        {
            if (injected != null) return injected;
            if (!loaded) Reload();
            return current;
        }
    }

    public static LoadedQuestionBank Reload()
    {
        loaded = true;
        current = null;
        foreach (var candidate in Candidates())
        {
            if (candidate == null) continue;
            if (candidate.Usable) { current = candidate; break; }
            Debug.LogWarning($"Question bank: skipped {candidate.source} ({candidate.origin}):\n" +
                             string.Join("\n", candidate.issues.Where(i => i.IsError && i.where.Length == 0)));
        }
        if (current != null)
        {
            int errors = current.issues.Count(i => i.IsError);
            string msg = $"Question bank: using {current.source} content '{current.data.contentVersion}' ({current.data.questions.Count} questions" +
                         (errors > 0 ? $", {errors} problems - those questions are left out" : "") + ").";
            if (errors > 0) Debug.LogWarning(msg + "\n" + string.Join("\n", current.issues.Where(i => i.IsError))); else Debug.Log(msg);
        }
        else Debug.LogWarning("Question bank: no usable content file; levels use their built-in questions.");
        Changed?.Invoke(current);
        return current;
    }

    static IEnumerable<LoadedQuestionBank> Candidates()
    {
        yield return FromFile(CloudCachePath, ContentSource.CloudCache);
        yield return FromFile(OverridePath, ContentSource.LocalOverride);
        var asset = Resources.Load<TextAsset>(BundledResource);
        yield return asset != null ? Parse(asset.text, ContentSource.Bundled, BundledAssetPath) : null;
    }

    static LoadedQuestionBank FromFile(string path, ContentSource source)
    {
        try { return File.Exists(path) ? Parse(File.ReadAllText(path), source, path) : null; }
        catch (Exception e)
        {
            Debug.LogWarning($"Question bank: can't read {path} ({e.Message}).");
            return null;
        }
    }

    /// <summary>Reads and checks bank JSON. Never throws: a broken file comes back with a file-level error.</summary>
    public static LoadedQuestionBank Parse(string json, ContentSource source, string origin = "")
    {
        var bank = new LoadedQuestionBank { source = source, origin = origin ?? "" };
        try { bank.data = string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<QuestionBankFile>(json); }
        catch (Exception e) { bank.issues.Add(new ContentIssue(ContentSeverity.Error, "", "not valid JSON: " + e.Message)); return bank; }
        bank.issues.AddRange(QuestionBankValidator.Validate(bank.data));
        return bank;
    }

    // ---------- Tests and tools ----------

    /// <summary>Plays this bank instead of the files (tests, previews). Pass null to go back to the files.</summary>
    public static void UseForTesting(LoadedQuestionBank bank) => injected = bank;

    public static void UseForTesting(QuestionBankFile file) =>
        injected = file == null ? null : new LoadedQuestionBank { data = file, source = ContentSource.LocalOverride, origin = "test", issues = QuestionBankValidator.Validate(file) };

    /// <summary>Writes a bank as the cloud cache (atomic: a half-written file is never read).</summary>
    public static void WriteCloudCache(string json)
    {
        Directory.CreateDirectory(ContentFolder);
        string tmp = CloudCachePath + ".tmp";
        File.WriteAllText(tmp, json);
        if (File.Exists(CloudCachePath)) File.Delete(CloudCachePath);
        File.Move(tmp, CloudCachePath);
    }
}
