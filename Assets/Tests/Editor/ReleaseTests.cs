using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Release checks: a build must carry a proper version number and its own release notes
/// (see Docs/RELEASE_PROCESS.md), and Tools > Scam Sense > Build Release refuses to run without them.
/// </summary>
public class ReleaseTests
{
    [Test]
    public void Version_IsMajorMinorPatch()
    {
        string v = PlayerSettings.bundleVersion;
        Assert.IsTrue(ReleaseBuilder.VersionFormat.IsMatch(v), $"Player Settings > Version is \"{v}\"; use MAJOR.MINOR.PATCH, e.g. 0.1.0");
    }

    [Test]
    public void ReleaseNotes_ExistForCurrentVersion()
    {
        string path = ReleaseBuilder.NotesPath(PlayerSettings.bundleVersion);
        Assert.IsTrue(File.Exists(path), $"Missing release notes for this version: {path}");
        StringAssert.Contains("Version:        " + PlayerSettings.bundleVersion, File.ReadAllText(path), "The release notes name a different version");
    }

    [Test]
    public void ReleaseReadme_Exists()
    {
        string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Docs", "Releases", "README.txt");
        Assert.IsTrue(File.Exists(path), "Docs/Releases/README.txt (copied into every release) is missing");
    }

    [Test]
    public void BuildTarget_IsWindows64()
    {
        Assert.AreEqual(BuildTarget.StandaloneWindows64, EditorUserBuildSettings.activeBuildTarget, "The release platform is Windows 64-bit");
    }
}
