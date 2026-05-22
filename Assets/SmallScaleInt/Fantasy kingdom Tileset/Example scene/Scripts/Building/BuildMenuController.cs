using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.Scripting.APIUpdating;
using SkillSystem;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using SmallScale.FantasyKingdomTileset.Balance;
using Input = SmallScale.FantasyKingdomTileset.InputAdapter;

namespace SmallScale.FantasyKingdomTileset.Building
{
/// <summary>
/// Manages the build menu, toggling it with the keyboard and populating the scroll view with build parts.
/// </summary>
[MovedFrom(true, null, null, "BuildMenuController")]
public sealed class BuildMenuController : MonoBehaviour
{
    [Serializable]
    private sealed class CategoryTab
    {
        [Tooltip("Category that this tab represents.")]
        public BuildPartCategory category = BuildPartCategory.Ground;

        [Tooltip("Button component used to select this category.")]
        public Button tabButton;

        [Tooltip("Whether this tab should be selected when the menu opens.")]
        public bool selectByDefault;
    }

    /// <summary>
    /// Event invoked when a build part is selected from the menu.
    /// </summary>
    [Serializable]
    public sealed class BuildPartSelectedEvent : UnityEvent<DestructibleTileData>
    {
    }

    [Header("Menu References")]
    [SerializeField]
    [Tooltip("Root GameObject for the build menu UI.")]
    private GameObject menuRoot;

    [SerializeField]
    [Tooltip("Scroll view content transform where part buttons will be placed.")]
    private Transform partButtonContainer;

    [SerializeField]
    [Tooltip("Prefab instantiated for each buildable part.")]
    private BuildPartButton partButtonPrefab;

    [SerializeField]
    [Tooltip("Panel used to display the resource cost of the hovered build part.")]
    private ResourceCostPanel resourceCostPanel;

    [SerializeField]
    [Tooltip("TMP label used to display the build info text (object named INFO).")]
    private TMP_Text buildInfoLabel;

    [SerializeField]
    [Tooltip("Tabs representing each build category.")]
    private CategoryTab[] categoryTabs = Array.Empty<CategoryTab>();

    [Header("Data")]
    [SerializeField]
    [Tooltip("Catalogue containing every buildable part.")]
    private BuildCatalogue buildCatalogue;

    [SerializeField]
    [Tooltip("Inventory panel that should be closed when the build menu opens.")]
    private InventoryUIController inventoryUI;

    [SerializeField]
    [Tooltip("Skill window that should be closed when the build menu opens.")]
    private SkillWindowController skillWindow;

    [SerializeField]
    [Tooltip("Spell book that should be closed when the build menu opens.")]
    private SkillSystem.SpellBookController spellBook;

    [Header("Events")]
    [SerializeField]
    [Tooltip("Raised whenever a part is selected by the player.")]
    private BuildPartSelectedEvent onPartSelected = new BuildPartSelectedEvent();

    private static readonly HashSet<BuildMenuController> OpenMenus = new HashSet<BuildMenuController>();
    private static readonly HashSet<object> ExternalMenus = new HashSet<object>();

    private readonly List<BuildPartButton> activeButtons = new List<BuildPartButton>();
    private readonly List<DestructibleTileData> filteredPartsBuffer = new List<DestructibleTileData>();

    private bool isOpen;
    private BuildPartCategory currentCategory = BuildPartCategory.Ground;
    private DestructibleTileData activeSelection;
    private DynamicResourceManager subscribedDynamicResourceManager;
    private bool isPopulatingButtons;
    private bool pendingUnlockRefresh;

    /// <summary>
    /// Gets the event raised when the player selects a part.
    /// </summary>
    public BuildPartSelectedEvent OnPartSelected => onPartSelected;

    /// <summary>
    /// Gets a value indicating whether any build menu is currently open.
    /// </summary>
    public static bool IsAnyMenuOpen => OpenMenus.Count > 0 || ExternalMenus.Count > 0;

    /// <summary>
    /// Gets a value indicating whether this menu instance is currently open.
    /// </summary>
    public bool IsOpen => isOpen;

