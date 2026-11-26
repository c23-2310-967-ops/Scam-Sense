using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Shared open / close motion for pop-up panels: fade plus a small scale (0.96 -> 1) with a slight upward drift,
/// about a fifth of a second, on unscaled time (works while paused). Clicks are blocked while it closes, so a
/// panel can never be clicked twice on its way out. Lowest quality level (reduced effects) = no motion.
/// Add it to the panel RectTransform; it adds its own CanvasGroup if needed.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class PopupAnimator : MonoBehaviour
{
    public float openTime = 0.22f, closeTime = 0.14f, startScale = 0.96f, drift = 14f;

    private CanvasGroup group;
    private RectTransform rt;
    private Vector2 restPosition;
    private bool restKnown;
    private Coroutine anim;

    static bool ReducedEffects => QualitySettings.GetQualityLevel() == 0;

    void Awake() => Init();

    void Init()
    {
        if (rt != null) return;
        rt = (RectTransform)transform;
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
    }

    /// <summary>Activates the panel and plays the opening motion.</summary>
    public void Open()
    {
        Init();
        if (anim != null) { StopCoroutine(anim); anim = null; Settle(); }
        if (!restKnown) { restPosition = rt.anchoredPosition; restKnown = true; }
        gameObject.SetActive(true);
        group.interactable = group.blocksRaycasts = true;
        if (ReducedEffects || !isActiveAndEnabled) { Settle(); group.alpha = 1f; return; }
        anim = StartCoroutine(Run(true, null));
    }

    /// <summary>Plays the closing motion, then deactivates the panel and calls <paramref name="done"/>.</summary>
    public void Close(Action done = null)
    {
        Init();
        if (!gameObject.activeInHierarchy || ReducedEffects)
        {
            if (anim != null) { StopCoroutine(anim); anim = null; }
            Settle();
            gameObject.SetActive(false);
            done?.Invoke();
            return;
        }
        if (anim != null) StopCoroutine(anim);
        group.interactable = group.blocksRaycasts = false;
        anim = StartCoroutine(Run(false, done));
    }

    IEnumerator Run(bool opening, Action done)
    {
        float time = opening ? openTime : closeTime;
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.Clamp01(t / time);
            float e = opening ? 1f - (1f - k) * (1f - k) * (1f - k) : k * k;   // ease-out in, ease-in out
            float shown = opening ? e : 1f - e;
            Apply(shown);
            yield return null;
        }
        anim = null;
        Settle();
        if (!opening) { gameObject.SetActive(false); done?.Invoke(); }
    }

    void Apply(float shown)
    {
        group.alpha = shown;
        rt.localScale = Vector3.one * Mathf.Lerp(startScale, 1f, shown);
        // Panels inside a layout group use drift = 0: their position belongs to the layout.
        if (restKnown && drift != 0f) rt.anchoredPosition = restPosition + new Vector2(0f, -drift * (1f - shown));
    }

    void Settle()
    {
        group.alpha = 1f;
        rt.localScale = Vector3.one;
        if (restKnown && drift != 0f) rt.anchoredPosition = restPosition;
    }

    void OnDisable()
    {
        // Hidden mid-animation (scene change, pause): come back clean next time.
        if (anim != null) { anim = null; }
        if (rt != null) Settle();
        if (group != null) group.interactable = group.blocksRaycasts = true;
    }
}
