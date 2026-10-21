using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Level sound effects. Assign your own clips in the Inspector; any empty slot uses a
/// small generated sound so the level works without extra audio assets.
/// </summary>
public class LevelAudio : MonoBehaviour
{
    [Header("Routing")]
    [Tooltip("SFX group of MainMixer: notifications, answers, clicks (Settings > Sound effects).")]
    [SerializeField] private AudioMixerGroup sfxGroup;
    [Tooltip("Music group of MainMixer: background music / ambience loop (Settings > Music).")]
    [SerializeField] private AudioMixerGroup musicGroup;

    [Header("Clips (optional - generated if empty)")]
    [SerializeField] private AudioClip smsNotification;
    [SerializeField] private AudioClip emailNotification;
    [SerializeField] private AudioClip correctAnswer;
    [SerializeField] private AudioClip wrongAnswer;
    [SerializeField] private AudioClip uiClick;
    [SerializeField] private AudioClip starReveal;
    [SerializeField] private AudioClip levelComplete;
    [SerializeField] private AudioClip roomUnlocked;
    [Tooltip("Short, soft alert under a wrong answer's investigation warning.")]
    [SerializeField] private AudioClip investigationWarning;

    [Header("Background (Music group)")]
    [Tooltip("Optional background music or room-tone loop. If empty and Generate Ambience is on, a soft hum is generated.")]
    [SerializeField] private AudioClip ambienceLoop;
    [SerializeField] private bool generateAmbience = true;
    [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.06f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.8f;

    private AudioSource sfx;
    private AudioSource ambience;

    void Awake()
    {
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;
        sfx.outputAudioMixerGroup = sfxGroup;

        if (smsNotification == null) smsNotification = Chime("SMS", new[] { 1318.5f, 1760f }, 0.09f);
        if (emailNotification == null) emailNotification = Chime("Email", new[] { 880f, 1174.7f, 1568f }, 0.08f);
        if (correctAnswer == null) correctAnswer = Chime("Correct", new[] { 659.3f, 987.8f }, 0.11f);
        if (wrongAnswer == null) wrongAnswer = Chime("Wrong", new[] { 392f, 311.1f }, 0.14f);
        if (uiClick == null) uiClick = Chime("Click", new[] { 1200f }, 0.03f);
        if (starReveal == null) starReveal = Chime("Star", new[] { 1567.9f }, 0.12f);
        if (levelComplete == null) levelComplete = Chime("Complete", new[] { 523.3f, 659.3f, 784f }, 0.12f);           // calm C major arpeggio
        if (roomUnlocked == null) roomUnlocked = Chime("Unlocked", new[] { 784f, 987.8f, 1174.7f, 1568f }, 0.09f);   // brighter, rising
        if (investigationWarning == null) investigationWarning = Chime("Warning", new[] { 622.3f, 466.2f, 622.3f, 466.2f }, 0.07f);   // quiet two-tone alert

        if (ambienceLoop == null && generateAmbience) ambienceLoop = RoomTone();
        if (ambienceLoop != null)
        {
            ambience = gameObject.AddComponent<AudioSource>();
            ambience.clip = ambienceLoop;
            ambience.loop = true;
            ambience.volume = ambienceVolume;
            ambience.spatialBlend = 0f;
            ambience.outputAudioMixerGroup = musicGroup != null ? musicGroup : sfxGroup;
            ambience.Play();
        }
    }

    public void PlayNotification(MessageChannel channel) =>
        Play(MessageChannels.OnPhone(channel) ? smsNotification : emailNotification);
    public void PlayAnswer(bool correct) => Play(correct ? correctAnswer : wrongAnswer);
    public void PlayClick() => Play(uiClick);
    public void PlayStar() => Play(starReveal);
    public void PlayLevelComplete() => Play(levelComplete);
    public void PlayRoomUnlocked() => Play(roomUnlocked);
    public void PlayWarning() => Play(investigationWarning, 0.6f);

    void Play(AudioClip clip, float volumeScale = 1f)
    {
        if (sfx != null && clip != null) sfx.PlayOneShot(clip, sfxVolume * volumeScale);
    }

    // ---------- Generated sounds ----------

    /// <summary>A short sequence of soft sine "pings" (one per frequency).</summary>
    static AudioClip Chime(string name, float[] freqs, float noteLength)
    {
        const int rate = 44100;
        int noteSamples = Mathf.RoundToInt(noteLength * rate);
        int tail = Mathf.RoundToInt(0.25f * rate);
        int total = noteSamples * freqs.Length + tail;
        var data = new float[total];

        for (int n = 0; n < freqs.Length; n++)
        {
            int start = n * noteSamples;
            int len = noteSamples + tail;
            for (int i = 0; i < len && start + i < total; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Min(1f, i / 200f) * Mathf.Exp(-t * 9f);
                float v = Mathf.Sin(2f * Mathf.PI * freqs[n] * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * freqs[n] * t);
                data[start + i] += v * env * 0.35f;
            }
        }

        var clip = AudioClip.Create("Generated_" + name, total, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Quiet filtered noise with a faint low hum, like a fridge in another room.</summary>
    static AudioClip RoomTone()
    {
        const int rate = 22050;
        const float seconds = 8f;
        int total = Mathf.RoundToInt(rate * seconds);
        var data = new float[total];
        var rng = new System.Random(7);
        float brown = 0f;

        for (int i = 0; i < total; i++)
        {
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            brown = Mathf.Clamp(brown + white * 0.02f, -1f, 1f) * 0.998f;
            // 60 Hz divides evenly into 8 s, so the loop point is seamless for the hum.
            float hum = Mathf.Sin(2f * Mathf.PI * 60f * i / rate) * 0.15f;
            data[i] = brown * 0.8f + hum;
        }

        // Cross-fade: the start of the loop blends in the samples that follow its last sample,
        // so playback wraps from the end back to the start without a click.
        int fade = rate / 4;
        int length = total - fade;
        var loop = new float[length];
        for (int i = 0; i < length; i++)
        {
            if (i < fade)
            {
                float k = i / (float)fade;
                loop[i] = data[i] * k + data[length + i] * (1f - k);
            }
            else loop[i] = data[i];
        }

        var clip = AudioClip.Create("Generated_RoomTone", length, 1, rate, false);
        clip.SetData(loop, 0);
        return clip;
    }
}