    /// <summary>
    /// Registers an external menu so it participates in the shared "any menu open" state.
    /// </summary>
    /// <param name="owner">Owner instance representing the external menu.</param>
    public static void RegisterExternalMenu(object owner)
    {
        if (owner == null)
        {
            return;
        }

        ExternalMenus.Add(owner);
    }

    /// <summary>
    /// Removes an external menu from the shared "any menu open" state.
    /// </summary>
    /// <param name="owner">Owner instance representing the external menu.</param>
    public static void UnregisterExternalMenu(object owner)
    {
        if (owner == null)
        {
            return;
        }

        ExternalMenus.Remove(owner);
    }

    private void Awake()
    {
        if (buildCatalogue != null)
        {
            BuildUnlockService.RegisterDefinitions(buildCatalogue.EnumerateBuildableParts());
        }

        InitialiseTabs();
        if (!skillWindow)
        {
            skillWindow = FindFirstObjectByType<SkillWindowController>();
        }
        if (!spellBook)
        {
            spellBook = FindFirstObjectByType<SkillSystem.SpellBookController>();
        }
        HideMenuImmediate();
    }

    private void OnEnable()
    {
        RegisterTabListeners();
        BuildUnlockService.OnUnlocksChanged += HandleUnlocksChanged;
        EnsureDynamicResourceManagerSubscription();
    }

    private void OnDisable()
    {
        UnregisterTabListeners();
        BuildUnlockService.OnUnlocksChanged -= HandleUnlocksChanged;
        UnsubscribeFromDynamicResourceManager();
        HideMenuImmediate();
    }

    private void Update()
    {
        EnsureDynamicResourceManagerSubscription();

        if (WasToggleKeyPressed())
        {
            if (isOpen)
            {
                CloseMenu();
            }
            else
            {
                OpenMenu();
            }

            return;
        }

        if (!isOpen && activeSelection == null)
        {
            return;
        }

        if (WasCancelKeyPressed())
        {
            if (activeSelection != null)
            {
                DeselectActivePart();
            }
            else
            {
                CloseMenu();
            }
        }
    }

    private void InitialiseTabs()
    {
        if (categoryTabs == null)
        {
            categoryTabs = Array.Empty<CategoryTab>();
        }
    }

    private void RegisterTabListeners()
    {
        if (categoryTabs == null)
        {
            return;
        }

        foreach (CategoryTab tab in categoryTabs)
        {
            if (tab?.tabButton == null)
            {
                continue;
            }

            BuildPartCategory category = tab.category;
            tab.tabButton.onClick.AddListener(() => SelectCategory(category));
        }
    }

    private void UnregisterTabListeners()
    {
        if (categoryTabs == null)
        {
            return;
        }

        foreach (CategoryTab tab in categoryTabs)
        {
            if (tab?.tabButton == null)
            {
                continue;
            }

            tab.tabButton.onClick.RemoveAllListeners();
        }
    }

    public void OpenMenu()
    {
        if (isOpen)
        {
            return;
        }

        if (inventoryUI != null)
        {
            inventoryUI.SetInventoryVisible(false);
        }

        if (skillWindow != null && skillWindow.IsOpen)
        {
            skillWindow.CloseWindow();
        }

        if (spellBook != null && spellBook.IsOpen)
        {
            spellBook.CloseWindow();
        }

        SetOpenState(true);
        if (menuRoot != null)
        {
            menuRoot.SetActive(true);
        }

        BuildPartCategory defaultCategory = ResolveDefaultCategory();
        SelectCategory(defaultCategory);
    }

    public void CloseMenu()
    {
        if (!isOpen)
        {
            return;
        }

        DeselectActivePart();
        SetOpenState(false);
        if (menuRoot != null)
        {
            menuRoot.SetActive(false);
        }

        ClearButtons();
    }

    private void HideMenuImmediate()
    {
        SetOpenState(false);
        DeselectActivePart();
        if (menuRoot != null)
        {
            menuRoot.SetActive(false);
        }
        ClearButtons();
    }

    private void SelectCategory(BuildPartCategory category)
    {
        currentCategory = category;
        PopulateButtons(category);
        HighlightActiveTab(category);
    }

