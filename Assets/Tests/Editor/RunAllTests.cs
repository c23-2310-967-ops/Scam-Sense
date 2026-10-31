using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>
/// Tools > Scam Sense > Run All Tests
/// Runs every test in Assets/Tests (Edit Mode, a few seconds, nothing is saved) and writes a readable report to
/// TestResults/latest.txt in the project folder. The Console shows one line per failure plus the summary.
/// Run it every time a feature is finished, before committing.
/// The same tests can also be run from Window > General > Test Runner, or from the command line (Tests/run-tests.ps1).
/// </summary>
public static class RunAllTests
{
    public const string ReportPath = "TestResults/latest.txt";

    [MenuItem("Tools/Scam Sense/Run All Tests", priority = 0)]
    public static void Run()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Run All Tests: stop Play Mode first."); return; }
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new Report(api));
        api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
    }

    class Report : ICallbacks
    {
        readonly TestRunnerApi api;
        public Report(TestRunnerApi api) { this.api = api; }

        public void RunStarted(ITestAdaptor tests) => Debug.Log($"Run All Tests: running {tests.TestCaseCount} tests...");
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }

        public void RunFinished(ITestResultAdaptor result)
        {
            var leaves = new List<ITestResultAdaptor>();
            Collect(result, leaves);
            int passed = 0, failed = 0, skipped = 0;
            var sb = new StringBuilder();
            sb.AppendLine($"Scam Sense test report - {System.DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine();

            string group = null;
            foreach (var r in leaves)
            {
                string cls = ClassOf(r.Test.FullName);
                if (cls != group) { group = cls; sb.AppendLine(cls); }
                string mark;
                switch (r.TestStatus)
                {
                    case TestStatus.Passed: passed++; mark = "  PASS  "; break;
                    case TestStatus.Failed: failed++; mark = "  FAIL  "; break;
                    default: skipped++; mark = "  SKIP  "; break;
                }
                sb.AppendLine(mark + r.Test.Name);
                if (r.TestStatus != TestStatus.Passed && !string.IsNullOrEmpty(r.Message))
                    foreach (var line in r.Message.Trim().Split('\n')) sb.AppendLine("          " + line.TrimEnd());
                if (r.TestStatus == TestStatus.Failed)
                    Debug.LogError($"FAILED  {cls}.{r.Test.Name}\n{r.Message}");
            }

            string summary = $"{passed} passed, {failed} failed, {skipped} skipped  ({result.Duration:0.0} s)";
            sb.AppendLine().AppendLine(summary);
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, sb.ToString());

            string msg = $"Run All Tests: {summary}. Report: {Path.GetFullPath(ReportPath)}";
            if (failed > 0) Debug.LogError(msg); else Debug.Log(msg);
            api.UnregisterCallbacks(this);
        }

        /// <summary>"ScoringTests.Clock(5f,\"0:05\")" -> "ScoringTests" (arguments may contain dots).</summary>
        static string ClassOf(string fullName)
        {
            int paren = fullName.IndexOf('(');
            string name = paren >= 0 ? fullName.Substring(0, paren) : fullName;
            int dot = name.LastIndexOf('.');
            return dot > 0 ? name.Substring(0, dot) : name;
        }

        static void Collect(ITestResultAdaptor r, List<ITestResultAdaptor> into)
        {
            if (!r.HasChildren) { into.Add(r); return; }
            foreach (var c in r.Children) Collect(c, into);
        }
    }
}
