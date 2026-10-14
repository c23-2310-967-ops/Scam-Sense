using TMPro;
using UnityEngine;

/// <summary>
/// The large "threat analysis" wall screen in the operations room. Shows live case progress from
/// QuizManager: messages reviewed, accuracy so far and the current status (monitoring / new message /
/// complete). Display only - it never reveals an answer before the player has made a decision.
/// Its texts are scene objects (built by Tools > Scam Sense > Build Level 3 Room).
/// </summary>
public class OpsWallDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text reviewedText;
    [SerializeField] private TMP_Text accuracyText;
    [SerializeField] private TMP_Text statusText;
    [Tooltip("Bar whose width shows progress (anchorMax.x goes 0 to 1).")]
    [SerializeField] private RectTransform progressFill;
    [SerializeField] private QuizManager quiz;

    void Awake()
    {
        if (quiz == null) quiz = FindFirstObjectByType<QuizManager>();
    }

    void OnEnable()
    {
        if (quiz != null) quiz.StateChanged += Refresh;
    }

    void OnDisable()
    {
        if (quiz != null) quiz.StateChanged -= Refresh;
    }

    void Start() => Refresh();

    void Refresh()
    {
        if (quiz == null || quiz.Level == null) return;
        int total = quiz.Level.MessageCount;
        int done = quiz.Scoring.Results.Count;
        int correct = quiz.Scoring.CorrectCount;

        if (reviewedText != null) reviewedText.text = $"{done} / {total}";
        if (accuracyText != null) accuracyText.text = done > 0 ? $"{Mathf.RoundToInt(100f * correct / done)}%" : "--";
        if (progressFill != null)
        {
            var max = progressFill.anchorMax;
            max.x = total > 0 ? (float)done / total : 0f;
            progressFill.anchorMax = max;
        }
        if (statusText != null)
        {
            if (quiz.IsComplete) statusText.text = $"<color={UIKit.HexSafe}>INVESTIGATION COMPLETE</color>";
            else if (quiz.PendingMessage != null) statusText.text = $"<color={UIKit.HexWarning}>NEW MESSAGE INBOUND</color>";
            else if (quiz.OpenMessage != null) statusText.text = $"<color={UIKit.HexAccent}>ANALYSIS IN PROGRESS</color>";
            else statusText.text = $"<color={UIKit.HexAccent}>MONITORING</color>";
        }
    }
}
