using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Stops a text line from being squashed when its layout group runs out of room: its minimum height becomes the
/// height its text needs at the current width (TMP reports a minimum of 0, so a crowded vertical layout would
/// otherwise shrink lines onto each other). Only the height is affected; widths come from the text as usual.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class LayoutKeepHeight : UIBehaviour, ILayoutElement
{
    TMP_Text text;

    TMP_Text Text => text != null ? text : (text = GetComponent<TMP_Text>());

    public float minHeight
    {
        get
        {
            float w = ((RectTransform)transform).rect.width;
            return w > 0f ? Text.GetPreferredValues(Text.text, w, 0f).y : Text.preferredHeight;
        }
    }

    public float minWidth => -1f;
    public float preferredWidth => -1f;
    public float flexibleWidth => -1f;
    public float preferredHeight => -1f;
    public float flexibleHeight => -1f;
    public int layoutPriority => 2;   // above TMP's own layout element (priority 0) for minHeight only
    public void CalculateLayoutInputHorizontal() { }
    public void CalculateLayoutInputVertical() { }
}
