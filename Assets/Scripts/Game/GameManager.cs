using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Level lifecycle: briefing -> free exploration while QuizManager delivers messages -> results.
/// At the end it works out stars (reward only) and room unlocking (score only), saves progress
/// with LocalDataManager, and handles Continue / Replay / Main Menu through the existing SceneLoader.
/// </summary>
public class GameManager : MonoBehaviour
{
    public struct LevelResult
    {
        public int levelNumber;
        public string levelName;
        public int score, maxScore, bestScore;
        public int correct, incorrect, totalMessages;
        public float totalAnswerTime, averageAnswerTime;
        public string learningSummary;
        public int stars;
        public int requiredScore;
        public bool metRequirement;
        public bool hasNextRoom;
        public bool nextRoomUnlocked;
        public bool nextSceneAvailable;
        public string performanceMessage;
        public string unlockMessage;
        public string[] lessons;
        /// <summary>Case evaluation: grade A-F, first-answer accuracy, mistakes recovered, hints, key clues found, penalties.</summary>
        public string caseGrade;
        public float accuracy;
        public int recovered, hintsUsed, evidenceFound, penaltyPoints, recoveryPoints, missedRisks, falseAlarms;
        /// <summary>Career XP, rank and achievements for this run (CareerSystem).</summary>
        public CareerSystem.CaseCareer career;
    }

    [Header("Level")]
    [SerializeField] private LevelDefinition level;

    [Header("References (auto-found on this object if empty)")]
    [SerializeField] private QuizManager quiz;
    [SerializeField] private ScoringSystem scoring;
    [SerializeField] private LevelHUD hud;
    [SerializeField] private BriefingUI briefing;
    [SerializeField] private ResultsUI results;

    [Header("Scenes")]
    [SerializeField] private string mainMenuScene = "Menu";

    [Header("Timing")]
    [Tooltip("Pause after the last feedback before the results appear.")]
    [SerializeField] private float resultsDelay = 0.6f;

    /// <summary>The level being played: a runtime copy of the asset with the question bank's content applied.</summary>
    public LevelDefinition Level { get { EnsureContent(); return level; } }
    /// <summary>Where this level's questions came from (question bank source or the asset's built-in fallback).</summary>
    public ResolvedLevel Content { get { EnsureContent(); return content; } }

    private bool finished, leaving;
    private ResolvedLevel content;

    /// <summary>Swaps the authored asset for its content-applied copy once, before anything reads it.</summary>
    void EnsureContent()
    {
        if (content != null || level == null) return;
        content = LevelContentResolver.Resolve(level);
        foreach (var issue in content.issues)
            if (issue.IsError) Debug.LogWarning("Level content: " + issue, this);
        if (content.level != null) level = content.level;
    }

    void Awake()
    {
        EnsureContent();
        if (quiz == null) quiz = GetComponent<QuizManager>();
        if (scoring == null) scoring = GetComponent<ScoringSystem>();
        if (hud == null) hud = GetComponent<LevelHUD>();
        if (briefing == null) briefing = GetComponent<BriefingUI>();
        if (results == null) results = GetComponent<ResultsUI>();
        SceneLoader.RegisterLevel(level);   // Replay shows this room on the loading screen even when started from the editor
    }

    void OnEnable()
    {
        if (quiz != null) quiz.Completed += OnLevelCompleted;
        if (results != null)
        {
            results.ContinueClicked += ContinueToNextRoom;
            results.ReplayClicked += ReplayLevel;
            results.MenuClicked += ReturnToMainMenu;
        }
    }

    void OnDisable()
    {
        if (quiz != null) quiz.Completed -= OnLevelCompleted;
        if (results != null)
        {
            results.ContinueClicked -= ContinueToNextRoom;
            results.ReplayClicked -= ReplayLevel;
            results.MenuClicked -= ReturnToMainMenu;
        }
    }