    private void SetOpenState(bool shouldBeOpen)
    {
        isOpen = shouldBeOpen;
        if (shouldBeOpen)
        {
            OpenMenus.Add(this);
        }
        else
        {
            OpenMenus.Remove(this);
        }
    }

    private void PopulateButtons(BuildPartCategory category)
    {
        if (isPopulatingButtons)
        {
            pendingUnlockRefresh = true;
            return;
        }

        isPopulatingButtons = true;
        pendingUnlockRefresh = false;
        bool shouldRepopulateAgain = false;

        try
        {
            ClearButtons();

            if (buildCatalogue == null || partButtonPrefab == null || partButtonContainer == null)
            {
                return;
            }

            IReadOnlyList<DestructibleTileData> parts = buildCatalogue.GetParts(category, filteredPartsBuffer);
            for (int i = 0; i < parts.Count; i++)
            {
                DestructibleTileData definition = parts[i];
                if (definition == null)
                {
                    continue;
                }

                BuildUnlockService.RegisterDefinition(definition);
                if (!BuildUnlockService.IsUnlocked(definition))
                {
                    continue;
                }

                BuildPartButton buttonInstance = Instantiate(partButtonPrefab, partButtonContainer);
                buttonInstance.Initialise(definition);
                buttonInstance.OnPartClicked.AddListener(HandlePartClicked);
                buttonInstance.OnPointerEntered.AddListener(HandlePartHovered);
                buttonInstance.OnPointerExited.AddListener(HandlePartUnhovered);
                activeButtons.Add(buttonInstance);
            }

            RefreshAllButtonStates();
        }
        finally
        {
            isPopulatingButtons = false;
            if (pendingUnlockRefresh && isOpen)
            {
                shouldRepopulateAgain = true;
            }
            pendingUnlockRefresh = false;
        }

        if (shouldRepopulateAgain)
        {
            PopulateButtons(currentCategory);
        }
    }

    private void ClearButtons()
    {
        for (int i = 0; i < activeButtons.Count; i++)
        {
            BuildPartButton button = activeButtons[i];
            if (button != null)
            {
                button.OnPartClicked.RemoveListener(HandlePartClicked);
                button.OnPointerEntered.RemoveListener(HandlePartHovered);
                button.OnPointerExited.RemoveListener(HandlePartUnhovered);
                Destroy(button.gameObject);
            }
        }

        activeButtons.Clear();

        if (resourceCostPanel != null)
        {
            resourceCostPanel.ClearCost();
        }

        UpdateInfoLabel(string.Empty);
    }

    private void HighlightActiveTab(BuildPartCategory category)
    {
        if (categoryTabs == null)
        {
            return;
        }

        foreach (CategoryTab tab in categoryTabs)
        {
            if (tab?.tabButton == null)
            {
                continue;
            }

            bool isActive = tab.category == category;
            tab.tabButton.interactable = !isActive;

            TMP_Text label = tab.tabButton.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.alpha = isActive ? 1f : 0.6f;
            }
        }
    }

    private BuildPartCategory ResolveDefaultCategory()
    {
        if (categoryTabs != null)
        {
            foreach (CategoryTab tab in categoryTabs)
            {
                if (tab != null && tab.selectByDefault)
                {
                    return tab.category;
                }
            }
        }

        return currentCategory;
    }

    private void HandlePartClicked(DestructibleTileData definition)
    {
        if (definition != null && !BuildUnlockService.IsUnlocked(definition))
        {
            return;
        }

        activeSelection = definition;
        onPartSelected.Invoke(definition);
    }

    private void HandlePartHovered(DestructibleTileData definition)
    {
        if (definition == null)
        {
            return;
        }

        if (resourceCostPanel != null)
        {
            GameBalanceManager balance = GameBalanceManager.Instance;
            ResourceSet costToDisplay = balance != null ? balance.GetAdjustedBuildCost(definition.ResourceCostSet) : definition.ResourceCostSet;

            resourceCostPanel.DisplayCost(costToDisplay, definition.InfoText);
        }

        UpdateInfoLabel(definition.BuildInfoText);
    }

