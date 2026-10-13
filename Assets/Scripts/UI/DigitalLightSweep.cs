using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// "Digital Light Sweep": a soft cyan band of light that glides diagonally across a button when it is
/// hovered or selected with the keyboard, plus a frame glow while hovered. Optional idle sweep for the
/// main call-to-action button.
///
/// Put it on the button root. The root should have a Mask (UIKit.HudButton sets this up) so the band is
/// clipped to the button shape. The band never receives raycasts, so it can't block clicks.
/// Uses unscaled time, so it also animates while the game is paused.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class DigitalLightSweep : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [Header("Sweep")]
    [SerializeField] private Color sweepColor = new Color(0.55f, 0.92f, 1f, 0.55f);
    [SerializeField] private float duration = 0.6f;
    [Tooltip("Band width as a fraction of the button width.")]
    [SerializeField, Range(0.1f, 1f)] private float bandWidth = 0.32f;
    [Tooltip("Tilt of the band in degrees.")]
    [SerializeField] private float angle = 18f;

    [Header("Idle sweep (0 = only on hover)")]
    [SerializeField] private float idleInterval = 0f;
    [SerializeField, Range(0f, 1f)] private float idleStrength = 0.45f;

    [Header("Frame glow")]
    [SerializeField] private Graphic frame;
    [SerializeField] private Color frameIdle = new Color(0.18f, 0.26f, 0.39f, 1f);
    [SerializeField] private Color frameHover = new Color(0.22f, 0.74f, 0.97f, 1f);
    [SerializeField] private float frameFadeSpeed = 8f;

    private RectTransform rect, band;
    private Image bandImage;
    private Selectable selectable;
    private Coroutine running;
    private bool hovered;
    private float idleTimer;

    /// <summary>Configure from code (UIKit.HudButton).</summary>
    public void Setup(Graphic frameGraphic, Color idleColor, Color hoverColor, float idleEvery = 0f, Color? sweep = null)
    {
        frame = frameGraphic;
        frameIdle = idleColor;
        frameHover = hoverColor;
        idleInterval = idleEvery;
        if (sweep.HasValue) sweepColor = sweep.Value;
        if (frame != null) frame.color = frameIdle;
    }

    void Awake()
    {
        rect = (RectTransform)transform;
        selectable = GetComponent<Selectable>();

        var go = new GameObject("LightSweep", typeof(RectTransform));
        band = (RectTransform)go.transform;
        band.SetParent(transform, false);
        band.SetSiblingIndex(0);                       // above the fill, below frame and text
        band.anchorMin = band.anchorMax = new Vector2(0f, 0.5f);
        band.pivot = new Vector2(0.5f, 0.5f);
        bandImage = go.AddComponent<Image>();
        bandImage.sprite = UIKit.SweepSprite;
        bandImage.raycastTarget = false;               // never blocks clicks
        go.AddComponent<LayoutElement>().ignoreLayout = true;
        go.SetActive(false);
    }

    void OnDisable()
    {
        if (running != null) StopCoroutine(running);
        running = null;
        hovered = false;
        if (band != null) band.gameObject.SetActive(false);
        if (frame != null) frame.color = frameIdle;
    }

    bool Interactable => selectable == null || selectable.IsInteractable();

    public void OnPointerEnter(PointerEventData e)
    {
        hovered = true;
        if (Interactable) Play(1f);
    }

    public void OnPointerExit(PointerEventData e) => hovered = false;

    public void OnSelect(BaseEventData e)
    {
        if (e is PointerEventData) return;             // mouse already handled by hover
        hovered = true;
        if (Interactable) Play(1f);
    }

    public void OnDeselect(BaseEventData e) => hovered = false;

    /// <summary>Runs one sweep at the given strength (0..1).</summary>
    public void Play(float strength)
    {
        if (!isActiveAndEnabled) return;
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(Sweep(Mathf.Clamp01(strength)));
    }

    void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        if (frame != null)
        {
            Color target = hovered && Interactable ? frameHover : frameIdle;
            frame.color = Color.Lerp(frame.color, target, 1f - Mathf.Exp(-frameFadeSpeed * dt));
        }

        if (idleInterval > 0f && running == null && Interactable)
        {
            idleTimer += dt;
            if (idleTimer >= idleInterval)
            {
                idleTimer = 0f;
                Play(idleStrength);
            }
        }
    }

    IEnumerator Sweep(float strength)
    {
        float w = rect.rect.width, h = rect.rect.height;
        if (w <= 0f || h <= 0f) { running = null; yield break; }

        band.gameObject.SetActive(true);

        // Capped step: a frame hitch slows the sweep for a moment instead of making it jump.
        for (float t = 0f; t < 1f; t += Mathf.Min(Time.unscaledDeltaTime, 0.05f) / Mathf.Max(0.05f, duration))
        {
            ApplyBand(t, strength);
            yield return null;
        }

        band.gameObject.SetActive(false);
        idleTimer = 0f;
        running = null;
    }

    void ApplyBand(float t, float strength)
    {
        float w = rect.rect.width, h = rect.rect.height;
        float bw = w * bandWidth;
        band.sizeDelta = new Vector2(bw, h * 2.4f);
        band.localRotation = Quaternion.Euler(0f, 0f, -angle);
        float e = 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI);                  // ease in-out
        band.anchoredPosition = new Vector2(Mathf.Lerp(-bw, w + bw, e), 0f);
        float fade = Mathf.Sin(t * Mathf.PI);                               // soft in and out
        var c = sweepColor;
        c.a *= strength * Mathf.Lerp(0.35f, 1f, fade);
        bandImage.color = c;
    }

    /// <summary>
    /// Freezes the band at a point of the sweep (0..1) for previews/screenshots. Pass a negative value to hide it.
    /// Ignored while a sweep is playing.
    /// </summary>
    public void PreviewAt(float progress, float strength = 1f)
    {
        if (running != null || band == null) return;
        if (progress < 0f) { band.gameObject.SetActive(false); return; }
        band.gameObject.SetActive(true);
        ApplyBand(Mathf.Clamp01(progress), Mathf.Clamp01(strength));
    }
}
