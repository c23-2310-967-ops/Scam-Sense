using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One readable interaction prompt for the whole scene ("[E] Sit", "[E] Use computer", "[E] Open door").
/// Interactable scripts call InteractionPrompt.Set(this, "E", "Sit") every frame they are focused and
/// Clear(this) when not; the highest-priority request is shown. Hidden while any panel is open.
/// Creates itself on first use - nothing to set up in the scene.
/// </summary>
public class InteractionPrompt : MonoBehaviour
{
    struct Request
    {
        public string key, action, secondary;
        public int priority;
        public int frame;
    }

    static InteractionPrompt instance;
    static readonly Dictionary<object, Request> requests = new Dictionary<object, Request>();

    private RectTransform pill;
    private TMP_Text keyText, actionText, secondaryText;
    private RectTransform keyBox;
    private CanvasGroup group;

    /// <summary>Show "[key] action" (optionally with a second hint line). Call every frame while relevant.</summary>
    public static void Set(object owner, string key, string action, int priority = 0, string secondary = null)
    {
        if (owner == null) return;
        Ensure();
        requests[owner] = new Request { key = key, action = action, secondary = secondary, priority = priority, frame = Time.frameCount };
    }

    public static void Clear(object owner)
    {
        if (owner != null) requests.Remove(owner);
    }

    static readonly Dictionary<UnityEngine.InputSystem.Key, string> keyNames = new Dictionary<UnityEngine.InputSystem.Key, string>();

    /// <summary>The key's label ("E"), cached: enum ToString() allocates a new string every call.</summary>
    public static string KeyName(UnityEngine.InputSystem.Key key)
    {
        if (!keyNames.TryGetValue(key, out var name)) keyNames[key] = name = key.ToString();
        return name;
    }

    static void Ensure()
    {
        if (instance != null) return;
        var go = new GameObject("InteractionPrompt");
        instance = go.AddComponent<InteractionPrompt>();
    }

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        Build();
    }

    void OnDestroy()
    {
        if (instance == this) { instance = null; requests.Clear(); }
    }

    void LateUpdate()
    {
        // Pick the best request made this frame (stale ones from disabled scripts expire automatically).
        bool found = false;
        Request best = default;
        List<object> stale = null;
        foreach (var kv in requests)
        {
            if (kv.Value.frame < Time.frameCount - 1 || (kv.Key is Object uo && uo == null))
            {
                (stale ??= new List<object>()).Add(kv.Key);
                continue;
            }
            if (!found || kv.Value.priority > best.priority) { best = kv.Value; found = true; }
        }
        if (stale != null) foreach (var k in stale) requests.Remove(k);

        bool show = found && !InteractionGate.UIOpen && !PauseMenu.IsPaused;
        pill.gameObject.SetActive(show);
        if (!show) return;

        keyBox.gameObject.SetActive(!string.IsNullOrEmpty(best.key));
        if (keyText.text != best.key) keyText.text = best.key;
        if (actionText.text != best.action) actionText.text = best.action;
        bool hasSecondary = !string.IsNullOrEmpty(best.secondary);
        secondaryText.gameObject.SetActive(hasSecondary);
        if (hasSecondary && secondaryText.text != best.secondary) secondaryText.text = best.secondary;
    }

    void Build()
    {
        var root = UIKit.CreateCanvas(transform, "Prompt Canvas", 15);
        root.GetComponent<GraphicRaycaster>().enabled = false;   // display only

        pill = UIKit.Rect("Pill", root);
        UIKit.Place(pill, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, -70), new Vector2(0, 0));
        UIKit.PanelImage(pill, new Color(UIKit.Background.r, UIKit.Background.g, UIKit.Background.b, 0.92f)).raycastTarget = false;
        var v = UIKit.Vertical(pill, 0, 6, TextAnchor.MiddleCenter);
        v.padding = new RectOffset(18, 26, 12, 12);
        var fit = pill.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        group = pill.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;

        var row = UIKit.Rect("Row", pill);
        var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 14;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;

        // Key cap: cyan box with the key letter
        keyBox = UIKit.Rect("Key", row);
        UIKit.AddImage(keyBox, UIKit.Accent, rounded: true).raycastTarget = false;
        var kh = keyBox.gameObject.AddComponent<HorizontalLayoutGroup>();
        kh.padding = new RectOffset(14, 14, 4, 4);
        kh.childControlWidth = kh.childControlHeight = true;
        UIKit.Size(keyBox, -1, 46).minWidth = 46;
        keyText = UIKit.Text(keyBox, "KeyText", "E", 28, UIKit.Background, TextAlignmentOptions.Center, bold: true);
        keyText.textWrappingMode = TextWrappingModes.NoWrap;

        actionText = UIKit.Text(row, "Action", "", 30, UIKit.TextPrimary, TextAlignmentOptions.MidlineLeft, bold: true);
        actionText.textWrappingMode = TextWrappingModes.NoWrap;

        secondaryText = UIKit.Text(pill, "Secondary", "", UIKit.SizeSmall, UIKit.TextSecondary, TextAlignmentOptions.Center);
        secondaryText.textWrappingMode = TextWrappingModes.NoWrap;

        pill.gameObject.SetActive(false);
    }
}