    void Start()
    {
        if (level == null || quiz == null || scoring == null)
        {
            Debug.LogError("GameManager: assign a LevelDefinition and make sure QuizManager and ScoringSystem exist.", this);
            enabled = false;
            return;
        }

        Time.timeScale = 1f;
        finished = leaving = false;

        // No valid question anywhere (bank and built-in fallback): stop safely instead of starting an empty room.
        if (!content.Playable)
        {
            InteractionGate.Acquire(this);
            MouseLook.SetCursorLocked(false);
            var errors = content.issues.FindAll(i => i.IsError).ConvertAll(i => i.ToString());
            string details = string.Join("\n", errors.GetRange(0, Mathf.Min(3, errors.Count)));
            ContentErrorScreen.Show(transform, level.displayName, details, ReturnToMainMenu);
            return;
        }
        ProgressController.EnterRoom(level);   // a room you are standing in is always unlocked
        scoring.Configure(level);   // this level's points, reading time and penalties
        scoring.ResetRun();

        int max = scoring.MaxScoreFor(level.MessageCount);
        if (hud != null)
        {
            hud.Setup(level);
            hud.SetProgress(0, level.MessageCount, 0, max);
        }

        if (briefing != null)
        {
            InteractionGate.Acquire(this);
            briefing.Show(level, max, BeginLevel);
        }
        else BeginLevel();
    }

    void OnDestroy() => InteractionGate.Release(this);

    void BeginLevel()
    {
        InteractionGate.Release(this);
        quiz.Begin(level);
    }

    void OnLevelCompleted()
    {
        if (finished) return;   // completion is handled once per run
        finished = true;
        StartCoroutine(ShowResultsAfterDelay());
    }

    IEnumerator ShowResultsAfterDelay()
    {
        yield return new WaitForSecondsRealtime(resultsDelay);
        LevelResult r = EvaluateAndSave();
        InteractionGate.Acquire(this);
        if (results != null) results.Show(r);
        else Debug.Log($"Level complete: {r.score}/{r.maxScore}, {r.stars} stars. {r.unlockMessage}", this);
    }

    /// <summary>Works out stars and unlocking from the final score, then saves progress.</summary>
    LevelResult EvaluateAndSave()
    {
        // 1) Every message must have been answered before the level counts as finished.
        if (scoring.Results.Count < level.MessageCount)
            Debug.LogError($"GameManager: only {scoring.Results.Count}/{level.MessageCount} messages were answered.", this);

        // 2) Score -> 3) stars (reward only) -> 4) unlock check (score only). Stars never unlock anything.
        int score = scoring.TotalScore;
        int stars = ScoringSystem.StarsFor(score, level);
        bool met = ScoringSystem.MeetsUnlockScore(score, level.minScoreToUnlockNext);
        bool hasNext = !string.IsNullOrEmpty(level.nextLevelId);

        // 5) Completed (finished the level) is saved separately from 6) the next-room unlock - ProgressController
        // handles both, plus the run history for the cloud.
        var careerBefore = CareerSystem.TakeSnapshot(level.levelId);
        // The run's per-question results go with it (class records: weak topics, hardest questions).
        var answers = RunRecord.FromAnswers(scoring.Results, scoring.WasRecovered, scoring.HintUsedFor);
        var saved = ProgressController.SaveRun(level, score, scoring.MaxScoreFor(level.MessageCount), stars, met,
                                               scoring.CorrectCount, scoring.TotalAnswerTime,
                                               answers, scoring.Results.Count >= level.MessageCount);

        var r = new LevelResult
        {
            levelNumber = level.levelNumber,
            levelName = level.displayName,
            score = score,
            maxScore = scoring.MaxScoreFor(level.MessageCount),
            bestScore = saved.progress.bestScore,
            correct = scoring.CorrectCount,
            incorrect = scoring.IncorrectCount,
            totalMessages = level.MessageCount,
            totalAnswerTime = scoring.TotalAnswerTime,
            averageAnswerTime = scoring.Results.Count > 0 ? scoring.TotalAnswerTime / scoring.Results.Count : 0f,
            learningSummary = level.learningSummary,
            stars = stars,
            requiredScore = level.minScoreToUnlockNext,
            metRequirement = met,
            hasNextRoom = hasNext,
            nextRoomUnlocked = saved.nextUnlocked,
            nextSceneAvailable = CanLoad(level.nextSceneName),
            lessons = level.lessons,
            caseGrade = scoring.CaseGrade(scoring.MaxScoreFor(level.MessageCount)),
            accuracy = scoring.Accuracy,
            recovered = scoring.RecoveredCount,
            hintsUsed = scoring.HintsUsed,
            penaltyPoints = scoring.PenaltyPoints,
            recoveryPoints = scoring.RecoveryPointsEarned,
            missedRisks = scoring.MissedRisks,
            falseAlarms = scoring.FalseAlarms
        };
        // 7) Career: the one place case XP is awarded (improvement over this case's best run only).
        var finalTerminal = FindFirstObjectByType<FinalCaseTerminal>();
        var evidenceTracker = FindFirstObjectByType<EvidenceTracker>();
        r.career = CareerSystem.RecordCase(level, careerBefore, scoring, met,
                                           finalTerminal != null ? finalTerminal.RequiredEvidence : null, evidenceTracker);
        r.evidenceFound = scoring.EvidenceFound + (evidenceTracker != null ? evidenceTracker.Count : 0);   // key clues + records opened
        r.performanceMessage = PerformanceMessage(r.correct, level.MessageCount, level);
        r.unlockMessage = UnlockMessage(r, saved.nextWasUnlocked);
        return r;
    }

