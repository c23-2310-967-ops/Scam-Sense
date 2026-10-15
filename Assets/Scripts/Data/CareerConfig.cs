using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// All career numbers in one place: ranks with their cumulative XP thresholds, the XP awarded for each part of
/// a case, and the achievements. Loaded from Resources/CareerConfig (the defaults below are used if the asset
/// is missing). Nothing else in the project hardcodes a rank threshold or an XP value.
/// </summary>
[CreateAssetMenu(fileName = "CareerConfig", menuName = "Scam Sense/Career Config")]
public class CareerConfig : ScriptableObject
{
    [Serializable]
    public class Rank
    {
        public string title;
        [Tooltip("Total career XP needed to hold this rank. The first rank must be 0; each next one must be higher.")]
        public int minXp;
        [TextArea(2, 3)] public string summary;
        public Color color = Color.white;
        public Rank(string title, int minXp, string summary, string hex)
        {
            this.title = title; this.minXp = minXp; this.summary = summary;
            ColorUtility.TryParseHtmlString(hex, out color);
        }
    }

    [Serializable]
    public class Achievement
    {
        public string id;
        public string title;
        [TextArea(1, 3)] public string description;
        public Achievement(string id, string title, string description) { this.id = id; this.title = title; this.description = description; }
    }

    [Header("Ranks (cumulative XP, lowest first)")]
    public List<Rank> ranks = new List<Rank>
    {
        new Rank("Cybersecurity Trainee", 0, "Learning the basics of spotting scam messages.", "#94A3B8"),
        new Rank("Junior Scam Analyst", 150, "You can tell basic suspicious messages and links from genuine ones.", "#7DD3FC"),
        new Rank("Scam Detection Specialist", 350, "You identify scams and legitimate messages consistently.", "#38BDF8"),
        new Rank("Cybersecurity Investigator", 650, "You solve harder cases by checking senders and context.", "#22D3EE"),
        new Rank("Senior Cybersecurity Investigator", 1000, "Strong investigation skills across several cases.", "#2DD4BF"),
        new Rank("Digital Forensics Analyst", 1400, "You analyse complex cases using multiple pieces of evidence.", "#34D399"),
        new Rank("Cybercrime Specialist", 1800, "Advanced, careful decisions under pressure.", "#A3E635"),
        new Rank("Elite Cybersecurity Agent", 2100, "A high-level rank for experienced, accurate investigators.", "#FBBF24"),
        new Rank("Master Digital Investigator", 2300, "Exceptional performance across every case in the game.", "#F59E0B"),
    };

    [Header("XP per case (multiplied by the case's difficulty)")]
    [Tooltip("Difficulty multiplier = 1 + this x (level number - 1). Later cases are worth more.")]
    public float difficultyStep = 0.25f;
    [Tooltip("Every message in the case was answered (the case was closed).")]
    public int completionXp = 60;
    [Tooltip("The case reached its passing score.")]
    public int solvedXp = 60;
    public int perCorrectDecisionXp = 25;
    [Tooltip("Scaled by investigation score / maximum score (rewards quick, correct answers).")]
    public int responseQualityXp = 80;
    [Tooltip("Every message identified correctly.")]
    public int flawlessXp = 50;
    [Tooltip("For each required piece of evidence that was actually opened (cases with an evidence checklist).")]
    public int perEvidenceXp = 15;
    [Tooltip("The final multi-evidence case was concluded correctly.")]
    public int finalConclusionXp = 40;

    [Header("Achievements")]
    [Tooltip("How many cases the game has (for 'solve every case').")]
    public int casesInGame = 4;
    [Tooltip("Best-run accuracy that counts as consistent, and how many cases need it.")]
    [Range(0f, 1f)] public float consistentAccuracy = 0.8f;
    public int consistentCases = 3;
    public List<Achievement> achievements = new List<Achievement>
    {
        new Achievement(CareerSystem.AchFirstCase, "First Case Solved", "Solve your first case."),
        new Achievement(CareerSystem.AchFlawless, "Flawless Analysis", "Identify every message in a case correctly."),
        new Achievement(CareerSystem.AchRapid, "Rapid Response", "Answer every message in a case correctly and at full speed."),
        new Achievement(CareerSystem.AchEvidence, "Evidence Expert", "Inspect all the required evidence before closing a case."),
        new Achievement(CareerSystem.AchConsistent, "Consistent Analyst", "Reach 80% accuracy in three different cases."),
        new Achievement(CareerSystem.AchPromotion, "Promotion Day", "Earn a new career rank."),
        new Achievement(CareerSystem.AchForensics, "Master of Digital Forensics", "Solve a final multi-evidence case correctly."),
        new Achievement(CareerSystem.AchAllCases, "Case Load Cleared", "Solve every case in the game."),
        new Achievement(CareerSystem.AchElite, "Elite Investigator", "Reach the rank of Elite Cybersecurity Agent."),
        new Achievement(CareerSystem.AchMaster, "Master Digital Investigator", "Reach the highest career rank."),
    };

    /// <summary>Checks the rank table (called by CareerSystem; problems are logged, never thrown).</summary>
    public bool Validate(out string problem)
    {
        problem = null;
        if (ranks == null || ranks.Count == 0) { problem = "no ranks"; return false; }
        if (ranks[0].minXp != 0) { problem = "the first rank must start at 0 XP"; return false; }
        for (int i = 1; i < ranks.Count; i++)
            if (ranks[i].minXp <= ranks[i - 1].minXp) { problem = $"'{ranks[i].title}' must need more XP than '{ranks[i - 1].title}'"; return false; }
        return true;
    }
}
