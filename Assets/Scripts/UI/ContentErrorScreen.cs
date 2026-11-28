using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Safe stop when a level has no valid question anywhere (question bank and built-in fallback both empty or broken).
/// Shows a plain explanation and one way out (Main Menu) instead of starting an empty or broken level.
/// Built from UIKit so it matches the rest of the game; used only by GameManager.
/// </summary>
public class ContentErrorScreen : MonoBehaviour
{
    public const string Title = "THIS CASE CAN'T START";

    public static ContentErrorScreen Show(Transform owner, string levelName, string details, Action onMainMenu)
    {
        var root = UIKit.CreateCanvas(owner, "ContentErrorScreen", 500);
        var screen = root.gameObject.AddComponent<ContentErrorScreen>();
        UIKit.Dimmer(root, 0.9f);

        var panel = UIKit.Rect("Panel", root);
        UIKit.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 0f));
        UIKit.PanelImage(panel);
        UIKit.TintPanel(panel, UIKit.PopupTone.Warning);
        UIKit.Vertical(panel, 40, 18f, TextAnchor.UpperCenter);
        UIKit.FitHeight(panel);

        UIKit.Text(panel, "Title", Title, 40f, UIKit.Warning, TextAlignmentOptions.Center, bold: true);
        UIKit.Text(panel, "Body",
            $"The questions for <b>{levelName}</b> are missing or could not be read, so this case was not started. " +
            "Your saved progress is safe.\n\nAsk your teacher or the game's admin to check the question bank, then try again.",
            24f, UIKit.TextPrimary, TextAlignmentOptions.Center);
        if (!string.IsNullOrEmpty(details))
            UIKit.Text(panel, "Details", details, 18f, UIKit.TextSecondary, TextAlignmentOptions.Center);
        var b = UIKit.Button(panel, "MAIN MENU", () => onMainMenu?.Invoke(), UIKit.Primary);
        UIKit.Size(b, 320f, 64f);
        return screen;
    }
}
