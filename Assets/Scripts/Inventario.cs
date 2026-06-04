using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Inventario : MonoBehaviour
{
    [SerializeField] private InventorySlot[] inventorySlots;
    [SerializeField] private Image[] iconos;
    [SerializeField] private TMP_Text[] quantity;

    [System.Serializable]
    public class InventorySlot
    {
        public Item item;
        public int quantity;
    }

    void Start()
    {
        for (int i = 0; i < inventorySlots.Length; i++)
        {
            inventorySlots[i] = new InventorySlot();
        }
        UpdateUI();
    }

    public bool AddItem(Item item, int quantity)
    {
        for(int i = 0; i < inventorySlots.Length; i++)
        {
            if(inventorySlots[i].item == item && inventorySlots[i].quantity < item.maxStack)
            {
                int availableSpace = item.maxStack - inventorySlots[i].quantity;
                int quantityToAdd = Mathf.Min(availableSpace, quantity);
                inventorySlots[i].quantity += quantityToAdd;
                quantity -= quantityToAdd;
                UpdateUI();
                if (quantity <= 0) 
                return true;
            }
        }

        for(int i = 0;i < inventorySlots.Length; i++)
        {
            if(inventorySlots[i].item == null)
            {
                inventorySlots[i].item = item;
                inventorySlots[i].quantity = quantity;
                UpdateUI();
                return true;
            }
        }

        return false;
    }

    public void UpdateUI()
    {
               for(int i = 0; i < inventorySlots.Length; i++)
        {
            if(inventorySlots[i].item != null)
            {
                iconos[i].sprite = inventorySlots[i].item.objectImage;
                iconos[i].enabled = true;
                quantity[i].text = inventorySlots[i].quantity.ToString();
            }
            else
            {
                iconos[i].enabled = false;
                quantity[i].text = "";
            }
        }
    }
}
