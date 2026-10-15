using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// <summary>
/// The 3D main-menu background. Lives in MenuBackdrop_Room1: copies of the game's rooms (Level 1's house and
/// Rooms 2-4) with all gameplay removed, one room switched on at a time (built by Tools > Scam Sense > Menu >
/// Build Menu Backdrop). MainMenuUI loads that scene additively
/// behind the menu; if it is missing, the menu keeps its still background shots.
///
/// While enabled it is the only camera that renders. It plays a loop of cinematic shots across the rooms
/// (establishing shot, workstation close-up, investigation office, operations room, evidence wall, lab): each shot is
/// a slow dolly from one pose to another; between shots the background dips briefly towards dark
/// (<see cref="CutFade"/>, applied by MainMenuUI to the menu's Tint layer behind the UI), so the camera never
/// travels through furniture and the loop has no visible jump. Gentle sway and mouse parallax on top.
/// At each cut it switches on the shot's room and applies that room's own lighting settings; only that room
/// renders. It also blinks the Level 1 server-rack LEDs and types a "threat monitor" feed on its workstation.
/// Everything it changes outside its own scene (other cameras and lights, RenderSettings) is put back when it
/// is disabled, so the menu scene and the levels are never affected.
/// </summary>
public class MenuBackdrop : MonoBehaviour
{
    /// <summary>The backdrop currently on screen (null when the menu shows its still shots).</summary>
    public static MenuBackdrop Active { get; private set; }

    /// <summary>One room copy and the lighting settings of the scene it came from (captured by the builder).</summary>
    [System.Serializable]
    public class Room
    {
        public string name = "Room";
        public GameObject root;
        public AmbientMode ambientMode = AmbientMode.Trilight;
        public Color ambientSky = Color.gray, ambientEquator = Color.gray, ambientGround = Color.gray;
        public float ambientIntensity = 1f;
        public Material skybox;
        public float reflectionIntensity = 1f;
        public bool fog;
        public FogMode fogMode = FogMode.ExponentialSquared;
        public Color fogColor = Color.gray;
        public float fogDensity = 0.01f, fogStart = 0f, fogEnd = 300f;
    }

    /// <summary>One camera shot: a slow move from one pose to another, in one room (that room's world space).</summary>
    [System.Serializable]
    public class Shot
    {
        public string name = "Shot";
        [Tooltip("Index into Rooms.")]
        public int room;
        public Vector3 fromPosition, toPosition;
        [Tooltip("Point the camera looks at, at the start and at the end of the shot.")]
        public Vector3 fromLookAt, toLookAt;
        [Range(20f, 90f)] public float fieldOfView = 55f;
        [Min(2f)] public float duration = 10f;
    }

    /// <summary>0 normally, rising to 1 at a cut between shots. MainMenuUI darkens the background with it.</summary>
    public static float CutFade => Active != null ? Active.cutFade : 0f;

    [Header("Shots (played in order, looping)")]
    [SerializeField] private Camera cam;
    [SerializeField] private Shot[] shots = new Shot[0];
    [Tooltip("Seconds the background takes to dip out at the end of a shot (and back in at the start of the next).")]
    [SerializeField, Min(0.1f)] private float cutSeconds = 0.9f;
    [SerializeField] private float swayAmount = 0.015f;
    [SerializeField] private float parallaxDegrees = 1.0f;

    [Header("Rooms (one is on at a time)")]
    [SerializeField] private Room[] rooms = new Room[0];

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

    float t0, cutFade;
    int shotIndex, activeRoom = -1;
    Vector2 parallax;
    float[] ledNext;
    readonly List<string> shown = new List<string>();
    readonly StringBuilder sb = new StringBuilder(1024);
    int lineIndex, charIndex;
    float typeTimer, clockTimer;

    void OnEnable()
    {
        if (cam == null) cam = GetComponentInChildren<Camera>(true);
        if (cam == null || shots == null || shots.Length == 0 || rooms == null || rooms.Length == 0)
        {
            Debug.LogWarning("MenuBackdrop: no camera shots - rebuild with Tools > Scam Sense > Menu > Build Menu Backdrop.", this);
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
        activeRoom = -1;
        ShowRoom(shots[0].room);

        t0 = Time.unscaledTime;
        ledNext = new float[blinkingLeds.Length];
        for (int i = 0; i < ledNext.Length; i++) ledNext[i] = t0 + Random.Range(0.2f, 2f);
        shotIndex = 0;
        cutFade = 1f;   // the first shot fades in
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

    /// <summary>Switches on one room (the others off) and applies its lighting. Called at a cut, while the background is dark.</summary>
    void ShowRoom(int index)
    {
        index = Mathf.Clamp(index, 0, rooms.Length - 1);
        if (index == activeRoom) return;
        activeRoom = index;
        for (int i = 0; i < rooms.Length; i++)
            if (rooms[i] != null && rooms[i].root != null) rooms[i].root.SetActive(i == index);
        var r = rooms[index];
        ApplyLighting(r.ambientMode, r.ambientSky, r.ambientEquator, r.ambientGround, r.ambientIntensity, r.reflectionIntensity, r.skybox,
                      r.fog, r.fogMode, r.fogColor, r.fogDensity, r.fogStart, r.fogEnd);
        cam.clearFlags = r.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
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
        float t = now - t0;
        if (t >= shots[shotIndex].duration)   // next shot (loops)
        {
            t0 += shots[shotIndex].duration;
            t = now - t0;
            shotIndex = (shotIndex + 1) % shots.Length;
            ShowRoom(shots[shotIndex].room);   // the background is fully dipped here
        }
        PlaceCamera(t, parallax);

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

    /// <summary>
    /// Current shot at time <paramref name="t"/>: eased dolly from its start pose to its end pose, plus sway and
    /// parallax. The cut fade is 1 at the very start and end of each shot and 0 in between.
    /// </summary>
    void PlaceCamera(float t, Vector2 look)
    {
        var shot = shots[shotIndex];
        float d = Mathf.Max(0.01f, shot.duration);
        float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / d));
        Vector3 pos = Vector3.Lerp(shot.fromPosition, shot.toPosition, k);
        Vector3 at = Vector3.Lerp(shot.fromLookAt, shot.toLookAt, k);
        float c = Mathf.Min(cutSeconds, d * 0.3f);
        cutFade = shots.Length > 1 ? Mathf.Max(1f - Mathf.Clamp01(t / c), Mathf.Clamp01((t - (d - c)) / c)) : 1f - Mathf.Clamp01(t / c);
        cutFade = cutFade * cutFade * (3f - 2f * cutFade);   // smooth in/out

        float n = Time.unscaledTime;
        pos += new Vector3(Mathf.PerlinNoise(n * 0.11f, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, n * 0.09f) - 0.5f, 0f) * (swayAmount * 2f);
        var rot = Quaternion.LookRotation(at - pos, Vector3.up);
        rot = rot * Quaternion.Euler(-look.y * parallaxDegrees, look.x * parallaxDegrees, 0f);
        cam.transform.SetPositionAndRotation(pos, rot);
        cam.fieldOfView = shot.fieldOfView;
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
