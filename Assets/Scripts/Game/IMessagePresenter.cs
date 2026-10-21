using System;

/// <summary>
/// Anything that can show one message and collect the answer: the phone pop-up (MessagePopupUI)
/// or a computer screen (ComputerTerminal). QuizManager owns the timer and scoring; presenters only display.
/// </summary>
public interface IMessagePresenter
{
    event Action<MessageVerdict> AnswerChosen;
    event Action ContinueClicked;

    void ShowMessage(ScamMessage msg);
    /// <summary>
    /// Response timer: <paramref name="seconds"/> spent reading so far, the message's reading allowance
    /// (0 = this level has no time pressure: show time spent, no countdown) and what a correct answer earns now.
    /// </summary>
    void SetTimer(float seconds, float allowance, int pointsIfCorrect);
    void ShowFeedback(ScoringSystem.AnswerResult result, int score, int maxScore, int reviewed, int total);

    /// <summary>The player has finished with this message (feedback closed).</summary>
    void Hide();
}

/// <summary>
/// Optional: a presenter that needs a moment before the message is readable (e.g. the phone being
/// pulled out of a pocket). QuizManager only counts response time while <see cref="TimerRunning"/> is true.
/// </summary>
public interface IPresenterTiming
{
    bool TimerRunning { get; }
}

/// <summary>
/// Optional: a presenter that supports the follow-up re-check after a wrong answer (WrongAnswerConsequences).
/// The message is shown again with ShowMessage; the verdict buttons stay locked until the re-check tasks are done,
/// and <paramref name="status"/> replaces the response timer (re-checks are not timed).
/// </summary>
public interface IRecheckPresenter
{
    void SetRecheck(bool answersEnabled, string status);
}

/// <summary>Optional: the part of a presenter that briefly flashes red after a wrong answer.</summary>
public interface IWarningTarget
{
    UnityEngine.RectTransform WarningTarget { get; }
}

/// <summary>
/// Optional: a screen area (pixels) where the investigation UI (findings card, Evidence Notebook) can sit
/// without covering the message or the answer buttons. Without it the UI docks on the right of the screen.
/// </summary>
public interface IInvestigationDock
{
    bool TryGetDockArea(out UnityEngine.Rect screenArea);
}
