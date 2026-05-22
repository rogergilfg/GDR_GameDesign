using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using System;
using UnityEngine;
using SmallScale.FantasyKingdomTileset;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Reusable button component used to represent a single gear item inside the
/// player's inventory UI. The button displays the gear icon and exposes an
/// event that notifies listeners when the item is selected.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
[MovedFrom(true, null, null, "InventoryItemButton")]
public class InventoryItemButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField]
    [Tooltip("Image component that will display the gear icon.")]
    private Image iconImage;

    [SerializeField]
    [Tooltip("Background image that receives a tint based on the gear's rarity.")]
    private Image iconBackground;

    private Button button;
    private GearItem gearItem;
    private bool isResource;
    private ResourceTypeDef resourceType;
    private int resourceAmount;
    private TMP_Text countLabel;
    private Color iconBackgroundDefaultColor = Color.white;

    /// <summary>
    /// Raised whenever the button is clicked.
    /// </summary>
    public event Action<InventoryItemButton> Clicked;

    /// <summary>
    /// Raised whenever the pointer enters this button.
    /// </summary>
    public event Action<InventoryItemButton> PointerEntered;

    /// <summary>
    /// Raised whenever the pointer exits this button.
    /// </summary>
    public event Action<InventoryItemButton> PointerExited;

    /// <summary>
    /// Gear item associated with this button instance.
    /// </summary>
    public GearItem GearItem => gearItem;
    public bool IsResource => isResource;
    public ResourceTypeDef ResourceType => resourceType;
    public int ResourceAmount => resourceAmount;

    private void Awake()
    {
        button = GetComponent<Button>();

        if (iconBackground == null)
        {
            // Fall back to the image attached to the same game object when a
            // dedicated background reference has not been provided in the
            // inspector. This allows the button prefab to work out of the box
            // so long as it keeps the standard hierarchy of a background image
            // on the root and the gear icon on a child object.
            iconBackground = GetComponent<Image>();
        }

        if (iconBackground != null)
        {
            iconBackgroundDefaultColor = iconBackground.color;
        }

        if (button != null)
        {
            button.onClick.AddListener(HandleButtonClicked);
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleButtonClicked);
        }
    }

    /// <summary>
    /// Configures the button to represent the provided gear item.
    /// </summary>
    public void SetGearItem(GearItem item)
    {
        isResource = false;
        gearItem = item;

        if (button != null)
        {
            button.interactable = item != null;
        }

        if (iconImage == null)
        {
            return;
        }

        if (item != null && item.Icon != null)
        {
            iconImage.sprite = item.Icon;
            iconImage.enabled = true;
            iconImage.color = Color.white;
        }
        else
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

        if (iconBackground != null)
        {
            if (item != null)
            {
                Color rarityColor = item.RarityColor;
                rarityColor.a = iconBackgroundDefaultColor.a;
                iconBackground.color = rarityColor;
            }
            else
            {
                iconBackground.color = iconBackgroundDefaultColor;
            }
        }

        // Hide count label for gear
        if (countLabel != null)
        {
            countLabel.gameObject.SetActive(false);
        }
    }

    private void HandleButtonClicked()
    {
        Clicked?.Invoke(this);
    }

    // New: configure as resource entry with amount and icon
    public void SetResource(ResourceTypeDef type, int amount, Sprite overrideIcon = null)
    {
        isResource = true;
        gearItem = null;
        resourceType = type;
        resourceAmount = Mathf.Max(0, amount);

        if (button != null)
        {
            button.interactable = type != null && amount > 0;
        }

        if (iconImage != null)
        {
            Sprite sprite = overrideIcon != null ? overrideIcon : (type != null ? type.Icon : null);
            if (sprite != null)
            {
                iconImage.sprite = sprite;
                iconImage.enabled = true;
                iconImage.color = Color.white;
            }
            else
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }
        }

        if (iconBackground != null)
        {
            Color c = iconBackgroundDefaultColor; if (type != null) { c = type.RarityColor; }
            c.a = iconBackgroundDefaultColor.a;
            iconBackground.color = c;
        }

        EnsureCountLabel();
        if (countLabel != null)
        {
            countLabel.text = resourceAmount.ToString();
            countLabel.gameObject.SetActive(true);
        }
    }

    private void EnsureCountLabel()
    {
        if (countLabel != null)
        {
            return;
        }

        // Try find existing TMP child
        countLabel = GetComponentInChildren<TMP_Text>(includeInactive: true);
        if (countLabel != null && countLabel.gameObject.name.Contains("Count", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Create a new child for the count
        var go = new GameObject("Count", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-6f, 6f);
        rt.sizeDelta = new Vector2(0f, 0f);

        countLabel = go.AddComponent<TextMeshProUGUI>();
        countLabel.text = "0";
        countLabel.fontSize = 18f;
        countLabel.alignment = TextAlignmentOptions.BottomRight;
        countLabel.color = Color.white;
        countLabel.enableAutoSizing = true;
        countLabel.textWrappingMode = TextWrappingModes.NoWrap;
        countLabel.raycastTarget = false;
        countLabel.gameObject.SetActive(false);
    }

    /// <inheritdoc />
    public void OnPointerEnter(PointerEventData eventData)
    {
        PointerEntered?.Invoke(this);
    }

    /// <inheritdoc />
    public void OnPointerExit(PointerEventData eventData)
    {
        PointerExited?.Invoke(this);
    }
}




}







