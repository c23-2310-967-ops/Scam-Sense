using UnityEngine;

/// <summary>
/// Put this on your menu object and hook the methods to your buttons' OnClick.
/// </summary>
public class MenuButtons : MonoBehaviour
{
    [Header("Option A: separate scenes")]
    public string gameSceneName = "Game";
    public string menuSceneName = "Menu";

    [Header("Option B: menu and game in the same scene")]
    public GameObject menuRoot;                 // the menu object to hide
    public GameObject[] enableOnPlay;           // objects to turn on when the game starts
    public GameObject[] disableOnPlay;          // extra objects to turn off (optional)

    // ----- Option A -----
    public void PlayNewScene()
    {
        SceneLoader.LoadScene(gameSceneName);
    }

    public void BackToMenuScene()
    {
        SceneLoader.LoadScene(menuSceneName);
    }

    // ----- Option B -----
    public void PlaySameScene()
    {
        SceneLoader.Transition(() =>
        {
            if (menuRoot != null) menuRoot.SetActive(false);
            foreach (var go in enableOnPlay) if (go != null) go.SetActive(true);
            foreach (var go in disableOnPlay) if (go != null) go.SetActive(false);
            Time.timeScale = 1f;
        });
    }

    public void BackToMenuSameScene()
    {
        SceneLoader.Transition(() =>
        {
            foreach (var go in enableOnPlay) if (go != null) go.SetActive(false);
            if (menuRoot != null) menuRoot.SetActive(true);
        });
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}