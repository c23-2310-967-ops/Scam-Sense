using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Tools > Scam Sense > Performance > Build Benchmark Player
/// Builds a Windows development player with the three level scenes (no Menu, so no sign-in or Cloud Save runs).
/// Run it with:  ScamSenseBenchmark.exe -perfprobe -perfout "C:\path\report.txt" -screen-width 1600 -screen-height 900 -screen-fullscreen 0
/// It plays every level with a scripted walk-and-turn, writes frame-time statistics, restores its saved
/// progress and quits (see PerformanceProbe). Your normal Build Settings are not changed.
/// </summary>
public static class PerformanceBenchmarkBuild
{
    [MenuItem("Tools/Scam Sense/Performance/Build Benchmark Player")]
    static void BuildMenu()
    {
        string dir = EditorUtility.SaveFolderPanel("Benchmark build folder", "", "ScamSenseBenchmark");
        if (!string.IsNullOrEmpty(dir)) Debug.Log(Build(dir));
    }

    /// <param name="connectProfiler">Also stream profiler data to this Editor (Window > Analysis > Profiler) while it runs.</param>
    public static string Build(string folder, bool connectProfiler = false)
    {
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled && !s.path.EndsWith("/Menu.unity")).Select(s => s.path).ToArray();
        bool timing = PlayerSettings.enableFrameTimingStats;
        PlayerSettings.enableFrameTimingStats = true;   // lets the probe read GPU frame time
        try
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = System.IO.Path.Combine(folder, "ScamSenseBenchmark.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development | (connectProfiler ? BuildOptions.ConnectWithProfiler : BuildOptions.None)
            });
            return $"Benchmark build {report.summary.result}: {report.summary.outputPath}  ({report.summary.totalTime.TotalSeconds:F0} s, {report.summary.totalErrors} errors)";
        }
        finally
        {
            PlayerSettings.enableFrameTimingStats = timing;
        }
    }
}
