#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Text;
using Unity.Profiling;
using UnityEngine;

/// <summary>
/// Development-only frame-time probe (compiled out of release builds).
/// PerformanceProbe.Run(seconds, scripted) records every frame for a fixed time and writes a report to
/// Logs/PerformanceProbe.txt: frame time (avg / percentiles / worst), main- and render-thread time, GPU time
/// (when the platform reports it), managed allocations per frame, GC collections, batches, set-pass calls and
/// shadow casters. With scripted = true it drives a repeatable walk-and-turn so before/after runs are comparable.
/// </summary>
public class PerformanceProbe : MonoBehaviour
{
    public static string LastReport { get; private set; } = "";
    public static bool Running => current != null;
    /// <summary>Where reports are appended (the build's benchmark mode passes -perfout).</summary>
    public static string ReportPath = "Logs/PerformanceProbe.txt";
    /// <summary>Benchmark mode with -perfraw: profile only this scene, between these probe times (keeps captures small).</summary>
    static string rawScene;
    static Vector2 rawWindow = new Vector2(2f, 6f);

    static PerformanceProbe current;

    float duration;
    bool scripted;
    float elapsed;
    readonly List<float> frameMs = new List<float>(4096);
    readonly List<float> mainMs = new List<float>(4096);
    readonly List<float> renderMs = new List<float>(4096);
    readonly List<float> gpuMs = new List<float>(4096);
    readonly List<long> gcBytes = new List<long>(4096);
    readonly List<int> gcRuns = new List<int>(4096);     // GC collections that happened during each frame
    readonly List<float> atTime = new List<float>(4096);
    int lastGcCount;
    long batches, setPass, shadowCasters;
    int gcStart;

    ProfilerRecorder mainRec, renderRec, gpuRec, gcRec, batchRec, setPassRec, shadowRec;

    CharacterController controller;
    Transform player, cam;

    public static void Run(float seconds, bool scriptedMotion)
    {
        if (current != null) Destroy(current.gameObject);
        var go = new GameObject("PerformanceProbe");
        current = go.AddComponent<PerformanceProbe>();
        current.duration = seconds;
        current.scripted = scriptedMotion;
    }

