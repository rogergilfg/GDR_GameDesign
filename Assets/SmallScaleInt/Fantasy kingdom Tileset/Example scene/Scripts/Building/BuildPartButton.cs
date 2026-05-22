using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Scripting.APIUpdating;
using SmallScale.FantasyKingdomTileset.Balance;

namespace SmallScale.FantasyKingdomTileset.Building
{
/// <summary>
/// UI helper that represents a single buildable part inside the build menu.
/// </summary>
[RequireComponent(typeof(Button))]
[MovedFrom(true, null, null, "BuildPartButton")]
public sealed class BuildPartButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    /// <summary>
    /// Unity event invoked when the button is clicked.
    /// </summary>
    [System.Serializable]
    public sealed class PartClickedEvent : UnityEvent<DestructibleTileData>
    {
    }

    [SerializeField]
    [Tooltip("Image component used to display the part icon.")]
    private Image iconImage;

    [SerializeField]
    [Tooltip("Optional label that displays the part name.")]
    private TMP_Text nameLabel;

    [SerializeField]
    [Tooltip("Optional label that displays the resource cost for the part.")]
    private TMP_Text costLabel;

    [SerializeField]
    [Tooltip("Icon tint applied when the part cannot currently be afforded.")]
    private Color unaffordableIconTint = new Color(1f, 0.5f, 0.5f, 1f);

    [SerializeField]
    [Tooltip("Event invoked whenever the button is pressed.")]
    private PartClickedEvent onPartClicked = new PartClickedEvent();

    [SerializeField]
    [Tooltip("Event invoked whenever the cursor begins hovering the button.")]
    private PartClickedEvent onPointerEntered = new PartClickedEvent();

    [SerializeField]
    [Tooltip("Event invoked whenever the cursor stops hovering the button.")]
    private PartClickedEvent onPointerExited = new PartClickedEvent();

    private Button button;
    private DestructibleTileData definition;
    private Color defaultIconColor = Color.white;
    private string defaultCostLabel = string.Empty;

    /// <summary>
    /// Gets the definition represented by this button.
    /// </summary>
    public DestructibleTileData Definition => definition;

    /// <summary>
    /// Gets the event raised when the button is clicked.
    /// </summary>
    public PartClickedEvent OnPartClicked => onPartClicked;

    /// <summary>
    /// Gets the event raised when the pointer enters this button.
    /// </summary>
    public PartClickedEvent OnPointerEntered => onPointerEntered;

    /// <summary>
    /// Gets the event raised when the pointer exits this button.
    /// </summary>
    public PartClickedEvent OnPointerExited => onPointerExited;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(HandleButtonClicked);

        if (iconImage != null)
        {
            defaultIconColor = iconImage.color;
        }
    }

    private void OnDestroy()
    {
        button.onClick.RemoveListener(HandleButtonClicked);
    }

    /// <summary>
    /// Configures the button using the provided definition.
    /// </summary>
    /// <param name="partDefinition">The buildable part information.</param>
    public void Initialise(DestructibleTileData partDefinition)
    {
        definition = partDefinition;

        if (iconImage != null)
        {
            iconImage.sprite = definition != null ? definition.Icon : null;
            iconImage.enabled = iconImage.sprite != null;
        }

        if (nameLabel != null)
        {
            nameLabel.text = definition != null ? definition.DisplayName : string.Empty;
        }

        RefreshDefaultCostLabel();
        if (costLabel != null)
        {
            costLabel.text = defaultCostLabel;
        }

        UpdateAvailability(true, true, string.Empty);
    }

    /// <summary>
    /// Updates the button state based on whether the part can currently be afforded.
    /// </summary>
    /// <param name="isAffordable">True when the player can afford to place the part.</param>
    public void SetAffordability(bool isAffordable)
    {
        UpdateAvailability(true, isAffordable, string.Empty);
    }

    /// <summary>
    /// Updates the button to reflect both unlock status and affordability.
    /// </summary>
    /// <param name="isUnlocked">True when the build part has been unlocked.</param>
    /// <param name="canAfford">True when the current resources cover the placement cost.</param>
    /// <param name="lockedHint">Hint to display when the part is locked.</param>
    public void UpdateAvailability(bool isUnlocked, bool canAfford, string lockedHint)
    {
        RefreshDefaultCostLabel();

        bool interactable = isUnlocked && canAfford;

        if (button != null)
        {
            button.interactable = interactable;
        }

        if (iconImage != null)
        {
            iconImage.color = interactable ? defaultIconColor : unaffordableIconTint;
        }

        if (costLabel != null)
        {
            if (!isUnlocked && !string.IsNullOrWhiteSpace(lockedHint))
            {
                costLabel.text = lockedHint;
            }
            else
            {
                costLabel.text = defaultCostLabel;
            }
        }
    }

    private void RefreshDefaultCostLabel()
    {
        ResourceSet costForDisplay = definition != null ? definition.ResourceCostSet : null;
        if (definition != null && GameBalanceManager.Instance != null)
        {
            costForDisplay = GameBalanceManager.Instance.GetAdjustedBuildCost(costForDisplay);
        }

        defaultCostLabel = BuildCostLabel(costForDisplay);
    }

    private void HandleButtonClicked()
    {
        if (definition != null)
        {
            onPartClicked.Invoke(definition);
        }
    }

    /// <inheritdoc />
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (definition != null)
        {
            onPointerEntered.Invoke(definition);
        }
    }

    /// <inheritdoc />
    public void OnPointerExit(PointerEventData eventData)
    {
        if (definition != null)
        {
            onPointerExited.Invoke(definition);
        }
    }

    private static string BuildCostLabel(ResourceSet set)
    {
        if (set == null || set.IsEmpty)
        {
            return string.Empty;
        }

        List<string> parts = new List<string>();
        var list = set.Amounts;
        for (int i = 0; i < list.Count; i++)
        {
            var a = list[i];
            if (a.type == null || a.amount <= 0) continue;
            parts.Add($"{a.amount} {a.type.DisplayName}");
        }

        return string.Join(", ", parts);
    }
}
}






