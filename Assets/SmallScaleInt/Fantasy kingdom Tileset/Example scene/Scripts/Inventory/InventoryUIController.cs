using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using SkillSystem;
using SmallScale.FantasyKingdomTileset.Building;
using Input = SmallScale.FantasyKingdomTileset.InputAdapter;

namespace SmallScale.FantasyKingdomTileset
{
/// <summary>
/// Controls the in-game inventory panel. The controller keeps the equipment
/// slots and inventory grid synchronized with the player's gear and allows
/// items to be equipped or unequipped via simple button clicks.
/// </summary>
[DisallowMultipleComponent]
public class InventoryUIController : MonoBehaviour
{
    /// <summary>
    /// Singleton-style accessor that exposes the active inventory UI controller.
    /// This is primarily used by world objects (e.g. loot pickups) that need to
    /// display the shared gear tooltip without holding an explicit reference in
    /// the scene.
    /// </summary>
    public static InventoryUIController Instance { get; private set; }

    [Serializable]
    private class GearSlotView
    {
        [SerializeField]
        private GearType gearType = GearType.Weapon;

        [SerializeField]
        private Image iconImage;

        [SerializeField]
        private Sprite emptySprite;

        [SerializeField]
        private Color emptyColor = new Color(1f, 1f, 1f, 0f);

        [SerializeField]
        private Color filledColor = Color.white;

        [SerializeField]
        private Button button;

        [SerializeField]
        [Tooltip("Image displayed behind the gear icon that is tinted based on rarity.")]
        private Image rarityIndicatorImage;

        private Color rarityIndicatorDefaultColor = Color.white;

        public GearType GearType => gearType;
        public Image IconImage => iconImage;
        public Sprite EmptySprite => emptySprite;
        public Color EmptyColor => emptyColor;
        public Color FilledColor => filledColor;
        public Button Button => button;
        public Image RarityIndicatorImage => rarityIndicatorImage;
        public Color RarityIndicatorDefaultColor => rarityIndicatorDefaultColor;

        public void CacheRarityIndicatorDefaults()
        {
            if (rarityIndicatorImage != null)
            {
                rarityIndicatorDefaultColor = rarityIndicatorImage.color;
            }
        }
    }

    public readonly struct TradeTooltipData
    {
        private readonly bool isActive;

        public TradeTooltipData(string headerText, string valueText, Sprite icon)
        {
            HeaderText = headerText ?? string.Empty;
            ValueText = valueText ?? string.Empty;
            Icon = icon;
            isActive = true;
        }

        public static TradeTooltipData None => default;

        public bool IsActive => isActive;
        public string HeaderText { get; }
        public string ValueText { get; }
        public Sprite Icon { get; }
    }

    [SerializeField]
    [Tooltip("Root game object of the inventory UI. Toggled on and off when the inventory is opened.")]
    private GameObject inventoryRoot;

    [SerializeField]
    [Tooltip("Player inventory that stores unequipped gear items.")]
    private PlayerInventory playerInventory;

    [SerializeField]
    [Tooltip("Manager responsible for equipping and unequipping player gear.")]
    private PlayerGearManager gearManager;

    [SerializeField]
    [Tooltip("Centralized stat tracker used to populate the stat summary labels.")]
    private PlayerStats playerStats;

    [SerializeField]
    [Tooltip("Button prefab used to represent gear items inside the inventory grid.")]
    private InventoryItemButton inventoryButtonPrefab;

    [SerializeField]
    [Tooltip("Parent transform containing the grid layout for inventory items.")]
    private Transform inventoryGridParent;

    [SerializeField]
    [Tooltip("Configuration for the equipment slot UI elements on the left side of the panel.")]
    private GearSlotView[] slotViews = Array.Empty<GearSlotView>();

    [SerializeField]
    [Tooltip("Build menu that should be hidden when the inventory opens.")]
    private BuildMenuController buildMenu;

    [SerializeField]
    [Tooltip("Skill window that should be hidden when the inventory opens.")]
    private SkillWindowController skillWindow;

    [SerializeField]
    [Tooltip("Spell book that should be hidden when the inventory opens.")]
    private SkillSystem.SpellBookController spellBook;

    [Header("Drag & Drop")]
    [SerializeField]
    [Tooltip("World loot prefab used when dragging an item out of the inventory onto the map.")]
    private LootPickup lootPickupPrefab;

    [SerializeField]
    [Tooltip("Optional UI root under which the drag icon is spawned. If null, uses the root canvas.")]
    private RectTransform dragIconRoot;

    [SerializeField]
    [Tooltip("Scale multiplier applied to the drag icon sprite.")]
    private float dragIconScale = 1f;

    [SerializeField]
    [Tooltip("When an item is dragged onto this button, it will be salvaged into resources.")]
    private Button salvageButton;

    [Header("Stat Summary")]
    [SerializeField]
    [Tooltip("Displays the player's total strength, including base stats and gear.")]
    private TMP_Text totalStrengthText;

    [SerializeField]
    [Tooltip("Displays the player's total defense, including base stats and gear.")]
    private TMP_Text totalDefenseText;

    [Header("Attribute Allocation")]
    [SerializeField]
    [Tooltip("Button used to spend attribute points on strength.")]
    private Button strengthAttributeButton;

    [SerializeField]
    [Tooltip("Button used to spend attribute points on defense.")]
    private Button defenseAttributeButton;

    [SerializeField]
    [Tooltip("Button used to spend attribute points on health.")]
    private Button healthAttributeButton;

    [SerializeField]
    [Tooltip("Button used to spend attribute points on intelligence.")]
    private Button intelligenceAttributeButton;

    [SerializeField]
    [Tooltip("Button used to spend attribute points on knowledge.")]
    private Button knowledgeAttributeButton;

    [SerializeField]
    [Tooltip("Optional label that shows how many attribute points remain.")]
    private TMP_Text unspentAttributePointsText;

    [SerializeField]
    [Tooltip("Displays the player's total health stat contribution.")]
    private TMP_Text totalHealthText;

    [SerializeField]
    [Tooltip("Displays the player's total intelligence stat contribution.")]
    private TMP_Text totalIntelligenceText;

    [SerializeField]
    [Tooltip("Displays the player's total knowledge stat contribution.")]
    private TMP_Text totalKnowledgeText;

    [SerializeField]
    [Tooltip("Keyboard key used to toggle the inventory panel.")]
    private KeyCode toggleKey = KeyCode.I;

    [SerializeField]
    [Tooltip("If enabled, the inventory UI will be hidden on start.")]
    private bool closeInventoryOnStart = true;

    [Header("Tooltip")]
    [SerializeField]
    [Tooltip("Root object of the tooltip panel that displays information about hovered items.")]
    private GameObject tooltipRoot;

    [SerializeField]
    [Tooltip("Image component used to display the hovered item's icon.")]
    private Image tooltipIcon;

    [SerializeField]
    [Tooltip("Text field that displays the hovered item's name.")]
    private TMP_Text tooltipNameText;

    [SerializeField]
    [Tooltip("Text field showing the hovered item's strength stat.")]
    private TMP_Text tooltipStrengthText;

    [SerializeField]
    [Tooltip("Text field showing the hovered item's defense stat.")]
    private TMP_Text tooltipDefenseText;

    [SerializeField]
    [Tooltip("Text field showing the hovered item's health stat.")]
    private TMP_Text tooltipHealthText;

    [SerializeField]
    [Tooltip("Text field showing the hovered item's knowledge stat.")]
    private TMP_Text tooltipKnowledgeText;

    [SerializeField]
    [Tooltip("Text field showing the hovered item's intelligence stat.")]
    private TMP_Text tooltipIntelligenceText;

    [SerializeField]
    [Tooltip("Text field showing the hovered item's damage stat.")]
    private TMP_Text tooltipDamageText;

    [SerializeField]
    [Tooltip("Label displayed next to the hovered item's damage stat.")]
    private TMP_Text tooltipDamageLabelText;

    [SerializeField]
    [Tooltip("Rich text block containing additional information about the hovered item.")]
    private TMP_Text tooltipInfoText;

    [SerializeField]
    [Tooltip("Label displaying the rarity tier of the hovered item.")]
    private TMP_Text tooltipRarityText;

    [SerializeField]
    [Tooltip("Background image that should be tinted to match the hovered item's rarity.")]
    private Image tooltipIconBackground;

    [SerializeField]
    [Tooltip("Graphic used for the tooltip panel background. A glowing outline will be applied to this element for rare items.")]
    private Graphic tooltipPanelGraphic;

    [SerializeField]
    [Tooltip("Thickness of the glowing outline that surrounds non-common item tooltips.")]
    private float tooltipGlowWidth = 6f;

    [SerializeField]
    [Tooltip("Speed multiplier for the pulsing glow animation.")]
    private float tooltipGlowPulseSpeed = 2f;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Lowest alpha multiplier applied during the glow pulse animation.")]
    private float tooltipGlowMinAlpha = 0.35f;

    [SerializeField]
    [Tooltip("Delay in seconds before the tooltip becomes visible when hovering an item.")]
    private float tooltipShowDelay = 0.15f;

    [SerializeField]
    [Tooltip("Offset applied to the tooltip position relative to the mouse cursor.")]
    private Vector2 tooltipMouseOffset = new Vector2(24f, -24f);

    [SerializeField]
    [Tooltip("Objects that should be hidden while showing a resource tooltip (gear-only UI panels).")]
    private List<GameObject> resourceTooltipHideObjects = new List<GameObject>();

    [SerializeField]
    [Tooltip("Optional flip animation played whenever the tooltip becomes visible.")]
    private CardFlipAnimator tooltipAnimator;

    [Header("Tooltip Salvage Resources")]
    [SerializeField]
    [Tooltip("Root object containing the salvage resource section within the tooltip.")]
    private GameObject tooltipSalvageRoot;

