using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cybersecurity career: XP, ranks and achievements, on top of the existing save (LocalDataManager).
///
/// - <see cref="RecordCase"/> is the only place that awards career XP. GameManager calls it once, when a case
///   is closed (every message answered) - entering a room or quitting earns nothing.
/// - Each case remembers its best XP. Total XP = the sum of every case's best, so replaying only adds the
///   improvement over your best run, never the same reward twice.
/// - The rank is derived from total XP (never stored), using the thresholds in <see cref="CareerConfig"/>.
/// - Achievements are saved permanently (cleared only by RESET PROGRESS, like the rest of the progress).
/// </summary>
public static class CareerSystem
{
    public const string AchFirstCase = "first_case", AchFlawless = "flawless", AchRapid = "rapid", AchEvidence = "evidence_expert",
                        AchConsistent = "consistent", AchPromotion = "promotion_day", AchForensics = "forensics",
                        AchAllCases = "all_cases", AchElite = "elite", AchMaster = "master";

    public struct XpLine
    {
        public string label;
        public int xp;
        public XpLine(string label, int xp) { this.label = label; this.xp = xp; }
    }

    /// <summary>Everything the results screen needs about one closed case.</summary>
    public class CaseCareer
    {
        public int caseNumber;
        public string caseName;
        public bool solved;
        public string outcome;
        public readonly List<XpLine> lines = new List<XpLine>();
        public int runXp;            // what this run was worth
        public int previousBestXp;   // this case's best before the run
        public int awardedXp;        // what was actually added to the career (improvement only)
        public int previousTotalXp, totalXp;
        public int previousRank, rank;
        public readonly List<CareerConfig.Achievement> newAchievements = new List<CareerConfig.Achievement>();
        public bool Promoted => rank > previousRank;
    }

    static CareerConfig config;

    /// <summary>Resources/CareerConfig, or the built-in defaults if that asset is missing.</summary>
    public static CareerConfig Config
    {
        get
        {
            if (config != null) return config;
            config = Resources.Load<CareerConfig>("CareerConfig");
            if (config == null)
            {
                Debug.LogWarning("CareerSystem: Resources/CareerConfig.asset not found - using the default rank table.");
                config = ScriptableObject.CreateInstance<CareerConfig>();
            }
            if (!config.Validate(out var problem)) Debug.LogError("CareerSystem: CareerConfig rank table is invalid: " + problem);
            return config;
        }
    }

    public static IReadOnlyList<CareerConfig.Rank> Ranks => Config.ranks;

    // ---------- Derived career state ----------

    /// <summary>Career XP of one saved case: its best run, or an estimate for cases finished before careers existed.</summary>
    public static int CaseXp(LocalDataManager.LevelProgress lp)
    {
        if (lp == null) return 0;
        if (lp.messageCount > 0 || lp.bestXp > 0) return lp.bestXp;   // has career data (any run since careers exist)
        if (!lp.completed) return 0;
        // Older saves only know the best score: credit closing/solving plus quality, conservatively.
        return Mathf.RoundToInt(lp.bestScore * 0.6f) + Config.completionXp + (lp.passed ? Config.solvedXp : 0);
    }

    public static int TotalXp
    {
        get
        {
            int sum = 0;
            foreach (var lp in LocalDataManager.Levels) sum += CaseXp(lp);
            return sum;
        }
    }

    public static int RankIndexFor(int xp)
    {
        var r = Config.ranks;
        int i = 0;
        while (i + 1 < r.Count && xp >= r[i + 1].minXp) i++;
        return i;
    }

    public static CareerConfig.Rank RankAt(int index) => Config.ranks[Mathf.Clamp(index, 0, Config.ranks.Count - 1)];
    public static bool IsTopRank(int index) => index >= Config.ranks.Count - 1;

    /// <summary>Progress inside a rank: XP into it and the size of the step to the next one (0 at the top rank).</summary>
    public static void ProgressInRank(int xp, out int rank, out int into, out int span)
    {
        rank = RankIndexFor(xp);
        int start = RankAt(rank).minXp;
        into = xp - start;
        span = IsTopRank(rank) ? 0 : RankAt(rank + 1).minXp - start;
    }

    public static int CasesSolved
    {
        get { int n = 0; foreach (var lp in LocalDataManager.Levels) if (lp.passed) n++; return n; }
    }

    public static int PerfectCases
    {
        get { int n = 0; foreach (var lp in LocalDataManager.Levels) if (lp.perfect) n++; return n; }
    }

    /// <summary>Accuracy of your best runs (correct / messages), or -1 when no case has career data yet.</summary>
    public static float BestRunAccuracy
    {
        get
        {
            int c = 0, t = 0;
            foreach (var lp in LocalDataManager.Levels) if (lp.messageCount > 0) { c += lp.bestCorrect; t += lp.messageCount; }
            return t > 0 ? (float)c / t : -1f;
        }
    }

    public static CareerConfig.Achievement AchievementDef(string id) => Config.achievements.Find(a => a.id == id);

    // ---------- The one place XP is awarded ----------

    /// <summary>The career before a run is saved (the run's score changes what "completed" means for the estimate).</summary>
    public struct Snapshot { public int caseXp, totalXp; }