    static string PerformanceMessage(int correct, int total, LevelDefinition lvl)
    {
        if (total > 0 && correct >= total)
            return Or(lvl.performanceHigh, "Excellent! You identified every message correctly.");
        if (total > 0 && correct >= Mathf.CeilToInt(total * 0.6f))
            return Or(lvl.performanceModerate, "Good work! Review the warning signs to improve your judgment.");
        return Or(lvl.performanceLow, "Keep learning. Look carefully at the sender, message context, urgency, and links.");
    }

    static string Or(string preferred, string fallback) => string.IsNullOrEmpty(preferred) ? fallback : preferred;

    static string UnlockMessage(LevelResult r, bool wasUnlockedBefore)
    {
        if (!r.hasNextRoom) return "This was the final case. Well done, investigator!";

        if (r.metRequirement)
        {
            // Unlocking depends on the score only; stars are a separate reward.
            string msg = $"<b>Room unlocked!</b> You scored {r.score} points, meeting the required score of {r.requiredScore}.";
            return msg + (r.nextSceneAvailable
                ? " Continue to the next case."
                : " The next case isn't in this build yet - your unlock has been saved.");
        }

        if (wasUnlockedBefore)
            return $"You scored {r.score} points this run ({r.requiredScore} needed). " +
                   "The next room stays unlocked from an earlier run.";

        return $"<b>Room locked.</b> You scored {r.score} points, but you need {r.requiredScore} points " +
               "to unlock the next room. Replay this case to improve your score.";
    }

    // ---------- Navigation ----------

    public void ReplayLevel() => Load(SceneManager.GetActiveScene().name);

    public void ReturnToMainMenu() => Load(mainMenuScene);

    public void ContinueToNextRoom()
    {
        if (level == null || !ProgressController.IsUnlocked(level.nextLevelId)) return;
        if (!CanLoad(level.nextSceneName))
        {
            Debug.LogWarning($"GameManager: next scene '{level.nextSceneName}' is not in the Build Profile scene list.", this);
            return;
        }
        Load(level.nextSceneName);
    }

    void Load(string sceneName)
    {
        if (leaving) return;
        if (!CanLoad(sceneName))
        {
            Debug.LogError($"GameManager: scene '{sceneName}' is not in the Build Profile scene list.", this);
            return;
        }
        leaving = true;
        Time.timeScale = 1f;
        InteractionGate.ResetAll();
        MouseLook.SetCursorLocked(false);   // menu needs a visible cursor; the game scene re-locks it

        if (SceneLoader.Instance != null) SceneLoader.LoadScene(sceneName);
        else SceneManager.LoadScene(sceneName);
    }

    static bool CanLoad(string sceneName) =>
        !string.IsNullOrEmpty(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);
}