    [SerializeField]
    [Tooltip("Parent transform where salvage resource icon/amount pairs will be instantiated.")]
    private Transform tooltipSalvageContentRoot;

    [SerializeField]
    [Tooltip("Icon template used when listing salvage resources. This object will be duplicated per resource.")]
    private Image tooltipSalvageIconTemplate;

    [SerializeField]
    [Tooltip("Text template used when listing salvage resource amounts.")]
    private TMP_Text tooltipSalvageAmountTemplate;

    [Header("Tooltip Trade Info")]
    [SerializeField]
    [Tooltip("Root object containing the trade information section within the tooltip.")]
    private GameObject tooltipTradeInfoRoot;

    [SerializeField]
    [Tooltip("Icon that displays the currency or resource associated with the trade value.")]
    private Image tooltipTradeInfoIcon;

    [SerializeField]
    [Tooltip("Header text for the trade info section.")]
    private TMP_Text tooltipTradeInfoHeaderText;

    [SerializeField]
    [Tooltip("Value text describing the buy or sell price for the hovered entry.")]
    private TMP_Text tooltipTradeInfoValueText;

    [Header("Currency Panel")]
    [SerializeField]
    [Tooltip("Root object for the currency panel in the inventory UI.")]
    private GameObject currencyPanelRoot;

    [SerializeField]
    [Tooltip("Icon template used when listing player currencies.")]
    private Image currencyIconTemplate;

    [SerializeField]
    [Tooltip("Value template used when listing currency amounts.")]
    private TMP_Text currencyValueTemplate;

    private enum InventoryFilter
    {
        All,
        Weapons,
        Armor,
        Resources
    }

    [Header("Inventory Filters")]
    [SerializeField] private Button filterAllButton;
    [SerializeField] private Button filterWeaponsButton;
    [SerializeField] private Button filterArmorButton;
    [SerializeField] private Button filterResourcesButton;
    [SerializeField] private Color filterSelectedColor = new Color(0.85f, 0.85f, 0.85f);
    [SerializeField] private Color filterNormalColor = Color.white;

    private readonly List<InventoryItemButton> activeButtons = new List<InventoryItemButton>();
    private readonly Dictionary<GearType, GearSlotView> slotLookup = new Dictionary<GearType, GearSlotView>();
    private readonly Dictionary<Button, UnityAction> slotButtonHandlers = new Dictionary<Button, UnityAction>();

    private readonly Dictionary<UIHoverListener, Action> slotHoverEnterHandlers = new Dictionary<UIHoverListener, Action>();
    private readonly Dictionary<UIHoverListener, Action> slotHoverExitHandlers = new Dictionary<UIHoverListener, Action>();

    private readonly Dictionary<Transform, GearType> slotTransformToType = new Dictionary<Transform, GearType>();
    private readonly Dictionary<Button, UnityAction> attributeButtonHandlers = new Dictionary<Button, UnityAction>();
    private readonly List<Button> attributePointButtons = new List<Button>();
    private int lastKnownUnspentAttributePoints = -1;

    // Drag state
    private GearItem dragItem;
    private InventoryItemButton dragSourceButton;
    private bool dragFromSlot;
    private GearType dragSourceSlotType;
    private Image dragIconImage;
    private RectTransform dragIconRect;
    private ResourceTypeDef dragResourceType;
    private int dragResourceAmount;

    private bool hasSynchronizedWithEquipment;
    private DynamicResourceManager subscribedDynamicResourceManager;
    private GearItem tooltipCurrentItem;
    private object tooltipCurrentSource;
    private GearItem tooltipPendingItem;
    private object tooltipPendingSource;
    private Coroutine tooltipShowCoroutine;
    private ResourceTypeDef tooltipCurrentResource;
    private ResourceTypeDef tooltipPendingResource;
    private RectTransform tooltipRectTransform;
    private Color tooltipIconBackgroundDefaultColor = Color.white;
    private Color tooltipRarityDefaultColor = Color.white;
    private Outline tooltipGlowOutline;
    private Coroutine tooltipGlowCoroutine;
    private Color tooltipGlowBaseColor = Color.clear;

    private readonly List<GameObject> tooltipSalvageEntries = new List<GameObject>();
    private readonly List<GameObject> currencyEntries = new List<GameObject>();
    private Sprite currencyIconDefaultSprite;
    private Color currencyIconDefaultColor = Color.white;
    private string currencyValueDefaultText = string.Empty;
    private Sprite tooltipTradeInfoDefaultSprite;
    private Color tooltipTradeInfoIconDefaultColor = Color.white;
    private string tooltipTradeInfoDefaultHeaderText = string.Empty;
    private string tooltipTradeInfoDefaultValueText = string.Empty;
    private TradeTooltipData tooltipCurrentTradeInfo;
    private TradeTooltipData tooltipPendingTradeInfo;
    private ResourceSet tooltipCurrentOverrideSalvageSet;
    private ResourceSet tooltipPendingOverrideSalvageSet;

    private readonly Dictionary<Button, UnityAction> filterButtonHandlers = new Dictionary<Button, UnityAction>();
    private InventoryFilter activeFilter = InventoryFilter.All;

    private void ResetSingleton()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple InventoryUIController instances detected. The most recent instance will be used for gear tooltips.", this);
        }

        Instance = this;

        if (inventoryRoot == null)
        {
            inventoryRoot = gameObject;
        }

        if (!skillWindow)
        {
            skillWindow = FindFirstObjectByType<SkillWindowController>();
        }

        if (!spellBook)
        {
            spellBook = FindFirstObjectByType<SkillSystem.SpellBookController>();
        }

        EnsureTooltipSalvageTemplatesDisabled();
        ClearTooltipSalvageEntries();

        if (tooltipTradeInfoIcon != null)
        {
            tooltipTradeInfoDefaultSprite = tooltipTradeInfoIcon.sprite;
            tooltipTradeInfoIconDefaultColor = tooltipTradeInfoIcon.color;
        }

        if (tooltipTradeInfoHeaderText != null)
        {
            tooltipTradeInfoDefaultHeaderText = tooltipTradeInfoHeaderText.text;
        }

        if (tooltipTradeInfoValueText != null)
        {
            tooltipTradeInfoDefaultValueText = tooltipTradeInfoValueText.text;
        }

        ResetTradeInfoUI();

        if (currencyIconTemplate != null)
        {
            currencyIconDefaultSprite = currencyIconTemplate.sprite;
            currencyIconDefaultColor = currencyIconTemplate.color;
            currencyIconTemplate.gameObject.SetActive(false);
        }

        if (currencyValueTemplate != null)
        {
            currencyValueDefaultText = currencyValueTemplate.text;
            currencyValueTemplate.gameObject.SetActive(false);
        }

        if (currencyPanelRoot != null)
        {
            currencyPanelRoot.SetActive(false);
        }

        slotLookup.Clear();
        slotButtonHandlers.Clear();

        foreach (GearSlotView view in slotViews)
        {
            if (view == null)
            {
                continue;
            }

            view.CacheRarityIndicatorDefaults();

            if (!slotLookup.ContainsKey(view.GearType))
            {
                slotLookup.Add(view.GearType, view);
            }

            Button button = view.Button;
            if (button == null || slotButtonHandlers.ContainsKey(button))
            {
                continue;
            }

            GearType capturedType = view.GearType;
            UnityAction handler = () => HandleSlotClicked(capturedType);
            button.onClick.AddListener(handler);
            slotButtonHandlers.Add(button, handler);

            // Track transform ancestry to detect drops on this slot.
            Transform t = button.transform;
            if (!slotTransformToType.ContainsKey(t))
            {
                slotTransformToType.Add(t, capturedType);
            }

            // Install drag listener to allow dragging equipped items out or onto other targets.
            UIDragListener dragListener = button.GetComponent<UIDragListener>();
            if (dragListener == null)
            {
                dragListener = button.gameObject.AddComponent<UIDragListener>();
            }
            dragListener.BeginDragEvent += (ev) => HandleSlotBeginDrag(capturedType, ev);
            dragListener.DragEvent += HandleAnyDrag;
            dragListener.EndDragEvent += HandleAnyEndDrag;

            UIHoverListener hoverListener = button.GetComponent<UIHoverListener>();
            if (hoverListener == null)
            {
                hoverListener = button.gameObject.AddComponent<UIHoverListener>();
            }

            if (hoverListener != null)
            {
                Action enterHandler = () => HandleSlotPointerEntered(capturedType);
                Action exitHandler = () => HandleSlotPointerExited(capturedType);
                hoverListener.PointerEntered += enterHandler;
                hoverListener.PointerExited += exitHandler;
                slotHoverEnterHandlers[hoverListener] = enterHandler;
                slotHoverExitHandlers[hoverListener] = exitHandler;
            }
        }

        if (tooltipRoot != null)
        {
            tooltipRectTransform = tooltipRoot.GetComponent<RectTransform>();
        }

        if (tooltipAnimator == null && tooltipRoot != null)
        {
            tooltipAnimator = tooltipRoot.GetComponent<CardFlipAnimator>();
        }

        if (tooltipIconBackground != null)
        {
            tooltipIconBackgroundDefaultColor = tooltipIconBackground.color;
        }

        if (tooltipRarityText != null)
        {
            tooltipRarityDefaultColor = tooltipRarityText.color;
        }

        if (tooltipPanelGraphic != null)
        {
            tooltipGlowOutline = tooltipPanelGraphic.GetComponent<Outline>();
            if (tooltipGlowOutline == null)
            {
                tooltipGlowOutline = tooltipPanelGraphic.gameObject.AddComponent<Outline>();
            }

            tooltipGlowOutline.useGraphicAlpha = false;
            float initialWidth = Mathf.Abs(tooltipGlowWidth);
            tooltipGlowOutline.effectDistance = new Vector2(initialWidth, initialWidth);
            tooltipGlowOutline.effectColor = Color.clear;
            tooltipGlowOutline.enabled = false;
        }

