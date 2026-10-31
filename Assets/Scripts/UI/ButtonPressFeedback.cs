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
        float target = pressed ? 0.96f : (hovered && IsOn() ? 1.03f : 1f);
        float current = transform.localScale.x;
        if (Mathf.Abs(current - target) < 0.0005f) { if (current != target) transform.localScale = new Vector3(target, target, 1f); return; }
        // Ease out: quick at first, settling softly (about 0.15 s), instead of snapping.
        float s = Mathf.Lerp(current, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 16f));
        transform.localScale = new Vector3(s, s, 1f);
    }
}
