using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuActions : MonoBehaviour
{
    public void LoadScene(string sceneName) { SceneManager.LoadScene(sceneName); }

    public void OpenPanel(GameObject panel) { panel.SetActive(true); }
    public void ClosePanel(GameObject panel) { panel.SetActive(false); }
}