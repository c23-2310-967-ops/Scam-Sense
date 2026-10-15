using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Makes &lt;link="id"&gt; parts of a TextMeshPro text clickable (mouse click or touch tap, through the EventSystem).
/// Used by Investigation Mode for inspectable words in a message. Nothing is ever opened - it only reports the id.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class InvestigationLinkText : MonoBehaviour, IPointerClickHandler
{
    public Action<string> clicked;
    TMP_Text text;

    void Awake()
    {
        text = GetComponent<TMP_Text>();
        text.raycastTarget = true;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (text == null || clicked == null) return;
        var canvas = text.canvas;
        // The camera that raycast this click (null for overlay canvases; Camera.main for world canvases without one).
        Camera cam = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null
                   : eventData.pressEventCamera != null ? eventData.pressEventCamera
                   : canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
        int index = TMP_TextUtilities.FindIntersectingLink(text, eventData.position, cam);
        if (index < 0 || index >= text.textInfo.linkCount) return;
        clicked(text.textInfo.linkInfo[index].GetLinkID());
    }
}