    void OnEnable()
    {
        mainRec = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
        renderRec = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Render Thread", 1);
        gpuRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "GPU Frame Time", 1);
        gcRec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
        batchRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", 1);
        setPassRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 1);
        shadowRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shadow Casters Count", 1);
        gcStart = lastGcCount = System.GC.CollectionCount(0);
    }

    void OnDisable()
    {
        mainRec.Dispose(); renderRec.Dispose(); gpuRec.Dispose(); gcRec.Dispose();
        batchRec.Dispose(); setPassRec.Dispose(); shadowRec.Dispose();
        if (current == this) current = null;
    }

    void Start()
    {
        var cc = FindFirstObjectByType<CharacterController>();
        if (cc != null)
        {
            controller = cc;
            player = cc.transform;
            var c = cc.GetComponentInChildren<Camera>();
            cam = c != null ? c.transform : null;
        }
    }

    void Update()
    {
        // Skip the first frame (it includes the probe's own setup).
        if (elapsed > 0f)
        {
            frameMs.Add(Time.unscaledDeltaTime * 1000f);
            mainMs.Add(mainRec.LastValue * 1e-6f);
            renderMs.Add(renderRec.LastValue * 1e-6f);
            gpuMs.Add(gpuRec.LastValue * 1e-6f);
            gcBytes.Add(gcRec.LastValue);
            int gcNow = System.GC.CollectionCount(0);
            gcRuns.Add(gcNow - lastGcCount);
            lastGcCount = gcNow;
            atTime.Add(elapsed);
            batches += batchRec.LastValue;
            setPass += setPassRec.LastValue;
            shadowCasters += shadowRec.LastValue;
        }
        elapsed += Time.unscaledDeltaTime;

        if (rawScene != null && rawScene == UnityEngine.SceneManagement.SceneManager.GetActiveScene().name)
            UnityEngine.Profiling.Profiler.enabled = elapsed >= rawWindow.x && elapsed < rawWindow.y;

        if (scripted && player != null && controller != null && controller.enabled)
        {
            // Repeatable motion: walk a circle (radius ~1.5 m) while turning and nodding the camera.
            float t = elapsed;
            float dt = Time.deltaTime;
            player.Rotate(0f, 70f * dt, 0f);
            controller.Move((player.forward * 2.2f + Vector3.down * 2f) * dt);
            if (cam != null) cam.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.3f) * 25f, 0f, 0f);
        }

        if (elapsed >= duration) Finish();
    }

    void Finish()
    {
        var sb = new StringBuilder();
        int n = frameMs.Count;
        sb.AppendLine($"Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}   frames: {n}   seconds: {elapsed:F1}   scripted: {scripted}   quality: {QualitySettings.names[QualitySettings.GetQualityLevel()]}   vSync: {QualitySettings.vSyncCount}");
        if (n > 0)
        {
            sb.AppendLine("Frame ms  " + Stats(frameMs));
            sb.AppendLine("Main  ms  " + Stats(mainMs));
            sb.AppendLine("Render ms " + Stats(renderMs));
            sb.AppendLine("GPU   ms  " + Stats(gpuMs));
            long total = 0, max = 0; int allocFrames = 0;
            foreach (var b in gcBytes) { total += b; if (b > max) max = b; if (b > 0) allocFrames++; }
            sb.AppendLine($"GC alloc  total {total / 1024f:F1} KB   avg {total / (float)n:F0} B/frame   max {max} B   frames allocating {allocFrames}/{n}   collections {System.GC.CollectionCount(0) - gcStart}");
            sb.AppendLine($"Avg batches {batches / (float)n:F0}   set-pass {setPass / (float)n:F0}   shadow casters {shadowCasters / (float)n:F0}");
            int spikes = 0; float median = Percentile(frameMs, 0.5f);
            foreach (var f in frameMs) if (f > median * 2f) spikes++;
            sb.AppendLine($"Spikes (> 2x median): {spikes}");

            // The 6 worst frames: when they happened and what else happened in that frame.
            var order = new List<int>(n);
            for (int i = 0; i < n; i++) order.Add(i);
            order.Sort((a, b) => frameMs[b].CompareTo(frameMs[a]));
            for (int k = 0; k < Mathf.Min(6, n); k++)
            {
                int i = order[k];
                sb.AppendLine($"   worst #{k + 1}: {frameMs[i]:F1} ms at {atTime[i]:F2}s  main {mainMs[i]:F1}  gpu {gpuMs[i]:F1}  alloc {gcBytes[i]} B  GC runs {gcRuns[i]}");
            }
        }
        LastReport = sb.ToString();
        System.IO.File.AppendAllText(ReportPath, LastReport + "\n");
        Debug.Log("PerformanceProbe\n" + LastReport);
        Destroy(gameObject);
    }

    // ---------- Benchmark mode (development build launched with -perfprobe) ----------
    // Loads each level in the build, starts it (BEGIN), records the scripted walk-and-turn, writes the
    // report to -perfout and quits. Saved progress is snapshotted first and restored before quitting,
    // so a benchmark run leaves no trace. Normal play never enters this path.

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoBenchmark()
    {
        var args = System.Environment.GetCommandLineArgs();
        if (System.Array.IndexOf(args, "-perfprobe") < 0) return;
        int o = System.Array.IndexOf(args, "-perfout");
        if (o >= 0 && o + 1 < args.Length) ReportPath = args[o + 1];
        var go = new GameObject("PerformanceBenchmark");
        DontDestroyOnLoad(go);
        go.AddComponent<BenchmarkRunner>();
    }

    class BenchmarkRunner : MonoBehaviour
    {
        const string Base = "scamsense_progress_v1", Active = "scamsense_active_profile", Migrated = "scamsense_legacy_migrated";
        readonly Dictionary<string, string> savedStrings = new Dictionary<string, string>();
        int savedMigrated = int.MinValue;

        bool restored;

        void OnEnable() => Application.quitting += Restore;    // also restore if the window is closed early
        void OnDisable() => Application.quitting -= Restore;

        System.Collections.IEnumerator Start()
        {
            Snapshot();

            // Optional: save full profiler data for analysis in the Editor (Profiler window > Load).
            var args = System.Environment.GetCommandLineArgs();
            int r = System.Array.IndexOf(args, "-perfraw");
            if (r >= 0 && r + 1 < args.Length)
            {
                UnityEngine.Profiling.Profiler.maxUsedMemory = 256 * 1024 * 1024;
                UnityEngine.Profiling.Profiler.logFile = args[r + 1];
                UnityEngine.Profiling.Profiler.enableBinaryLog = true;
                int s = System.Array.IndexOf(args, "-perfrawscene");
                rawScene = s >= 0 && s + 1 < args.Length ? args[s + 1] : "Game";
            }
            QualitySettings.vSyncCount = 0;          // measure the real cost, not the monitor's refresh cap
            Application.targetFrameRate = -1;
            System.IO.File.AppendAllText(ReportPath, $"=== Benchmark {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}  {Screen.width}x{Screen.height}  {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType})  {SystemInfo.processorType}\n");

            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
                string scene = System.IO.Path.GetFileNameWithoutExtension(path);
                if (scene == "Menu") continue;
                int only = System.Array.IndexOf(args, "-benchscene");
                if (only >= 0 && only + 1 < args.Length && args[only + 1] != scene) continue;

                float t0 = Time.realtimeSinceStartup;
                var op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(scene);
                while (!op.isDone) yield return null;
                System.IO.File.AppendAllText(ReportPath, $"Load {scene}: {(Time.realtimeSinceStartup - t0) * 1000f:F0} ms\n");
                ApplyExperiment(args);
                yield return new WaitForSecondsRealtime(2f);

                foreach (var b in FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None))
                {
                    var label = b.GetComponentInChildren<TMPro.TMP_Text>();
                    if (label != null && label.text.Contains("BEGIN")) { b.onClick.Invoke(); break; }
                }
                yield return new WaitForSecondsRealtime(1f);

                Run(15f, true);
                while (Running) yield return null;
            }

            UnityEngine.Profiling.Profiler.enabled = false;
            UnityEngine.Profiling.Profiler.enableBinaryLog = false;
            Restore();
            Application.Quit();
        }

        void Update()
        {
            // A stray click or focus change must not pause the benchmark (timeScale 0 would freeze the scripted motion).
            if (PauseMenu.IsPaused && PauseMenu.Instance != null) PauseMenu.Instance.Resume();
        }

        /// <summary>
        /// Runtime-only "what if" switches for measuring a change before making it in the project:
        /// -benchexp deferred | pixel4 | nopointshadows (comma-separated). They only affect this benchmark run.
        /// </summary>
        void ApplyExperiment(string[] args)
        {
            int e = System.Array.IndexOf(args, "-benchexp");
            if (e < 0 || e + 1 >= args.Length) return;
            string exp = args[e + 1];
            if (exp.Contains("deferred"))
                foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.renderingPath = RenderingPath.DeferredShading;
            if (exp.Contains("pixel4")) QualitySettings.pixelLightCount = 4;
            if (exp.Contains("shadowhigh")) QualitySettings.shadowResolution = ShadowResolution.High;
            if (exp.Contains("shadowmedium")) QualitySettings.shadowResolution = ShadowResolution.Medium;
            if (exp.Contains("noaa")) QualitySettings.antiAliasing = 0;
            if (exp.Contains("pointmedium") || exp.Contains("pointlow"))
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Point && l.shadows != LightShadows.None)
                        l.shadowResolution = exp.Contains("pointlow") ? UnityEngine.Rendering.LightShadowResolution.Low : UnityEngine.Rendering.LightShadowResolution.Medium;
            if (exp.Contains("hard"))
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.shadows == LightShadows.Soft) l.shadows = LightShadows.Hard;
            if (exp.Contains("nopointshadows"))
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Point) l.shadows = LightShadows.None;
            System.IO.File.AppendAllText(ReportPath, $"Experiment: {exp}\n");
        }

        void Snapshot()
        {
            string active = PlayerPrefs.GetString(Active, "");
            foreach (var k in new[] { Base, Active, Base + "_" + active })
                savedStrings[k] = PlayerPrefs.HasKey(k) ? PlayerPrefs.GetString(k) : null;
            if (PlayerPrefs.HasKey(Migrated)) savedMigrated = PlayerPrefs.GetInt(Migrated);
        }

        void Restore()
        {
            if (restored) return;
            restored = true;
            foreach (var kv in savedStrings)
            {
                if (kv.Value == null) PlayerPrefs.DeleteKey(kv.Key);
                else PlayerPrefs.SetString(kv.Key, kv.Value);
            }
            if (savedMigrated == int.MinValue) PlayerPrefs.DeleteKey(Migrated);
            else PlayerPrefs.SetInt(Migrated, savedMigrated);
            PlayerPrefs.Save();
        }
    }

    static string Stats(List<float> v)
    {
        float sum = 0f, max = 0f;
        foreach (var x in v) { sum += x; if (x > max) max = x; }
        return $"avg {sum / v.Count:F2}   p50 {Percentile(v, 0.5f):F2}   p95 {Percentile(v, 0.95f):F2}   p99 {Percentile(v, 0.99f):F2}   max {max:F2}";
    }

    static float Percentile(List<float> v, float p)
    {
        var s = new List<float>(v);
        s.Sort();
        return s[Mathf.Clamp(Mathf.RoundToInt(p * (s.Count - 1)), 0, s.Count - 1)];
    }
}
#endif
