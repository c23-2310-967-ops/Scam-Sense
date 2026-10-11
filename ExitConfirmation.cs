using UnityEngine;
using UnityEngine.InputSystem;

public class ExitConfirmation : MonoBehaviour
{
    [SerializeField] private GameObject confirmPanel;

    private void Start()
    {
        confirmPanel.SetActive(false);
    }

    private void Update()
    {
        if (confirmPanel.activeSelf && Keyboard.current != null
            && Keyboard.current.escapeKey.wasPressedThisFrame)
            ClosePanel();
    }

    public void OpenPanel()  => confirmPanel.SetActive(true);
    public void ClosePanel() => confirmPanel.SetActive(false);

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