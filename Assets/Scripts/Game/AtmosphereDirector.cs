using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Soft mood lighting for the investigation rooms (3D soft realism, mobile-friendly: no post-processing).
///   - A wrong answer raises the room's "tension": lights dim slightly and cool, ambient light drops, and a soft
///     dark vignette closes in. Trusting a scam (missed risk) raises it more than a false alarm.
///   - A correct answer (first try or follow-up re-check) lowers it again, with a brief warm lift.
///   - Tension also drifts back to calm over time, so the effect never lingers.
/// Added automatically to any scene with a ScoringSystem; no scene or gameplay changes are needed.
/// Costs nothing while calm: lights and ambient are only written while a transition is running, and the vignette
/// is one full-screen image drawn under the HUD. Lights driven by other scripts (e.g. monitor flicker) are left alone.
/// </summary>
public class AtmosphereDirector : MonoBehaviour
{
    const float MissedScamTension = 0.55f;   // trusted a scam
    const float FalseAlarmTension = 0.35f;   // reported a genuine message as a scam
    const float CorrectRelief = 0.5f;
    const float CalmPerSecond = 0.02f;       // tension drifts back to calm (0.5 takes about 25 s)
    const float FollowSeconds = 1.6f;        // how quickly the look follows the tension
    const float MaxLightDim = 0.28f, MaxAmbientDim = 0.35f, MaxCoolTint = 0.35f, MaxVignette = 0.42f;
    const float WarmLift = 0.06f, WarmTintAmount = 0.25f, WarmSeconds = 1.8f;

    static readonly Color CoolTint = new Color(0.72f, 0.82f, 1f);
    static readonly Color WarmTint = new Color(1f, 0.93f, 0.82f);
    static readonly Color AmbientNight = new Color(0.16f, 0.19f, 0.26f);
    static readonly Color VignetteColor = new Color(0.03f, 0.05f, 0.10f);

    class LightState
    {
        public Light light;
        public float baseIntensity, lastIntensity;
        public Color baseColor, lastColor;
        public bool external;   // another script changed it: hands off
    }

    ScoringSystem scoring;
    float tension, shown, warm;
    bool active;   // base values captured and effects applied
    readonly List<LightState> lights = new List<LightState>();
    Color baseSky, baseEquator, baseGround, baseFlat;
    float baseAmbientIntensity;
    Image vignette;

    // ---------- Setup ----------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var s in root.GetComponentsInChildren<ScoringSystem>(true))
                if (s.GetComponent<AtmosphereDirector>() == null) s.gameObject.AddComponent<AtmosphereDirector>();
    }

    void OnEnable()
    {
        scoring = GetComponent<ScoringSystem>();
        if (scoring != null) scoring.AnswerRecorded += OnAnswer;
    }

    void OnDisable()
    {
        if (scoring != null) scoring.AnswerRecorded -= OnAnswer;
        Restore();
    }

    void OnAnswer(ScoringSystem.AnswerResult r)
    {
        if (r.isCorrect)
        {
            tension = Mathf.Max(0f, tension - CorrectRelief);
            warm = 1f;
        }
        else
        {
            bool missedScam = r.message != null && r.message.correctVerdict == MessageVerdict.Scam;
            tension = Mathf.Min(1f, tension + (missedScam ? MissedScamTension : FalseAlarmTension));
            warm = 0f;
        }
    }

    // ---------- Per frame ----------

    void Update()
    {
        tension = Mathf.Max(0f, tension - CalmPerSecond * Time.deltaTime);
        float dt = Time.unscaledDeltaTime;
        shown = Mathf.MoveTowards(shown, tension, dt / FollowSeconds);
        warm = Mathf.MoveTowards(warm, 0f, dt / WarmSeconds);

        bool calm = shown <= 0.001f && warm <= 0.001f;
        if (calm)
        {
            if (active) Restore();   // back to the room's own lighting, then stop writing anything
            return;
        }
        if (!active) Capture();
        Apply();
    }

    /// <summary>Remembers the room's lighting as it is right now (other scripts may have changed it since loading).</summary>
    void Capture()
    {
        lights.Clear();
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.gameObject.scene != gameObject.scene || !l.isActiveAndEnabled) continue;
            lights.Add(new LightState { light = l, baseIntensity = l.intensity, lastIntensity = l.intensity, baseColor = l.color, lastColor = l.color });
        }
        baseSky = RenderSettings.ambientSkyColor;
        baseEquator = RenderSettings.ambientEquatorColor;
        baseGround = RenderSettings.ambientGroundColor;
        baseFlat = RenderSettings.ambientLight;
        baseAmbientIntensity = RenderSettings.ambientIntensity;
        if (vignette == null) vignette = BuildVignette();
        active = true;
    }

    void Apply()
    {
        float dim = 1f - MaxLightDim * shown + WarmLift * warm;
        foreach (var s in lights)
        {
            if (s.external || s.light == null) continue;
            if (!Mathf.Approximately(s.light.intensity, s.lastIntensity) || s.light.color != s.lastColor) { s.external = true; continue; }
            Color c = Color.Lerp(s.baseColor, s.baseColor * CoolTint, MaxCoolTint * shown);
            c = Color.Lerp(c, c * WarmTint, WarmTintAmount * warm);
            c.a = s.baseColor.a;
            s.light.intensity = s.lastIntensity = s.baseIntensity * dim;
            s.light.color = s.lastColor = c;
        }

        float amb = MaxAmbientDim * shown;
        RenderSettings.ambientSkyColor = Color.Lerp(baseSky, baseSky * AmbientNight * 3f, amb);
        RenderSettings.ambientEquatorColor = Color.Lerp(baseEquator, baseEquator * AmbientNight * 3f, amb);
        RenderSettings.ambientGroundColor = Color.Lerp(baseGround, baseGround * AmbientNight * 3f, amb);
        RenderSettings.ambientLight = Color.Lerp(baseFlat, baseFlat * AmbientNight * 3f, amb);
        RenderSettings.ambientIntensity = baseAmbientIntensity * (1f - amb);

        if (vignette != null)
        {
            var vc = VignetteColor;
            vc.a = MaxVignette * shown;
            vignette.color = vc;
            vignette.enabled = vc.a > 0.002f;
        }
    }

    void Restore()
    {
        if (!active) return;
        foreach (var s in lights)
        {
            if (s.external || s.light == null) continue;
            s.light.intensity = s.baseIntensity;
            s.light.color = s.baseColor;
        }
        RenderSettings.ambientSkyColor = baseSky;
        RenderSettings.ambientEquatorColor = baseEquator;
        RenderSettings.ambientGroundColor = baseGround;
        RenderSettings.ambientLight = baseFlat;
        RenderSettings.ambientIntensity = baseAmbientIntensity;
        if (vignette != null) vignette.enabled = false;
        lights.Clear();
        active = false;
    }

    // ---------- Vignette ----------

    /// <summary>A soft radial vignette on its own canvas, drawn under every other screen UI (the HUD stays crisp).</summary>
    Image BuildVignette()
    {
        var go = new GameObject("Atmosphere Vignette", typeof(Canvas));
        go.transform.SetParent(transform, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -100;

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "AtmosphereVignette" };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / 1.4142f;   // 0 centre .. 1 corner
                float a = Mathf.Lerp(0.12f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 1f, r)));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);

        var imgGo = new GameObject("Vignette", typeof(RectTransform), typeof(Image));
        imgGo.transform.SetParent(go.transform, false);
        var rt = (RectTransform)imgGo.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = imgGo.GetComponent<Image>();
        img.sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        img.raycastTarget = false;
        img.enabled = false;
        return img;
    }
}
