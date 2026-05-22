using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.UI;
using SmallScale.FantasyKingdomTileset.Building;
using Input = SmallScale.FantasyKingdomTileset.InputAdapter;

namespace SmallScale.FantasyKingdomTileset
{
/// <summary>
/// Controls the crafting menu UI. Populates the recipe list with craftable gear items,
/// displays their crafting costs, and performs crafting when requested.
/// </summary>
public sealed class CraftingMenuController : MonoBehaviour
{
    private static readonly HashSet<GearType> ArmorGearTypes = new HashSet<GearType>
    {
        GearType.Head,
        GearType.Chest,
        GearType.Legs,
        GearType.Shield
    };

    [Header("Root")]
    [SerializeField]
    private CanvasGroup menuRoot;

    [Header("Data Sources")]
    [SerializeField]
    private GearItemDatabase gearDatabase;

    [SerializeField]
    private ResourceDatabase resourceDatabase;

    [SerializeField]
    private PlayerInventory playerInventory;

    [SerializeField]
    private DynamicResourceManager resourceManager;

    [Header("Recipe List")]
    [SerializeField]
    private Transform recipeListRoot;

    [SerializeField]
    private CraftingRecipeButton recipeButtonPrefab;

    [SerializeField]
    private TMP_Text emptyStateLabel;

    [Header("Detail Panel")]
    [SerializeField]
    private GameObject detailPanelRoot;

    [SerializeField]
    private Image detailIcon;

    [SerializeField]
    private TMP_Text detailNameLabel;

    [SerializeField]
    private TMP_Text detailInfoLabel;

    [SerializeField]
    private Image detailResourceIconTemplate;

    [SerializeField]
    private TMP_Text detailResourceRequirementTemplate;

    [SerializeField]
    private TMP_Text detailUnavailableLabel;

    [Header("Actions")]
    [SerializeField]
    private Slider craftProgressSlider;

    [SerializeField]
    private Color craftSliderReadyColor = Color.white;

    [SerializeField]
    private Color craftSliderDisabledColor = new Color(1f, 1f, 1f, 0.35f);

    [SerializeField]
    [Tooltip("Seconds the player must hold the craft slider to craft the selected recipe.")]
    private float craftHoldDuration = 1.25f;

    [SerializeField]
    [Tooltip("Scale multiplier applied when crafting completes.")]
    private float craftCompletePulseScale = 1.15f;

    [SerializeField]
    [Tooltip("Duration of the completion pulse animation (seconds).")]
    private float craftCompletePulseDuration = 0.25f;

    [Header("Sorting")]
    [SerializeField]
    private SortButtonEntry[] sortButtons = System.Array.Empty<SortButtonEntry>();

    [Header("Type Filters (Blacksmith only)")]
    [SerializeField]
    private TypeFilterButtonEntry[] typeFilterButtons = System.Array.Empty<TypeFilterButtonEntry>();

    [SerializeField]
    private Button closeButton;

    private readonly List<CraftingRecipeEntry> recipeEntries = new List<CraftingRecipeEntry>();
    private readonly List<DetailCostEntry> detailCostEntries = new List<DetailCostEntry>();
    private readonly List<RecipeCandidate> recipeCandidates = new List<RecipeCandidate>();
    private CraftingRecipeEntry selectedRecipe;
    private CraftingStationType currentStation;
    private Vector3 stationWorldPosition;
    private bool isOpen;
    private bool resourcesSubscribed;
    private bool canCraftCurrentSelection;
    private bool isCraftHoldActive;
    private float craftHoldTimer;
    private Coroutine craftPulseRoutine;
    private Graphic craftSliderGraphic;
    private RectTransform craftSliderRect;
    private CraftingSortMode currentSortMode = CraftingSortMode.NameAsc;
    private CraftingTypeFilter currentTypeFilter = CraftingTypeFilter.All;

    private sealed class CraftingRecipeEntry
    {
        public GearItem Item;
        public ResourceTypeDef Resource;
        public int ResourceYield = 1;
        public ResourceSet Cost;
        public CraftingRecipeButton Button;

        public bool IsResourceRecipe => Resource != null && Item == null;
    }

    private struct DetailCostEntry
    {
        public ResourceTypeDef Type;
        public int Amount;
        public GameObject IconObject;
        public Image Icon;
        public GameObject LabelObject;
        public TMP_Text Label;
    }

    public bool IsOpen => isOpen;
    public CraftingStationType CurrentStation => currentStation;

    private sealed class RecipeCandidate
    {
        public GearItem Item;
        public ResourceTypeDef Resource;
        public int ResourceYield;
        public ResourceSet Cost;

        public bool IsResourceRecipe => Resource != null && Item == null;
    }

    [System.Serializable]
    private sealed class SortButtonEntry
    {
        public Button button;
        public CraftingSortMode mode = CraftingSortMode.NameAsc;
        [System.NonSerialized] public UnityAction cachedAction;
    }

    [System.Serializable]
    private sealed class TypeFilterButtonEntry
    {
        public Button button;
        public CraftingTypeFilter filter = CraftingTypeFilter.All;
        [System.NonSerialized] public UnityAction cachedAction;
    }

    private enum CraftingSortMode
    {
        NameAsc = 0,
        RarityDesc = 1
    }

