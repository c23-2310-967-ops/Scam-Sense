using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Soft 3D fan/electronics hum for server racks. The clip is generated (no audio file needed) and plays
/// through the same mixer group as the level ambience, so the existing volume sliders control it.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class RackHum : MonoBehaviour
{
    [SerializeField] private AudioMixerGroup outputGroup;
    [SerializeField, Range(0f, 1f)] private float volume = 0.12f;
    [SerializeField] private float maxDistance = 7f;

    static AudioClip sharedClip;

    void Start()
    {
        var src = GetComponent<AudioSource>();
        if (sharedClip == null) sharedClip = MakeClip();
        src.clip = sharedClip;
        src.loop = true;
        src.playOnAwake = false;
        src.volume = volume;
        src.spatialBlend = 1f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 1f;
        src.maxDistance = maxDistance;
        src.dopplerLevel = 0f;
        if (outputGroup != null) src.outputAudioMixerGroup = outputGroup;
        src.time = Random.Range(0f, sharedClip.length * 0.9f);   // racks don't hum in sync
        src.Play();
    }

    /// <summary>Airy fan noise with a faint 120 Hz electrical tone, looped without a click.</summary>
    static AudioClip MakeClip()
    {
        const int rate = 22050;
        const float seconds = 4f;
        int total = Mathf.RoundToInt(rate * seconds);
        var data = new float[total];
        var rng = new System.Random(31);
        float low = 0f;
        for (int i = 0; i < total; i++)
        {
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            low += (white - low) * 0.08f;                                   // soft low-pass "whoosh"
            float tone = Mathf.Sin(2f * Mathf.PI * 120f * i / rate) * 0.05f; // 120 Hz divides 4 s evenly
            data[i] = low * 0.9f + tone;
        }

        int fade = rate / 4, length = total - fade;
        var loop = new float[length];
        for (int i = 0; i < length; i++)
        {
            if (i < fade) { float k = i / (float)fade; loop[i] = data[i] * k + data[length + i] * (1f - k); }
            else loop[i] = data[i];
        }
        var clip = AudioClip.Create("Generated_RackHum", length, 1, rate, false);
        clip.SetData(loop, 0);
        return clip;
    }
}
