using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// New Case sequence: folder slides in -> "CASE OPENED" stamp slams down with a shake
/// -> menu slowly pushes in while music and screen fade out -> game scene loads.
/// Hook PlayNewCase() to the NEW CASE button's OnClick.
/// </summary>
public class NewCaseTransition : MonoBehaviour
{
    [Header("Scene")]
    public string gameSceneName = "Game";

    [Header("UI References")]
    [Tooltip("Parent of all menu art. Shaken on stamp impact and pushed in during the fade. Must be a child panel, NOT the Canvas itself. Optional.")]
    public RectTransform sceneRoot;
    public CanvasGroup menuButtons;
    [Tooltip("Place the folder where it should END UP. It is moved off-screen at start.")]
    public RectTransform folder;
    public RectTransform caseOpenedStamp;
    [Tooltip("Full-screen black Image with a CanvasGroup. Must be the LAST sibling in the Canvas.")]
    public CanvasGroup fadeOverlay;

    [Header("Timing")]
    public Vector2 folderHiddenOffset = new Vector2(0f, -1300f);
    public float folderSlideTime = 0.6f;
    public float pauseBeforeStamp = 0.25f;
    public float pauseAfterStamp = 0.7f;
    [Tooltip("Length of the fade to black (longer = smoother).")]
    public float fadeTime = 0.9f;
    [Tooltip("Stay on black this long before the scene switches.")]
    public float holdBlackTime = 0.25f;

    [Header("Smooth Exit")]
    [Tooltip("The 'Main Menu Music' AudioSource. It fades to silence during the transition. Optional.")]
    public AudioSource menuMusic;
    [Tooltip("Menu slowly scales up by this much while fading. 1 = no push-in.")]
    public float pushInScale = 1.08f;

    [Header("Stamp Feel")]
    public float stampAngle = -12f;
    public float stampStartScale = 3f;
    public float shakeStrength = 14f;
    public float shakeTime = 0.22f;

    [Header("Folder Feel (new)")]
    [Tooltip("0 = plain smooth slide. Around 1.2 - 1.8 gives a nice bounce past the target.")]
    public float folderOvershoot = 1.4f;
    [Tooltip("Folder starts tilted by this many degrees and straightens as it arrives.")]
    public float folderTilt = 6f;
    [Tooltip("Fade the menu buttons out while the folder arrives.")]
    public bool fadeOutMenuButtons = true;

    [Header("Folder Open (new)")]
    [Tooltip("The front cover image, a child of the folder. Set its Pivot to X = 0 (left edge) so it hinges like a book. Optional.")]
    public RectTransform folderCover;
    [Tooltip("Papers image inside the folder (sits BELOW the cover). Slides up as the folder opens. Optional.")]
    public RectTransform folderPapers;
    public float pauseBeforeOpen = 0.2f;
    public float coverOpenTime = 0.45f;
    [Tooltip("0 = cover squashes flat and vanishes. -1 = cover flips fully to the left side.")]
    public float coverOpenScaleX = 0f;
    [Tooltip("How many pixels the papers rise when the folder opens.")]
    public float paperRise = 40f;

    [Header("Audio (optional)")]
    public AudioClip openClip;
    public AudioSource sfx;
    public AudioClip slideClip;
    public AudioClip stampClip;

    Vector2 folderShownPos;
    CanvasGroup stampGroup;
    bool playing;

    void Awake()
    {
        if (folder)
        {
            folderShownPos = folder.anchoredPosition;
            folder.anchoredPosition = folderShownPos + folderHiddenOffset;
            folder.localRotation = Quaternion.Euler(0f, 0f, folderTilt);
        }
        else Debug.LogWarning("NewCaseTransition: 'folder' is not assigned.", this);

        if (folderCover) folderCover.localScale = Vector3.one;

        if (caseOpenedStamp)
        {
            stampGroup = caseOpenedStamp.GetComponent<CanvasGroup>();
            if (!stampGroup) stampGroup = caseOpenedStamp.gameObject.AddComponent<CanvasGroup>();
            stampGroup.alpha = 0f;
            stampGroup.blocksRaycasts = false;
        }
        else Debug.LogWarning("NewCaseTransition: 'caseOpenedStamp' is not assigned.", this);

        if (fadeOverlay)
        {
            fadeOverlay.alpha = 0f;
            fadeOverlay.blocksRaycasts = false;
        }
        else Debug.LogWarning("NewCaseTransition: 'fadeOverlay' is not assigned.", this);
    }

    public void PlayNewCase()
    {
        if (!playing) StartCoroutine(Sequence());
    }

