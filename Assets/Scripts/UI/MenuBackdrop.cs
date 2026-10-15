using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// <summary>
/// The 3D main-menu background. Lives in MenuBackdrop_Room1, a copy of Level 1's house with all gameplay
/// removed (built by Tools > Scam Sense > Menu > Build Menu Backdrop). MainMenuUI loads that scene additively
/// behind the menu; if it is missing, the menu keeps its still background shots.
///
/// While enabled it is the only camera that renders: a slow cinematic drift through the Level 1 computer room
/// with a gentle sway and a little mouse parallax. It applies Level 1's own lighting settings, blinks the
/// server-rack LEDs and types a "threat monitor" feed on the workstation screen.
/// Everything it changes outside its own scene (other cameras and lights, RenderSettings) is put back when it
/// is disabled, so the menu scene and the levels are never affected.
/// </summary>
public class MenuBackdrop : MonoBehaviour
{
    /// <summary>The backdrop currently on screen (null when the menu shows its still shots).</summary>
    public static MenuBackdrop Active { get; private set; }

    [Header("Camera path (world space of the Level 1 house)")]
    [SerializeField] private Camera cam;
    [SerializeField] private Vector3[] positions = new Vector3[0];
    [SerializeField] private Vector3[] lookTargets = new Vector3[0];
    [Tooltip("Seconds to glide from one viewpoint to the next.")]
    [SerializeField] private float legSeconds = 24f;
    [SerializeField] private float swayAmount = 0.02f;
    [SerializeField] private float parallaxDegrees = 1.2f;

    [Header("Level 1 lighting (captured by the builder)")]
    [SerializeField] private AmbientMode ambientMode = AmbientMode.Trilight;
    [SerializeField] private Color ambientSky = Color.gray, ambientEquator = Color.gray, ambientGround = Color.gray;
    [SerializeField] private float ambientIntensity = 1f;
    [SerializeField] private Material skybox;
    [SerializeField] private float reflectionIntensity = 1f;
    [SerializeField] private bool fog;
    [SerializeField] private FogMode fogMode = FogMode.ExponentialSquared;
    [SerializeField] private Color fogColor = Color.gray;
    [SerializeField] private float fogDensity = 0.01f, fogStart = 0f, fogEnd = 300f;

    [Header("Ambient animation")]
    [SerializeField] private Renderer[] blinkingLeds = new Renderer[0];
    [SerializeField] private TMP_Text screenFeed;
    [SerializeField] private TMP_Text screenClock;

    // Fictional entries only (game brands and .example domains).
    static readonly string[] FeedLines =
    {
        "> scanning inbox ............ 12 new messages",
        "  <color=#F87171>[!]</color> spoofed sender  support@bank-secure-verify.example",
        "  <color=#22C55E>[OK]</color> receipt from orders@brightway.example verified",
        "> checking link  http://claim-prize-now.example/win",
        "  <color=#F87171>[!]</color> urgency + payment request detected",
        "  <color=#F87171>[!]</color> look-alike domain  novacl0ud-login.example",
        "  <color=#22C55E>[OK]</color> sign-in alert from security@novacloud.example",
        "> SMS from unknown number: \"parcel on hold\"",
        "  <color=#FBBF24>[?]</color> link shortener hides destination",
        "> threat level ............. <color=#FBBF24>ELEVATED</color>",
        "> awaiting investigator ...",
    };
    const int VisibleLines = 9;
    const float CharsPerSecond = 46f;

    // Restored on disable
    readonly List<Behaviour> disabledOutside = new List<Behaviour>();
    AmbientMode oldAmbientMode; Color oldSky, oldEquator, oldGround; float oldAmbientIntensity, oldReflection;
    Material oldSkybox; bool oldFog; FogMode oldFogMode; Color oldFogColor; float oldFogDensity, oldFogStart, oldFogEnd;

    float t0;
    Vector2 parallax;
    float[] ledNext;
    readonly List<string> shown = new List<string>();
    readonly StringBuilder sb = new StringBuilder(1024);
    int lineIndex, charIndex;
    float typeTimer, clockTimer;

    void OnEnable()
    {
        if (cam == null) cam = GetComponentInChildren<Camera>(true);
        if (cam == null || positions.Length == 0 || positions.Length != lookTargets.Length)
        {
            Debug.LogWarning("MenuBackdrop: camera path missing - rebuild with Tools > Scam Sense > Menu > Build Menu Backdrop.", this);
            enabled = false;
            return;
        }

        // Only this camera renders; the menu scene's camera and lights pause while the backdrop is shown.
        foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            if (c != cam && c.enabled && c.gameObject.scene != gameObject.scene) { c.enabled = false; disabledOutside.Add(c); }
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.enabled && l.gameObject.scene != gameObject.scene) { l.enabled = false; disabledOutside.Add(l); }
        cam.enabled = true;

        oldAmbientMode = RenderSettings.ambientMode; oldSky = RenderSettings.ambientSkyColor; oldEquator = RenderSettings.ambientEquatorColor;
        oldGround = RenderSettings.ambientGroundColor; oldAmbientIntensity = RenderSettings.ambientIntensity; oldReflection = RenderSettings.reflectionIntensity;
        oldSkybox = RenderSettings.skybox; oldFog = RenderSettings.fog; oldFogMode = RenderSettings.fogMode; oldFogColor = RenderSettings.fogColor;
        oldFogDensity = RenderSettings.fogDensity; oldFogStart = RenderSettings.fogStartDistance; oldFogEnd = RenderSettings.fogEndDistance;
        ApplyLighting(ambientMode, ambientSky, ambientEquator, ambientGround, ambientIntensity, reflectionIntensity, skybox,
                      fog, fogMode, fogColor, fogDensity, fogStart, fogEnd);

