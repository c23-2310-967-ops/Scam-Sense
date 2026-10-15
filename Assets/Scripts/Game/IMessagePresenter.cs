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
    void SetTimer(float seconds, int pointsIfCorrect);
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
/// Optional: a screen area (pixels) where the investigation UI (findings card, Evidence Notebook) can sit
/// without covering the message or the answer buttons. Without it the UI docks on the right of the screen.
/// </summary>
public interface IInvestigationDock
{
    bool TryGetDockArea(out UnityEngine.Rect screenArea);
}
