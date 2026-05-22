using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Lightweight helper that forwards Unity UI drag events via C# events.
/// Attach to any UI element to receive begin/drag/end callbacks without
/// setting up EventTrigger components in the editor.
/// </summary>
[DisallowMultipleComponent]
[MovedFrom(true, null, null, "UIDragListener")]
public class UIDragListener : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public event Action<PointerEventData> BeginDragEvent;
    public event Action<PointerEventData> DragEvent;
    public event Action<PointerEventData> EndDragEvent;

    public void OnBeginDrag(PointerEventData eventData)
    {
        BeginDragEvent?.Invoke(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        DragEvent?.Invoke(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        EndDragEvent?.Invoke(eventData);
    }
}


}