        t0 = Time.unscaledTime;
        ledNext = new float[blinkingLeds.Length];
        for (int i = 0; i < ledNext.Length; i++) ledNext[i] = t0 + Random.Range(0.2f, 2f);
        PlaceCamera(0f, Vector2.zero);
        Active = this;
    }

    void OnDisable()
    {
        if (Active == this) Active = null;
        foreach (var b in disabledOutside) if (b != null) b.enabled = true;
        disabledOutside.Clear();
        if (ledNext != null)
            ApplyLighting(oldAmbientMode, oldSky, oldEquator, oldGround, oldAmbientIntensity, oldReflection, oldSkybox,
                          oldFog, oldFogMode, oldFogColor, oldFogDensity, oldFogStart, oldFogEnd);
    }

    static void ApplyLighting(AmbientMode mode, Color sky, Color equator, Color ground, float intensity, float reflection, Material box,
                              bool useFog, FogMode fMode, Color fColor, float density, float start, float end)
    {
        RenderSettings.ambientMode = mode;
        RenderSettings.ambientSkyColor = sky;
        RenderSettings.ambientEquatorColor = equator;
        RenderSettings.ambientGroundColor = ground;
        RenderSettings.ambientIntensity = intensity;
        RenderSettings.reflectionIntensity = reflection;
        RenderSettings.skybox = box;
        RenderSettings.fog = useFog;
        RenderSettings.fogMode = fMode;
        RenderSettings.fogColor = fColor;
        RenderSettings.fogDensity = density;
        RenderSettings.fogStartDistance = start;
        RenderSettings.fogEndDistance = end;
    }

    void Update()
    {
        float now = Time.unscaledTime;

        // Gentle mouse parallax (eased so it never feels like the camera is being steered).
        var mouse = Mouse.current;
        Vector2 target = Vector2.zero;
        if (mouse != null && Screen.width > 0 && Screen.height > 0)
        {
            Vector2 m = mouse.position.ReadValue();
            target = new Vector2(m.x / Screen.width * 2f - 1f, m.y / Screen.height * 2f - 1f);
            target = Vector2.ClampMagnitude(target, 1.4f);
        }
        parallax = Vector2.Lerp(parallax, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 2f));
        PlaceCamera(now - t0, parallax);

        // Server-rack LEDs: mostly on, with short random blinks.
        for (int i = 0; i < blinkingLeds.Length; i++)
        {
            if (blinkingLeds[i] == null || now < ledNext[i]) continue;
            bool on = !blinkingLeds[i].enabled;
            blinkingLeds[i].enabled = on;
            ledNext[i] = now + (on ? Random.Range(0.4f, 2.6f) : Random.Range(0.06f, 0.22f));
        }

        TypeFeed(Time.unscaledDeltaTime);

        if (screenClock != null && (clockTimer -= Time.unscaledDeltaTime) <= 0f)
        {
            clockTimer = 1f;
            screenClock.SetText(System.DateTime.Now.ToString("HH:mm"));
        }
    }

    /// <summary>Glides back and forth along the viewpoints with an ease at each end, plus sway and parallax.</summary>
    void PlaceCamera(float t, Vector2 look)
    {
        int n = positions.Length;
        Vector3 pos; Vector3 at;
        if (n == 1) { pos = positions[0]; at = lookTargets[0]; }
        else
        {
            int legs = n - 1;
            float cycle = legs * 2f * legSeconds;               // there and back
            float u = Mathf.Repeat(t, cycle) / legSeconds;      // 0 .. 2*legs
            if (u > legs) u = 2f * legs - u;                    // ping-pong
            int i = Mathf.Min(Mathf.FloorToInt(u), legs - 1);
            float k = Mathf.SmoothStep(0f, 1f, u - i);
            pos = Vector3.Lerp(positions[i], positions[i + 1], k);
            at = Vector3.Lerp(lookTargets[i], lookTargets[i + 1], k);
        }
        pos += new Vector3(Mathf.PerlinNoise(t * 0.11f, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t * 0.09f) - 0.5f, 0f) * (swayAmount * 2f);
        var rot = Quaternion.LookRotation(at - pos, Vector3.up);
        rot = rot * Quaternion.Euler(-look.y * parallaxDegrees, look.x * parallaxDegrees, 0f);
        cam.transform.SetPositionAndRotation(pos, rot);
    }

    void TypeFeed(float dt)
    {
        if (screenFeed == null) return;
        typeTimer += dt * CharsPerSecond;
        if (typeTimer < 1f) return;
        int steps = (int)typeTimer;
        typeTimer -= steps;

        string line = FeedLines[lineIndex];
        charIndex = Mathf.Min(line.Length, charIndex + steps);
        // Don't stop half-way through a colour tag.
        int open = line.LastIndexOf('<', Mathf.Max(0, charIndex - 1));
        if (open >= 0 && line.IndexOf('>', open) >= charIndex) charIndex = line.IndexOf('>', open) + 1;

        sb.Clear();
        foreach (var s in shown) sb.Append(s).Append('\n');
        sb.Append(line, 0, charIndex);
        if (charIndex >= line.Length)
        {
            shown.Add(line);
            if (shown.Count >= VisibleLines) shown.RemoveAt(0);
            lineIndex = (lineIndex + 1) % FeedLines.Length;
            charIndex = 0;
            typeTimer = -CharsPerSecond * 0.6f;   // short pause between lines
        }
        else sb.Append("<color=#38BDF8>_</color>");
        screenFeed.SetText(sb);
    }
}
