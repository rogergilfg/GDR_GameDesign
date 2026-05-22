using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Simple component that raises events when the pointer enters or exits the
/// associated UI element. This is used to detect hovering over equipment slots
/// without requiring manual setup of event triggers in the Unity editor.
/// </summary>
[DisallowMultipleComponent]
[MovedFrom(true, null, null, "UIHoverListener")]
public class UIHoverListener : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    /// <summary>
    /// Invoked when the pointer enters this UI element.
    /// </summary>
    public event Action PointerEntered;

    /// <summary>
    /// Invoked when the pointer exits this UI element.
    /// </summary>
    public event Action PointerExited;

    /// <inheritdoc />
    public void OnPointerEnter(PointerEventData eventData)
    {
        PointerEntered?.Invoke();
    }

    /// <inheritdoc />
    public void OnPointerExit(PointerEventData eventData)
    {
        PointerExited?.Invoke();
    }
}


}





