using UnityEngine;

public class ResourceNode : MonoBehaviour
{
    [SerializeField] private Item item;
    [SerializeField] private Item requieredItem;
    [SerializeField] private int quantity;
    
    private Inventario inventario;
    private bool playerInRange;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        inventario = FindObjectOfType<Inventario>();
    }

    // Update is called once per frame
    void Update()
    {
        if(playerInRange && Input.GetKeyDown(KeyCode.E))
        {
            if(requieredItem != null && !inventario.HasItem(requieredItem))
            {
                Debug.Log("Necesitas " + requieredItem.itemName + " para recolectar este recurso.");
                return;
            }
            if (inventario.AddItem(item, quantity))
            {
                Destroy(gameObject);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            playerInRange = true;
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            playerInRange = false;
        }
    }
}
