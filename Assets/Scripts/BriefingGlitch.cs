using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Glitch-style animation for the Briefing panel.
/// Replaces BriefingAnimator (remove that component if you added it).
/// Effects: flicker-on, scrambled "decoding" text, red/cyan RGB-split ghosts,
/// random glitch bursts while open, and a glitch-out on close.
/// Note: the text is plain text only (rich text tags would get scrambled).
[RequireComponent(typeof(CanvasGroup))]
public class BriefingGlitch : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform panel;          // usually this same object
    [SerializeField] private TMP_Text titleText;           // optional
    [SerializeField] private TMP_Text bodyText;            // the briefing text
    [SerializeField] private CanvasGroup[] staggerItems;   // optional: Back/Continue buttons etc.

    [Header("Flicker In")]
    [SerializeField] private float flickerDuration = 0.5f;
    [SerializeField] private float flickerStep = 0.04f;

    [Header("Text Decode")]
    [SerializeField] private float titleCharsPerSecond = 40f;
    [SerializeField] private float bodyCharsPerSecond = 120f;
    [SerializeField] private string scrambleChars = "!<>-_\\/[]{}=+*^?#01";

    [Header("Looping Flicker (while open)")]
    [SerializeField] private bool loopFlicker = true;
    [SerializeField] private float loopMinAlpha = 0.55f;                       // how dim a flicker dips
    [SerializeField, Range(0f, 1f)] private float loopDipChance = 0.35f;       // chance each step dips
    [SerializeField] private Vector2 loopStep = new Vector2(0.03f, 0.12f);     // time between flicker steps
    [SerializeField, Range(0f, 1f)] private float loopGhostChance = 0.25f;     // chance a dip also shows RGB split

    [Header("Glitch Bursts (while open)")]
    [SerializeField] private Vector2 burstInterval = new Vector2(1.5f, 4f);
    [SerializeField] private float burstDuration = 0.18f;
    [SerializeField] private float jitterAmount = 8f;
    [SerializeField] private float splitAmount = 5f;
    [SerializeField] private Color ghostColorA = new Color(1f, 0.1f, 0.2f, 0.6f); // red
    [SerializeField] private Color ghostColorB = new Color(0f, 0.9f, 1f, 0.6f);   // cyan

    [Header("Audio (optional)")]
    [SerializeField] private AudioSource glitchSound;

    private CanvasGroup group;
    private Vector2 restPos;
    private string titleOriginal, bodyOriginal;

    private struct Ghost { public TMP_Text a, b; }
    private Ghost titleGhost, bodyGhost;

    private Coroutine running, burstLoop;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        if (panel == null) panel = transform as RectTransform;
        restPos = panel.anchoredPosition;

        if (titleText != null) { titleOriginal = titleText.text; titleGhost = MakeGhosts(titleText); }
        if (bodyText != null) { bodyOriginal = bodyText.text; bodyGhost = MakeGhosts(bodyText); }
    }

    private void OnEnable() => Play();

    private void OnDisable()
    {
        running = null;
        burstLoop = null;
    }

    public void Play()
    {
        if (running != null) StopCoroutine(running);
        if (burstLoop != null) StopCoroutine(burstLoop);
        running = StartCoroutine(Sequence());
    }

    public void Close(System.Action onDone = null)
    {
        if (running != null) StopCoroutine(running);
        if (burstLoop != null) StopCoroutine(burstLoop);
        running = StartCoroutine(CloseRoutine(onDone));
    }

    // ---------- Sequences ----------

    private IEnumerator Sequence()
    {
        // Reset
        group.alpha = 0f;
        panel.anchoredPosition = restPos;
        SetGhosts(false, 0f);
        SetText(titleText, titleGhost, "");
        SetText(bodyText, bodyGhost, "");
        if (staggerItems != null)
            foreach (var item in staggerItems) if (item) item.alpha = 0f;

        PlaySound();

        // 1) Flicker on
        float t = 0f;
        while (t < flickerDuration)
        {
            group.alpha = Random.value > 0.45f ? Random.Range(0.4f, 1f) : 0f;
            panel.anchoredPosition = restPos + new Vector2(
                Random.Range(-jitterAmount, jitterAmount), Random.Range(-2f, 2f));
            SetGhosts(true, splitAmount * 1.5f);
            yield return new WaitForSecondsRealtime(flickerStep);
            t += flickerStep;
        }
        group.alpha = 1f;
        panel.anchoredPosition = restPos;
        SetGhosts(false, 0f);

        // 2) Decode title, then body
        if (titleText != null)
            yield return Decode(titleText, titleGhost, titleOriginal, titleCharsPerSecond);
        if (bodyText != null)
            yield return Decode(bodyText, bodyGhost, bodyOriginal, bodyCharsPerSecond);

        // 3) Pop in extra items with a tiny flicker
        if (staggerItems != null)
        {
            foreach (var item in staggerItems)
            {
                if (item == null) continue;
                for (int i = 0; i < 4; i++)
                {
                    item.alpha = i % 2 == 0 ? 1f : 0.2f;
                    yield return new WaitForSecondsRealtime(0.04f);
                }
                item.alpha = 1f;
                yield return new WaitForSecondsRealtime(0.06f);
            }
        }

        running = null;
        burstLoop = StartCoroutine(BurstLoop());
    }

    private IEnumerator Decode(TMP_Text txt, Ghost ghost, string original, float cps)
    {
        float revealed = 0f;
        int len = original.Length;
        var sb = new StringBuilder(len);
        int lastRevealed = -1;

        while (revealed < len)
        {
            revealed += cps * Time.unscaledDeltaTime;
            int r = Mathf.Min(len, Mathf.FloorToInt(revealed));

            sb.Clear();
            for (int i = 0; i < len; i++)
            {
                char c = original[i];
                if (i < r || char.IsWhiteSpace(c)) sb.Append(c);
                else sb.Append(scrambleChars[Random.Range(0, scrambleChars.Length)]);
            }
            SetText(txt, ghost, sb.ToString());

            // little ghost shake while decoding
            if (r != lastRevealed && Random.value > 0.7f)
                SetGhosts(true, splitAmount * 0.6f);
            else
                SetGhosts(false, 0f);
            lastRevealed = r;

            yield return null;
        }

        SetText(txt, ghost, original);
        SetGhosts(false, 0f);
    }

    private IEnumerator BurstLoop()
    {
        while (true)
        {
            float wait = Random.Range(burstInterval.x, burstInterval.y);
            if (loopFlicker)
            {
                // Continuous flicker until the next burst
                float elapsed = 0f;
                while (elapsed < wait)
                {
                    float step = Random.Range(loopStep.x, loopStep.y);
                    bool dip = Random.value < loopDipChance;
                    group.alpha = dip ? Random.Range(loopMinAlpha, 0.95f) : 1f;
                    SetGhosts(dip && Random.value < loopGhostChance, splitAmount * 0.5f);
                    yield return new WaitForSecondsRealtime(step);
                    elapsed += step;
                }
                group.alpha = 1f;
                SetGhosts(false, 0f);
            }
            else
            {
                yield return new WaitForSecondsRealtime(wait);
            }

            PlaySound();
            float t = 0f;
            while (t < burstDuration)
            {
                panel.anchoredPosition = restPos + new Vector2(
                    Random.Range(-jitterAmount, jitterAmount), Random.Range(-jitterAmount * 0.3f, jitterAmount * 0.3f));
                group.alpha = Random.value > 0.2f ? 1f : 0.5f;
                SetGhosts(true, splitAmount);
                yield return new WaitForSecondsRealtime(0.04f);
                t += 0.04f;
            }
            panel.anchoredPosition = restPos;
            group.alpha = 1f;
            SetGhosts(false, 0f);
        }
    }

    private IEnumerator CloseRoutine(System.Action onDone)
    {
        PlaySound();
        float t = 0f;
        while (t < 0.3f)
        {
            group.alpha = Random.value > 0.5f ? Random.Range(0.2f, 1f) : 0f;
            panel.anchoredPosition = restPos + new Vector2(
                Random.Range(-jitterAmount * 2f, jitterAmount * 2f), Random.Range(-4f, 4f));
            SetGhosts(true, splitAmount * 2f);
            yield return new WaitForSecondsRealtime(0.04f);
            t += 0.04f;
        }
        group.alpha = 0f;
        panel.anchoredPosition = restPos;
        SetGhosts(false, 0f);
        running = null;
        onDone?.Invoke();
        gameObject.SetActive(false);
    }

    // ---------- Helpers ----------

    private Ghost MakeGhosts(TMP_Text src)
    {
        var g = new Ghost();
        g.a = CloneBehind(src, ghostColorA);
        g.b = CloneBehind(src, ghostColorB);
        return g;
    }

    private TMP_Text CloneBehind(TMP_Text src, Color color)
    {
        var clone = Instantiate(src, src.transform.parent);
        clone.name = src.name + "_Ghost";
        clone.color = color;
        clone.raycastTarget = false;
        clone.transform.SetSiblingIndex(src.transform.GetSiblingIndex()); // draws behind original

        var le = clone.gameObject.GetComponent<LayoutElement>();
        if (le == null) le = clone.gameObject.AddComponent<LayoutElement>();
        le.ignoreLayout = true;

        clone.gameObject.SetActive(false);
        return clone;
    }

    private void SetText(TMP_Text txt, Ghost ghost, string value)
    {
        if (txt == null) return;
        txt.text = value;
        if (ghost.a != null) ghost.a.text = value;
        if (ghost.b != null) ghost.b.text = value;
    }

    private void SetGhosts(bool on, float amount)
    {
        ApplyGhost(titleText, titleGhost, on, amount);
        ApplyGhost(bodyText, bodyGhost, on, amount);
    }

    private void ApplyGhost(TMP_Text src, Ghost g, bool on, float amount)
    {
        if (src == null || g.a == null) return;
        g.a.gameObject.SetActive(on);
        g.b.gameObject.SetActive(on);
        if (!on) return;

        var srcRt = src.rectTransform;
        g.a.rectTransform.anchoredPosition = srcRt.anchoredPosition +
            new Vector2(-Random.Range(0.5f, 1f) * amount, Random.Range(-1f, 1f));
        g.b.rectTransform.anchoredPosition = srcRt.anchoredPosition +
            new Vector2(Random.Range(0.5f, 1f) * amount, Random.Range(-1f, 1f));
    }

    private void PlaySound()
    {
        if (glitchSound != null) glitchSound.Play();
    }
}