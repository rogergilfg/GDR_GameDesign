using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using SmallScale.FantasyKingdomTileset;

/// <summary>
/// UI helper attached to the crafting recipe entry prefab. Displays a craftable gear item
/// together with the dynamic resource cost and exposes a click event.
/// </summary>
[DisallowMultipleComponent]
[MovedFrom(true, null, null, "CraftingRecipeButton")]
public sealed class CraftingRecipeButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI References")]
    [SerializeField]
    private Button button;

    [SerializeField]
    private Image iconImage;

    [SerializeField]
    private Image iconBackground;

    [SerializeField]
    private TMP_Text nameLabel;

    [SerializeField]
    private GameObject selectionHighlight;

    [Header("Cost Templates")]
    [SerializeField]
    private Image defaultResourceIcon;

    [SerializeField]
    private TMP_Text resourcesRequirement;

    [Header("Styling")]
    [SerializeField]
    private Color affordableNameColor = Color.white;

    [SerializeField]
    private Color insufficientNameColor = new Color(0.85f, 0.3f, 0.3f, 1f);

    [SerializeField, Range(0.1f, 1f)]
    private float unavailableIconAlpha = 0.35f;

    private readonly List<CostEntry> costEntries = new List<CostEntry>();
    private GearItem gearItem;
    private ResourceTypeDef resourceType;
    private int resourceYield = 1;
    private ResourceSet boundCost;
    private bool isSelected;
    private Color iconImageDefaultColor = Color.white;
    private Color iconBackgroundDefaultColor = Color.white;

    private struct CostEntry
    {
        public ResourceTypeDef Type;
        public int Amount;
        public GameObject IconObject;
        public Image Icon;
        public GameObject LabelObject;
        public TMP_Text Label;
    }

    public event Action<CraftingRecipeButton> Clicked;

    public GearItem GearItem => gearItem;
    public ResourceTypeDef ResourceType => resourceType;
    public bool IsResourceRecipe => resourceType != null;
    public ResourceSet Cost => boundCost;

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (iconImage != null)
        {
            iconImageDefaultColor = iconImage.color;
        }

        if (iconBackground != null)
        {
            iconBackgroundDefaultColor = iconBackground.color;
        }

        if (button != null)
        {
            button.onClick.AddListener(HandleButtonClicked);
        }

        DisableTemplates();
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleButtonClicked);
        }
    }

    private void OnDisable()
    {
        InventoryUIController.TryClearTooltip(this);
    }

    public void Bind(GearItem item, ResourceSet cost, DynamicResourceManager resourceManager)
    {
        gearItem = item;
        resourceType = null;
        resourceYield = 1;
        boundCost = cost;
        UpdateIconAndName();
        RebuildCostEntries();
        UpdateAffordability(resourceManager);
        SetSelected(isSelected);
    }

    public void BindResource(ResourceTypeDef resource, int yield, ResourceSet cost, DynamicResourceManager resourceManager)
    {
        gearItem = null;
        resourceType = resource;
        resourceYield = Mathf.Max(1, yield);
        boundCost = cost;
        UpdateIconAndName();
        RebuildCostEntries();
        UpdateAffordability(resourceManager);
        SetSelected(isSelected);
    }

    public void UpdateAffordability(DynamicResourceManager resourceManager)
    {
        bool hasAllResources = true;
        for (int i = 0; i < costEntries.Count; i++)
        {
            CostEntry entry = costEntries[i];
            int available = resourceManager != null && entry.Type != null
                ? resourceManager.Get(entry.Type)
                : int.MaxValue;
            bool enough = available >= entry.Amount;
            hasAllResources &= enough;

            if (entry.Label != null)
            {
                entry.Label.text = FormatRequirementText(entry.Amount, entry.Type);
                entry.Label.color = enough ? affordableNameColor : insufficientNameColor;
            }

            if (entry.Icon != null)
            {
                Color iconColor = entry.Icon.color;
                iconColor.a = enough ? 1f : unavailableIconAlpha;
                entry.Icon.color = iconColor;
            }
        }

        if (nameLabel != null)
        {
            nameLabel.color = hasAllResources ? affordableNameColor : insufficientNameColor;
        }

        if (iconImage != null)
        {
            Color iconColor = iconImage.color;
            iconColor.a = hasAllResources ? iconImageDefaultColor.a : iconImageDefaultColor.a * unavailableIconAlpha;
            iconImage.color = iconColor;
        }

        if (iconBackground != null)
        {
            Color backgroundColor = iconBackground.color;
            backgroundColor.a = hasAllResources ? iconBackgroundDefaultColor.a : iconBackgroundDefaultColor.a * unavailableIconAlpha;
            iconBackground.color = backgroundColor;
        }
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        if (selectionHighlight != null)
        {
            // Keep highlight object active to preserve layout height; fade via CanvasGroup.
            var cg = selectionHighlight.GetComponent<CanvasGroup>();
            if (cg == null)
            {
                cg = selectionHighlight.AddComponent<CanvasGroup>();
            }
            if (!selectionHighlight.activeSelf)
            {
                selectionHighlight.SetActive(true);
            }
            cg.alpha = selected ? 1f : 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;
        }
    }

    private void HandleButtonClicked()
    {
        Clicked?.Invoke(this);
    }

    /// <inheritdoc />
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (gearItem == null)
        {
            return;
        }

        if (gearItem != null)
        {
            InventoryUIController.TryRequestTooltip(gearItem, this, boundCost);
        }
    }

    /// <inheritdoc />
    public void OnPointerExit(PointerEventData eventData)
    {
        InventoryUIController.TryClearTooltip(this);
    }

    private void UpdateIconAndName()
    {
        if (iconImage != null)
        {
            Sprite sprite = null;
            if (gearItem != null && gearItem.Icon != null)
            {
                sprite = gearItem.Icon;
            }
            else if (resourceType != null && resourceType.Icon != null)
            {
                sprite = resourceType.Icon;
            }

            iconImage.sprite = sprite;
            iconImage.enabled = sprite != null;
            iconImage.color = iconImageDefaultColor;
        }

        if (iconBackground != null)
        {
            Color bgColor = iconBackgroundDefaultColor;
            if (gearItem != null)
            {
                bgColor = gearItem.RarityColor;
                bgColor.a = iconBackgroundDefaultColor.a;
            }
            else if (resourceType != null)
            {
                bgColor = resourceType.RarityColor;
                bgColor.a = iconBackgroundDefaultColor.a;
            }
            iconBackground.color = bgColor;
        }

        if (nameLabel != null)
        {
            if (gearItem != null)
            {
                nameLabel.text = gearItem.DisplayName;
            }
            else if (resourceType != null)
            {
                nameLabel.text = resourceType.DisplayName;
            }
            else
            {
                nameLabel.text = string.Empty;
            }
            nameLabel.color = affordableNameColor;
        }
    }

    private void RebuildCostEntries()
    {
        ClearCostEntries();

        if (boundCost == null || boundCost.Amounts == null)
        {
            return;
        }

        for (int i = 0; i < boundCost.Amounts.Count; i++)
        {
            ResourceAmount amount = boundCost.Amounts[i];
            if (amount.type == null || amount.amount <= 0)
            {
                continue;
            }

            Image iconInstance = InstantiateTemplateIcon();
            TMP_Text labelInstance = InstantiateTemplateLabel();

            if (iconInstance != null)
            {
                iconInstance.sprite = amount.type.Icon;
                iconInstance.color = iconImageDefaultColor;
                iconInstance.gameObject.SetActive(true);
            }

            if (labelInstance != null)
            {
                labelInstance.text = FormatRequirementText(amount.amount, amount.type);
                labelInstance.color = affordableNameColor;
                labelInstance.gameObject.SetActive(true);
            }

            // Interleave icon and amount: label immediately after its icon
            if (iconInstance != null)
            {
                iconInstance.transform.SetAsLastSibling();
                if (labelInstance != null)
                {
                    int iconIndex = iconInstance.transform.GetSiblingIndex();
                    labelInstance.transform.SetSiblingIndex(iconIndex + 1);
                }
            }
            costEntries.Add(new CostEntry
            {
                Type = amount.type,
                Amount = amount.amount,
                IconObject = iconInstance != null ? iconInstance.gameObject : null,
                Icon = iconInstance,
                LabelObject = labelInstance != null ? labelInstance.gameObject : null,
                Label = labelInstance
            });
        }
    }

    private Image InstantiateTemplateIcon()
    {
        if (defaultResourceIcon == null)
        {
            return null;
        }

        Image clone = Instantiate(defaultResourceIcon, defaultResourceIcon.transform.parent);
        SetSiblingAfterTemplate(clone.transform, defaultResourceIcon.transform);
        return clone;
    }

    private TMP_Text InstantiateTemplateLabel()
    {
        if (resourcesRequirement == null)
        {
            return null;
        }

        TMP_Text clone = Instantiate(resourcesRequirement, resourcesRequirement.transform.parent);
        SetSiblingAfterTemplate(clone.transform, resourcesRequirement.transform);
        return clone;
    }

    private static void SetSiblingAfterTemplate(Transform instance, Transform template)
    {
        if (instance == null || template == null)
        {
            return;
        }

        int templateIndex = template.GetSiblingIndex();
        instance.SetSiblingIndex(templateIndex + 1);
    }

    private static string FormatRequirementText(int amount, ResourceTypeDef type)
    {
        return amount.ToString();
    }

    private void ClearCostEntries()
    {
        for (int i = 0; i < costEntries.Count; i++)
        {
            DestroyUiObject(costEntries[i].IconObject);
            DestroyUiObject(costEntries[i].LabelObject);
        }
        costEntries.Clear();
    }

    private void DestroyUiObject(GameObject go)
    {
        if (go == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(go);
        }
        else
        {
            DestroyImmediate(go);
        }
    }

    private void DisableTemplates()
    {
        if (defaultResourceIcon != null)
        {
            defaultResourceIcon.gameObject.SetActive(false);
        }
        if (resourcesRequirement != null)
        {
            resourcesRequirement.gameObject.SetActive(false);
        }
    }
}








}










