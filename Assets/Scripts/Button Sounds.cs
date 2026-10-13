using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonSounds : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler, ISelectHandler
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hoverSound;
    [SerializeField] private AudioClip clickSound;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hoverSound != null) audioSource.PlayOneShot(hoverSound);
    }

    public void OnSelect(BaseEventData eventData) // keyboard/controller navigation
    {
        if (hoverSound != null) audioSource.PlayOneShot(hoverSound);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (clickSound != null) audioSource.PlayOneShot(clickSound);
    }
}