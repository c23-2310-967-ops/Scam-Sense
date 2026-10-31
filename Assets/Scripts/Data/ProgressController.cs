/// <summary>
/// Coordinates saving and loading a player's room progress, so the game flow (GameManager) doesn't talk to the
/// storage classes directly.
///   - Local save: LocalDataManager (PlayerPrefs JSON) - unlocks, completion, best scores and stars.
///   - Cloud: the finished run is queued in GameResultLog. CloudAccountService uploads it, together with the
///     changed progress, when LocalDataManager reports a change - nothing here talks to Supabase directly.
/// The order matters: the run is queued before the result is saved, because saving starts the sync that uploads it.
/// </summary>
public static class ProgressController
{
    /// <summary>What saving a finished run changed.</summary>
    public struct SaveOutcome
    {
        public LocalDataManager.LevelProgress progress;   // this room's saved progress after the run
        public bool nextWasUnlocked;                      // the next room was already open before this run
        public bool nextUnlocked;                         // the next room is open now
    }

    /// <summary>A room the player is standing in is always unlocked (e.g. opened from Level Selection or the Editor).</summary>
    public static void EnterRoom(LevelDefinition level) => LocalDataManager.Unlock(level.levelId);

    public static bool IsUnlocked(string levelId) => LocalDataManager.IsUnlocked(levelId);

    /// <summary>
    /// Saves a finished run: queues it for the cloud history, records the result (completion, bests, stars) and
    /// unlocks the next room when the unlock score was met. Unlocking depends on the score only, never on stars.
    /// </summary>
    public static SaveOutcome SaveRun(LevelDefinition level, int score, int maxScore, int stars, bool metUnlockScore,
                                      int correct, float answerSeconds)
    {
        bool hasNext = !string.IsNullOrEmpty(level.nextLevelId);
        var outcome = new SaveOutcome { nextWasUnlocked = hasNext && IsUnlocked(level.nextLevelId) };

        GameResultLog.Add(level.levelId, score, maxScore, stars, correct, level.MessageCount, metUnlockScore, answerSeconds);
        outcome.progress = LocalDataManager.RecordResult(level.levelId, score, stars, metUnlockScore);
        if (metUnlockScore && hasNext) LocalDataManager.Unlock(level.nextLevelId);

        outcome.nextUnlocked = hasNext && IsUnlocked(level.nextLevelId);
        return outcome;
    }
}
