using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI regressions that have happened before: text lines drawn on top of each other in a crowded panel
/// (the computer's email screen), and the shared visual pieces (soft-depth panels, highlight shader) failing to load.
/// </summary>
public class UILayoutTests
{
    GameObject canvasGo;
    RectTransform panel;

    [SetUp]
    public void SetUp()
    {
        canvasGo = new GameObject("TestCanvas", typeof(Canvas));
        panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
        panel.SetParent(canvasGo.transform, false);
        panel.sizeDelta = new Vector2(400, 120);   // far too short for the text below: the layout must not squash it
        var v = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        v.spacing = 6;
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(canvasGo);

    TMP_Text AddLine(string text, bool keepHeight)
    {
        var go = new GameObject("Line", typeof(RectTransform));
        go.transform.SetParent(panel, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = 24;
        t.text = text;
        if (keepHeight) go.AddComponent<LayoutKeepHeight>();
        return t;
    }

    const string Long = "Immediate Account Verification Required - Action Needed Today Before Your Access Is Restricted";

    static readonly Vector3[] corners = new Vector3[4];

    /// <summary>True if the lower line's top edge is above the upper line's bottom edge.</summary>
    static bool Overlap(TMP_Text upper, TMP_Text lower)
    {
        upper.rectTransform.GetWorldCorners(corners);
        float upperBottom = corners[0].y;
        lower.rectTransform.GetWorldCorners(corners);
        float lowerTop = corners[1].y;
        return lowerTop > upperBottom + 0.5f;
    }

    [Test]
    public void CrowdedPanel_LinesWithKeepHeight_DoNotOverlap()
    {
        var a = AddLine(Long, true);
        var b = AddLine(Long, true);
        var c = AddLine("From: Security Team - Example Bank", true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);

        Assert.GreaterOrEqual(a.rectTransform.rect.height, a.preferredHeight - 1f, "A wrapped line was squashed");
        Assert.IsFalse(Overlap(a, b), "Line 2 is drawn over line 1");
        Assert.IsFalse(Overlap(b, c), "Line 3 is drawn over line 2");
    }

    [Test]
    public void CrowdedPanel_WithoutKeepHeight_WouldOverlap()
    {
        // Shows what the test above protects against: plain TMP lines get squashed in a full layout.
        var a = AddLine(Long, false);
        AddLine(Long, false);
        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        Assert.Less(a.rectTransform.rect.height, a.preferredHeight - 1f, "Expected plain text to be squashed (its text then spills onto the next line)");
    }

    [Test]
    public void UIKitPanels_GetSoftDepth()
    {
        var img = UIKit.PanelImage(panel, Color.gray);
        Assert.IsNotNull(img);
        Assert.IsNotNull(img.GetComponent<UISoftDepth>(), "UIKit panels lost their soft depth effect");
    }

    [Test]
    public void HighlightShader_LoadsAndCompiles()
    {
        var shader = Resources.Load<Shader>("Shaders/InteractHighlight");
        Assert.IsNotNull(shader, "Resources/Shaders/InteractHighlight is missing - interactable objects would not glow");
        Assert.IsTrue(shader.isSupported, "The highlight shader is not supported on this GPU");
        Assert.IsFalse(UnityEditor.ShaderUtil.ShaderHasError(shader), "The highlight shader has compile errors");
    }
}
