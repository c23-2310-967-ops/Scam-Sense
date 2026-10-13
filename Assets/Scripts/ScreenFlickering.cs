using UnityEngine;

/// <summary>
/// Flickers a monitor screen by animating the material's emission,
/// plus occasional hard "dropouts". Optionally drives a Light so the
/// glow also flickers on nearby surfaces.
/// </summary>
public class ScreenFlicker : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Renderer that holds the screen material. Defaults to the first Renderer on this object or its children.")]
    [SerializeField] private Renderer screenRenderer;
    [Tooltip("Which slot in the Renderer's Materials list is the screen face.")]
    [SerializeField] private int materialIndex = 0;
    [Tooltip("Optional light (e.g. a point light in front of the screen) that flickers with it.")]
    [SerializeField] private Light screenGlow;

    [Header("Look")]
    [ColorUsage(false, true)]
    [SerializeField] private Color baseColor = new Color(0.55f, 0.8f, 1f);
    [SerializeField] private float minIntensity = 0.3f;
    [SerializeField] private float maxIntensity = 1.4f;

    [Header("Smooth flicker")]
    [Tooltip("Higher = faster, more nervous flicker.")]
    [SerializeField] private float noiseSpeed = 10f;

    [Header("Glitch dropouts")]
    [Tooltip("Average number of sudden dropouts per second.")]
    [SerializeField] private float glitchesPerSecond = 0.6f;
    [SerializeField] private Vector2 glitchDuration = new Vector2(0.03f, 0.12f);
    [Range(0f, 1f)]
    [SerializeField] private float glitchBrightness = 0.05f;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private Material screenMat;
    private float glitchUntil;
    private float seed;
    private float glowBaseIntensity;

    private void Awake()
    {
        if (screenRenderer == null)
            screenRenderer = GetComponentInChildren<Renderer>();

        // Accessing .materials creates per-renderer instances, so each
        // screen flickers independently and shared assets aren't modified.
        Material[] mats = screenRenderer.materials;
        materialIndex = Mathf.Clamp(materialIndex, 0, mats.Length - 1);
        screenMat = mats[materialIndex];

        screenMat.EnableKeyword("_EMISSION");
        screenMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

        seed = Random.value * 100f; // so two screens don't flicker in sync
        if (screenGlow != null) glowBaseIntensity = screenGlow.intensity;
    }

    private void Update()
    {
        float n = Mathf.PerlinNoise(seed, Time.time * noiseSpeed);
        float level = Mathf.Lerp(minIntensity, maxIntensity, n);

        if (Time.time < glitchUntil)
        {
            level *= glitchBrightness;
        }
        else if (Random.value < glitchesPerSecond * Time.deltaTime)
        {
            glitchUntil = Time.time + Random.Range(glitchDuration.x, glitchDuration.y);
        }

        screenMat.SetColor(EmissionColorId, baseColor * level);

        if (screenGlow != null)
            screenGlow.intensity = glowBaseIntensity * level;
    }

    private void OnDestroy()
    {
        if (screenMat != null) Destroy(screenMat);
    }
}