    private enum CraftingTypeFilter
    {
        All = 0,
        Head = 1,
        Chest = 2,
        Legs = 3
    }

    private void Awake()
    {
        ConfigureCraftSlider();
        RegisterSortButtonCallbacks();
        RegisterTypeFilterButtonCallbacks();
        ConfigureSortButtonsForStation(currentStation);
        ConfigureTypeFilterButtonsForStation(currentStation);

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }

        EnsureMenuHidden();
        DisableDetailTemplates();
    }

    private void OnDestroy()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
        }

        if (craftPulseRoutine != null)
        {
            StopCoroutine(craftPulseRoutine);
            craftPulseRoutine = null;
        }

        UnsubscribeFromResourceUpdates();
        UnregisterSortButtonCallbacks();
        UnregisterTypeFilterButtonCallbacks();
    }

    private void Update()
    {
        if (!isOpen)
        {
            return;
        }
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current[UnityEngine.InputSystem.Key.Escape].wasPressedThisFrame)
        {
            Close();
            return;
        }
#endif
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
            return;
        }

        UpdateCraftHoldProgress(Time.deltaTime);
    }
    public void OpenForStation(CraftingStationType stationType, Vector3 worldPosition)
    {
        if (stationType == CraftingStationType.Trader)
        {
            Debug.LogWarning("[Crafting] Trader station requested via CraftingMenuController. Trading posts should be handled by the trading system instead.");
            return;
        }

        currentStation = stationType;
        stationWorldPosition = worldPosition;
        isOpen = true;

        EnsurePlayerInventoryReference();
        SubscribeToResourceUpdates();
        ConfigureSortButtonsForStation(stationType);
        ConfigureTypeFilterButtonsForStation(stationType);
        RefreshRecipeList();
        UpdateCraftButtonState();
        ResetCraftSliderProgress();
        BuildMenuController.RegisterExternalMenu(this);

        if (menuRoot != null)
        {
            menuRoot.alpha = 1f;
            menuRoot.interactable = true;
            menuRoot.blocksRaycasts = true;
        }
    }

    public void Close()
    {
        isOpen = false;
        currentStation = CraftingStationType.None;
        stationWorldPosition = Vector3.zero;
        ConfigureTypeFilterButtonsForStation(currentStation);

        if (menuRoot != null)
        {
            menuRoot.alpha = 0f;
            menuRoot.interactable = false;
            menuRoot.blocksRaycasts = false;
        }

        ClearRecipeList();
        ClearDetailCostEntries();
        selectedRecipe = null;
        UpdateDetailPanel();
        UnsubscribeFromResourceUpdates();
        BuildMenuController.UnregisterExternalMenu(this);
        if (craftPulseRoutine != null)
        {
            StopCoroutine(craftPulseRoutine);
            craftPulseRoutine = null;
        }
        ResetCraftSliderProgress();
    }

    private void RefreshRecipeList()
    {
        ClearRecipeList();

        bool hasResourceRecipes = TryGetCraftableResourceCategory(out CraftableResourceCategory resourceCategory, out bool resourceOnly);

        GearItemDatabase db = gearDatabase;
        IReadOnlyList<GearItem> items = null;
        if (resourceOnly)
        {
            db = null;
            items = null;
        }
        else if (db == null)
        {
            if (!hasResourceRecipes)
            {
                Debug.LogWarning("[Crafting] Gear database not assigned.");
                ShowEmptyStateAndReset("No gear database assigned.");
                return;
            }
        }
        else
        {
            items = db.Items;
            if ((items == null || items.Count == 0) && !hasResourceRecipes)
            {
                ShowEmptyStateAndReset("No gear items in database.");
                return;
            }
        }

        recipeCandidates.Clear();

        if (items != null)
        {
            for (int i = 0; i < items.Count; i++)
            {
                GearItem gearItem = items[i];
                if (gearItem == null || !gearItem.IsCraftable)
                {
                    continue;
                }

                if (!IsItemAllowedForCurrentStation(gearItem))
                {
                    continue;
                }

                ResourceSet cost = BuildCraftingCost(gearItem);
                recipeCandidates.Add(new RecipeCandidate
                {
                    Item = gearItem,
                    Cost = cost,
                    ResourceYield = 1
                });
            }
        }

        if (hasResourceRecipes)
        {
            AddResourceCandidatesForStation(resourceCategory);
        }

        if (recipeCandidates.Count == 0)
        {
            ShowEmptyStateAndReset(GetEmptyStateMessageForCurrentStation());
            return;
        }

        SortRecipeCandidates();
        HideEmptyState();

        DynamicResourceManager resources = EnsureResourceManagerReference();

        if (recipeButtonPrefab == null)
        {
            Debug.LogWarning("[Crafting] No recipe button prefab assigned.");
            ShowEmptyStateAndReset("Missing crafting item prefab.");
            return;
        }

        for (int i = 0; i < recipeCandidates.Count; i++)
        {
            RecipeCandidate candidate = recipeCandidates[i];
            if (!PassesCurrentFilter(candidate))
            {
                continue;
            }

            CraftingRecipeButton button = Instantiate(recipeButtonPrefab, recipeListRoot != null ? recipeListRoot : transform);
            if (button != null)
            {
                button.gameObject.SetActive(true);
                if (candidate.IsResourceRecipe)
                {
                    button.BindResource(candidate.Resource, candidate.ResourceYield, candidate.Cost, resources);
                }
                else
                {
                    button.Bind(candidate.Item, candidate.Cost, resources);
                }
                button.Clicked += HandleRecipeButtonClicked;
            }

            recipeEntries.Add(new CraftingRecipeEntry
            {
                Item = candidate.Item,
                Resource = candidate.Resource,
                ResourceYield = candidate.ResourceYield,
                Cost = candidate.Cost,
                Button = button
            });
        }

        if (recipeEntries.Count == 0)
        {
            ShowEmptyStateAndReset(GetEmptyStateMessageForCurrentStation());
            return;
        }

        HideEmptyState();
        SelectInitialRecipe();
        RefreshAllRecipeAffordability();
        UpdateDetailPanel();
    }

    private void SelectInitialRecipe()
    {
        DynamicResourceManager resources = EnsureResourceManagerReference();
        CraftingRecipeEntry affordableEntry = null;

        for (int i = 0; i < recipeEntries.Count; i++)
        {
            CraftingRecipeEntry entry = recipeEntries[i];
            if (resources != null && entry.Cost != null && resources.HasResources(entry.Cost))
            {
                affordableEntry = entry;
                break;
            }
        }

        if (affordableEntry == null && recipeEntries.Count > 0)
        {
            affordableEntry = recipeEntries[0];
        }

        SelectRecipe(affordableEntry);
    }

    private void SelectRecipe(CraftingRecipeEntry entry)
    {
        if (selectedRecipe == entry)
        {
            return;
        }

        if (selectedRecipe != null && selectedRecipe.Button != null)
        {
            selectedRecipe.Button.SetSelected(false);
        }

        selectedRecipe = entry;

        if (selectedRecipe != null && selectedRecipe.Button != null)
        {
            selectedRecipe.Button.SetSelected(true);
        }

        UpdateDetailPanel();
        UpdateCraftButtonState();
        ResetCraftSliderProgress();
    }

    private void UpdateDetailPanel()
    {
        bool hasSelection = selectedRecipe != null && (selectedRecipe.Item != null || selectedRecipe.Resource != null);

        if (detailPanelRoot != null)
        {
            detailPanelRoot.SetActive(hasSelection);
        }

        if (!hasSelection)
        {
            if (detailNameLabel != null)
            {
                detailNameLabel.text = string.Empty;
            }
            if (detailInfoLabel != null)
            {
                detailInfoLabel.text = string.Empty;
            }
            if (detailIcon != null)
            {
                detailIcon.enabled = false;
                detailIcon.sprite = null;
            }
            if (detailUnavailableLabel != null)
            {
                detailUnavailableLabel.gameObject.SetActive(false);
            }
            ClearDetailCostEntries();
            return;
        }

        GearItem item = selectedRecipe.Item;
        ResourceTypeDef resource = selectedRecipe.Resource;
        ResourceSet cost = selectedRecipe.Cost;

        if (detailNameLabel != null)
        {
            detailNameLabel.text = resource != null ? resource.DisplayName : item.DisplayName;
        }

        if (detailInfoLabel != null)
        {
            if (resource != null)
            {
                string info = string.IsNullOrWhiteSpace(resource.Note) ? $"Craft {resource.DisplayName}." : resource.Note;
                int yield = Mathf.Max(1, selectedRecipe.ResourceYield);
                info += $"\nProduces {yield} {(yield == 1 ? "unit" : "units")} per craft.";
                detailInfoLabel.text = info;
            }
            else
            {
                detailInfoLabel.text = item.Info;
            }
        }

        if (detailIcon != null)
        {
            Sprite icon = null;
            if (resource != null)
            {
                icon = resource.Icon;
            }
            else if (item != null)
            {
                icon = item.Icon;
            }

            detailIcon.sprite = icon;
            detailIcon.enabled = icon != null;
        }

        RebuildDetailCostEntries(cost);
        UpdateDetailCostDisplay();
    }

    private void RefreshAllRecipeAffordability()
    {
        DynamicResourceManager resources = EnsureResourceManagerReference();
        for (int i = 0; i < recipeEntries.Count; i++)
        {
            CraftingRecipeEntry entry = recipeEntries[i];
            if (entry.Button != null)
            {
                entry.Button.UpdateAffordability(resources);
            }
        }
    }

    private void UpdateDetailCostDisplay()
    {
        DynamicResourceManager resources = EnsureResourceManagerReference();
        bool hasAll = true;

        for (int i = 0; i < detailCostEntries.Count; i++)
        {
            DetailCostEntry entry = detailCostEntries[i];
            int available = resources != null && entry.Type != null ? resources.Get(entry.Type) : int.MaxValue;
            bool enough = available >= entry.Amount;
            hasAll &= enough;

            if (entry.Label != null)
            {
                entry.Label.text = FormatRequirementText(entry.Amount, entry.Type);
                entry.Label.color = enough ? Color.white : new Color(0.85f, 0.3f, 0.3f, 1f);
            }

            if (entry.Icon != null)
            {
                Color iconColor = entry.Icon.color;
                iconColor.a = enough ? 1f : 0.35f;
                entry.Icon.color = iconColor;
            }
        }

        if (detailUnavailableLabel != null)
        {
            detailUnavailableLabel.gameObject.SetActive(!hasAll);
        }
    }

    private void HandleRecipeButtonClicked(CraftingRecipeButton button)
    {
        for (int i = 0; i < recipeEntries.Count; i++)
        {
            if (recipeEntries[i].Button == button)
            {
                SelectRecipe(recipeEntries[i]);
                return;
            }
        }
    }

    private void HandleCraftButtonClicked()
    {
        TryExecuteCraft();
    }

    private void TryExecuteCraft()
    {
        if (selectedRecipe == null || selectedRecipe.Cost == null)
        {
            return;
        }

        bool isResourceRecipe = selectedRecipe.IsResourceRecipe;
        if (!isResourceRecipe && selectedRecipe.Item == null)
        {
            return;
        }

        DynamicResourceManager resources = EnsureResourceManagerReference();
        if (resources == null)
        {
            Debug.LogWarning("[Crafting] Cannot craft without a DynamicResourceManager.");
            return;
        }

        if (!resources.HasResources(selectedRecipe.Cost))
        {
            UpdateCraftButtonState();
            RefreshAllRecipeAffordability();
            UpdateDetailCostDisplay();
            return;
        }

        bool spent = resources.TrySpendResources(selectedRecipe.Cost, stationWorldPosition, showFeedback: true);
        if (!spent)
        {
            UpdateCraftButtonState();
            RefreshAllRecipeAffordability();
            UpdateDetailCostDisplay();
            return;
        }

        if (isResourceRecipe)
        {
            ResourceSet grant = new ResourceSet();
            grant.Set(selectedRecipe.Resource, Mathf.Max(1, selectedRecipe.ResourceYield));
            resources.GrantResources(grant, stationWorldPosition, showFeedback: true);
            Debug.Log($"[Crafting] Crafted {selectedRecipe.ResourceYield}x {selectedRecipe.Resource.DisplayName}.");
        }
        else
        {
            PlayerInventory inventory = EnsurePlayerInventoryReference();
            if (inventory == null)
            {
                resources.GrantResources(selectedRecipe.Cost, stationWorldPosition, showFeedback: false);
                Debug.LogWarning("[Crafting] Cannot craft gear without a PlayerInventory. Resources refunded.");
                return;
            }

            bool added = inventory.Add(selectedRecipe.Item);
            if (!added)
            {
                resources.GrantResources(selectedRecipe.Cost, stationWorldPosition, showFeedback: false);
                Debug.LogWarning($"[Crafting] Failed to add crafted item {selectedRecipe.Item.DisplayName} to inventory. Resources refunded.");
                return;
            }

            Debug.Log($"[Crafting] Crafted {selectedRecipe.Item.DisplayName}.");
        }

        craftHoldTimer = 0f;
        isCraftHoldActive = false;
        SetCraftSliderValue(1f);
        TriggerCraftCompleteFeedback();
        UpdateCraftButtonState();
        RefreshAllRecipeAffordability();
        UpdateDetailCostDisplay();
    }

    private void UpdateCraftButtonState()
    {
        bool canCraft = false;
        if (selectedRecipe != null && selectedRecipe.Cost != null)
        {
            DynamicResourceManager resources = EnsureResourceManagerReference();
            canCraft = resources != null && resources.HasResources(selectedRecipe.Cost);
        }

        SetCraftActionAvailability(canCraft);
    }

    private void ClearRecipeList()
    {
        for (int i = 0; i < recipeEntries.Count; i++)
        {
            CraftingRecipeEntry entry = recipeEntries[i];
            if (entry.Button != null)
            {
                entry.Button.Clicked -= HandleRecipeButtonClicked;
                DestroyUiObject(entry.Button.gameObject);
            }
        }

        recipeEntries.Clear();
        selectedRecipe = null;
    }

    private void RebuildDetailCostEntries(ResourceSet cost)
    {
        ClearDetailCostEntries();

        if (detailResourceIconTemplate != null)
        {
            detailResourceIconTemplate.gameObject.SetActive(false);
        }
        if (detailResourceRequirementTemplate != null)
        {
            detailResourceRequirementTemplate.gameObject.SetActive(false);
        }

        if (cost == null || cost.Amounts == null)
        {
            return;
        }

        for (int i = 0; i < cost.Amounts.Count; i++)
        {
            ResourceAmount amount = cost.Amounts[i];
            if (amount.type == null || amount.amount <= 0)
            {
                continue;
            }

            Image icon = InstantiateDetailIcon();
            TMP_Text label = InstantiateDetailLabel();

            if (icon != null)
            {
                icon.sprite = amount.type.Icon;
                icon.color = Color.white;
                icon.gameObject.SetActive(true);
            }

            if (label != null)
            {
                label.text = FormatRequirementText(amount.amount, amount.type);
                label.color = Color.white;
                label.gameObject.SetActive(true);
            }

            // Interleave icon and amount: label immediately after its icon
            if (icon != null)
            {
                icon.transform.SetAsLastSibling();
                if (label != null)
                {
                    int iconIndex = icon.transform.GetSiblingIndex();
                    label.transform.SetSiblingIndex(iconIndex + 1);
                }
            }
            detailCostEntries.Add(new DetailCostEntry
            {
                Type = amount.type,
                Amount = amount.amount,
                IconObject = icon != null ? icon.gameObject : null,
                Icon = icon,
                LabelObject = label != null ? label.gameObject : null,
                Label = label
            });
        }
    }

    private void ClearDetailCostEntries()
    {
        for (int i = 0; i < detailCostEntries.Count; i++)
        {
            DestroyUiObject(detailCostEntries[i].IconObject);
            DestroyUiObject(detailCostEntries[i].LabelObject);
        }
        detailCostEntries.Clear();
    }

    private Image InstantiateDetailIcon()
    {
        if (detailResourceIconTemplate == null)
        {
            return null;
        }
        Image clone = Instantiate(detailResourceIconTemplate, detailResourceIconTemplate.transform.parent);
        SetSiblingAfterTemplate(clone.transform, detailResourceIconTemplate.transform);
        return clone;
    }

    private TMP_Text InstantiateDetailLabel()
    {
        if (detailResourceRequirementTemplate == null)
        {
            return null;
        }
        TMP_Text clone = Instantiate(detailResourceRequirementTemplate, detailResourceRequirementTemplate.transform.parent);
        SetSiblingAfterTemplate(clone.transform, detailResourceRequirementTemplate.transform);
        return clone;
    }

    private void ShowEmptyState(string message)
    {
        if (emptyStateLabel != null)
        {
            emptyStateLabel.text = message;
            emptyStateLabel.gameObject.SetActive(true);
        }
    }

    private void ShowEmptyStateAndReset(string message)
    {
        ShowEmptyState(message);
        UpdateDetailPanel();
        UpdateCraftButtonState();
    }

    private void HideEmptyState()
    {
        if (emptyStateLabel != null)
        {
            emptyStateLabel.gameObject.SetActive(false);
        }
    }

    private void EnsureMenuHidden()
    {
        if (menuRoot != null)
        {
            menuRoot.alpha = 0f;
            menuRoot.interactable = false;
            menuRoot.blocksRaycasts = false;
        }
    }

    private void DisableDetailTemplates()
    {
        if (detailResourceIconTemplate != null)
        {
            detailResourceIconTemplate.gameObject.SetActive(false);
        }
        if (detailResourceRequirementTemplate != null)
        {
            detailResourceRequirementTemplate.gameObject.SetActive(false);
        }
        if (detailUnavailableLabel != null)
        {
            detailUnavailableLabel.gameObject.SetActive(false);
        }
    }

    private DynamicResourceManager EnsureResourceManagerReference()
    {
        if (resourceManager == null)
        {
            resourceManager = DynamicResourceManager.Instance;
            if (resourceManager == null)
            {
                Debug.LogWarning("[Crafting] DynamicResourceManager instance not found in scene.");
            }
        }
        return resourceManager;
    }

    private PlayerInventory EnsurePlayerInventoryReference()
    {
        if (playerInventory == null)
        {
            playerInventory = FindFirstObjectByType<PlayerInventory>();
            if (playerInventory == null)
            {
                Debug.LogWarning("[Crafting] PlayerInventory not found in scene.");
            }
        }
        return playerInventory;
    }

    private void SubscribeToResourceUpdates()
    {
        DynamicResourceManager manager = EnsureResourceManagerReference();
        if (manager != null && !resourcesSubscribed)
        {
            manager.OnResourcesUpdated += HandleResourcesUpdated;
            resourcesSubscribed = true;
        }
    }

    private void UnsubscribeFromResourceUpdates()
    {
        if (resourceManager != null && resourcesSubscribed)
        {
            resourceManager.OnResourcesUpdated -= HandleResourcesUpdated;
            resourcesSubscribed = false;
        }
    }

    private void HandleResourcesUpdated(ResourceSet _)
    {
        RefreshAllRecipeAffordability();
        UpdateCraftButtonState();
        UpdateDetailCostDisplay();
    }

    private static ResourceSet BuildCraftingCost(GearItem item)
    {
        ResourceSet cost = new ResourceSet();
        if (item == null)
        {
            return cost;
        }

        ResourceSet salvage = item.SalvageResourcesSet;
        if (salvage == null || salvage.Amounts == null)
        {
            return cost;
        }

        for (int i = 0; i < salvage.Amounts.Count; i++)
        {
            ResourceAmount amount = salvage.Amounts[i];
            if (amount.type == null || amount.amount <= 0)
            {
                continue;
            }

            long doubled = (long)amount.amount * 2L;
            int required = doubled > int.MaxValue ? int.MaxValue : (int)doubled;
            cost.Set(amount.type, required);
        }

        return cost;
    }

    private static string FormatRequirementText(int amount, ResourceTypeDef type)
    {
        return amount.ToString();
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

    void ConfigureCraftSlider()
    {
        if (craftProgressSlider == null)
        {
            return;
        }

        craftProgressSlider.interactable = false;
        craftProgressSlider.minValue = 0f;
        craftProgressSlider.maxValue = 1f;
        craftProgressSlider.value = 0f;
        craftSliderRect = craftProgressSlider.GetComponent<RectTransform>();
        if (craftProgressSlider.fillRect != null)
        {
            craftSliderGraphic = craftProgressSlider.fillRect.GetComponent<Graphic>();
        }
        if (craftSliderGraphic == null)
        {
            craftSliderGraphic = craftProgressSlider.targetGraphic;
        }

        RegisterCraftSliderEvents();
        UpdateCraftSliderVisual();
    }

    void RegisterCraftSliderEvents()
    {
        if (craftProgressSlider == null)
        {
            return;
        }

        EventTrigger trigger = craftProgressSlider.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = craftProgressSlider.gameObject.AddComponent<EventTrigger>();
        }

        if (trigger.triggers == null)
        {
            trigger.triggers = new List<EventTrigger.Entry>();
        }

        AddCraftSliderTrigger(trigger, EventTriggerType.PointerDown, OnCraftSliderPointerDown);
        AddCraftSliderTrigger(trigger, EventTriggerType.PointerUp, OnCraftSliderPointerUp);
        AddCraftSliderTrigger(trigger, EventTriggerType.PointerExit, OnCraftSliderPointerUp);
    }

    void AddCraftSliderTrigger(EventTrigger trigger, EventTriggerType type, UnityAction<BaseEventData> callback)
    {
        if (trigger == null || callback == null)
        {
            return;
        }

        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(callback);
        trigger.triggers.Add(entry);
    }

    void OnCraftSliderPointerDown(BaseEventData data)
    {
        if (!isOpen || !canCraftCurrentSelection)
        {
            return;
        }

        if (data is PointerEventData ped && ped.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        isCraftHoldActive = true;
        craftHoldTimer = 0f;
        SetCraftSliderValue(0f);
    }

    void OnCraftSliderPointerUp(BaseEventData data)
    {
        if (data is PointerEventData ped && ped.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        CancelCraftHold();
    }

    void UpdateCraftHoldProgress(float deltaTime)
    {
        if (!isCraftHoldActive)
        {
            return;
        }

        if (!canCraftCurrentSelection || selectedRecipe == null || selectedRecipe.Cost == null)
        {
            CancelCraftHold();
            return;
        }

        craftHoldTimer += Mathf.Max(0f, deltaTime);
        float duration = Mathf.Max(0.01f, craftHoldDuration);
        float normalized = Mathf.Clamp01(craftHoldTimer / duration);
        SetCraftSliderValue(normalized);

        if (normalized >= 1f)
        {
            isCraftHoldActive = false;
            TryExecuteCraft();
        }
    }

    void CancelCraftHold()
    {
        if (!isCraftHoldActive && craftHoldTimer <= 0f)
        {
            return;
        }

        ResetCraftSliderProgress();
    }

    void ResetCraftSliderProgress()
    {
        isCraftHoldActive = false;
        craftHoldTimer = 0f;
        SetCraftSliderValue(0f);
    }

    void SetCraftSliderValue(float value)
    {
        if (craftProgressSlider == null)
        {
            return;
        }

        craftProgressSlider.value = Mathf.Clamp01(value);
    }

    void SetCraftActionAvailability(bool isAvailable)
    {
        canCraftCurrentSelection = isAvailable;
        if (!isAvailable)
        {
            if (craftPulseRoutine == null)
            {
                CancelCraftHold();
            }
        }
        UpdateCraftSliderVisual();
    }

    void UpdateCraftSliderVisual()
    {
        if (craftProgressSlider == null)
        {
            return;
        }

        if (!canCraftCurrentSelection && craftPulseRoutine == null)
        {
            craftProgressSlider.value = 0f;
        }

        if (craftSliderGraphic != null)
        {
            craftSliderGraphic.color = canCraftCurrentSelection ? craftSliderReadyColor : craftSliderDisabledColor;
        }
    }

    void TriggerCraftCompleteFeedback()
    {
        if (craftProgressSlider == null)
        {
            return;
        }

        if (craftPulseRoutine != null)
        {
            StopCoroutine(craftPulseRoutine);
        }

        craftPulseRoutine = StartCoroutine(CraftCompletePulseRoutine());
    }

    IEnumerator CraftCompletePulseRoutine()
    {
        if (craftSliderRect == null && craftProgressSlider != null)
        {
            craftSliderRect = craftProgressSlider.GetComponent<RectTransform>();
        }

        var target = craftSliderRect;
        if (target == null)
        {
            yield break;
        }

        Vector3 originalScale = target.localScale;
        float duration = Mathf.Max(0.01f, craftCompletePulseDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float pulse = Mathf.Sin(normalized * Mathf.PI);
            float scale = Mathf.Lerp(1f, craftCompletePulseScale, pulse);
            target.localScale = originalScale * scale;

            if (craftSliderGraphic != null)
            {
                Color baseColor = canCraftCurrentSelection ? craftSliderReadyColor : craftSliderDisabledColor;
                float blink = Mathf.Lerp(1f, 0.5f, pulse);
                Color tinted = Color.Lerp(baseColor, Color.white, pulse);
                tinted.a *= blink;
                craftSliderGraphic.color = tinted;
            }

            yield return null;
        }

        target.localScale = originalScale;
        craftPulseRoutine = null;
        UpdateCraftSliderVisual();
        ResetCraftSliderProgress();
    }

    private bool IsItemAllowedForCurrentStation(GearItem gearItem)
    {
        if (gearItem == null)
        {
            return false;
        }

        switch (currentStation)
        {
            case CraftingStationType.Anvil:
                return gearItem.GearType == GearType.Weapon;
            case CraftingStationType.Blacksmith:
                return ArmorGearTypes.Contains(gearItem.GearType);
            default:
                return true;
        }
    }

    private void AddResourceCandidatesForStation(CraftableResourceCategory category)
    {
        ResourceDatabase db = EnsureResourceDatabaseReference();
        if (db == null)
        {
            Debug.LogWarning("[Crafting] Resource database not assigned.");
            return;
        }

        var defs = db.Resources;
        if (defs == null || defs.Count == 0)
        {
            return;
        }

        for (int i = 0; i < defs.Count; i++)
        {
            var def = defs[i];
            if (def == null || !def.IsCraftable || def.CraftCategory != category)
            {
                continue;
            }

            ResourceSet cost = CloneResourceSet(def.CraftingCost);
            recipeCandidates.Add(new RecipeCandidate
            {
                Resource = def,
                ResourceYield = def.CraftYield,
                Cost = cost
            });
        }
    }

    private bool TryGetCraftableResourceCategory(out CraftableResourceCategory category, out bool resourceOnly)
    {
        switch (currentStation)
        {
            case CraftingStationType.Furnace:
                category = CraftableResourceCategory.Metal;
                resourceOnly = true;
                return true;
            case CraftingStationType.Tannery:
                category = CraftableResourceCategory.Leather;
                resourceOnly = true;
                return true;
            case CraftingStationType.StoneCutter:
                category = CraftableResourceCategory.Stone;
                resourceOnly = true;
                return true;
            case CraftingStationType.AlchemyTable:
                category = CraftableResourceCategory.Wood;
                resourceOnly = true;
                return true;
            default:
                category = default;
                resourceOnly = false;
                return false;
        }
    }

    private string GetEmptyStateMessageForCurrentStation()
    {
        switch (currentStation)
        {
            case CraftingStationType.Furnace:
                return "No metal resources can be crafted yet.";
            case CraftingStationType.Tannery:
                return "No leather resources can be crafted yet.";
            case CraftingStationType.StoneCutter:
                return "No stone resources can be crafted yet.";
            case CraftingStationType.AlchemyTable:
                return "No wood resources can be crafted yet.";
            default:
                return "No craftable items available.";
        }
    }

    private ResourceDatabase EnsureResourceDatabaseReference()
    {
        if (resourceDatabase != null)
        {
            return resourceDatabase;
        }

        DynamicResourceManager manager = EnsureResourceManagerReference();
        if (manager != null && manager.Database != null)
        {
            resourceDatabase = manager.Database;
        }

        return resourceDatabase;
    }

    private static ResourceSet CloneResourceSet(ResourceSet source)
    {
        var clone = new ResourceSet();
        if (source == null || source.Amounts == null)
        {
            return clone;
        }

        var amounts = source.Amounts;
        for (int i = 0; i < amounts.Count; i++)
        {
            var amount = amounts[i];
            if (amount.type == null || amount.amount <= 0)
            {
                continue;
            }

            clone.Set(amount.type, amount.amount);
        }

        return clone;
    }

    private void RegisterSortButtonCallbacks()
    {
        if (sortButtons == null)
        {
            return;
        }

        for (int i = 0; i < sortButtons.Length; i++)
        {
            var entry = sortButtons[i];
            if (entry?.button == null) continue;
            if (entry.cachedAction == null)
            {
                CraftingSortMode modeLocal = entry.mode;
                entry.cachedAction = () => HandleSortButtonClicked(modeLocal);
            }
            entry.button.onClick.AddListener(entry.cachedAction);
        }
    }

    private void UnregisterSortButtonCallbacks()
    {
        if (sortButtons == null)
        {
            return;
        }

        for (int i = 0; i < sortButtons.Length; i++)
        {
            var entry = sortButtons[i];
            if (entry?.button == null || entry.cachedAction == null) continue;
            entry.button.onClick.RemoveListener(entry.cachedAction);
        }
    }

    private void RegisterTypeFilterButtonCallbacks()
    {
        if (typeFilterButtons == null)
        {
            return;
        }

        for (int i = 0; i < typeFilterButtons.Length; i++)
        {
            var entry = typeFilterButtons[i];
            if (entry?.button == null) continue;
            if (entry.cachedAction == null)
            {
                CraftingTypeFilter filterLocal = entry.filter;
                entry.cachedAction = () => HandleTypeFilterButtonClicked(filterLocal);
            }
            entry.button.onClick.AddListener(entry.cachedAction);
        }
    }

    private void UnregisterTypeFilterButtonCallbacks()
    {
        if (typeFilterButtons == null)
        {
            return;
        }

        for (int i = 0; i < typeFilterButtons.Length; i++)
        {
            var entry = typeFilterButtons[i];
            if (entry?.button == null || entry.cachedAction == null) continue;
            entry.button.onClick.RemoveListener(entry.cachedAction);
        }
    }

    private void ConfigureSortButtonsForStation(CraftingStationType stationType)
    {
        if (sortButtons == null || sortButtons.Length == 0)
        {
            currentSortMode = CraftingSortMode.NameAsc;
            return;
        }

        bool requireFallback = !IsSortModeAllowed(currentSortMode, stationType);
        bool hasActive = false;

        for (int i = 0; i < sortButtons.Length; i++)
        {
            var entry = sortButtons[i];
            if (entry?.button == null) continue;

            bool allowed = IsSortModeAllowed(entry.mode, stationType);
            entry.button.gameObject.SetActive(allowed);
            if (allowed)
            {
                hasActive = true;
            }

            if (!allowed && entry.mode == currentSortMode)
            {
                requireFallback = true;
            }
        }

        if (requireFallback || !hasActive)
        {
            currentSortMode = CraftingSortMode.NameAsc;
        }

        UpdateSortButtonVisuals();
    }

    private void HandleSortButtonClicked(CraftingSortMode mode)
    {
        if (!IsSortModeAllowed(mode, currentStation))
        {
            return;
        }

        if (currentSortMode == mode)
        {
            return;
        }

        currentSortMode = mode;
        UpdateSortButtonVisuals();

        if (isOpen)
        {
            RefreshRecipeList();
        }
    }

    private bool IsSortModeAllowed(CraftingSortMode mode, CraftingStationType stationType)
    {
        return mode == CraftingSortMode.NameAsc || mode == CraftingSortMode.RarityDesc;
    }

    private void ConfigureTypeFilterButtonsForStation(CraftingStationType stationType)
    {
        currentTypeFilter = CraftingTypeFilter.All;

        if (typeFilterButtons == null || typeFilterButtons.Length == 0)
        {
            return;
        }

        bool showFilters = stationType == CraftingStationType.Blacksmith;

        for (int i = 0; i < typeFilterButtons.Length; i++)
        {
            var entry = typeFilterButtons[i];
            if (entry?.button == null) continue;
            bool shouldShow = showFilters;
            entry.button.gameObject.SetActive(shouldShow);
        }

        UpdateTypeFilterButtonVisuals();
    }

    private void HandleTypeFilterButtonClicked(CraftingTypeFilter filter)
    {
        if (currentStation != CraftingStationType.Blacksmith)
        {
            return;
        }

        if (currentTypeFilter == filter)
        {
            return;
        }

        currentTypeFilter = filter;
        UpdateTypeFilterButtonVisuals();

        if (isOpen)
        {
            RefreshRecipeList();
        }
    }

    private void UpdateTypeFilterButtonVisuals()
    {
        if (typeFilterButtons == null)
        {
            return;
        }

        for (int i = 0; i < typeFilterButtons.Length; i++)
        {
            var entry = typeFilterButtons[i];
            if (entry?.button == null) continue;

            bool allowed = currentStation == CraftingStationType.Blacksmith;
            entry.button.gameObject.SetActive(allowed);
            if (!allowed)
            {
                continue;
            }

            bool isActive = entry.filter == currentTypeFilter;
            entry.button.interactable = !isActive;
        }
    }

    private void UpdateSortButtonVisuals()
    {
        if (sortButtons == null)
        {
            return;
        }

        for (int i = 0; i < sortButtons.Length; i++)
        {
            var entry = sortButtons[i];
            if (entry?.button == null) continue;
            bool isActiveMode = entry.mode == currentSortMode;
            bool interactable = entry.button.gameObject.activeSelf && !isActiveMode;
            entry.button.interactable = interactable;
        }
    }

    private void SortRecipeCandidates()
    {
        recipeCandidates.Sort((a, b) => CompareCandidates(a, b, currentSortMode));
    }

    private int CompareCandidates(RecipeCandidate a, RecipeCandidate b, CraftingSortMode mode)
    {
        switch (mode)
        {
            case CraftingSortMode.RarityDesc:
                {
                    int rarityA = GetCandidateRarityScore(a);
                    int rarityB = GetCandidateRarityScore(b);
                    int cmp = rarityB.CompareTo(rarityA);
                    if (cmp != 0) return cmp;
                    return string.Compare(GetCandidateDisplayName(a), GetCandidateDisplayName(b), System.StringComparison.OrdinalIgnoreCase);
                }
            case CraftingSortMode.NameAsc:
            default:
                return string.Compare(GetCandidateDisplayName(a), GetCandidateDisplayName(b), System.StringComparison.OrdinalIgnoreCase);
        }
    }

    private string GetCandidateDisplayName(RecipeCandidate candidate)
    {
        if (candidate.Item != null) return candidate.Item.DisplayName;
        if (candidate.Resource != null) return candidate.Resource.DisplayName;
        return string.Empty;
    }

    private int GetCandidateRarityScore(RecipeCandidate candidate)
    {
        if (candidate.Item != null)
        {
            return (int)candidate.Item.Rarity;
        }

        if (candidate.Resource != null)
        {
            return (int)candidate.Resource.Rarity;
        }

        return -1;
    }

    private bool PassesCurrentFilter(RecipeCandidate candidate)
    {
        if (currentStation != CraftingStationType.Blacksmith || currentTypeFilter == CraftingTypeFilter.All)
        {
            return true;
        }

        if (candidate.Item == null)
        {
            return false;
        }

        return currentTypeFilter switch
        {
            CraftingTypeFilter.Head => candidate.Item.GearType == GearType.Head,
            CraftingTypeFilter.Chest => candidate.Item.GearType == GearType.Chest,
            CraftingTypeFilter.Legs => candidate.Item.GearType == GearType.Legs,
            _ => true
        };
    }

    private bool CandidateMatchesGearType(RecipeCandidate candidate, GearType type)
    {
        return candidate.Item != null && candidate.Item.GearType == type;
    }
















}
}