    IEnumerator Sequence()
    {
        playing = true;
        if (menuButtons)
        {
            menuButtons.interactable = false;
            menuButtons.blocksRaycasts = false;
        }

        if (!Application.CanStreamedLevelBeLoaded(gameSceneName))
        {
            Debug.LogError("NewCaseTransition: scene '" + gameSceneName + "' is not in the Build Profile Scene List.");
            if (menuButtons)
            {
                menuButtons.interactable = true;
                menuButtons.blocksRaycasts = true;
            }
            playing = false;
            yield break;
        }

        // Exactly ONE load of the game scene. If the SceneLoader (loading screen with tips) exists,
        // it does the loading after this animation; otherwise load here in the background with
        // activation held until the fade ends. (Two loads at once made New Case hang / load twice.)
        bool useSceneLoader = SceneLoader.Instance != null;
        AsyncOperation load = null;
        if (!useSceneLoader)
        {
            load = SceneManager.LoadSceneAsync(gameSceneName);
            load.allowSceneActivation = false;
        }

        // 1. Folder slides in (with a little overshoot and tilt)
        if (folder)
        {
            Play(slideClip);
            Vector2 from = folder.anchoredPosition;
            float buttonsStart = menuButtons ? menuButtons.alpha : 1f;

            yield return Tween(folderSlideTime, k =>
            {
                float e = EaseOutBack(k, folderOvershoot);
                folder.anchoredPosition = Vector2.LerpUnclamped(from, folderShownPos, e);
                folder.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(folderTilt, 0f, Smooth(k)));
                if (fadeOutMenuButtons && menuButtons)
                    menuButtons.alpha = Mathf.Lerp(buttonsStart, 0f, Smooth(k));
            });

            folder.anchoredPosition = folderShownPos;
            folder.localRotation = Quaternion.identity;
        }

        // 1b. Folder opens: cover swings open, papers rise
        if (folderCover)
        {
            yield return new WaitForSecondsRealtime(pauseBeforeOpen);
            Play(openClip);
            Vector2 papersFrom = folderPapers ? folderPapers.anchoredPosition : Vector2.zero;

            yield return Tween(coverOpenTime, k =>
            {
                float e = Smooth(k);
                folderCover.localScale = new Vector3(Mathf.Lerp(1f, coverOpenScaleX, e), 1f, 1f);
                if (folderPapers)
                    folderPapers.anchoredPosition = papersFrom + new Vector2(0f, paperRise * e);
            });
        }

        yield return new WaitForSecondsRealtime(pauseBeforeStamp);

        // 2. Stamp slams down
        if (caseOpenedStamp)
        {
            caseOpenedStamp.localRotation = Quaternion.Euler(0f, 0f, stampAngle);
            caseOpenedStamp.localScale = Vector3.one * stampStartScale;

            yield return Tween(0.12f, k =>
            {
                stampGroup.alpha = k;
                caseOpenedStamp.localScale = Vector3.one * Mathf.Lerp(stampStartScale, 1f, k * k);
            });
            caseOpenedStamp.localScale = Vector3.one;
            stampGroup.alpha = 1f;
        }

        // 3. Impact
        Play(stampClip);
        yield return Shake(sceneRoot ? sceneRoot : folder, shakeTime);
        yield return new WaitForSecondsRealtime(pauseAfterStamp);

        // 4. Smooth exit: push-in + music fade + fade to black, all together
        if (fadeOverlay) fadeOverlay.blocksRaycasts = true;
        float musicStart = menuMusic ? menuMusic.volume : 0f;
        Vector3 rootStart = sceneRoot ? sceneRoot.localScale : Vector3.one;

        yield return Tween(fadeTime, k =>
        {
            float e = Smooth(k);
            if (fadeOverlay) fadeOverlay.alpha = e;
            if (menuMusic) menuMusic.volume = Mathf.Lerp(musicStart, 0f, e);
            if (sceneRoot) sceneRoot.localScale = rootStart * Mathf.Lerp(1f, pushInScale, e);
        });

        yield return new WaitForSecondsRealtime(holdBlackTime);

        if (useSceneLoader)
        {
            // Screen is black now; the loading screen fades in over it and loads the scene.
            SceneLoader.LoadScene(gameSceneName);
            yield break;
        }

        while (load.progress < 0.9f) yield return null;
        load.allowSceneActivation = true;
    }

    IEnumerator Shake(RectTransform target, float duration)
    {
        if (!target) yield break;
        Vector2 origin = target.anchoredPosition;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float falloff = 1f - t / duration;
            target.anchoredPosition = origin + Random.insideUnitCircle * shakeStrength * falloff;
            yield return null;
        }
        target.anchoredPosition = origin;
    }

    // Raw 0..1 progress; easing is applied by the caller.
    IEnumerator Tween(float duration, System.Action<float> apply)
    {
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            apply(t / duration);
            yield return null;
        }
        apply(1f);
    }

    static float Smooth(float k) { return k * k * (3f - 2f * k); }

    static float EaseOutBack(float k, float s)
    {
        if (s <= 0f) return Smooth(k);
        k -= 1f;
        return 1f + (s + 1f) * k * k * k + s * k * k;
    }

    void Play(AudioClip clip)
    {
        if (sfx && clip) sfx.PlayOneShot(clip);
    }
}