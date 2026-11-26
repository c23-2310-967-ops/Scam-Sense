using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared pop-up styling (UIKit tones, PopupIcon, TintPanel, PopupAnimator): every tone has its own colour AND glyph
/// (never colour alone), icons stay square, and panels can be tinted and reset. Throwaway objects only.
/// </summary>
public class PopupStyleTests
{
    static readonly UIKit.PopupTone[] Tones = { UIKit.PopupTone.Info, UIKit.PopupTone.Success, UIKit.PopupTone.Error, UIKit.PopupTone.Warning };
    GameObject root;

    [SetUp] public void SetUp() => root = new GameObject("PopupTest", typeof(RectTransform));
    [TearDown] public void TearDown() => Object.DestroyImmediate(root);

    [Test]
    public void EveryTone_HasItsOwnColourAndGlyph()
    {
        Assert.AreEqual(Tones.Length, Tones.Select(UIKit.ToneColor).Distinct().Count(), "two tones share a colour");
        Assert.AreEqual(Tones.Length, Tones.Select(UIKit.ToneGlyph).Distinct().Count(), "two tones share a glyph");
        foreach (var t in Tones) StringAssert.StartsWith("#", UIKit.ToneHex(t));
    }

    [Test]
    public void IconBadge_IsSquareAndShowsTheToneGlyph()
    {
        foreach (var tone in Tones)
        {
            var icon = UIKit.IconBadge(root.transform, tone, 64f);
            var rt = (RectTransform)icon.transform;
            Assert.AreEqual(rt.sizeDelta.x, rt.sizeDelta.y, "icon must stay round");
            var le = icon.GetComponent<LayoutElement>();
            Assert.AreEqual(64f, le.preferredWidth);
            Assert.AreEqual(64f, le.preferredHeight);
            var glyph = icon.GetComponentsInChildren<TMP_Text>(true).Single();
            Assert.AreEqual(UIKit.ToneGlyph(tone), glyph.text);
            Assert.AreEqual(UIKit.ToneColor(tone), glyph.color);
            Assert.AreEqual(tone, icon.Tone);
        }
    }

    [Test]
    public void IconBadge_ChangesTone()
    {
        var icon = UIKit.IconBadge(root.transform, UIKit.PopupTone.Success);
        icon.SetTone(UIKit.PopupTone.Error);
        Assert.AreEqual("×", icon.GetComponentsInChildren<TMP_Text>(true).Single().text);
    }

    [Test]
    public void TintPanel_RecoloursTheStripAndBorder_AndInfoResetsIt()
    {
        var panel = UIKit.Rect("Panel", root.transform);
        UIKit.PanelImage(panel);
        UIKit.AccentStrip(panel);
        UIKit.TintPanel(panel, UIKit.PopupTone.Error);
        Assert.AreEqual(UIKit.Danger, panel.Find("Accent").GetComponent<Image>().color);
        Assert.AreNotEqual(UIKit.Border, panel.GetComponent<Outline>().effectColor);
        UIKit.TintPanel(panel, UIKit.PopupTone.Info);
        Assert.AreEqual(UIKit.Accent, panel.Find("Accent").GetComponent<Image>().color);
        Assert.AreEqual(UIKit.Border, panel.GetComponent<Outline>().effectColor);
    }

    [Test]
    public void PopupAnimator_ClosesAndCallsBack_WhenNotRunning()
    {
        var panel = UIKit.Rect("Panel", root.transform);
        var anim = panel.gameObject.AddComponent<PopupAnimator>();
        root.SetActive(false);   // inactive: no coroutine, closes at once
        bool done = false;
        anim.Close(() => done = true);
        Assert.IsTrue(done);
        Assert.IsFalse(panel.gameObject.activeSelf);
        Assert.AreEqual(Vector3.one, panel.localScale);
    }

    [Test]
    public void RingSprite_IsGenerated()
    {
        Assert.IsNotNull(UIKit.RingSprite);
        Assert.IsTrue(UIKit.GenerateSpriteSet().Any(s => s.name == "UIKit_Ring"));
    }
}
