using System.Collections;
using UnityEngine;

/// <summary>
/// Put this on a full-screen black Image (with a CanvasGroup) in the GAME scene.
/// It starts fully black and fades out, so the scene eases in instead of cutting in.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class SceneFadeIn : MonoBehaviour
{
    public float holdBlackTime = 0.3f;
    public float fadeTime = 1.2f;
    [Tooltip("Also fade the game audio up from silence.")]
    public bool fadeAudio = true;

    CanvasGroup group;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        group.alpha = 1f;
        group.blocksRaycasts = true;
        if (fadeAudio) AudioListener.volume = 0f;
    }

    IEnumerator Start()
    {
        // Wait a moment so loading hitches happen while the screen is still black
        yield return null;
        yield return new WaitForSecondsRealtime(holdBlackTime);

        for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            float k = t / fadeTime;
            k = k * k * (3f - 2f * k);
            group.alpha = 1f - k;
            if (fadeAudio) AudioListener.volume = k;
            yield return null;
        }

        group.alpha = 0f;
        group.blocksRaycasts = false;
        if (fadeAudio) AudioListener.volume = 1f;
    }
}