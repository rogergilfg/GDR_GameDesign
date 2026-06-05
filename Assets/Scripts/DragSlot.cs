using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DragSlot : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
{

    [SerializeField] private Inventario inventario;
    [SerializeField] private int slotIndex;

    public static int draggedSlotIndex = -1;
    private static Image dragIcon;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        dragIcon = GameObject.Find("DragIcon").GetComponent<Image>();
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        Debug.Log("BeginDrag slot " + slotIndex);
        Sprite itemSprite = inventario.GetItemSprite(slotIndex);
        dragIcon.sprite = itemSprite;
        Debug.Log(dragIcon.sprite);
        dragIcon.enabled = true;
        draggedSlotIndex = slotIndex;
    }

    public void OnDrag(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToWorldPointInRectangle(
            dragIcon.rectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector3 worldPos
        );
        dragIcon.transform.position = worldPos;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Debug.Log("EndDrag slot " + slotIndex);
        GameObject target = eventData.pointerCurrentRaycast.gameObject;
        if (target != null && target.GetComponent<DragSlot>() != null)
        {
            int targetSlotIndex = target.GetComponent<DragSlot>().slotIndex;
            inventario.SwapItems(draggedSlotIndex, targetSlotIndex);
        }
        dragIcon.enabled = false;
    }
}