    /// <summary>Take this before LocalDataManager.RecordResult, then pass it to <see cref="RecordCase"/>.</summary>
    public static Snapshot TakeSnapshot(string levelId) =>
        new Snapshot { caseXp = CaseXp(LocalDataManager.Peek(levelId)), totalXp = TotalXp };

    /// <summary>
    /// Scores a closed case and saves its career result. Call once per finished run, after
    /// LocalDataManager.RecordResult, with the <see cref="Snapshot"/> taken before it.
    /// <paramref name="requiredEvidence"/>/<paramref name="evidence"/> are optional (cases with an evidence checklist).
    /// </summary>
    public static CaseCareer RecordCase(LevelDefinition level, Snapshot before, ScoringSystem scoring, bool solved,
                                        IReadOnlyList<string> requiredEvidence = null, EvidenceTracker evidence = null)
    {
        var cfg = Config;
        var res = new CaseCareer { caseNumber = level.levelNumber, caseName = level.displayName, solved = solved };
        res.previousBestXp = before.caseXp;
        res.previousTotalXp = before.totalXp;
        res.previousRank = RankIndexFor(res.previousTotalXp);

        int total = level.MessageCount;
        int answered = scoring.Results.Count;
        int correct = scoring.CorrectCount;
        bool closed = total > 0 && answered >= total;
        bool allCorrect = closed && correct >= total;
        float f = 1f + cfg.difficultyStep * Mathf.Max(0, level.levelNumber - 1);
        int Scale(float xp) => Mathf.RoundToInt(xp * f);

        if (closed) res.lines.Add(new XpLine("Case closed", Scale(cfg.completionXp)));
        if (closed && solved) res.lines.Add(new XpLine("Case solved (passing score)", Scale(cfg.solvedXp)));
        if (correct > 0) res.lines.Add(new XpLine($"Correct decisions  ×{correct}", Scale(cfg.perCorrectDecisionXp * correct)));
        int max = scoring.MaxScoreFor(total);
        if (max > 0 && scoring.TotalScore > 0)
            res.lines.Add(new XpLine("Response quality", Scale(cfg.responseQualityXp * Mathf.Clamp01((float)scoring.TotalScore / max))));
        if (allCorrect) res.lines.Add(new XpLine("Flawless accuracy", Scale(cfg.flawlessXp)));

        int evidenceNeeded = requiredEvidence != null ? requiredEvidence.Count : 0, evidenceSeen = 0;
        if (evidenceNeeded > 0 && evidence != null)
            foreach (var e in requiredEvidence) if (evidence.IsInspected(e)) evidenceSeen++;
        if (evidenceSeen > 0) res.lines.Add(new XpLine($"Evidence inspected  {evidenceSeen}/{evidenceNeeded}", Scale(cfg.perEvidenceXp * evidenceSeen)));

        bool finalCorrect = false;
        foreach (var r in scoring.Results)
            if (r.message != null && r.message.channel == MessageChannel.Case && r.isCorrect) finalCorrect = true;
        if (finalCorrect) res.lines.Add(new XpLine("Final conclusion correct", Scale(cfg.finalConclusionXp)));

        foreach (var l in res.lines) res.runXp += l.xp;
        res.outcome = !closed ? "CASE INCOMPLETE" : solved ? "CASE SOLVED" : "CASE CLOSED  ·  NEEDS REVIEW";

        // Save the best values (improvement-only), then read the career back from the save.
        // Always saved (even 0 XP), so the estimate for pre-career saves never applies to a played run.
        LocalDataManager.RecordCaseCareer(level.levelId, Mathf.Max(res.runXp, res.previousBestXp), correct, total, allCorrect);
        res.totalXp = TotalXp;
        res.awardedXp = Mathf.Max(0, res.totalXp - res.previousTotalXp);
        res.rank = RankIndexFor(res.totalXp);

        // Achievements: only from what actually happened in this run (or the saved career it produced).
        void Grant(string id) { if (LocalDataManager.UnlockAchievement(id)) { var d = AchievementDef(id); if (d != null) res.newAchievements.Add(d); } }
        if (closed && solved) Grant(AchFirstCase);
        if (allCorrect) Grant(AchFlawless);
        if (allCorrect && scoring.MaxPointsPerMessage > 0 && scoring.Results.Count > 0)
        {
            bool fullSpeed = true;
            foreach (var r in scoring.Results) if (r.points < scoring.MaxPointsPerMessage) fullSpeed = false;
            if (fullSpeed) Grant(AchRapid);
        }
        if (closed && evidenceNeeded > 0 && evidenceSeen >= evidenceNeeded) Grant(AchEvidence);
        int consistent = 0;
        foreach (var lp in LocalDataManager.Levels)
            if (lp.messageCount > 0 && (float)lp.bestCorrect / lp.messageCount >= cfg.consistentAccuracy) consistent++;
        if (consistent >= cfg.consistentCases) Grant(AchConsistent);
        if (res.Promoted) Grant(AchPromotion);
        if (closed && solved && finalCorrect) Grant(AchForensics);
        if (CasesSolved >= cfg.casesInGame) Grant(AchAllCases);
        if (res.rank >= cfg.ranks.Count - 2) Grant(AchElite);
        if (IsTopRank(res.rank)) Grant(AchMaster);
        return res;
    }
}
