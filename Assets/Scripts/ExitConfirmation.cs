using UnityEngine;
using UnityEngine.InputSystem;

public class ExitConfirmation : MonoBehaviour
{
    [SerializeField] private GameObject confirmPanel;

    private void Start()
    {
        // The rebuilt main menu has its own QUIT confirmation (MainMenuUI), so this panel may be gone.
        if (confirmPanel == null) { enabled = false; return; }
        confirmPanel.SetActive(false);
    }

    private void Update()
    {
        if (confirmPanel != null && confirmPanel.activeSelf && Keyboard.current != null
            && Keyboard.current.escapeKey.wasPressedThisFrame)
            ClosePanel();
    }

    public void OpenPanel()  { if (confirmPanel != null) confirmPanel.SetActive(true); }
    public void ClosePanel() { if (confirmPanel != null) confirmPanel.SetActive(false); }

    public void QuitGame()
    {
        Debug.Log("QuitGame called!\n" + System.Environment.StackTrace);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}