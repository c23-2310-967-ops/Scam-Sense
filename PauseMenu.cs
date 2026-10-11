using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

// Press Esc to pause. Builds its own Resume / Quit menu at runtime,
// so there is no Canvas or Button to set up by hand.
public class PauseMenu : MonoBehaviour
{
    [Tooltip("Key that opens/closes the pause menu.")]
    public Key pauseKey = Key.Escape;

    [Tooltip("Backup key, because the Unity editor sometimes grabs Esc.")]
    public Key altPauseKey = Key.P;

    [Tooltip("Optional: scripts to switch off while paused (e.g. a mouse-look script).")]
    public Behaviour[] disableWhilePaused;

    private GameObject menuRoot;
    private bool isPaused;
    private CursorLockMode previousLockState;
    private bool previousCursorVisible;
    private readonly List<Behaviour> pausedBehaviours = new List<Behaviour>();

    // Other scripts (e.g. a mouse-look script) can check this to ignore input while paused
    public static bool IsPaused { get; private set; }

    void Start()
    {
        EnsureEventSystem();
        BuildMenu();
        menuRoot.SetActive(false);
        Debug.Log("PauseMenu is running. Press " + pauseKey + " or " + altPauseKey + " in the Game view.");
    }

    void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current[pauseKey].wasPressedThisFrame || Keyboard.current[altPauseKey].wasPressedThisFrame)
        {
            Debug.Log("Pause key pressed. Paused = " + !isPaused);
            if (isPaused) Resume();
            else Pause();
        }
    }

    public void Pause()
    {
        isPaused = true;
        IsPaused = true;
        menuRoot.SetActive(true);
        Time.timeScale = 0f;
        AudioListener.pause = true;

        previousLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        PauseBehaviours();
    }

    public void Resume()
    {
        isPaused = false;
        IsPaused = false;
        menuRoot.SetActive(false);
        Time.timeScale = 1f;
        AudioListener.pause = false;

        Cursor.lockState = previousLockState;
        Cursor.visible = previousCursorVisible;

        ResumeBehaviours();
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        // Application.Quit() does nothing in the editor, so stop Play mode instead
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void OnDestroy()
    {
        // Never leave the game frozen if the scene reloads while paused
        Time.timeScale = 1f;
        AudioListener.pause = false;
        IsPaused = false;
    }

    // Switches off player control scripts while paused, not just time.
    private void PauseBehaviours()
    {
        pausedBehaviours.Clear();

        foreach (PlayerMotor m in FindObjectsByType<PlayerMotor>(FindObjectsSortMode.None))
            DisableAndRemember(m);
        foreach (InputManager i in FindObjectsByType<InputManager>(FindObjectsSortMode.None))
            DisableAndRemember(i);
        if (disableWhilePaused != null)
        {
            foreach (Behaviour b in disableWhilePaused)
                DisableAndRemember(b);
        }
    }

    private void DisableAndRemember(Behaviour b)
    {
        if (b != null && b != this && b.enabled)
        {
            b.enabled = false;
            pausedBehaviours.Add(b);
        }
    }

    private void ResumeBehaviours()
    {
        foreach (Behaviour b in pausedBehaviours)
        {
            if (b != null) b.enabled = true;
        }
        pausedBehaviours.Clear();
    }

    // ---------- UI building ----------

    private void EnsureEventSystem()
    {
        EventSystem es = FindFirstObjectByType<EventSystem>();
        if (es == null)
        {
            GameObject go = new GameObject("EventSystem", typeof(EventSystem));
            go.AddComponent<InputSystemUIInputModule>();
            return;
        }

        // The old input module throws errors when the project uses the new Input System
        if (es.GetComponent<InputSystemUIInputModule>() == null)
        {
            StandaloneInputModule old = es.GetComponent<StandaloneInputModule>();
            if (old != null) Destroy(old);
            es.gameObject.AddComponent<InputSystemUIInputModule>();
        }
    }

    private void BuildMenu()
    {
        // Canvas
        GameObject canvasGO = new GameObject("PauseMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        menuRoot = canvasGO;

        // Dark background that covers the screen
        GameObject dim = new GameObject("Background", typeof(RectTransform), typeof(Image));
        dim.transform.SetParent(canvasGO.transform, false);
        Stretch(dim.GetComponent<RectTransform>());
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

        // Title + buttons
        CreateLabel(canvasGO.transform, "PAUSED", new Vector2(0, 160), 80);
        CreateButton(canvasGO.transform, "Resume", new Vector2(0, 30), Resume);
        CreateButton(canvasGO.transform, "Quit Game", new Vector2(0, -70), QuitGame);
    }

    private void CreateLabel(Transform parent, string content, Vector2 position, int fontSize)
    {
        GameObject go = new GameObject("Title", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(800, 120);
        rt.anchoredPosition = position;

        Text text = go.GetComponent<Text>();
        text.text = content;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
    }

    private void CreateButton(Transform parent, string label, Vector2 position, UnityAction onClick)
    {
        GameObject go = new GameObject(label + " Button", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(360, 80);
        rt.anchoredPosition = position;

        go.GetComponent<Image>().color = new Color(0.22f, 0.22f, 0.22f, 1f);
        go.GetComponent<Button>().onClick.AddListener(onClick);

        GameObject textGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGO.transform.SetParent(go.transform, false);
        Stretch(textGO.GetComponent<RectTransform>());

        Text text = textGO.GetComponent<Text>();
        text.text = label;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 36;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}