using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Round status icon used in pop-up headers (UIKit.IconBadge): a softly tinted disc, a ring and a glyph
/// (✓ success, × error, ! warning, i info), all in the tone's colour.
/// Play() gives a short entrance that matches the tone: success pops with an expanding ring, an error pops and
/// shakes once, a warning pops and pulses, info just pops. Unscaled time, so it also runs while paused;
/// the lowest quality level (reduced effects) skips the motion.
/// </summary>
public class PopupIcon : MonoBehaviour
{
    private Image disc, ring, pulse;
    private TMP_Text glyph;
    private RectTransform body;
    private UIKit.PopupTone tone;
    private Coroutine anim;

    static bool ReducedEffects => QualitySettings.GetQualityLevel() == 0;

    public UIKit.PopupTone Tone => tone;

    public void Build(float size)
    {
        var rt = (RectTransform)transform;
        // Expanding "ping" ring behind the icon (success only).
        var p = UIKit.Rect("Pulse", rt);
        UIKit.Stretch(p);
        pulse = UIKit.AddImage(p, Color.clear, UIKit.RingSprite);
        pulse.raycastTarget = false;

        body = UIKit.Rect("Body", rt);
        UIKit.Stretch(body);
        disc = UIKit.AddImage(body, Color.clear, UIKit.CircleSprite);
        disc.raycastTarget = false;
        var r = UIKit.Rect("Ring", body);
        UIKit.Stretch(r);
        ring = UIKit.AddImage(r, Color.white, UIKit.RingSprite);
        ring.raycastTarget = false;
        glyph = UIKit.Text(body, "Glyph", "", size * 0.56f, Color.white, TextAlignmentOptions.Center, bold: true);
        UIKit.Stretch(glyph.rectTransform);
        glyph.textWrappingMode = TextWrappingModes.NoWrap;
        glyph.margin = new Vector4(0, 0, 0, size * 0.04f);   // optical centring of the glyph
    }

    public void SetTone(UIKit.PopupTone t)
    {
        tone = t;
        Color c = UIKit.ToneColor(t);
        disc.color = new Color(c.r, c.g, c.b, 0.16f);
        ring.color = c;
        glyph.color = c;
        glyph.text = UIKit.ToneGlyph(t);
        // The italic-looking "i" reads better as a serif-free lowercase in bold; keep size consistent across glyphs.
        glyph.fontSize = ((RectTransform)transform).sizeDelta.x * (t == UIKit.PopupTone.Info ? 0.5f : 0.56f);
        pulse.color = Color.clear;
    }

    /// <summary>Plays the entrance for the current tone (restarts if already playing).</summary>
    public void Play()
    {
        if (anim != null) StopCoroutine(anim);
        body.localScale = Vector3.one;
        body.anchoredPosition = Vector2.zero;
        pulse.color = Color.clear;
        if (ReducedEffects || !isActiveAndEnabled) return;
        anim = StartCoroutine(Entrance());
    }

    IEnumerator Entrance()
    {
        // Pop: 0.35 -> 1.12 -> 1 (ease-out), about a quarter of a second.
        const float pop = 0.26f;
        for (float t = 0f; t < pop; t += Time.unscaledDeltaTime)
        {
            float k = t / pop;
            float s = k < 0.65f ? Mathf.Lerp(0.35f, 1.12f, EaseOut(k / 0.65f)) : Mathf.Lerp(1.12f, 1f, (k - 0.65f) / 0.35f);
            body.localScale = Vector3.one * s;
            yield return null;
        }
        body.localScale = Vector3.one;

        Color c = UIKit.ToneColor(tone);
        switch (tone)
        {
            case UIKit.PopupTone.Success:
                // One soft ring that grows and fades - a "verified" ping.
                for (float t = 0f; t < 0.55f; t += Time.unscaledDeltaTime)
                {
                    float k = t / 0.55f;
                    pulse.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.7f, EaseOut(k));
                    pulse.color = new Color(c.r, c.g, c.b, 0.55f * (1f - k));
                    yield return null;
                }
                pulse.color = Color.clear;
                pulse.rectTransform.localScale = Vector3.one;
                break;

            case UIKit.PopupTone.Error:
                // One short, small shake: noticeable, not alarming.
                for (float t = 0f; t < 0.32f; t += Time.unscaledDeltaTime)
                {
                    body.anchoredPosition = new Vector2(Mathf.Sin(t * 60f) * 6f * (1f - t / 0.32f), 0f);
                    yield return null;
                }
                body.anchoredPosition = Vector2.zero;
                break;

            case UIKit.PopupTone.Warning:
                // Two gentle pulses.
                for (float t = 0f; t < 0.7f; t += Time.unscaledDeltaTime)
                {
                    float w = Mathf.Sin(t / 0.7f * Mathf.PI * 2f);
                    body.localScale = Vector3.one * (1f + 0.06f * w * w);
                    yield return null;
                }
                body.localScale = Vector3.one;
                break;
        }
        anim = null;
    }

    static float EaseOut(float x) => 1f - (1f - x) * (1f - x) * (1f - x);

    void OnDisable()
    {
        // Never leave the icon mid-animation (pop-ups are hidden and reused).
        anim = null;
        if (body != null) { body.localScale = Vector3.one; body.anchoredPosition = Vector2.zero; }
        if (pulse != null) { pulse.color = Color.clear; pulse.rectTransform.localScale = Vector3.one; }
    }
}