        HideTooltip();
        InitializeAttributeButtons();
        RefreshAttributeUI();
    }

    private void Start()
    {
        if (closeInventoryOnStart && inventoryRoot != null)
        {
            inventoryRoot.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (playerInventory != null)
        {
            playerInventory.InventoryChanged += HandleInventoryChanged;
        }

        if (gearManager != null)
        {
            gearManager.GearChanged += HandleGearChanged;
        }

        if (playerStats != null)
        {
            playerStats.StatsChanged += HandleStatsChanged;
            playerStats.AttributePointsChanged += HandleAttributePointsChanged;
        }

        RegisterFilterButtons();
        SetInventoryFilter(activeFilter, true);

        EnsureDynamicResourceManagerSubscription();

        if (!hasSynchronizedWithEquipment)
        {
            SynchronizeInventoryWithEquipment();
            hasSynchronizedWithEquipment = true;
        }

        RefreshAll();
        RefreshAttributeUI();
    }

    private void OnDisable()
    {
        if (playerInventory != null)
        {
            playerInventory.InventoryChanged -= HandleInventoryChanged;
        }

        if (gearManager != null)
        {
            gearManager.GearChanged -= HandleGearChanged;
        }

        if (playerStats != null)
        {
            playerStats.StatsChanged -= HandleStatsChanged;
            playerStats.AttributePointsChanged -= HandleAttributePointsChanged;
        }

        UnregisterFilterButtons();
        UnsubscribeFromDynamicResourceManager();
        HideTooltip();
        ClearCurrencyEntries();
        if (currencyPanelRoot != null)
        {
            currencyPanelRoot.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        ResetSingleton();

        foreach (KeyValuePair<Button, UnityAction> pair in slotButtonHandlers)
        {
            if (pair.Key != null)
            {
                pair.Key.onClick.RemoveListener(pair.Value);
            }
        }

        slotButtonHandlers.Clear();

        foreach (KeyValuePair<Button, UnityAction> pair in attributeButtonHandlers)
        {
            if (pair.Key != null)
            {
                pair.Key.onClick.RemoveListener(pair.Value);
            }
        }

        attributeButtonHandlers.Clear();
        attributePointButtons.Clear();

        foreach (KeyValuePair<UIHoverListener, Action> pair in slotHoverEnterHandlers)
        {
            if (pair.Key != null)
            {
                pair.Key.PointerEntered -= pair.Value;
            }
        }

        foreach (KeyValuePair<UIHoverListener, Action> pair in slotHoverExitHandlers)
        {
            if (pair.Key != null)
            {
                pair.Key.PointerExited -= pair.Value;
            }
        }

        slotHoverEnterHandlers.Clear();
        slotHoverExitHandlers.Clear();
    }

    private void Update()
    {
        EnsureDynamicResourceManagerSubscription();
        if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
        {
            ToggleInventory();
        }

        if (IsInventoryVisible() && Input.GetKeyDown(KeyCode.Escape))
        {
            SetInventoryVisible(false);
        }

        UpdateTooltipPosition();
    }

    /// <summary>
    /// Toggles the visibility of the inventory UI.
    /// </summary>
    public void ToggleInventory()
    {
        if (inventoryRoot == null)
        {
            return;
        }

        bool targetState = !inventoryRoot.activeSelf;
        SetInventoryVisible(targetState);
    }

    /// <summary>
    /// Opens the inventory UI if it is not already visible.
    /// Intended for use by UI buttons.
    /// </summary>
    public void OpenInventory()
    {
        SetInventoryVisible(true);
    }

    /// <summary>
    /// Closes the inventory UI if it is currently visible.
    /// Intended for use by UI buttons.
    /// </summary>
    public void CloseInventory()
    {
        SetInventoryVisible(false);
    }

    /// <summary>
    /// Explicitly sets the visibility state of the inventory UI.
    /// </summary>
    public void SetInventoryVisible(bool visible)
    {
        if (inventoryRoot == null)
        {
            return;
        }

        bool wasActive = inventoryRoot.activeSelf;
        if (visible && buildMenu != null && buildMenu.IsOpen)
        {
            buildMenu.CloseMenu();
        }
        if (visible && skillWindow != null && skillWindow.IsOpen)
        {
            skillWindow.CloseWindow();
        }
        if (visible && spellBook != null && spellBook.IsOpen)
        {
            spellBook.CloseWindow();
        }

        if (wasActive == visible)
        {
            if (visible)
            {
                RefreshAll();
            }
            else
            {
                HideTooltip();
                ClearCurrencyEntries();
                if (currencyPanelRoot != null)
                {
                    currencyPanelRoot.SetActive(false);
                }
            }

            return;
        }

        inventoryRoot.SetActive(visible);

        if (visible)
        {
            RefreshAll();
        }
        else
        {
            HideTooltip();
            ClearCurrencyEntries();
            if (currencyPanelRoot != null)
            {
                currencyPanelRoot.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Returns true when the inventory root is active in the hierarchy.
    /// </summary>
    public bool IsInventoryVisible()
    {
        return inventoryRoot != null && inventoryRoot.activeSelf;
    }

    private void HandleInventoryChanged()
    {
        PopulateInventoryGrid();
    }

    private RectTransform GetDragIconRoot()
    {
        if (dragIconRoot != null)
        {
            return dragIconRoot;
        }

        Canvas c = GetComponentInParent<Canvas>();
        return c != null ? c.GetComponent<RectTransform>() : (RectTransform)transform;
    }

    private void HandleGearChanged(GearType gearType, GearItem newGear, GearItem previousGear)
    {
        UpdateSlotIcon(gearType);

        if (playerInventory == null)
        {
            return;
        }

        // Track whether we actually removed an instance of the newly equipped gear from the inventory.
        bool removedNewGear = false;
        if (newGear != null)
        {
            removedNewGear = playerInventory.Remove(newGear);
        }

        // Always return the previously equipped gear to the inventory. When the previous and new gear
        // reference the same asset we only add it back if we successfully removed one copy above, which
        // preserves duplicate counts without creating phantom items.
        if (previousGear != null && (previousGear != newGear || removedNewGear))
        {
            playerInventory.Add(previousGear);
        }
    }

    private void HandleSlotClicked(GearType gearType)
    {
        if (gearManager == null)
        {
            return;
        }

        GearItem equipped = gearManager.GetEquipped(gearType);
        if (equipped == null)
        {
            return;
        }

        gearManager.Unequip(gearType);
    }

    private void HandleInventoryButtonClicked(InventoryItemButton button)
    {
        if (button == null)
        {
            return;
        }

        if (button.IsResource)
        {
            // Resources are not equippable; click does nothing for now
            return;
        }

        GearItem item = button.GearItem;
        if (item == null || gearManager == null || playerInventory == null)
        {
            return;
        }

        if (!playerInventory.Contains(item))
        {
            return;
        }

        if (!gearManager.Equip(item))
        {
            Debug.LogWarning($"Unable to equip {item.DisplayName}: level requirement not met or slot unavailable.", item);
        }
    }

    private void HandleInventoryButtonPointerEntered(InventoryItemButton button)
    {
        if (button == null)
        {
            return;
        }

        if (button.IsResource)
        {
            ScheduleTooltip(button.ResourceType, button);
        }
        else
        {
            ScheduleTooltip(button.GearItem, button);
        }
    }

    private void HandleInventoryButtonPointerExited(InventoryItemButton button)
    {
        CancelTooltipForSource(button);
    }

    private void HandleSlotPointerEntered(GearType gearType)
    {
        if (gearManager == null)
        {
            return;
        }

        ScheduleTooltip(gearManager.GetEquipped(gearType), gearType);
    }

    private void HandleSlotPointerExited(GearType gearType)
    {
        CancelTooltipForSource(gearType);
    }

    private void CancelTooltipForSource(object source, bool force = false)
    {
        if (!force && source == null)
        {
            return;
        }

        bool matchesCurrent = Equals(tooltipCurrentSource, source);
        bool matchesPending = Equals(tooltipPendingSource, source);

        if (force || matchesCurrent || matchesPending)
        {
            HideTooltip();
        }
    }

    public static bool TryRequestTooltip(GearItem gearItem, object source)
    {
        if (Instance == null)
        {
            return false;
        }

        return Instance.ScheduleTooltip(gearItem, source, TradeTooltipData.None, null);
    }

    public static bool TryRequestTooltip(GearItem gearItem, object source, TradeTooltipData tradeInfo)
    {
        if (Instance == null)
        {
            return false;
        }

        return Instance.ScheduleTooltip(gearItem, source, tradeInfo, null);
    }

    public static bool TryRequestTooltip(GearItem gearItem, object source, ResourceSet salvageOverride)
    {
        if (Instance == null)
        {
            return false;
        }

        return Instance.ScheduleTooltip(gearItem, source, TradeTooltipData.None, salvageOverride);
    }

    public static bool TryRequestTooltip(GearItem gearItem, object source, ResourceSet salvageOverride, TradeTooltipData tradeInfo)
    {
        if (Instance == null)
        {
            return false;
        }

        return Instance.ScheduleTooltip(gearItem, source, tradeInfo, salvageOverride);
    }

    public static bool TryRequestResourceTooltip(ResourceTypeDef resource, object source)
    {
        if (Instance == null)
        {
            return false;
        }

        return Instance.ScheduleTooltip(resource, source);
    }

    public static bool TryRequestResourceTooltip(ResourceTypeDef resource, object source, TradeTooltipData tradeInfo)
    {
        if (Instance == null)
        {
            return false;
        }

        return Instance.ScheduleTooltip(resource, source, tradeInfo);
    }

    public static void TryClearTooltip(object source)
    {
        if (Instance == null)
        {
            return;
        }

        Instance.CancelTooltipForSource(source, source == null);
    }

    private bool ScheduleTooltip(GearItem gearItem, object source, TradeTooltipData tradeInfo = default, ResourceSet salvageOverride = null)
    {
        if (gearItem == null)
        {
            CancelTooltipForSource(source, true);
            return false;
        }

        bool matchesCurrent = ReferenceEquals(tooltipCurrentSource, source) &&
                              tooltipCurrentItem == gearItem &&
                              tradeInfo.Equals(tooltipCurrentTradeInfo) &&
                              ReferenceEquals(tooltipCurrentOverrideSalvageSet, salvageOverride);

        bool matchesPending = ReferenceEquals(tooltipPendingSource, source) &&
                              tooltipPendingItem == gearItem &&
                              tradeInfo.Equals(tooltipPendingTradeInfo) &&
                              ReferenceEquals(tooltipPendingOverrideSalvageSet, salvageOverride);

        if (matchesCurrent || matchesPending)
        {
            return true;
        }

        HideTooltip();

        tooltipPendingItem = gearItem;
        tooltipPendingSource = source;
        tooltipPendingTradeInfo = tradeInfo;
        tooltipPendingOverrideSalvageSet = salvageOverride;

        UpdateTooltipPosition();

        if (tooltipShowDelay <= 0f)
        {
            ShowTooltip(gearItem, source, tradeInfo, salvageOverride);
            return true;
        }

        if (tooltipShowCoroutine != null)

        {
            StopCoroutine(tooltipShowCoroutine);
        }

        tooltipShowCoroutine = StartCoroutine(ShowTooltipAfterDelay());
        return true;
    }


    private void HandleResourcesUpdated(ResourceSet set)
    {
        // Rebuild inventory grid to reflect new resource counts
        PopulateInventoryGrid();
        UpdateCurrencyPanel();
    }

    private bool ScheduleTooltip(ResourceTypeDef resource, object source, TradeTooltipData tradeInfo = default)
    {
        if (resource == null)
        {
            CancelTooltipForSource(source, true);
            return false;
        }

        // If same request is already current or pending, ignore
        if ((tooltipCurrentSource == source && tooltipCurrentResource == resource && tradeInfo.Equals(tooltipCurrentTradeInfo)) ||
            (tooltipPendingSource == source && tooltipPendingResource == resource && tradeInfo.Equals(tooltipPendingTradeInfo)))
        {
            return true;
        }

        HideTooltip();

        tooltipPendingResource = resource;
        tooltipPendingSource = source;
        tooltipPendingTradeInfo = tradeInfo;

        UpdateTooltipPosition();

        if (tooltipShowDelay <= 0f)
        {
            ShowTooltip(resource, source, tradeInfo);
            return true;
        }

        if (tooltipShowCoroutine != null)
        {
            StopCoroutine(tooltipShowCoroutine);
        }

        tooltipShowCoroutine = StartCoroutine(ShowTooltipAfterDelayResource());
        return true;
    }

    private void SetGearTooltipObjectsVisible(bool visible)
    {
        if (resourceTooltipHideObjects == null)
        {
            return;
        }

        for (int i = 0; i < resourceTooltipHideObjects.Count; i++)
        {
            GameObject obj = resourceTooltipHideObjects[i];
            if (obj == null)
            {
                continue;
            }

            if (obj.activeSelf != visible)
            {
                obj.SetActive(visible);
            }
        }

        if (!visible)
        {
            ClearTooltipSalvageEntries();
        }
    }

    private void RefreshAll()
    {
        UpdateAllSlotIcons();
        PopulateInventoryGrid();
        UpdateStatSummary();
        RefreshAttributeUI();
    }

    private void RegisterFilterButtons()
    {
        RegisterFilterButton(filterAllButton, InventoryFilter.All);
        RegisterFilterButton(filterWeaponsButton, InventoryFilter.Weapons);
        RegisterFilterButton(filterArmorButton, InventoryFilter.Armor);
        RegisterFilterButton(filterResourcesButton, InventoryFilter.Resources);
        UpdateFilterButtonStates();
    }

    private void RegisterFilterButton(Button button, InventoryFilter filter)
    {
        if (button == null || filterButtonHandlers.ContainsKey(button))
        {
            return;
        }

        UnityAction handler = () => SetInventoryFilter(filter);
        button.onClick.AddListener(handler);
        filterButtonHandlers.Add(button, handler);
    }

    private void UnregisterFilterButtons()
    {
        foreach (KeyValuePair<Button, UnityAction> kvp in filterButtonHandlers)
        {
            if (kvp.Key != null)
            {
                kvp.Key.onClick.RemoveListener(kvp.Value);
            }
        }

        filterButtonHandlers.Clear();
    }

    private void SetInventoryFilter(InventoryFilter filter, bool force = false)
    {
        if (!force && activeFilter == filter)
        {
            return;
        }

        activeFilter = filter;
        UpdateFilterButtonStates();
        PopulateInventoryGrid();
        UpdateCurrencyPanel();
    }

    private void UpdateFilterButtonStates()
    {
        UpdateFilterButtonVisual(filterAllButton, activeFilter == InventoryFilter.All);
        UpdateFilterButtonVisual(filterWeaponsButton, activeFilter == InventoryFilter.Weapons);
        UpdateFilterButtonVisual(filterArmorButton, activeFilter == InventoryFilter.Armor);
        UpdateFilterButtonVisual(filterResourcesButton, activeFilter == InventoryFilter.Resources);
    }

    private void UpdateFilterButtonVisual(Button button, bool selected)
    {
        if (button == null)
        {
            return;
        }

        if (button.TryGetComponent(out Image image))
        {
            image.color = selected ? filterSelectedColor : filterNormalColor;
        }
        else if (button.targetGraphic != null)
        {
            button.targetGraphic.color = selected ? filterSelectedColor : filterNormalColor;
        }

        button.interactable = !selected;
    }


    private bool ShouldDisplayGearItem(GearItem gearItem)
    {
        switch (activeFilter)
        {
            case InventoryFilter.Weapons:
                return gearItem.GearType == GearType.Weapon ||
                       gearItem.GearType == GearType.Shield;
            case InventoryFilter.Armor:
                return gearItem.GearType == GearType.Head ||
                       gearItem.GearType == GearType.Chest ||
                       gearItem.GearType == GearType.Legs;
            case InventoryFilter.Resources:
                return false;
            default:
                return true;
        }
    }

    private bool ShouldDisplayResourceStacks()
    {
        return activeFilter == InventoryFilter.All || activeFilter == InventoryFilter.Resources;
    }

    private void UpdateAllSlotIcons()
    {
        foreach (GearSlotView view in slotViews)
        {
            if (view == null)
            {
                continue;
            }

            UpdateSlotIcon(view.GearType);
        }
    }

    private void UpdateSlotIcon(GearType gearType)
    {
        if (!slotLookup.TryGetValue(gearType, out GearSlotView view))
        {
            return;
        }

        Image icon = view.IconImage;
        if (icon == null)
        {
            return;
        }

        GearItem equipped = gearManager != null ? gearManager.GetEquipped(gearType) : null;
        if (equipped != null && equipped.Icon != null)
        {
            icon.sprite = equipped.Icon;
            icon.color = view.FilledColor;
            icon.enabled = true;
        }
        else if (view.EmptySprite != null)
        {
            icon.sprite = view.EmptySprite;
            icon.color = view.EmptyColor;
            icon.enabled = true;
        }
        else
        {
            icon.sprite = null;
            icon.color = view.EmptyColor;
            icon.enabled = false;
        }

        Image rarityIndicator = view.RarityIndicatorImage;
        if (rarityIndicator == null)
        {
            return;
        }

        bool showIndicator = equipped != null && equipped.Rarity != GearRarity.Common;
        if (!showIndicator)
        {
            rarityIndicator.color = view.RarityIndicatorDefaultColor;
            if (rarityIndicator.gameObject.activeSelf)
            {
                rarityIndicator.gameObject.SetActive(false);
            }

            return;
        }

        Color rarityColor = equipped.RarityColor;
        Color defaultColor = view.RarityIndicatorDefaultColor;
        rarityColor.a = defaultColor.a;
        rarityIndicator.color = rarityColor;
        if (!rarityIndicator.gameObject.activeSelf)
        {
            rarityIndicator.gameObject.SetActive(true);
        }
        if (!rarityIndicator.enabled)
        {
            rarityIndicator.enabled = true;
        }
    }

    private void PopulateInventoryGrid()
    {
        // Clean up existing buttons
        foreach (InventoryItemButton button in activeButtons)
        {
            if (button != null)
            {
                button.Clicked -= HandleInventoryButtonClicked;
                button.PointerEntered -= HandleInventoryButtonPointerEntered;
                button.PointerExited -= HandleInventoryButtonPointerExited;
                UIDragListener oldDrag = button.GetComponent<UIDragListener>();
                if (oldDrag != null)
                {
                    oldDrag.BeginDragEvent -= (ev) => HandleInventoryItemBeginDrag(button, ev);
                    oldDrag.DragEvent -= HandleAnyDrag;
                    oldDrag.EndDragEvent -= HandleAnyEndDrag;
                }
                if (ReferenceEquals(tooltipCurrentSource, button))
                {
                    HideTooltip();
                }

                Destroy(button.gameObject);
            }
        }

        activeButtons.Clear();

        if (inventoryButtonPrefab == null || inventoryGridParent == null || playerInventory == null)
        {
            return;
        }

        // Gear items first
        IReadOnlyList<GearItem> items = playerInventory.Items;
        for (int i = 0; i < items.Count; i++)
        {
            GearItem gearItem = items[i];
            if (gearItem == null)
            {
                continue;
            }

            if (!ShouldDisplayGearItem(gearItem))
            {
                continue;
            }

            InventoryItemButton button = Instantiate(inventoryButtonPrefab, inventoryGridParent);
            button.SetGearItem(gearItem);
            button.Clicked += HandleInventoryButtonClicked;
            button.PointerEntered += HandleInventoryButtonPointerEntered;
            button.PointerExited += HandleInventoryButtonPointerExited;
            // Install drag listener for drag & drop from inventory
            UIDragListener dragListener = button.GetComponent<UIDragListener>();
            if (dragListener == null)
            {
                dragListener = button.gameObject.AddComponent<UIDragListener>();
            }
            dragListener.BeginDragEvent += (ev) => HandleInventoryItemBeginDrag(button, ev);
            dragListener.DragEvent += HandleAnyDrag;
            dragListener.EndDragEvent += HandleAnyEndDrag;
            activeButtons.Add(button);
        }

        // Append dynamic resources as entries at the end of the grid
        DynamicResourceManager dyn = DynamicResourceManager.Instance;
        if (dyn != null && ShouldDisplayResourceStacks())
        {
            ResourceSet current = dyn.CurrentResources;
            var list = current != null ? current.Amounts : null;
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var amt = list[i];
                    if (amt.type == null || amt.amount <= 0 || amt.type.IsCurrency) continue;

                    int remaining = amt.amount;
                    int maxStack = amt.type != null ? Mathf.Max(1, amt.type.MaxStackSize) : 100;
                    while (remaining > 0)
                    {
                        int stackAmount = Mathf.Min(remaining, maxStack);
                        InventoryItemButton button = Instantiate(inventoryButtonPrefab, inventoryGridParent);
                        button.SetResource(amt.type, stackAmount);
                        button.Clicked += HandleInventoryButtonClicked;      // no-op for resources
                        button.PointerEntered += HandleInventoryButtonPointerEntered; // ignore tooltip for resources
                        button.PointerExited += HandleInventoryButtonPointerExited;
                        UIDragListener dragListener = button.GetComponent<UIDragListener>();
                        if (dragListener == null)
                        {
                            dragListener = button.gameObject.AddComponent<UIDragListener>();
                        }
                        dragListener.BeginDragEvent += (ev) => HandleInventoryItemBeginDrag(button, ev);
                        dragListener.DragEvent += HandleAnyDrag;
                        dragListener.EndDragEvent += HandleAnyEndDrag;
                        activeButtons.Add(button);
                        remaining -= stackAmount;
                    }
                }
            }
        }

        if (tooltipCurrentItem != null && (playerInventory == null || !playerInventory.Contains(tooltipCurrentItem)))
        {
            HideTooltip();
        }

        if (tooltipCurrentResource != null && !ShouldDisplayResourceStacks())
        {
            HideTooltip();
        }

        UpdateCurrencyPanel();
    }

    private void HandleInventoryItemBeginDrag(InventoryItemButton button, PointerEventData eventData)
    {
        if (button == null)
        {
            return;
        }

        if (button.IsResource)
        {
            // Begin drag for resource stack
            dragItem = null;
            dragResourceType = button.ResourceType;
            dragResourceAmount = button.ResourceAmount;
            dragFromSlot = false;
            dragSourceButton = button;

            Button uiButton = button.GetComponent<Button>();
            if (uiButton != null)
            {
                uiButton.interactable = false;
            }

            BeginDragVisualForResource(dragResourceType);
            UpdateDragVisualPosition(eventData);
            return;
        }

        if (button.GearItem == null)
        {
            return;
        }

        dragItem = button.GearItem;
        dragFromSlot = false;
        dragSourceButton = button;

        Button itemBtn = button.GetComponent<Button>();
        if (itemBtn != null)
        {
            itemBtn.interactable = false;
        }

        BeginDragVisual(dragItem);
        UpdateDragVisualPosition(eventData);
    }

    private void HandleSlotBeginDrag(GearType gearType, PointerEventData eventData)
    {
        if (gearManager == null)
        {
            return;
        }

        GearItem equipped = gearManager.GetEquipped(gearType);
        if (equipped == null)
        {
            return;
        }

        dragItem = equipped;
        dragFromSlot = true;
        dragSourceSlotType = gearType;
        dragSourceButton = null;
        BeginDragVisual(dragItem);
        UpdateDragVisualPosition(eventData);
    }

    private void HandleAnyDrag(PointerEventData eventData)
    {
        // Update drag icon position for both gear and resource drags
        UpdateDragVisualPosition(eventData);
    }

    private void HandleAnyEndDrag(PointerEventData eventData)
    {
        if (dragItem == null && dragResourceType == null)
        {
            CleanupDragVisual();
            return;
        }

        bool handled = false;

        // Salvage takes priority if pointer is over the salvage button.
        if (!handled && IsPointerOverSalvageUI(eventData))
        {
            PerformSalvage(dragItem);
            handled = true;
        }

        if (!handled && dragItem != null)
        {
            handled = TryHandleDropOnSlot(eventData, dragItem);
        }
        if (!handled && dragFromSlot)
        {
            // If dragging from slot and the pointer is over inventory UI, unequip to inventory
            if (IsPointerOverInventoryUI(eventData))
            {
                if (gearManager != null)
                {
                    gearManager.Unequip(dragSourceSlotType);
                }
                handled = true;
            }
        }

        if (!handled)
        {
            // If dragging from inventory and still over inventory UI, cancel (no-op)
            if (!dragFromSlot && IsPointerOverInventoryUI(eventData))
            {
                // no action
            }
            else
            {
                // Drop to world
                Vector3 worldPoint = GetWorldPointFromScreen(eventData.position);
                if (dragItem != null)
                {
                    TryDropItemIntoWorld(dragItem, worldPoint);
                }
                else if (dragResourceType != null && dragResourceAmount > 0)
                {
                    TryDropResourceIntoWorld(dragResourceType, dragResourceAmount, worldPoint);
                }

                // If it came from a slot, ensure it's not left in inventory
                if (dragFromSlot && playerInventory != null)
                {
                    playerInventory.Remove(dragItem);
                }
            }
        }

        // Re-enable button if needed (only for gear items; resource buttons will be refreshed via PopulateInventoryGrid)
        if (dragSourceButton != null && dragItem != null)
        {
            Button uiButton = dragSourceButton.GetComponent<Button>();
            if (uiButton != null)
            {
                uiButton.interactable = true;
            }
        }

        dragItem = null;
        dragResourceType = null;
        dragResourceAmount = 0;
        dragSourceButton = null;
        dragFromSlot = false;
        CleanupDragVisual();
    }

    private bool IsPointerOverSalvageUI(PointerEventData eventData)
    {
        if (salvageButton == null || EventSystem.current == null || eventData == null)
        {
            return false;
        }

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        Transform salvageTransform = salvageButton.transform;
        for (int i = 0; i < results.Count; i++)
        {
            Transform t = results[i].gameObject.transform;
            if (t == null)
            {
                continue;
            }

            if (t == salvageTransform || t.IsChildOf(salvageTransform))
            {
                return true;
            }
        }

        return false;
    }

    private void PerformSalvage(GearItem item)
    {
        if (item == null)
        {
            return;
        }

        // If dragged from a slot, unequip first so visuals update. This will add the item back to inventory via GearChanged; remove it again below.
        if (dragFromSlot && gearManager != null)
        {
            gearManager.Unequip(dragSourceSlotType);
        }

        if (playerInventory != null)
        {
            playerInventory.Remove(item);
        }

        Vector3 worldPos = playerInventory != null ? playerInventory.transform.position : Vector3.zero;
        if (item.SalvageResourcesSet != null && !item.SalvageResourcesSet.IsEmpty && DynamicResourceManager.Instance != null)
        {
            DynamicResourceManager.Instance.GrantResources(item.SalvageResourcesSet, worldPos, showFeedback: true);
        }
    }

    private bool TryHandleDropOnSlot(PointerEventData eventData, GearItem item)
    {
        GearType targetSlot;
        if (TryFindSlotUnderPointer(eventData, out targetSlot))
        {
            if (gearManager != null && item != null)
            {
                gearManager.Equip(item);
                return true;
            }
        }

        return false;
    }

    private bool TryFindSlotUnderPointer(PointerEventData eventData, out GearType gearType)
    {
        gearType = default;
        if (EventSystem.current == null || eventData == null)
        {
            return false;
        }

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        for (int i = 0; i < results.Count; i++)
        {
            Transform t = results[i].gameObject.transform;
            while (t != null)
            {
                if (slotTransformToType.TryGetValue(t, out GearType type))
                {
                    gearType = type;
                    return true;
                }
                t = t.parent;
            }
        }

        return false;
    }

    private bool IsPointerOverInventoryUI(PointerEventData eventData)
    {
        if (inventoryRoot == null || EventSystem.current == null || eventData == null)
        {
            return false;
        }

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        for (int i = 0; i < results.Count; i++)
        {
            if (results[i].gameObject == null)
            {
                continue;
            }

            Transform t = results[i].gameObject.transform;
            if (t == null)
            {
                continue;
            }

            if (t == inventoryRoot.transform || t.IsChildOf(inventoryRoot.transform))
            {
                return true;
            }
        }

        return false;
    }

    private void BeginDragVisual(GearItem item)
    {
        if (item == null)
        {
            return;
        }

        if (dragIconImage == null)
        {
            GameObject go = new GameObject("Inventory Drag Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dragIconRect = go.GetComponent<RectTransform>();
            dragIconImage = go.GetComponent<Image>();
            dragIconImage.raycastTarget = false;
        }

        RectTransform root = GetDragIconRoot();
        if (dragIconRect.transform.parent != root)
        {
            dragIconRect.SetParent(root, worldPositionStays: false);
        }

        dragIconImage.enabled = true;
        dragIconImage.sprite = item.Icon;
        dragIconImage.color = Color.white;
        float scale = Mathf.Max(0.1f, dragIconScale);
        dragIconRect.localScale = new Vector3(scale, scale, 1f);
        dragIconRect.SetAsLastSibling();
    }

    private void BeginDragVisualForResource(ResourceTypeDef type)
    {
        if (type == null)
        {
            return;
        }

        if (dragIconImage == null)
        {
            GameObject go = new GameObject("Inventory Drag Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dragIconRect = go.GetComponent<RectTransform>();
            dragIconImage = go.GetComponent<Image>();
            dragIconImage.raycastTarget = false;
        }

        RectTransform root = GetDragIconRoot();
        if (dragIconRect.transform.parent != root)
        {
            dragIconRect.SetParent(root, worldPositionStays: false);
        }

        dragIconImage.enabled = true;
        dragIconImage.sprite = type.Icon;
        dragIconImage.color = Color.white;
        float scale = Mathf.Max(0.1f, dragIconScale);
        dragIconRect.localScale = new Vector3(scale, scale, 1f);
        dragIconRect.SetAsLastSibling();
    }

    private void UpdateDragVisualPosition(PointerEventData eventData)
    {
        if (dragIconRect == null)
        {
            return;
        }

        dragIconRect.position = eventData != null ? (Vector3)eventData.position : Input.mousePosition;
    }

    private void CleanupDragVisual()
    {
        if (dragIconImage != null)
        {
            dragIconImage.enabled = false;
        }
    }

    private Vector3 GetWorldPointFromScreen(Vector2 screenPosition)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            return Vector3.zero;
        }

        Vector3 pos = new Vector3(screenPosition.x, screenPosition.y, 0f);
        if (cam.orthographic)
        {
            Vector3 world = cam.ScreenToWorldPoint(pos);
            world.z = playerInventory != null ? playerInventory.transform.position.z : 0f;
            return world;
        }
        else
        {
            // Project onto plane at player's Z or Z=0 as a fallback
            float z = playerInventory != null ? playerInventory.transform.position.z : 0f;
            Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, z));
            Ray ray = cam.ScreenPointToRay(screenPosition);
            if (plane.Raycast(ray, out float enter))
            {
                return ray.GetPoint(enter);
            }

            return Vector3.zero;
        }
    }

    private void TryDropItemIntoWorld(GearItem item, Vector3 worldPoint)
    {
        if (item == null)
        {
            return;
        }

        if (playerInventory == null || lootPickupPrefab == null)
        {
            Debug.LogWarning("Cannot drop item into world: missing PlayerInventory or LootPickup prefab.", this);
            return;
        }

        // Remove from inventory if present
        playerInventory.Remove(item);

        Vector3 spawn = worldPoint;
        LootPickup pickup = Instantiate(lootPickupPrefab, spawn, Quaternion.identity);
        pickup.Initialize(item, playerInventory, spawn, playerInventory.transform);
    }

    private void TryDropResourceIntoWorld(ResourceTypeDef type, int amount, Vector3 worldPoint)
    {
        if (type == null || amount <= 0)
        {
            return;
        }

        if (lootPickupPrefab == null)
        {
            Debug.LogWarning("Cannot drop resource into world: missing LootPickup prefab.", this);
            return;
        }

        // Spend resources immediately so the UI reflects the new amount
        if (DynamicResourceManager.Instance != null)
        {
            var spend = new ResourceSet();
            spend.Set(type, amount);
            Vector3 pos = playerInventory != null ? playerInventory.transform.position : worldPoint;
            if (!DynamicResourceManager.Instance.TrySpendResources(spend, pos, showFeedback: false))
            {
                return; // Not enough resources or spend failed; do not spawn pickup
            }
        }

        Vector3 spawn = worldPoint;
        LootPickup pickup = Instantiate(lootPickupPrefab, spawn, Quaternion.identity);
        Transform playerXform = playerInventory != null ? playerInventory.transform : null;
        pickup.InitializeResource(type, amount, playerXform);

        // Immediately refresh inventory UI so the dropped stack disappears
        PopulateInventoryGrid();
    }

    private void HandleStatsChanged(PlayerStats.StatSnapshot snapshot)
    {
        UpdateStatSummary(snapshot);
    }

    private void UpdateStatSummary()
    {
        if (playerStats != null)
        {
            UpdateStatSummary(playerStats.CurrentStats);
        }
        else
        {
            UpdateStatSummary(default(PlayerStats.StatSnapshot));
        }
    }

    private void EnsureDynamicResourceManagerSubscription()
    {
        DynamicResourceManager dyn = DynamicResourceManager.Instance;
        if (dyn == subscribedDynamicResourceManager)
        {
            return;
        }

        if (subscribedDynamicResourceManager != null)
        {
            subscribedDynamicResourceManager.OnResourcesUpdated -= HandleResourcesUpdated;
            subscribedDynamicResourceManager = null;
        }

        if (dyn != null)
        {
            subscribedDynamicResourceManager = dyn;
            subscribedDynamicResourceManager.OnResourcesUpdated += HandleResourcesUpdated;
            // Ensure UI reflects latest values even if we missed an event before subscribing
            PopulateInventoryGrid();
        }
    }

    private void UnsubscribeFromDynamicResourceManager()
    {
        if (subscribedDynamicResourceManager != null)
        {
            subscribedDynamicResourceManager.OnResourcesUpdated -= HandleResourcesUpdated;
            subscribedDynamicResourceManager = null;
        }
    }

    private void UpdateStatSummary(PlayerStats.StatSnapshot snapshot)
    {
        if (totalStrengthText != null)
        {
            totalStrengthText.text = snapshot.Strength.ToString();
        }

        if (totalDefenseText != null)
        {
            totalDefenseText.text = snapshot.Defense.ToString();
        }

        if (totalHealthText != null)
        {
            totalHealthText.text = snapshot.Health.ToString();
        }

        if (totalIntelligenceText != null)
        {
            totalIntelligenceText.text = snapshot.Intelligence.ToString();
        }

        if (totalKnowledgeText != null)
        {
            totalKnowledgeText.text = snapshot.Knowledge.ToString();
        }
    }

    private void InitializeAttributeButtons()
    {
        attributePointButtons.Clear();
        lastKnownUnspentAttributePoints = -1;
        RegisterAttributeButton(strengthAttributeButton, PlayerStats.AttributeStat.Strength);
        RegisterAttributeButton(defenseAttributeButton, PlayerStats.AttributeStat.Defense);
        RegisterAttributeButton(healthAttributeButton, PlayerStats.AttributeStat.Health);
        RegisterAttributeButton(intelligenceAttributeButton, PlayerStats.AttributeStat.Intelligence);
        RegisterAttributeButton(knowledgeAttributeButton, PlayerStats.AttributeStat.Knowledge);
    }

    private void RegisterAttributeButton(Button button, PlayerStats.AttributeStat stat)
    {
        if (button == null)
        {
            return;
        }

        if (!attributePointButtons.Contains(button))
        {
            attributePointButtons.Add(button);
        }

        if (attributeButtonHandlers.TryGetValue(button, out UnityAction existing))
        {
            button.onClick.RemoveListener(existing);
        }

        UnityAction handler = () => HandleAttributeButtonClicked(stat);
        button.onClick.AddListener(handler);
        attributeButtonHandlers[button] = handler;
    }

    private void HandleAttributeButtonClicked(PlayerStats.AttributeStat stat)
    {
        if (playerStats == null)
        {
            RefreshAttributeUI();
            return;
        }

        if (playerStats.UnspentAttributePoints <= 0)
        {
            RefreshAttributeUI();
            return;
        }

        int strength = stat == PlayerStats.AttributeStat.Strength ? 1 : 0;
        int defense = stat == PlayerStats.AttributeStat.Defense ? 1 : 0;
        int health = stat == PlayerStats.AttributeStat.Health ? 1 : 0;
        int intelligence = stat == PlayerStats.AttributeStat.Intelligence ? 1 : 0;
        int knowledge = stat == PlayerStats.AttributeStat.Knowledge ? 1 : 0;

        bool spent = playerStats.TrySpendAttributePoints(strength, defense, health, intelligence, knowledge);
        if (!spent)
        {
            RefreshAttributeUI();
        }
    }

    private void HandleAttributePointsChanged(int unspent)
    {
        lastKnownUnspentAttributePoints = -1; // force refresh
        RefreshAttributeUI(unspent);
    }

    private void RefreshAttributeUI(int? overrideUnspent = null)
    {
        int unspent = overrideUnspent ?? (playerStats != null ? playerStats.UnspentAttributePoints : 0);
        if (lastKnownUnspentAttributePoints == unspent && playerStats != null)
        {
            return;
        }

        lastKnownUnspentAttributePoints = unspent;

        bool showButtons = playerStats != null && unspent > 0;
        SetAttributeButtonsActive(showButtons);

        if (unspentAttributePointsText != null)
        {
            unspentAttributePointsText.text = unspent.ToString();
        }
    }

    private void SetAttributeButtonsActive(bool active)
    {
        for (int i = 0; i < attributePointButtons.Count; i++)
        {
            Button button = attributePointButtons[i];
            if (button == null)
            {
                continue;
            }

            GameObject buttonObject = button.gameObject;
            if (buttonObject != null && buttonObject.activeSelf != active)
            {
                buttonObject.SetActive(active);
            }
        }
    }

    private void SynchronizeInventoryWithEquipment()
    {
        if (playerInventory == null || gearManager == null)
        {
            return;
        }

        IReadOnlyDictionary<GearType, GearItem> equippedGear = gearManager.EquippedGear;
        foreach (GearItem gearItem in equippedGear.Values)
        {
            if (gearItem == null)
            {
                continue;
            }

            playerInventory.Remove(gearItem);
        }
    }

    private void ShowTooltip(ResourceTypeDef resource, object source, TradeTooltipData tradeInfo = default)
    {
        if (resource == null)
        {
            HideTooltip();
            return;
        }

        if (tooltipShowCoroutine != null)
        {
            StopCoroutine(tooltipShowCoroutine);
            tooltipShowCoroutine = null;
        }

        tooltipPendingResource = null;
        tooltipPendingSource = null;
        tooltipPendingTradeInfo = TradeTooltipData.None;
        tooltipPendingItem = null;
        tooltipPendingOverrideSalvageSet = null;
        tooltipCurrentSource = source;
        tooltipCurrentResource = resource;
        tooltipCurrentItem = null;
        tooltipCurrentTradeInfo = tradeInfo;
        tooltipCurrentOverrideSalvageSet = null;

        if (tooltipRoot == null)
        {
            return;
        }

        tooltipRoot.SetActive(true);
        SetGearTooltipObjectsVisible(false);
        tooltipAnimator?.Play();

        if (tooltipIcon != null)
        {
            if (resource.Icon != null)
            {
                tooltipIcon.sprite = resource.Icon;
                tooltipIcon.enabled = true;
                tooltipIcon.color = Color.white;
            }
            else
            {
                tooltipIcon.sprite = null;
                tooltipIcon.enabled = false;
            }
        }

        if (tooltipIconBackground != null)
        {
            tooltipIconBackground.color = resource.RarityColor;
        }

        if (tooltipNameText != null)
        {
            tooltipNameText.text = string.IsNullOrWhiteSpace(resource.DisplayName) ? string.Empty : resource.DisplayName;
        }

        // Clear gear-specific stats
        if (tooltipStrengthText != null) tooltipStrengthText.text = string.Empty;
        if (tooltipDefenseText != null) tooltipDefenseText.text = string.Empty;
        if (tooltipHealthText != null) tooltipHealthText.text = string.Empty;
        if (tooltipKnowledgeText != null) tooltipKnowledgeText.text = string.Empty;
        if (tooltipIntelligenceText != null) tooltipIntelligenceText.text = string.Empty;

        if (tooltipDamageText != null)
        {
            tooltipDamageText.text = string.Empty;
            tooltipDamageText.gameObject.SetActive(false);
        }
        if (tooltipDamageLabelText != null)
        {
            tooltipDamageLabelText.gameObject.SetActive(false);
        }

        if (tooltipInfoText != null)
        {
            tooltipInfoText.text = resource.Note ?? string.Empty;
        }

        if (tooltipRarityText != null)
        {
            string rarityName = resource.Rarity.ToString();
            tooltipRarityText.text = rarityName;
            tooltipRarityText.color = resource.RarityColor;
        }

        if (resource.Rarity != GearRarity.Common)
        {
            StartTooltipGlow(resource.RarityColor);
        }
        else
        {
            StopTooltipGlow();
        }

        ApplyTradeInfo(tradeInfo);

        UpdateTooltipPosition();
    }

    private void ShowTooltip(GearItem gearItem, object source, TradeTooltipData tradeInfo = default, ResourceSet salvageOverride = null)
    {
        if (gearItem == null)
        {
            HideTooltip();
            return;
        }

        if (tooltipShowCoroutine != null)
        {
            StopCoroutine(tooltipShowCoroutine);
            tooltipShowCoroutine = null;
        }

        tooltipPendingItem = null;
        tooltipPendingSource = null;
        tooltipPendingResource = null;
        tooltipPendingTradeInfo = TradeTooltipData.None;
        tooltipPendingOverrideSalvageSet = null;
        tooltipCurrentSource = source;
        tooltipCurrentItem = gearItem;
        tooltipCurrentResource = null;
        tooltipCurrentTradeInfo = tradeInfo;
        tooltipCurrentOverrideSalvageSet = salvageOverride;

        if (tooltipRoot == null)
        {
            return;
        }

        tooltipRoot.SetActive(true);
        SetGearTooltipObjectsVisible(true);
        tooltipAnimator?.Play();

        EnsureTooltipSalvageTemplatesDisabled();

        if (tooltipIcon != null)
        {
            if (gearItem.Icon != null)
            {
                tooltipIcon.sprite = gearItem.Icon;
                tooltipIcon.enabled = true;
                tooltipIcon.color = Color.white;
            }
            else
            {
                tooltipIcon.sprite = null;
                tooltipIcon.enabled = false;
            }
        }

        if (tooltipIconBackground != null)
        {
            tooltipIconBackground.color = gearItem.RarityColor;
        }

        if (tooltipNameText != null)
        {
            tooltipNameText.text = string.IsNullOrWhiteSpace(gearItem.DisplayName) ? string.Empty : gearItem.DisplayName;
        }

        if (tooltipStrengthText != null)
        {
            tooltipStrengthText.text = gearItem.Strength.ToString();
        }

        if (tooltipDefenseText != null)
        {
            tooltipDefenseText.text = gearItem.Defense.ToString();
        }

        if (tooltipHealthText != null)
        {
            tooltipHealthText.text = gearItem.Health.ToString();
        }

        if (tooltipKnowledgeText != null)
        {
            tooltipKnowledgeText.text = gearItem.Knowledge.ToString();
        }

        if (tooltipIntelligenceText != null)
        {
            tooltipIntelligenceText.text = gearItem.Intelligence.ToString();
        }

        bool showDamage = gearItem.GearType == GearType.Weapon;

        if (tooltipDamageText != null)
        {
            tooltipDamageText.text = showDamage ? gearItem.Damage.ToString() : string.Empty;
            tooltipDamageText.gameObject.SetActive(showDamage);
        }

        if (tooltipDamageLabelText != null)
        {
            tooltipDamageLabelText.gameObject.SetActive(showDamage);
        }

        if (tooltipInfoText != null)
        {
            tooltipInfoText.text = string.IsNullOrWhiteSpace(gearItem.Info) ? string.Empty : gearItem.Info;
        }

        if (tooltipRarityText != null)
        {
            string rarityName = gearItem.Rarity.ToString();
            string typeName = gearItem.GearType.ToString();
            tooltipRarityText.text = string.IsNullOrWhiteSpace(typeName) ? rarityName : $"{rarityName} {typeName}";
            tooltipRarityText.color = gearItem.RarityColor;
        }

        if (gearItem.Rarity != GearRarity.Common)
        {
            StartTooltipGlow(gearItem.RarityColor);
        }
        else
        {
            StopTooltipGlow();
        }

        if (tradeInfo.IsActive)
        {
            ClearTooltipSalvageEntries();
        }
        else
        {
            RefreshTooltipSalvageSection(gearItem, salvageOverride);
        }

        ApplyTradeInfo(tradeInfo);

        UpdateTooltipPosition();
    }

    private IEnumerator ShowTooltipAfterDelay()
    {
        if (tooltipShowDelay > 0f)
        {
            yield return new WaitForSeconds(tooltipShowDelay);
        }

        tooltipShowCoroutine = null;

        if (tooltipPendingItem == null)
        {
            yield break;
        }

        ResourceSet pendingOverride = tooltipPendingOverrideSalvageSet;
        ShowTooltip(tooltipPendingItem, tooltipPendingSource, tooltipPendingTradeInfo, pendingOverride);
    }

    private IEnumerator ShowTooltipAfterDelayResource()
    {
        if (tooltipShowDelay > 0f)
        {
            yield return new WaitForSeconds(tooltipShowDelay);
        }

        tooltipShowCoroutine = null;

        if (tooltipPendingResource == null)
        {
            yield break;
        }

        ShowTooltip(tooltipPendingResource, tooltipPendingSource, tooltipPendingTradeInfo);
    }

    private void StartTooltipGlow(Color baseColor)
    {
        if (tooltipGlowOutline == null)
        {
            return;
        }

        float width = Mathf.Abs(tooltipGlowWidth);
        tooltipGlowOutline.effectDistance = new Vector2(width, width);
        tooltipGlowBaseColor = baseColor;
        tooltipGlowOutline.enabled = true;

        if (tooltipGlowCoroutine != null)
        {
            StopCoroutine(tooltipGlowCoroutine);
        }

        tooltipGlowCoroutine = StartCoroutine(AnimateTooltipGlow());
    }

    private void StopTooltipGlow()
    {
        if (tooltipGlowCoroutine != null)
        {
            StopCoroutine(tooltipGlowCoroutine);
            tooltipGlowCoroutine = null;
        }

        if (tooltipGlowOutline != null)
        {
            tooltipGlowOutline.effectColor = Color.clear;
            tooltipGlowOutline.enabled = false;
        }
    }

    private IEnumerator AnimateTooltipGlow()
    {
        float time = 0f;

        while (true)
        {
            time += Time.unscaledDeltaTime * Mathf.Max(0f, tooltipGlowPulseSpeed);
            float sine = Mathf.Sin(time);
            float normalized = (sine + 1f) * 0.5f;
            float alphaMultiplier = Mathf.Lerp(Mathf.Clamp01(tooltipGlowMinAlpha), 1f, normalized);
            Color color = tooltipGlowBaseColor;
            color.a *= alphaMultiplier;
            tooltipGlowOutline.effectColor = color;
            yield return null;
        }
    }

    private void UpdateTooltipPosition()
    {
        if (tooltipRectTransform == null)
        {
            return;
        }

        if (tooltipCurrentItem == null && tooltipPendingItem == null &&
            tooltipCurrentResource == null && tooltipPendingResource == null)
        {
            return;
        }

        Vector3 mousePosition = Input.mousePosition;

        Canvas canvas = tooltipRectTransform.GetComponentInParent<Canvas>();
        float scale = canvas != null ? canvas.scaleFactor : 1f;
        Vector3 offset = new Vector3(tooltipMouseOffset.x * scale, tooltipMouseOffset.y * scale, 0f);
        tooltipRectTransform.position = mousePosition + offset;
    }

    private void HideTooltip()
    {
        tooltipCurrentItem = null;
        tooltipCurrentSource = null;
        tooltipPendingItem = null;
        tooltipPendingSource = null;
        tooltipCurrentResource = null;
        tooltipPendingResource = null;
        tooltipCurrentOverrideSalvageSet = null;
        tooltipPendingOverrideSalvageSet = null;

        if (tooltipShowCoroutine != null)
        {
            StopCoroutine(tooltipShowCoroutine);
            tooltipShowCoroutine = null;
        }

        tooltipAnimator?.ResetState();

        if (tooltipRoot != null)
        {
            tooltipRoot.SetActive(false);
        }

        if (tooltipIconBackground != null)
        {
            tooltipIconBackground.color = tooltipIconBackgroundDefaultColor;
        }

        if (tooltipRarityText != null)
        {
            tooltipRarityText.text = string.Empty;
            tooltipRarityText.color = tooltipRarityDefaultColor;
        }

        if (tooltipDamageText != null)
        {
            tooltipDamageText.text = string.Empty;
            tooltipDamageText.gameObject.SetActive(false);
        }

        if (tooltipDamageLabelText != null)
        {
            tooltipDamageLabelText.gameObject.SetActive(false);
        }

        if (tooltipInfoText != null)
        {
            tooltipInfoText.text = string.Empty;
        }

        ClearTooltipSalvageEntries();
        ResetTradeInfoUI();

        StopTooltipGlow();
        SetGearTooltipObjectsVisible(true);
    }

    private void ApplyTradeInfo(TradeTooltipData tradeInfo)
    {
        bool active = tradeInfo.IsActive;

        if (tooltipTradeInfoRoot != null)
        {
            tooltipTradeInfoRoot.SetActive(active);
        }

        if (!active)
        {
            if (tooltipTradeInfoIcon != null)
            {
                tooltipTradeInfoIcon.sprite = tooltipTradeInfoDefaultSprite;
                tooltipTradeInfoIcon.color = tooltipTradeInfoIconDefaultColor;
                tooltipTradeInfoIcon.enabled = tooltipTradeInfoDefaultSprite != null;
            }

            if (tooltipTradeInfoHeaderText != null)
            {
                tooltipTradeInfoHeaderText.text = tooltipTradeInfoDefaultHeaderText;
            }

            if (tooltipTradeInfoValueText != null)
            {
                tooltipTradeInfoValueText.text = tooltipTradeInfoDefaultValueText;
            }

            return;
        }

        if (tooltipTradeInfoIcon != null)
        {
            if (tradeInfo.Icon != null)
            {
                tooltipTradeInfoIcon.sprite = tradeInfo.Icon;
                tooltipTradeInfoIcon.color = Color.white;
                tooltipTradeInfoIcon.enabled = true;
            }
            else if (tooltipTradeInfoDefaultSprite != null)
            {
                tooltipTradeInfoIcon.sprite = tooltipTradeInfoDefaultSprite;
                tooltipTradeInfoIcon.color = tooltipTradeInfoIconDefaultColor;
                tooltipTradeInfoIcon.enabled = true;
            }
            else
            {
                tooltipTradeInfoIcon.sprite = null;
                tooltipTradeInfoIcon.enabled = false;
            }
        }

        if (tooltipTradeInfoHeaderText != null)
        {
            tooltipTradeInfoHeaderText.text = string.IsNullOrEmpty(tradeInfo.HeaderText)
                ? tooltipTradeInfoDefaultHeaderText
                : tradeInfo.HeaderText;
        }

        if (tooltipTradeInfoValueText != null)
        {
            tooltipTradeInfoValueText.text = string.IsNullOrEmpty(tradeInfo.ValueText)
                ? tooltipTradeInfoDefaultValueText
                : tradeInfo.ValueText;
        }
    }

    private void ResetTradeInfoUI()
    {
        tooltipCurrentTradeInfo = TradeTooltipData.None;
        tooltipPendingTradeInfo = TradeTooltipData.None;

        if (tooltipTradeInfoRoot != null)
        {
            tooltipTradeInfoRoot.SetActive(false);
        }

        if (tooltipTradeInfoIcon != null)
        {
            tooltipTradeInfoIcon.sprite = tooltipTradeInfoDefaultSprite;
            tooltipTradeInfoIcon.color = tooltipTradeInfoIconDefaultColor;
            tooltipTradeInfoIcon.enabled = tooltipTradeInfoDefaultSprite != null;
        }

        if (tooltipTradeInfoHeaderText != null)
        {
            tooltipTradeInfoHeaderText.text = tooltipTradeInfoDefaultHeaderText;
        }

        if (tooltipTradeInfoValueText != null)
        {
            tooltipTradeInfoValueText.text = tooltipTradeInfoDefaultValueText;
        }
    }

    private void UpdateCurrencyPanel()
    {
        if (currencyPanelRoot == null || currencyIconTemplate == null || currencyValueTemplate == null)
        {
            return;
        }

        ClearCurrencyEntries();

        DynamicResourceManager dyn = DynamicResourceManager.Instance;
        ResourceSet current = dyn != null ? dyn.CurrentResources : null;
        IReadOnlyList<ResourceAmount> amounts = current != null ? current.Amounts : null;

        bool createdEntry = false;
        if (amounts != null)
        {
            for (int i = 0; i < amounts.Count; i++)
            {
                ResourceAmount amt = amounts[i];
                if (amt.type == null || amt.amount <= 0 || !amt.type.IsCurrency)
                {
                    continue;
                }

                Image iconInstance = Instantiate(currencyIconTemplate, currencyIconTemplate.transform.parent);
                iconInstance.gameObject.SetActive(true);
                if (amt.type.Icon != null)
                {
                    iconInstance.sprite = amt.type.Icon;
                    iconInstance.color = Color.white;
                }
                else
                {
                    iconInstance.sprite = currencyIconDefaultSprite;
                    iconInstance.color = currencyIconDefaultColor;
                }
                iconInstance.rectTransform.SetAsLastSibling();
                currencyEntries.Add(iconInstance.gameObject);

                TMP_Text valueInstance = Instantiate(currencyValueTemplate, currencyValueTemplate.transform.parent);
                valueInstance.gameObject.SetActive(true);
                valueInstance.text = amt.amount.ToString();
                valueInstance.rectTransform.SetAsLastSibling();
                currencyEntries.Add(valueInstance.gameObject);

                createdEntry = true;
            }
        }

        currencyPanelRoot.SetActive(createdEntry);
    }

    private void ClearCurrencyEntries()
    {
        for (int i = 0; i < currencyEntries.Count; i++)
        {
            GameObject entry = currencyEntries[i];
            if (entry == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(entry);
            }
            else
            {
                DestroyImmediate(entry);
            }
        }

        currencyEntries.Clear();
    }

    private void EnsureTooltipSalvageTemplatesDisabled()
    {
        if (tooltipSalvageIconTemplate != null)
        {
            tooltipSalvageIconTemplate.gameObject.SetActive(false);
        }

        if (tooltipSalvageAmountTemplate != null)
        {
            tooltipSalvageAmountTemplate.gameObject.SetActive(false);
        }
    }

    private void RefreshTooltipSalvageSection(GearItem gearItem, ResourceSet overrideSet = null)
    {
        if (tooltipSalvageRoot == null ||
            tooltipSalvageContentRoot == null ||
            tooltipSalvageIconTemplate == null ||
            tooltipSalvageAmountTemplate == null)
        {
            return;
        }

        ClearTooltipSalvageEntries();

        var salvageSet = overrideSet ?? (gearItem != null ? gearItem.SalvageResourcesSet : null);
        if (salvageSet == null || salvageSet.IsEmpty)
        {
            tooltipSalvageRoot.SetActive(false);
            return;
        }

        bool createdEntry = false;
        var amounts = salvageSet.Amounts;
        if (amounts != null)
        {
            for (int i = 0; i < amounts.Count; i++)
            {
                var resourceAmount = amounts[i];
                if (resourceAmount.type == null || resourceAmount.amount <= 0)
                {
                    continue;
                }

                Image iconInstance = Instantiate(tooltipSalvageIconTemplate, tooltipSalvageContentRoot);
                iconInstance.gameObject.SetActive(true);
                if (resourceAmount.type.Icon != null)
                {
                    iconInstance.sprite = resourceAmount.type.Icon;
                    iconInstance.enabled = true;
                    iconInstance.color = Color.white;
                }
                else
                {
                    iconInstance.sprite = tooltipSalvageIconTemplate.sprite;
                    iconInstance.enabled = true;
                }
                iconInstance.rectTransform.SetAsLastSibling();
                tooltipSalvageEntries.Add(iconInstance.gameObject);

                TMP_Text amountInstance = Instantiate(tooltipSalvageAmountTemplate, tooltipSalvageContentRoot);
                amountInstance.gameObject.SetActive(true);
                amountInstance.text = resourceAmount.amount.ToString();
                amountInstance.rectTransform.SetAsLastSibling();
                tooltipSalvageEntries.Add(amountInstance.gameObject);

                createdEntry = true;
            }
        }

        tooltipSalvageRoot.SetActive(createdEntry);
    }

    private void ClearTooltipSalvageEntries()
    {
        for (int i = 0; i < tooltipSalvageEntries.Count; i++)
        {
            DestroyTooltipObject(tooltipSalvageEntries[i]);
        }
        tooltipSalvageEntries.Clear();

        if (tooltipSalvageRoot != null)
        {
            tooltipSalvageRoot.SetActive(false);
        }
    }

    private void DestroyTooltipObject(GameObject go)
    {
        if (!go)
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
}
}










