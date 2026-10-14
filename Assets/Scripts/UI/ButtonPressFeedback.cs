using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hover grow + press "squish" so buttons feel responsive (unscaled time, works while paused).
/// Optional onHover callback (e.g. a hover sound).
/// (Moved out of UIKit.cs unchanged: a component must live in a file with its own name to be saved in scenes.)
/// </summary>
public class ButtonPressFeedback : MonoBehaviour,
    UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler,
    UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
{
    public System.Action onHover;
    bool hovered, pressed;

    public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e) { if (IsOn()) pressed = true; }
    public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e) => pressed = false;

    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e)
    {
        if (!IsOn()) return;
        hovered = true;
        onHover?.Invoke();
    }

    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { hovered = false; pressed = false; }

    bool IsOn()
    {
        var b = GetComponent<Selectable>();
        return b == null || b.IsInteractable();
    }

    void OnDisable()
    {
        hovered = pressed = false;
        transform.localScale = Vector3.one;
    }

    void Update()
    {
        float target = pressed ? 0.96f : (hovered && IsOn() ? 1.02f : 1f);
        float s = Mathf.MoveTowards(transform.localScale.x, target, Time.unscaledDeltaTime * 1.5f);
        transform.localScale = new Vector3(s, s, 1f);
    }
}
