using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Tools > Scam Sense > Build Release
/// Builds the packaged Windows release for the version in Player Settings > Version (MAJOR.MINOR.PATCH):
///   Builds/ScamSense-v0.1.0/          ScamSense.exe, ScamSense_Data/, UnityPlayer.dll, ...
///                                     README.txt, RELEASE_NOTES.txt, BUILD_INFO.txt (version, commit, date)
///   Builds/ScamSense-v0.1.0.zip       the same folder, ready to upload
/// The release notes come from Docs/Releases/RELEASE_NOTES_v{version}.txt, so every release needs its own notes.
/// The full process (version, build, test, tag) is in Docs/RELEASE_PROCESS.md.
/// </summary>
public static class ReleaseBuilder
{
    public const string ProductFile = "ScamSense";
    public static readonly Regex VersionFormat = new Regex(@"^\d+\.\d+\.\d+$");

    static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);
    public static string NotesPath(string version) => Path.Combine(ProjectRoot, "Docs", "Releases", $"RELEASE_NOTES_v{version}.txt");

    [MenuItem("Tools/Scam Sense/Build Release")]
    static void BuildMenu()
    {
        string version = PlayerSettings.bundleVersion;
        string git = UncommittedFiles();
        string warning = string.IsNullOrWhiteSpace(git) ? "" :
            "\n\nWARNING: there are uncommitted changes. A release should be built from a committed, reviewed version.";
        if (EditorUtility.DisplayDialog("Build Release", $"Build Scam Sense version {version} for Windows?{warning}", "Build", "Cancel"))
            Debug.Log(Build());
    }

    /// <summary>Builds, packages and zips the release. Returns a one-line summary (also used from scripts).</summary>
    public static string Build()
    {
        string version = PlayerSettings.bundleVersion;
        if (!VersionFormat.IsMatch(version))
            return $"Build Release stopped: Player Settings > Version is \"{version}\"; use MAJOR.MINOR.PATCH, e.g. 0.1.0.";
        string notes = NotesPath(version);
        if (!File.Exists(notes))
            return $"Build Release stopped: write the release notes first ({notes}).";

        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0 || !scenes[0].EndsWith("/Menu.unity"))
            return "Build Release stopped: Menu must be the first enabled scene in File > Build Profiles > Scene List.";

        string name = $"{ProductFile}-v{version}";
        string builds = Path.Combine(ProjectRoot, "Builds");
        string folder = Path.Combine(builds, name);
        string zip = Path.Combine(builds, name + ".zip");
        if (Directory.Exists(folder)) Directory.Delete(folder, true);   // never mix files from an older build
        if (File.Exists(zip)) File.Delete(zip);

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(folder, ProductFile + ".exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
            return $"Build Release FAILED ({report.summary.totalErrors} errors) - see the Console.";

        // Debug symbols Unity writes next to the player; they are not part of the game.
        foreach (var dir in Directory.GetDirectories(folder, "*DoNotShip")) Directory.Delete(dir, true);

        string readme = File.ReadAllText(Path.Combine(ProjectRoot, "Docs", "Releases", "README.txt")).Replace("{VERSION}", version);
        File.WriteAllText(Path.Combine(folder, "README.txt"), readme);
        File.Copy(notes, Path.Combine(folder, "RELEASE_NOTES.txt"));

        string commit = Git("rev-parse HEAD").Trim();
        string branch = Git("rev-parse --abbrev-ref HEAD").Trim();
        bool clean = string.IsNullOrWhiteSpace(UncommittedFiles());
        File.WriteAllText(Path.Combine(folder, "BUILD_INFO.txt"),
            $"Product:        Scam Sense: Think Before You Click\r\n" +
            $"Version:        {version}\r\n" +
            $"Platform:       Windows 64-bit (StandaloneWindows64)\r\n" +
            $"Built:          {DateTime.Now:yyyy-MM-dd HH:mm} (local time)\r\n" +
            $"Unity:          {Application.unityVersion}\r\n" +
            $"Source commit:  {(commit.Length > 0 ? commit : "unknown")}\r\n" +
            $"Source branch:  {(branch.Length > 0 ? branch : "unknown")}\r\n" +
            $"Clean source:   {(clean ? "yes (no uncommitted changes)" : "NO - built with uncommitted changes, not a traceable release")}\r\n" +
            $"Scenes:         {string.Join(", ", scenes.Select(Path.GetFileNameWithoutExtension))}\r\n");

        ZipFile.CreateFromDirectory(folder, zip, System.IO.Compression.CompressionLevel.Optimal, includeBaseDirectory: true);
        double mb = new FileInfo(zip).Length / (1024.0 * 1024.0);
        return $"Build Release {version} succeeded: {zip} ({mb:F0} MB, built in {report.summary.totalTime.TotalSeconds:F0} s)" +
               (clean ? "" : "  WARNING: built with uncommitted changes.");
    }

    /// <summary>
    /// Tracked files whose content differs from the last commit. Uses "git diff" rather than "git status": Unity re-saves
    /// some assets during a build with different line endings, which "git status" reports as changed although the content is identical.
    /// </summary>
    static string UncommittedFiles() => Git("diff HEAD --name-only");

    /// <summary>Runs git in the project folder; returns "" if git isn't available.</summary>
    static string Git(string args)
    {
        try
        {
            var p = Process.Start(new ProcessStartInfo("git", args)
            {
                WorkingDirectory = ProjectRoot, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true
            });
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(10000);
            return p.ExitCode == 0 ? output : "";
        }
        catch (Exception) { return ""; }
    }
}