    private void HandlePartUnhovered(DestructibleTileData definition)
    {
        if (resourceCostPanel != null)
        {
            resourceCostPanel.ClearCost();
        }

        UpdateInfoLabel(string.Empty);
    }

    private void DeselectActivePart()
    {
        if (activeSelection == null)
        {
            return;
        }

        activeSelection = null;
        onPartSelected.Invoke(null);
    }

    // Legacy resource manager removed

    private void EnsureDynamicResourceManagerSubscription()
    {
        DynamicResourceManager dyn = DynamicResourceManager.Instance;
        if (dyn == null)
        {
            if (subscribedDynamicResourceManager != null)
            {
                UnsubscribeFromDynamicResourceManager();
                RefreshAllButtonStates();
            }
            return;
        }

        if (dyn == subscribedDynamicResourceManager)
        {
            return;
        }

        SubscribeToDynamicResourceManager(dyn);
        RefreshAllButtonStates();
    }

    // Legacy resource manager removed

    private void SubscribeToDynamicResourceManager(DynamicResourceManager manager)
    {
        UnsubscribeFromDynamicResourceManager();
        subscribedDynamicResourceManager = manager;
        subscribedDynamicResourceManager.OnResourcesUpdated += HandleResourcesUpdatedDynamic;
        if (subscribedDynamicResourceManager != null)
        {
            BuildUnlockService.RefreshUnlocks(subscribedDynamicResourceManager.CurrentResources);
        }
    }

    // Legacy resource manager removed

    private void UnsubscribeFromDynamicResourceManager()
    {
        if (subscribedDynamicResourceManager == null)
        {
            return;
        }
        subscribedDynamicResourceManager.OnResourcesUpdated -= HandleResourcesUpdatedDynamic;
        subscribedDynamicResourceManager = null;
    }

    // Legacy resource manager removed

    private void HandleResourcesUpdatedDynamic(ResourceSet set)
    {
        BuildUnlockService.RefreshUnlocks(set);
        RefreshAllButtonStates();
    }

    private void HandleUnlocksChanged()
    {
        if (!isOpen || isPopulatingButtons)
        {
            pendingUnlockRefresh = true;
            return;
        }

        PopulateButtons(currentCategory);
    }

    private void RefreshAllButtonStates()
    {
        DynamicResourceManager dyn = subscribedDynamicResourceManager != null ? subscribedDynamicResourceManager : DynamicResourceManager.Instance;
        GameBalanceManager balance = GameBalanceManager.Instance;
        for (int i = 0; i < activeButtons.Count; i++)
        {
            BuildPartButton button = activeButtons[i];
            if (button == null)
            {
                continue;
            }

            DestructibleTileData definition = button.Definition;
            if (definition == null)
            {
                button.UpdateAvailability(false, false, string.Empty);
                continue;
            }

            bool unlocked = BuildUnlockService.IsUnlocked(definition);
            ResourceSet baseCost = definition.ResourceCostSet;
            ResourceSet effectiveCost = balance != null ? balance.GetAdjustedBuildCost(baseCost) : baseCost;

            bool affordable = dyn == null || effectiveCost == null || effectiveCost.IsEmpty || dyn.HasResources(effectiveCost);
            string hint = BuildUnlockService.GetUnlockHint(definition);
            button.UpdateAvailability(unlocked, affordable, hint);
        }
    }

    private bool WasToggleKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            return Keyboard.current[Key.B].wasPressedThisFrame;
        }
#endif
        return Input.GetKeyDown(KeyCode.B);
    }

    private static bool WasCancelKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current[Key.Escape].wasPressedThisFrame)
        {
            return true;
        }
#endif
        return Input.GetKeyDown(KeyCode.Escape);
    }

    private void UpdateInfoLabel(string infoText)
    {
        if (buildInfoLabel == null)
        {
            return;
        }

        bool hasContent = !string.IsNullOrWhiteSpace(infoText);
        buildInfoLabel.gameObject.SetActive(hasContent);
        buildInfoLabel.text = hasContent ? infoText : string.Empty;
    }
}
}







