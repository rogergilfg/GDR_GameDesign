using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using SmallScale.FantasyKingdomTileset.Building;
using Input = SmallScale.FantasyKingdomTileset.InputAdapter;

namespace SmallScale.FantasyKingdomTileset
{
/// <summary>
/// Handles the marketplace UI that allows trading between the player and a trader.
/// </summary>
[DisallowMultipleComponent]
public sealed class TraderPanelController : MonoBehaviour
{
    private enum ButtonOwner
    {
        Player,
        Trader
    }

    private enum ButtonEntry
    {
        Gear,
        Resource
    }

    private enum InventoryFilter
    {
        All,
        Weapons,
        Armor,
        Resources
    }

    private enum TradeAction
    {
        SellGear,
        SellResource,
        BuyGear,
        BuyResource
    }

    private sealed class TraderButtonContext : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public TraderPanelController Owner;
        public ButtonOwner OwnerType;
        public ButtonEntry EntryType;
        public GearItem GearItem;
        public ResourceTypeDef ResourceType;
        public int Amount;
        public int TraderItemPrice;
        public int TraderPricePerUnit;

        private InventoryItemButton inventoryButton;

        private void Awake()
        {
            inventoryButton = GetComponent<InventoryItemButton>();
        }

        private void OnEnable()
        {
            if (inventoryButton != null)
            {
                inventoryButton.Clicked += HandleClicked;
            }
        }

        private void OnDisable()
        {
            if (inventoryButton != null)
            {
                inventoryButton.Clicked -= HandleClicked;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Owner?.HandlePointerEnter(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Owner?.HandlePointerExit(this);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            Owner?.HandleBeginDrag(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Owner?.HandleDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            Owner?.HandleEndDrag(this, eventData);
        }

        private void HandleClicked(InventoryItemButton button)
        {
            Owner?.HandleButtonClicked(this);
        }

        public void SetInteractable(bool interactable)
        {
            if (inventoryButton == null)
            {
                inventoryButton = GetComponent<InventoryItemButton>();
            }

            Button uiButton = GetComponent<Button>();
            if (uiButton != null)
            {
                uiButton.interactable = interactable;
            }
        }
    }

    public static TraderPanelController Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Transform playerItemsParent;
    [SerializeField] private Transform traderItemsParent;
    [SerializeField] private InventoryItemButton itemButtonPrefab;
    [SerializeField] private RectTransform playerDropZone;
    [SerializeField] private RectTransform traderDropZone;
    [SerializeField] private RectTransform dragIconRoot;
    [SerializeField] private TMP_Text detailLabel;
    [SerializeField] private TMP_Text traderNameLabel;
    [SerializeField] private TMP_Text traderFundsLabel;
    [SerializeField] private Image traderCurrencyIcon;
    [SerializeField] private Button closeTraderButton;

    [Header("Player Filters")]
    [SerializeField] private Button playerFilterAllButton;
    [SerializeField] private Button playerFilterWeaponsButton;
    [SerializeField] private Button playerFilterArmorButton;
    [SerializeField] private Button playerFilterResourcesButton;

    [Header("Trader Filters")]
    [SerializeField] private Button traderFilterAllButton;
    [SerializeField] private Button traderFilterWeaponsButton;
    [SerializeField] private Button traderFilterArmorButton;
    [SerializeField] private Button traderFilterResourcesButton;

    [Header("Filter Colors")]
    [SerializeField] private Color filterNormalColor = Color.white;
    [SerializeField] private Color filterSelectedColor = new Color(0.85f, 0.85f, 0.85f);

    [Header("Currency Panel")]
    [SerializeField] private GameObject currencyPanelRoot;
    [SerializeField] private Image currencyIconTemplate;
    [SerializeField] private TMP_Text currencyValueTemplate;

    [Header("Feedback UI")]
    [SerializeField] private GameObject feedbackPanel;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private float feedbackDisplayDuration = 2.5f;

    [Header("Trade Prompt")]
    [SerializeField] private GameObject tradePromptRoot;
    [SerializeField] private TMP_Text tradePromptTitleLabel;
    [SerializeField] private TMP_Text tradePromptQuantityLabel;
    [SerializeField] private TMP_Text tradePromptUnitPriceLabel;
    [SerializeField] private TMP_Text tradePromptTotalPriceLabel;
    [SerializeField] private Slider tradePromptQuantitySlider;
    [SerializeField] private Button tradePromptConfirmButton;
    [SerializeField] private Button tradePromptCancelButton;
    [SerializeField] private Transform tradePromptItemContainer;
    [SerializeField] private Image tradePromptCurrencyIcon;

    [Header("Global Restrictions")]
    [SerializeField] private List<GearItem> globalDisallowedGear = new List<GearItem>();
    [SerializeField] private List<ResourceTypeDef> globalDisallowedResources = new List<ResourceTypeDef>();

    [Header("Dependencies")]
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private DynamicResourceManager resourceManager;
    [SerializeField] private Transform playerWorldAnchor;

    [Header("Settings")]
    [SerializeField] private float dragIconScale = 1f;

    private TraderComponent activeTrader;

    private readonly List<TraderButtonContext> playerButtons = new List<TraderButtonContext>();
    private readonly List<TraderButtonContext> traderButtons = new List<TraderButtonContext>();

    private readonly Dictionary<Button, InventoryFilter> playerFilterButtons = new Dictionary<Button, InventoryFilter>();
    private readonly Dictionary<Button, InventoryFilter> traderFilterButtons = new Dictionary<Button, InventoryFilter>();
    private readonly Dictionary<Button, UnityAction> playerFilterHandlers = new Dictionary<Button, UnityAction>();
    private readonly Dictionary<Button, UnityAction> traderFilterHandlers = new Dictionary<Button, UnityAction>();
    private InventoryFilter activePlayerFilter = InventoryFilter.All;
    private InventoryFilter activeTraderFilter = InventoryFilter.All;

    private TraderButtonContext highlightedButton;
    private TraderButtonContext dragContext;
    private RectTransform dragIconRect;
    private Image dragIconImage;
    private Coroutine feedbackCoroutine;
    private readonly List<GameObject> currencyEntries = new List<GameObject>();
    private Sprite currencyIconDefaultSprite;
    private Color currencyIconDefaultColor = Color.white;
    private string currencyValueDefaultText = string.Empty;
    private Sprite traderCurrencyDefaultSprite;
    private Color traderCurrencyDefaultColor = Color.white;
    private bool traderCurrencyDefaultEnabled = true;
    private Sprite tradePromptCurrencyDefaultSprite;
    private Color tradePromptCurrencyDefaultColor = Color.white;
    private bool tradePromptCurrencyDefaultEnabled = true;

    private TraderButtonContext pendingTradeContext;
    private TradeAction pendingTradeAction;
    private int pendingUnitPrice;
    private int pendingMaxQuantity = 1;
    private InventoryItemButton tradePromptItemDisplay;
    private bool tradePromptItemHovered;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple TraderPanelController instances detected. Destroying duplicate.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        RegisterFilterButtons();

        if (tradePromptRoot != null)
        {
            tradePromptRoot.SetActive(false);
        }

        if (tradePromptConfirmButton != null)
        {
            tradePromptConfirmButton.onClick.AddListener(HandleTradePromptConfirm);
        }

        if (tradePromptCancelButton != null)
        {
            tradePromptCancelButton.onClick.AddListener(CloseTradePrompt);
        }

        if (tradePromptQuantitySlider != null)
        {
            tradePromptQuantitySlider.wholeNumbers = true;
            tradePromptQuantitySlider.onValueChanged.AddListener(HandleTradePromptSliderChanged);
        }

        if (traderCurrencyIcon != null)
        {
            traderCurrencyDefaultSprite = traderCurrencyIcon.sprite;
            traderCurrencyDefaultColor = traderCurrencyIcon.color;
            traderCurrencyDefaultEnabled = traderCurrencyIcon.enabled;
        }

        if (tradePromptCurrencyIcon != null)
        {
            tradePromptCurrencyDefaultSprite = tradePromptCurrencyIcon.sprite;
            tradePromptCurrencyDefaultColor = tradePromptCurrencyIcon.color;
            tradePromptCurrencyDefaultEnabled = tradePromptCurrencyIcon.enabled;
        }

        if (closeTraderButton != null)
        {
            closeTraderButton.onClick.AddListener(CloseTrader);
        }

        HideFeedbackMessage();

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
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (tradePromptConfirmButton != null)
        {
            tradePromptConfirmButton.onClick.RemoveListener(HandleTradePromptConfirm);
        }

        if (tradePromptCancelButton != null)
        {
            tradePromptCancelButton.onClick.RemoveListener(CloseTradePrompt);
        }

        if (tradePromptQuantitySlider != null)
        {
            tradePromptQuantitySlider.onValueChanged.RemoveListener(HandleTradePromptSliderChanged);
        }

        if (closeTraderButton != null)
        {
            closeTraderButton.onClick.RemoveListener(CloseTrader);
        }

        if (tradePromptItemDisplay != null)
        {
            tradePromptItemDisplay.PointerEntered -= HandleTradePromptItemPointerEntered;
            tradePromptItemDisplay.PointerExited -= HandleTradePromptItemPointerExited;
        }

        CleanupDragVisual();
        UnsubscribeFromSources();
        UnregisterFilterButtons();
        HideFeedbackMessage();
        ClearCurrencyEntries();
        if (currencyPanelRoot != null)
        {
            currencyPanelRoot.SetActive(false);
        }
    }

    private void Update()
    {
        if (panelRoot == null || !panelRoot.activeSelf)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CloseTrader();
        }
    }

    public void OpenTrader(TraderComponent trader)
    {
        if (!this || trader == null)
        {
            return;
        }

        EnsureReferences();

        if (panelRoot == null || itemButtonPrefab == null)
        {
            Debug.LogWarning("TraderPanelController is missing required references.", this);
            return;
        }

        if (activeTrader == trader && panelRoot.activeSelf)
        {
            RefreshAll();
            return;
        }

        CloseTraderInternal();

        activeTrader = trader;
        if (traderNameLabel != null)
        {
            traderNameLabel.text = trader.TraderName;
        }

        BuildMenuController.RegisterExternalMenu(this);
        InventoryUIController.Instance?.CloseInventory();

        SubscribeToSources();

        HideFeedbackMessage();

        panelRoot.SetActive(true);
        RefreshAll();
    }

    public void CloseTrader()
    {
        CloseTraderInternal();
    }

    private void CloseTraderInternal()
    {
        CleanupDragVisual();
        CloseTradePrompt();

        InventoryUIController.TryClearTooltip(null);

        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        ClearAllButtons();
        UnsubscribeFromSources();

        if (activeTrader != null)
        {
            BuildMenuController.UnregisterExternalMenu(this);
        }

        activeTrader = null;
        highlightedButton = null;

        RefreshFundsLabel();
        RefreshTradePromptCurrencyIcon();

        HideFeedbackMessage();
        ClearCurrencyEntries();
        if (currencyPanelRoot != null)
        {
            currencyPanelRoot.SetActive(false);
        }
        if (detailLabel != null)
        {
            detailLabel.text = string.Empty;
            detailLabel.gameObject.SetActive(false);
        }
    }

    private void EnsureReferences()
    {
        if (playerInventory == null)
        {
            playerInventory = FindFirstObjectByType<PlayerInventory>();
        }

        if (resourceManager == null)
        {
            resourceManager = DynamicResourceManager.Instance;
        }

        if (playerWorldAnchor == null && playerInventory != null)
        {
            playerWorldAnchor = playerInventory.transform;
        }
    }

    private void RegisterFilterButtons()
    {
        playerFilterButtons.Clear();
        traderFilterButtons.Clear();
        playerFilterHandlers.Clear();
        traderFilterHandlers.Clear();

        AddFilterButton(playerFilterButtons, playerFilterHandlers, playerFilterAllButton, InventoryFilter.All, SetPlayerFilter);
        AddFilterButton(playerFilterButtons, playerFilterHandlers, playerFilterWeaponsButton, InventoryFilter.Weapons, SetPlayerFilter);
        AddFilterButton(playerFilterButtons, playerFilterHandlers, playerFilterArmorButton, InventoryFilter.Armor, SetPlayerFilter);
        AddFilterButton(playerFilterButtons, playerFilterHandlers, playerFilterResourcesButton, InventoryFilter.Resources, SetPlayerFilter);

        AddFilterButton(traderFilterButtons, traderFilterHandlers, traderFilterAllButton, InventoryFilter.All, SetTraderFilter);
        AddFilterButton(traderFilterButtons, traderFilterHandlers, traderFilterWeaponsButton, InventoryFilter.Weapons, SetTraderFilter);
        AddFilterButton(traderFilterButtons, traderFilterHandlers, traderFilterArmorButton, InventoryFilter.Armor, SetTraderFilter);
        AddFilterButton(traderFilterButtons, traderFilterHandlers, traderFilterResourcesButton, InventoryFilter.Resources, SetTraderFilter);

        UpdateFilterVisuals(playerFilterButtons, activePlayerFilter);
        UpdateFilterVisuals(traderFilterButtons, activeTraderFilter);
    }

    private void UnregisterFilterButtons()
    {
        foreach (KeyValuePair<Button, UnityAction> pair in playerFilterHandlers)
        {
            if (pair.Key != null)
            {
                pair.Key.onClick.RemoveListener(pair.Value);
            }
        }
        playerFilterHandlers.Clear();
        playerFilterButtons.Clear();

        foreach (KeyValuePair<Button, UnityAction> pair in traderFilterHandlers)
        {
            if (pair.Key != null)
            {
                pair.Key.onClick.RemoveListener(pair.Value);
            }
        }
        traderFilterHandlers.Clear();
        traderFilterButtons.Clear();
    }

    private void AddFilterButton(
        Dictionary<Button, InventoryFilter> map,
        Dictionary<Button, UnityAction> handlerMap,
        Button button,
        InventoryFilter filter,
        Action<InventoryFilter> onClick)
    {
        if (button == null || map.ContainsKey(button))
        {
            return;
        }

        map.Add(button, filter);
        UnityAction handler = () => onClick(filter);
        button.onClick.AddListener(handler);
        handlerMap[button] = handler;
    }

    private void SetPlayerFilter(InventoryFilter filter)
    {
        if (activePlayerFilter == filter)
        {
            UpdateFilterVisuals(playerFilterButtons, activePlayerFilter);
            return;
        }

        activePlayerFilter = filter;
        UpdateFilterVisuals(playerFilterButtons, activePlayerFilter);
        RefreshPlayerSection();
        RefreshCurrencyPanel();
    }

    private void SetTraderFilter(InventoryFilter filter)
    {
        if (activeTraderFilter == filter)
        {
            UpdateFilterVisuals(traderFilterButtons, activeTraderFilter);
            return;
        }

        activeTraderFilter = filter;
        UpdateFilterVisuals(traderFilterButtons, activeTraderFilter);
        RefreshTraderSection();
        RefreshCurrencyPanel();
    }

    private void UpdateFilterVisuals(Dictionary<Button, InventoryFilter> map, InventoryFilter activeFilter)
    {
        foreach (KeyValuePair<Button, InventoryFilter> pair in map)
        {
            Button button = pair.Key;
            if (button == null)
            {
                continue;
            }

            bool selected = pair.Value == activeFilter;
            if (button.image != null)
            {
                button.image.color = selected ? filterSelectedColor : filterNormalColor;
            }

            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.color = Color.white;
            }

            button.interactable = !selected;
        }
    }

    private void RefreshCurrencyPanel()
    {
        if (currencyPanelRoot == null || currencyIconTemplate == null || currencyValueTemplate == null)
        {
            return;
        }

        ClearCurrencyEntries();

        DynamicResourceManager dyn = resourceManager != null ? resourceManager : DynamicResourceManager.Instance;
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

    private bool PassesFilter(InventoryFilter filter, GearItem gearItem, ResourceTypeDef resourceType)
    {
        switch (filter)
        {
            case InventoryFilter.All:
                return true;
            case InventoryFilter.Weapons:
                return gearItem != null && gearItem.GearType == GearType.Weapon;
            case InventoryFilter.Armor:
                return gearItem != null && gearItem.GearType != GearType.Weapon;
            case InventoryFilter.Resources:
                return resourceType != null;
            default:
                return true;
        }
    }

    private void SubscribeToSources()
    {
        if (playerInventory != null)
        {
            playerInventory.InventoryChanged -= HandlePlayerInventoryChanged;
            playerInventory.InventoryChanged += HandlePlayerInventoryChanged;
        }

        if (resourceManager != null)
        {
            resourceManager.OnResourcesUpdated -= HandleResourcesUpdated;
            resourceManager.OnResourcesUpdated += HandleResourcesUpdated;
        }

        if (activeTrader != null)
        {
            activeTrader.InventoryChanged -= HandleTraderInventoryChanged;
            activeTrader.InventoryChanged += HandleTraderInventoryChanged;
        }
    }

    private void UnsubscribeFromSources()
    {
        if (playerInventory != null)
        {
            playerInventory.InventoryChanged -= HandlePlayerInventoryChanged;
        }

        if (resourceManager != null)
        {
            resourceManager.OnResourcesUpdated -= HandleResourcesUpdated;
        }

        if (activeTrader != null)
        {
            activeTrader.InventoryChanged -= HandleTraderInventoryChanged;
        }
    }

    private void HandlePlayerInventoryChanged()
    {
        RefreshPlayerSection();
        RefreshCurrencyPanel();
    }

    private void HandleResourcesUpdated(ResourceSet _)
    {
        RefreshPlayerSection();
        RefreshFundsLabel();
        RefreshCurrencyPanel();
    }

    private void HandleTraderInventoryChanged(TraderComponent _)
    {
        RefreshTraderSection();
        RefreshFundsLabel();
        RefreshCurrencyPanel();
    }

    private void RefreshAll()
    {
        RefreshPlayerSection();
        RefreshTraderSection();
        RefreshFundsLabel();
        RefreshCurrencyPanel();

        if (highlightedButton != null)
        {
            InventoryUIController.TradeTooltipData tradeData = UpdateDetail(highlightedButton);

            if (highlightedButton.EntryType == ButtonEntry.Resource && highlightedButton.ResourceType != null)
            {
                InventoryUIController.TryRequestResourceTooltip(highlightedButton.ResourceType, highlightedButton, tradeData);
            }
            else if (highlightedButton.EntryType == ButtonEntry.Gear && highlightedButton.GearItem != null)
            {
                InventoryUIController.TryRequestTooltip(highlightedButton.GearItem, highlightedButton, tradeData);
            }
        }
        else if (detailLabel != null)
        {
            detailLabel.text = string.Empty;
            detailLabel.gameObject.SetActive(false);
        }
    }

    private void RefreshPlayerSection()
    {
        ClearPlayerButtons();

        if (playerInventory == null || playerItemsParent == null || itemButtonPrefab == null)
        {
            return;
        }

        IReadOnlyList<GearItem> items = playerInventory.Items;
        for (int i = 0; i < items.Count; i++)
        {
            GearItem gearItem = items[i];
            if (gearItem == null)
            {
                continue;
            }

            if (!PassesFilter(activePlayerFilter, gearItem, null))
            {
                continue;
            }

            InventoryItemButton button = Instantiate(itemButtonPrefab, playerItemsParent);
            button.SetGearItem(gearItem);

            TraderButtonContext context = ConfigureButtonContext(button);
            context.OwnerType = ButtonOwner.Player;
            context.EntryType = ButtonEntry.Gear;
            context.GearItem = gearItem;
            context.ResourceType = null;
            context.Amount = 1;
            context.TraderItemPrice = 0;
            context.TraderPricePerUnit = 0;

            playerButtons.Add(context);
        }

        if (resourceManager != null)
        {
            ResourceSet resources = resourceManager.CurrentResources;
            IReadOnlyList<ResourceAmount> amounts = resources != null ? resources.Amounts : null;
            if (amounts != null)
            {
                for (int i = 0; i < amounts.Count; i++)
                {
                    ResourceAmount amount = amounts[i];
                    if (amount.type == null || amount.amount <= 0 || amount.type.IsCurrency)
                    {
                        continue;
                    }

                    if (!PassesFilter(activePlayerFilter, null, amount.type))
                    {
                        continue;
                    }

                    int remaining = amount.amount;
                    int maxStack = Mathf.Max(1, amount.type.MaxStackSize);
                    while (remaining > 0)
                    {
                        int stackAmount = Mathf.Min(remaining, maxStack);
                        InventoryItemButton button = Instantiate(itemButtonPrefab, playerItemsParent);
                        button.SetResource(amount.type, stackAmount);

                        TraderButtonContext context = ConfigureButtonContext(button);
                        context.OwnerType = ButtonOwner.Player;
                        context.EntryType = ButtonEntry.Resource;
                        context.GearItem = null;
                        context.ResourceType = amount.type;
                        context.Amount = stackAmount;
                        context.TraderItemPrice = 0;
                        context.TraderPricePerUnit = 0;

                        playerButtons.Add(context);

                        remaining -= stackAmount;
                    }
                }
            }
        }
    }

    private void RefreshTraderSection()
    {
        ClearTraderButtons();

        if (activeTrader == null || traderItemsParent == null || itemButtonPrefab == null)
        {
            return;
        }

        IReadOnlyList<TraderComponent.GearStockSnapshot> gearStock = activeTrader.GetGearStockSnapshot();
        for (int i = 0; i < gearStock.Count; i++)
        {
            TraderComponent.GearStockSnapshot snapshot = gearStock[i];
            if (snapshot.Item == null || snapshot.Quantity <= 0)
            {
                continue;
            }

            if (!PassesFilter(activeTraderFilter, snapshot.Item, null))
            {
                continue;
            }

            int copies = Mathf.Max(1, snapshot.Quantity);
            for (int j = 0; j < copies; j++)
            {
                InventoryItemButton button = Instantiate(itemButtonPrefab, traderItemsParent);
                button.SetGearItem(snapshot.Item);

                TraderButtonContext context = ConfigureButtonContext(button);
                context.OwnerType = ButtonOwner.Trader;
                context.EntryType = ButtonEntry.Gear;
                context.GearItem = snapshot.Item;
                context.ResourceType = null;
                context.Amount = 1;
                context.TraderItemPrice = Mathf.Max(0, snapshot.PlayerBuyPrice);
                context.TraderPricePerUnit = 0;

                traderButtons.Add(context);
            }
        }

        IReadOnlyList<TraderComponent.ResourceStockSnapshot> resourceStock = activeTrader.GetResourceStockSnapshot();
        for (int i = 0; i < resourceStock.Count; i++)
        {
            TraderComponent.ResourceStockSnapshot snapshot = resourceStock[i];
            if (snapshot.Resource == null || snapshot.Quantity <= 0 || snapshot.Resource.IsCurrency)
            {
                continue;
            }

            if (!PassesFilter(activeTraderFilter, null, snapshot.Resource))
            {
                continue;
            }

            int remaining = snapshot.Quantity;
            int maxStack = Mathf.Max(1, snapshot.Resource.MaxStackSize);
            while (remaining > 0)
            {
                int stackAmount = Mathf.Min(remaining, maxStack);
                InventoryItemButton button = Instantiate(itemButtonPrefab, traderItemsParent);
                button.SetResource(snapshot.Resource, stackAmount);

                TraderButtonContext context = ConfigureButtonContext(button);
                context.OwnerType = ButtonOwner.Trader;
                context.EntryType = ButtonEntry.Resource;
                context.GearItem = null;
                context.ResourceType = snapshot.Resource;
                context.Amount = stackAmount;
                context.TraderItemPrice = 0;
                context.TraderPricePerUnit = Mathf.Max(0, snapshot.PlayerBuyPricePerUnit);

                traderButtons.Add(context);

                remaining -= stackAmount;
            }
        }
    }

    private TraderButtonContext ConfigureButtonContext(InventoryItemButton button)
    {
        TraderButtonContext context = button.GetComponent<TraderButtonContext>();
        if (context == null)
        {
            context = button.gameObject.AddComponent<TraderButtonContext>();
        }

        context.Owner = this;
        return context;
    }

    private void ClearPlayerButtons()
    {
        for (int i = 0; i < playerButtons.Count; i++)
        {
            TraderButtonContext context = playerButtons[i];
            if (context == null)
            {
                continue;
            }

            if (highlightedButton == context)
            {
                highlightedButton = null;
            }

            InventoryUIController.TryClearTooltip(context);

            Destroy(context.gameObject);
        }

        playerButtons.Clear();

        if (highlightedButton == null && detailLabel != null)
        {
            detailLabel.text = string.Empty;
            detailLabel.gameObject.SetActive(false);
        }
    }

    private void ClearTraderButtons()
    {
        for (int i = 0; i < traderButtons.Count; i++)
        {
            TraderButtonContext context = traderButtons[i];
            if (context == null)
            {
                continue;
            }

            if (highlightedButton == context)
            {
                highlightedButton = null;
            }

            InventoryUIController.TryClearTooltip(context);

            Destroy(context.gameObject);
        }

        traderButtons.Clear();

        if (highlightedButton == null && detailLabel != null)
        {
            detailLabel.text = string.Empty;
            detailLabel.gameObject.SetActive(false);
        }
    }

    private void ClearAllButtons()
    {
        ClearPlayerButtons();
        ClearTraderButtons();
    }

    private void HandleButtonClicked(TraderButtonContext context)
    {
        if (context == null)
        {
            return;
        }

        InventoryUIController.TryClearTooltip(context);

        TryStartTrade(context);
    }

    private bool TryStartTrade(TraderButtonContext context)
    {
        if (context == null)
        {
            return false;
        }

        TradeAction action = DetermineTradeAction(context);

        if (!IsTradePromptConfigured())
        {
            return ExecuteTradeImmediate(context, action, Mathf.Max(1, context.Amount));
        }

        return ShowTradePrompt(context, action);
    }

    private TradeAction DetermineTradeAction(TraderButtonContext context)
    {
        if (context.OwnerType == ButtonOwner.Player)
        {
            return context.EntryType == ButtonEntry.Gear ? TradeAction.SellGear : TradeAction.SellResource;
        }

        return context.EntryType == ButtonEntry.Gear ? TradeAction.BuyGear : TradeAction.BuyResource;
    }

    private bool IsTradePromptConfigured()
    {
        if (tradePromptRoot == null)
        {
            Debug.LogWarning("Trade prompt root reference is missing on TraderPanelController; falling back to instant trades.", this);
            return false;
        }

        if (tradePromptConfirmButton == null || tradePromptCancelButton == null)
        {
            Debug.LogWarning("Trade prompt confirm/cancel buttons are not assigned on TraderPanelController; falling back to instant trades.", this);
            return false;
        }

        if (tradePromptQuantitySlider == null)
        {
            Debug.LogWarning("Trade prompt quantity slider is not assigned on TraderPanelController; falling back to instant trades.", this);
            return false;
        }

        return true;
    }

    private bool ShowTradePrompt(TraderButtonContext context, TradeAction action)
    {
        if (!IsTradePromptConfigured())
        {
            return false;
        }

        if (tradePromptRoot.activeSelf)
        {
            CloseTradePrompt();
        }

        if (activeTrader == null)
        {
            return false;
        }

        string itemLabel = string.Empty;
        bool sliderActive = false;
        int maxQuantity = Mathf.Max(1, context.Amount);
        int unitPrice = 0;

        switch (action)
        {
            case TradeAction.SellGear:
                if (context.GearItem == null)
                {
                    return false;
                }

                if (IsGearBlacklistedForTrader(context.GearItem, out string gearRestriction))
                {
                    ShowFeedbackMessage(gearRestriction);
                    return false;
                }

                if (!playerInventory || !playerInventory.Contains(context.GearItem))
                {
                    RefreshPlayerSection();
                    ShowFeedbackMessage("Item is no longer in your inventory.");
                    return false;
                }

                if (!activeTrader.TryGetPlayerSellPrice(context.GearItem, out unitPrice) || unitPrice <= 0)
                {
                    ShowFeedbackMessage($"{activeTrader.TraderName} is not interested in {context.GearItem.DisplayName}.");
                    return false;
                }

                if (!activeTrader.HasFunds(unitPrice))
                {
                    ShowFeedbackMessage($"{activeTrader.TraderName} doesn't have enough {FormatCurrency(unitPrice)}.");
                    return false;
                }

                itemLabel = context.GearItem.DisplayName;
                maxQuantity = 1;
                break;

            case TradeAction.BuyGear:
                if (context.GearItem == null)
                {
                    return false;
                }

                if (!activeTrader.TryGetPlayerBuyPrice(context.GearItem, out unitPrice) || unitPrice <= 0)
                {
                    ShowFeedbackMessage("Item is no longer available.");
                    RefreshTraderSection();
                    return false;
                }

                if (!HasEnoughPlayerCurrency(unitPrice))
                {
                    ShowFeedbackMessage($"You need {FormatCurrency(unitPrice)}.");
                    return false;
                }

                itemLabel = context.GearItem.DisplayName;
                maxQuantity = 1;
                break;

            case TradeAction.SellResource:
                if (context.ResourceType == null)
                {
                    return false;
                }

                if (IsResourceBlacklistedForTrader(context.ResourceType, out string resourceRestriction))
                {
                    ShowFeedbackMessage(resourceRestriction);
                    return false;
                }

                if (!activeTrader.TryGetPlayerSellPrice(context.ResourceType, out unitPrice) || unitPrice <= 0)
                {
                    ShowFeedbackMessage($"{activeTrader.TraderName} is not interested in {context.ResourceType.DisplayName}.");
                    return false;
                }

                int traderCapacity = unitPrice > 0 ? activeTrader.AvailableCurrency / unitPrice : 0;
                maxQuantity = Mathf.Min(maxQuantity, traderCapacity);
                if (maxQuantity <= 0)
                {
                    ShowFeedbackMessage($"{activeTrader.TraderName} doesn't have enough funds.");
                    return false;
                }

                sliderActive = maxQuantity > 1;
                itemLabel = context.ResourceType.DisplayName;
                break;

            case TradeAction.BuyResource:
                if (context.ResourceType == null)
                {
                    return false;
                }

                if (!activeTrader.TryGetPlayerBuyPrice(context.ResourceType, out unitPrice) || unitPrice <= 0)
                {
                    ShowFeedbackMessage("Resource is no longer available.");
                    RefreshTraderSection();
                    return false;
                }

                if (resourceManager == null || activeTrader.CurrencyType == null)
                {
                    ShowFeedbackMessage("Trader currency is unavailable.");
                    return false;
                }

                int playerCurrency = resourceManager.Get(activeTrader.CurrencyType);
                int maxAffordable = unitPrice > 0 ? playerCurrency / unitPrice : 0;
                maxQuantity = Mathf.Min(maxQuantity, maxAffordable);
                if (maxQuantity <= 0)
                {
                    ShowFeedbackMessage($"You need {FormatCurrency(unitPrice)}.");
                    return false;
                }

                sliderActive = maxQuantity > 1;
                itemLabel = context.ResourceType.DisplayName;
                break;

            default:
                return false;
        }

        maxQuantity = Mathf.Clamp(maxQuantity, 1, Mathf.Max(1, context.Amount));

        pendingTradeContext = context;
        pendingTradeAction = action;
        pendingUnitPrice = Mathf.Max(0, unitPrice);
        pendingMaxQuantity = maxQuantity;

        if (tradePromptQuantitySlider != null)
        {
            tradePromptQuantitySlider.gameObject.SetActive(sliderActive);
            tradePromptQuantitySlider.minValue = 1;
            tradePromptQuantitySlider.maxValue = maxQuantity;
            tradePromptQuantitySlider.value = sliderActive ? maxQuantity : 1;
            tradePromptQuantitySlider.interactable = sliderActive;
        }

        if (tradePromptTitleLabel != null)
        {
            string verb = action == TradeAction.SellGear || action == TradeAction.SellResource ? "Sell" : "Buy";
            tradePromptTitleLabel.text = string.IsNullOrEmpty(itemLabel) ? verb : $"{verb} {itemLabel}";
        }

        if (tradePromptConfirmButton != null)
        {
            tradePromptConfirmButton.interactable = pendingUnitPrice >= 0;
        }

        SetupTradePromptPreview();
        RefreshTradePromptCurrencyIcon();

        tradePromptRoot.SetActive(true);
        UpdateTradePromptDisplay();
        return true;
    }

    private void SetupTradePromptPreview()
    {
        if (pendingTradeContext == null || tradePromptItemContainer == null || itemButtonPrefab == null)
        {
            ClearTradePromptPreview();
            return;
        }

        tradePromptItemHovered = false;

        if (tradePromptItemDisplay == null)
        {
            tradePromptItemDisplay = Instantiate(itemButtonPrefab, tradePromptItemContainer);
            tradePromptItemDisplay.PointerEntered += HandleTradePromptItemPointerEntered;
            tradePromptItemDisplay.PointerExited += HandleTradePromptItemPointerExited;
        }
        else
        {
            if (tradePromptItemDisplay.transform.parent != tradePromptItemContainer)
            {
                tradePromptItemDisplay.transform.SetParent(tradePromptItemContainer, false);
            }

            if (!tradePromptItemDisplay.gameObject.activeSelf)
            {
                tradePromptItemDisplay.gameObject.SetActive(true);
            }
        }

        InventoryUIController.TryClearTooltip(tradePromptItemDisplay);
        UpdateTradePromptPreview(Mathf.Clamp(GetSelectedTradeQuantity(), 1, pendingMaxQuantity));
    }

    private void UpdateTradePromptPreview(int quantity)
    {
        if (tradePromptItemDisplay == null || pendingTradeContext == null)
        {
            return;
        }

        if (pendingTradeContext.EntryType == ButtonEntry.Resource && pendingTradeContext.ResourceType != null)
        {
            tradePromptItemDisplay.SetResource(pendingTradeContext.ResourceType, Mathf.Max(1, quantity));
        }
        else if (pendingTradeContext.EntryType == ButtonEntry.Gear && pendingTradeContext.GearItem != null)
        {
            tradePromptItemDisplay.SetGearItem(pendingTradeContext.GearItem);
        }

        if (tradePromptItemHovered)
        {
            HandleTradePromptItemPointerEntered(tradePromptItemDisplay);
        }
    }

    private void ClearTradePromptPreview()
    {
        if (tradePromptItemDisplay == null)
        {
            return;
        }

        tradePromptItemHovered = false;
        InventoryUIController.TryClearTooltip(tradePromptItemDisplay);
        tradePromptItemDisplay.gameObject.SetActive(false);

        if (tradePromptItemContainer != null && tradePromptItemDisplay.transform.parent != tradePromptItemContainer)
        {
            tradePromptItemDisplay.transform.SetParent(tradePromptItemContainer, false);
        }
    }

    private void HandleTradePromptItemPointerEntered(InventoryItemButton button)
    {
        if (button == null || pendingTradeContext == null)
        {
            return;
        }

        tradePromptItemHovered = true;
        InventoryUIController.TradeTooltipData tradeData = BuildTradePromptPreviewTooltipData();

        if (pendingTradeContext.EntryType == ButtonEntry.Resource && pendingTradeContext.ResourceType != null)
        {
            InventoryUIController.TryRequestResourceTooltip(pendingTradeContext.ResourceType, button, tradeData);
        }
        else if (pendingTradeContext.EntryType == ButtonEntry.Gear && pendingTradeContext.GearItem != null)
        {
            InventoryUIController.TryRequestTooltip(pendingTradeContext.GearItem, button, tradeData);
        }
    }

    private void HandleTradePromptItemPointerExited(InventoryItemButton button)
    {
        tradePromptItemHovered = false;

        if (button != null)
        {
            InventoryUIController.TryClearTooltip(button);
        }
    }

    private InventoryUIController.TradeTooltipData BuildTradePromptPreviewTooltipData()
    {
        if (pendingTradeContext == null || activeTrader == null)
        {
            return InventoryUIController.TradeTooltipData.None;
        }

        if (pendingTradeAction == TradeAction.SellGear || pendingTradeAction == TradeAction.BuyGear)
        {
            string _;
            return BuildTradeTooltipData(pendingTradeContext, out _);
        }

        Sprite currencyIcon = activeTrader.CurrencyType != null ? activeTrader.CurrencyType.Icon : null;
        int unitPrice = Mathf.Max(0, pendingUnitPrice);
        bool showEach = pendingTradeContext != null && pendingTradeContext.Amount > 1;
        const string SellHeader = "Sell price";
        const string BuyHeader = "Buy price";
        string header;

        switch (pendingTradeAction)
        {
            case TradeAction.SellResource:
                header = SellHeader;
                break;
            case TradeAction.BuyResource:
                header = BuyHeader;
                break;
            default:
                return InventoryUIController.TradeTooltipData.None;
        }

        string valueText = BuildPriceValueText(unitPrice, showEach);
        return new InventoryUIController.TradeTooltipData(header, valueText, currencyIcon);
    }

    private void HandleTradePromptConfirm()
    {
        TraderButtonContext context = pendingTradeContext;
        TradeAction action = pendingTradeAction;
        int quantity = Mathf.Clamp(GetSelectedTradeQuantity(), 1, pendingMaxQuantity);

        bool success = ExecuteTradeImmediate(context, action, quantity);
        CloseTradePrompt();

        if (!success)
        {
            // Refresh sections if trade failed due to availability changes.
            RefreshAll();
        }
    }

    private void HandleTradePromptSliderChanged(float value)
    {
        UpdateTradePromptDisplay();
    }

    private void UpdateTradePromptDisplay()
    {
        if (!IsTradePromptConfigured())
        {
            return;
        }

        int quantity = Mathf.Clamp(GetSelectedTradeQuantity(), 1, pendingMaxQuantity);
        UpdateTradePromptPreview(quantity);

        if (tradePromptQuantitySlider != null && tradePromptQuantitySlider.gameObject.activeSelf)
        {
            if (tradePromptQuantityLabel != null)
            {
                tradePromptQuantityLabel.text = $"Quantity: {quantity} / {pendingMaxQuantity}";
            }
        }
        else if (tradePromptQuantityLabel != null)
        {
            tradePromptQuantityLabel.text = "Quantity: 1";
        }

        if (tradePromptUnitPriceLabel != null)
        {
            string unitText = FormatCurrency(pendingUnitPrice);
            tradePromptUnitPriceLabel.text = (pendingTradeAction == TradeAction.SellResource || pendingTradeAction == TradeAction.BuyResource)
                ? $"Price per unit: {unitText}"
                : $"Price per item: {unitText}";
        }

        if (tradePromptTotalPriceLabel != null)
        {
            int total = pendingUnitPrice * quantity;
            tradePromptTotalPriceLabel.text = FormatCurrency(total);
        }
    }

    private void CloseTradePrompt()
    {
        ClearTradePromptPreview();

        if (tradePromptRoot != null)
        {
            tradePromptRoot.SetActive(false);
        }

        pendingTradeContext = null;
        pendingUnitPrice = 0;
        pendingMaxQuantity = 1;

        if (tradePromptQuantitySlider != null)
        {
            tradePromptQuantitySlider.value = 1;
        }

        if (tradePromptQuantityLabel != null)
        {
            tradePromptQuantityLabel.text = string.Empty;
        }

        if (tradePromptUnitPriceLabel != null)
        {
            tradePromptUnitPriceLabel.text = string.Empty;
        }

        if (tradePromptTotalPriceLabel != null)
        {
            tradePromptTotalPriceLabel.text = string.Empty;
        }
    }

    private int GetSelectedTradeQuantity()
    {
        if (tradePromptQuantitySlider != null && tradePromptQuantitySlider.gameObject.activeSelf)
        {
            return Mathf.Clamp(Mathf.RoundToInt(tradePromptQuantitySlider.value), 1, pendingMaxQuantity);
        }

        return 1;
    }

    private bool ExecuteTradeImmediate(TraderButtonContext context, TradeAction action, int quantity)
    {
        int clampedQuantity = Mathf.Max(1, quantity);

        switch (action)
        {
            case TradeAction.SellGear:
                return SellGearToTrader(context, clampedQuantity);
            case TradeAction.SellResource:
                return SellResourceToTrader(context, clampedQuantity);
            case TradeAction.BuyGear:
                return BuyGearFromTrader(context, clampedQuantity);
            case TradeAction.BuyResource:
                return BuyResourceFromTrader(context, clampedQuantity);
            default:
                return false;
        }
    }

    private void HandlePointerEnter(TraderButtonContext context)
    {
        highlightedButton = context;

        InventoryUIController.TradeTooltipData tradeData = UpdateDetail(context);

        if (context == null)
        {
            return;
        }

        if (context.EntryType == ButtonEntry.Resource && context.ResourceType != null)
        {
            InventoryUIController.TryRequestResourceTooltip(context.ResourceType, context, tradeData);
        }
        else if (context.EntryType == ButtonEntry.Gear && context.GearItem != null)
        {
            InventoryUIController.TryRequestTooltip(context.GearItem, context, tradeData);
        }
    }

    private void HandlePointerExit(TraderButtonContext context)
    {
        InventoryUIController.TryClearTooltip(context);

        if (highlightedButton == context)
        {
            highlightedButton = null;
        }

        if (detailLabel != null)
        {
            detailLabel.text = string.Empty;
            detailLabel.gameObject.SetActive(false);
        }
    }

    private void HandleBeginDrag(TraderButtonContext context, PointerEventData eventData)
    {
        if (context == null || eventData == null)
        {
            return;
        }

        InventoryUIController.TryClearTooltip(context);

        dragContext = context;
        dragContext.SetInteractable(false);

        Sprite icon = null;
        if (context.EntryType == ButtonEntry.Gear && context.GearItem != null)
        {
            icon = context.GearItem.Icon;
        }
        else if (context.EntryType == ButtonEntry.Resource && context.ResourceType != null)
        {
            icon = context.ResourceType.Icon;
        }

        if (icon != null)
        {
            BeginDragVisual(icon);
            UpdateDragVisualPosition(eventData);
        }
    }

    private void HandleDrag(PointerEventData eventData)
    {
        UpdateDragVisualPosition(eventData);
    }

    private void HandleEndDrag(TraderButtonContext context, PointerEventData eventData)
    {
        if (context != dragContext)
        {
            return;
        }

        TraderButtonContext current = dragContext;
        dragContext = null;

        if (current != null)
        {
            current.SetInteractable(true);
        }

        bool overTraderZone = IsPointerOverZone(eventData, traderDropZone);
        bool overPlayerZone = IsPointerOverZone(eventData, playerDropZone);

        if (current != null)
        {
            if (current.OwnerType == ButtonOwner.Player && overTraderZone)
            {
                TryStartTrade(current);
            }
            else if (current.OwnerType == ButtonOwner.Trader && overPlayerZone)
            {
                TryStartTrade(current);
            }
        }

        CleanupDragVisual();
    }

    private bool IsPointerOverZone(PointerEventData eventData, RectTransform zone)
    {
        if (zone == null || eventData == null)
        {
            return false;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(zone, eventData.position, eventData.pressEventCamera);
    }

    private void BeginDragVisual(Sprite icon)
    {
        if (icon == null)
        {
            return;
        }

        if (dragIconImage == null)
        {
            GameObject go = new GameObject("Trader Drag Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dragIconRect = go.GetComponent<RectTransform>();
            dragIconImage = go.GetComponent<Image>();
            dragIconImage.raycastTarget = false;
        }

        RectTransform root = dragIconRoot != null ? dragIconRoot : GetComponentInParent<Canvas>()?.transform as RectTransform;
        if (root != null && dragIconRect.parent != root)
        {
            dragIconRect.SetParent(root, false);
        }

        dragIconImage.enabled = true;
        dragIconImage.sprite = icon;
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

        Vector3 position = eventData != null ? eventData.position : Input.mousePosition;
        dragIconRect.position = position;
    }

    private void CleanupDragVisual()
    {
        if (dragIconImage != null)
        {
            dragIconImage.enabled = false;
        }
    }

    private InventoryUIController.TradeTooltipData UpdateDetail(TraderButtonContext context)
    {
        string detailText;
        InventoryUIController.TradeTooltipData tradeData = BuildTradeTooltipData(context, out detailText);

        if (detailLabel != null)
        {
            if (string.IsNullOrEmpty(detailText))
            {
                detailLabel.text = string.Empty;
                detailLabel.gameObject.SetActive(false);
            }
            else
            {
                detailLabel.text = detailText;
                detailLabel.gameObject.SetActive(true);
            }
        }

        return tradeData;
    }

    private InventoryUIController.TradeTooltipData BuildTradeTooltipData(TraderButtonContext context, out string detailText)
    {
        detailText = string.Empty;

        if (context == null || activeTrader == null)
        {
            return InventoryUIController.TradeTooltipData.None;
        }

        ResourceTypeDef currency = activeTrader.CurrencyType;
        Sprite currencyIcon = currency != null ? currency.Icon : null;
        const string BuyHeader = "Buy price";
        const string SellHeader = "Sell price";

        if (context.EntryType == ButtonEntry.Gear && context.GearItem != null)
        {
            if (context.OwnerType == ButtonOwner.Player)
            {
                if (IsGearBlacklistedForTrader(context.GearItem, out string restrictionMessage))
                {
                    detailText = $"{context.GearItem.DisplayName}\n{restrictionMessage}";
                    return new InventoryUIController.TradeTooltipData(SellHeader, BuildPriceValueText(0, showEach: false), currencyIcon);
                }

                if (!activeTrader.TryGetPlayerSellPrice(context.GearItem, out int payout) || payout <= 0)
                {
                    const string notInterestedText = "Trader is not interested.";
                    detailText = $"{context.GearItem.DisplayName}\n{notInterestedText}";
                    return new InventoryUIController.TradeTooltipData(SellHeader, BuildPriceValueText(0, showEach: false), currencyIcon);
                }

                bool hasFunds = activeTrader.HasFunds(payout);
                string priceValue = BuildPriceValueText(payout, showEach: false);
                string detailPrice = FormatCurrency(payout);

                detailText = $"{context.GearItem.DisplayName}\n{detailPrice}";
                if (!hasFunds)
                {
                    detailText += "\nTrader lacks funds.";
                }

                return new InventoryUIController.TradeTooltipData(SellHeader, priceValue, currencyIcon);
            }
            else
            {
                if (!activeTrader.TryGetPlayerBuyPrice(context.GearItem, out int price))
                {
                    const string soldOutText = "Sold out.";
                    detailText = $"{context.GearItem.DisplayName}\n{soldOutText}";
                    return new InventoryUIController.TradeTooltipData(BuyHeader, BuildPriceValueText(0, showEach: false), currencyIcon);
                }

                string priceValue = BuildPriceValueText(price, showEach: false);
                string detailPrice = FormatCurrency(price);
                detailText = $"{context.GearItem.DisplayName}\n{detailPrice}";
                if (!HasEnoughPlayerCurrency(price))
                {
                    detailText += "\nNot enough currency.";
                }

                return new InventoryUIController.TradeTooltipData(BuyHeader, priceValue, currencyIcon);
            }
        }

        if (context.EntryType == ButtonEntry.Resource && context.ResourceType != null)
        {
            int amount = Mathf.Max(1, context.Amount);
            string itemLabel = amount > 1 ? $"{context.ResourceType.DisplayName} x{amount}" : context.ResourceType.DisplayName;
            bool showEach = amount > 1;

            if (context.OwnerType == ButtonOwner.Player)
            {
                if (IsResourceBlacklistedForTrader(context.ResourceType, out string restrictionMessage))
                {
                    detailText = $"{itemLabel}\n{restrictionMessage}";
                    return new InventoryUIController.TradeTooltipData(SellHeader, BuildPriceValueText(0, showEach: false), currencyIcon);
                }

                if (!activeTrader.TryGetPlayerSellPrice(context.ResourceType, out int payoutPerUnit) || payoutPerUnit <= 0)
                {
                    const string notInterestedText = "Trader is not interested.";
                    detailText = $"{itemLabel}\n{notInterestedText}";
                    return new InventoryUIController.TradeTooltipData(SellHeader, BuildPriceValueText(0, showEach: false), currencyIcon);
                }

                bool traderCanAfford = activeTrader.HasFunds(payoutPerUnit);
                string priceValue = BuildPriceValueText(payoutPerUnit, showEach);
                string detailPrice = FormatCurrency(payoutPerUnit);
                if (showEach)
                {
                    detailPrice += " each";
                }
                detailText = $"{itemLabel}\n{detailPrice}";

                if (!traderCanAfford)
                {
                    detailText += "\nTrader lacks funds.";
                }

                return new InventoryUIController.TradeTooltipData(SellHeader, priceValue, currencyIcon);
            }
            else
            {
                if (!activeTrader.TryGetPlayerBuyPrice(context.ResourceType, out int pricePerUnit))
                {
                    const string soldOutText = "Sold out.";
                    detailText = $"{itemLabel}\n{soldOutText}";
                    return new InventoryUIController.TradeTooltipData(BuyHeader, BuildPriceValueText(0, showEach: false), currencyIcon);
                }

                string priceValue = BuildPriceValueText(pricePerUnit, showEach);
                string detailPrice = FormatCurrency(pricePerUnit);
                if (showEach)
                {
                    detailPrice += " each";
                }
                detailText = $"{itemLabel}\n{detailPrice}";

                if (!HasEnoughPlayerCurrency(pricePerUnit))
                {
                    detailText += "\nNot enough currency.";
                }

                return new InventoryUIController.TradeTooltipData(BuyHeader, priceValue, currencyIcon);
            }
        }

        return InventoryUIController.TradeTooltipData.None;
    }

    private bool SellGearToTrader(TraderButtonContext context, int quantity)
    {
        if (activeTrader == null || playerInventory == null || context == null || context.GearItem == null)
        {
            return false;
        }

        GearItem item = context.GearItem;

        if (IsGearBlacklistedForTrader(item, out string restrictionMessage))
        {
            ShowFeedbackMessage(restrictionMessage);
            return false;
        }

        if (!playerInventory.Contains(item))
        {
            RefreshPlayerSection();
            ShowFeedbackMessage("Item is no longer in your inventory.");
            return false;
        }

        if (!activeTrader.TryGetPlayerSellPrice(item, out int payout) || payout <= 0)
        {
            ShowFeedbackMessage($"{activeTrader.TraderName} is not interested in {item.DisplayName}.");
            return false;
        }

        if (!activeTrader.HasFunds(payout))
        {
            ShowFeedbackMessage($"{activeTrader.TraderName} doesn't have enough {FormatCurrency(payout)}.");
            return false;
        }

        if (!playerInventory.Remove(item))
        {
            ShowFeedbackMessage("Failed to remove the item from your inventory.");
            return false;
        }

        if (!activeTrader.TryProcessPlayerSale(item, 1, out int totalPayout, out _))
        {
            playerInventory.Add(item);
            ShowFeedbackMessage("Trade failed. Try again.");
            return false;
        }

        GrantCurrencyToPlayer(totalPayout);
        HideFeedbackMessage();
        RefreshAll();
        return true;
    }

    private bool SellResourceToTrader(TraderButtonContext context, int quantity)
    {
        if (activeTrader == null || resourceManager == null || context == null || context.ResourceType == null)
        {
            return false;
        }

        int available = Mathf.Max(0, context.Amount);
        if (available <= 0)
        {
            ShowFeedbackMessage("This resource stack is empty.");
            return false;
        }

        quantity = Mathf.Clamp(quantity, 1, available);

        if (IsResourceBlacklistedForTrader(context.ResourceType, out string restrictionMessage))
        {
            ShowFeedbackMessage(restrictionMessage);
            return false;
        }

        if (!activeTrader.TryGetPlayerSellPrice(context.ResourceType, out int payoutPerUnit) || payoutPerUnit <= 0)
        {
            ShowFeedbackMessage($"{activeTrader.TraderName} is not interested in {context.ResourceType.DisplayName}.");
            return false;
        }

        int totalPayout = payoutPerUnit * quantity;
        if (!activeTrader.HasFunds(totalPayout))
        {
            ShowFeedbackMessage($"{activeTrader.TraderName} doesn't have enough {FormatCurrency(totalPayout)}.");
            return false;
        }

        ResourceSet cost = CreateSingleResourceSet(context.ResourceType, quantity);
        if (!resourceManager.TrySpendResources(cost, GetPlayerWorldPosition()))
        {
            ShowFeedbackMessage($"You need {context.ResourceType.DisplayName} x{quantity} to sell this stack.");
            return false;
        }

        if (!activeTrader.TryProcessPlayerSale(context.ResourceType, quantity, out totalPayout, out _))
        {
            resourceManager.GrantResources(cost, GetPlayerWorldPosition(), showFeedback: false, awardExperience: false);
            ShowFeedbackMessage("Trade failed. Try again.");
            return false;
        }

        GrantCurrencyToPlayer(totalPayout);
        HideFeedbackMessage();
        RefreshAll();
        return true;
    }

    private bool BuyGearFromTrader(TraderButtonContext context, int quantity)
    {
        if (activeTrader == null || playerInventory == null || resourceManager == null || context == null || context.GearItem == null)
        {
            return false;
        }

        if (!activeTrader.TryGetPlayerBuyPrice(context.GearItem, out int price) || price <= 0)
        {
            ShowFeedbackMessage("Item is no longer available.");
            RefreshTraderSection();
            return false;
        }

        if (!HasEnoughPlayerCurrency(price))
        {
            ShowFeedbackMessage($"You need {FormatCurrency(price)}.");
            return false;
        }

        if (!SpendPlayerCurrency(price))
        {
            ShowFeedbackMessage($"You need {FormatCurrency(price)}.");
            return false;
        }

        if (!activeTrader.TryProcessPlayerPurchase(context.GearItem, 1, out int chargedPrice))
        {
            GrantCurrencyToPlayer(price);
            RefreshTraderSection();
            ShowFeedbackMessage("Item is no longer available.");
            return false;
        }

        if (chargedPrice > price)
        {
            Debug.LogWarning($"Trader price mismatch for {context.GearItem.DisplayName}: expected {price}, charged {chargedPrice}.", this);
        }
        else if (chargedPrice < price)
        {
            GrantCurrencyToPlayer(price - chargedPrice);
        }

        playerInventory.Add(context.GearItem);
        HideFeedbackMessage();
        RefreshAll();
        return true;
    }

    private bool BuyResourceFromTrader(TraderButtonContext context, int quantity)
    {
        if (activeTrader == null || resourceManager == null || context == null || context.ResourceType == null)
        {
            return false;
        }

        int available = Mathf.Max(0, context.Amount);
        if (available <= 0)
        {
            ShowFeedbackMessage("This resource stack is empty.");
            return false;
        }

        quantity = Mathf.Clamp(quantity, 1, available);

        if (!activeTrader.TryGetPlayerBuyPrice(context.ResourceType, out int pricePerUnit) || pricePerUnit <= 0)
        {
            ShowFeedbackMessage("Resource is no longer available.");
            RefreshTraderSection();
            return false;
        }

        int totalCost = pricePerUnit * quantity;
        if (!HasEnoughPlayerCurrency(totalCost))
        {
            ShowFeedbackMessage($"You need {FormatCurrency(totalCost)}.");
            return false;
        }

        if (!SpendPlayerCurrency(totalCost))
        {
            ShowFeedbackMessage($"You need {FormatCurrency(totalCost)}.");
            return false;
        }

        if (!activeTrader.TryProcessPlayerPurchase(context.ResourceType, quantity, out int charged))
        {
            GrantCurrencyToPlayer(totalCost);
            RefreshTraderSection();
            ShowFeedbackMessage("Resource is no longer available.");
            return false;
        }

        if (charged > totalCost)
        {
            Debug.LogWarning($"Trader price mismatch for {context.ResourceType.DisplayName}: expected {totalCost}, charged {charged}.", this);
        }
        else if (charged < totalCost)
        {
            GrantCurrencyToPlayer(totalCost - charged);
        }

        ResourceSet grant = CreateSingleResourceSet(context.ResourceType, quantity);
        resourceManager.GrantResources(grant, GetPlayerWorldPosition(), showFeedback: true, awardExperience: false);
        HideFeedbackMessage();
        RefreshAll();
        return true;
    }

    private ResourceSet CreateSingleResourceSet(ResourceTypeDef type, int amount)
    {
        var set = new ResourceSet();
        set.Set(type, Mathf.Max(0, amount));
        return set;
    }

    private bool HasEnoughPlayerCurrency(int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        if (resourceManager == null || activeTrader == null || activeTrader.CurrencyType == null)
        {
            return false;
        }

        return resourceManager.Get(activeTrader.CurrencyType) >= amount;
    }

    private bool SpendPlayerCurrency(int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        if (resourceManager == null || activeTrader == null || activeTrader.CurrencyType == null)
        {
            return false;
        }

        ResourceSet cost = CreateSingleResourceSet(activeTrader.CurrencyType, amount);
        return resourceManager.TrySpendResources(cost, GetPlayerWorldPosition());
    }

    private void GrantCurrencyToPlayer(int amount)
    {
        if (amount <= 0 || resourceManager == null || activeTrader == null || activeTrader.CurrencyType == null)
        {
            return;
        }

        ResourceSet grant = CreateSingleResourceSet(activeTrader.CurrencyType, amount);
        resourceManager.GrantResources(grant, GetPlayerWorldPosition(), showFeedback: true, awardExperience: false);
    }

    private void ShowFeedbackMessage(string message)
    {
        if (feedbackText != null)
        {
            feedbackText.text = message ?? string.Empty;
        }

        if (feedbackPanel != null)
        {
            feedbackPanel.SetActive(true);
        }

        if (feedbackDisplayDuration > 0f)
        {
            if (feedbackCoroutine != null)
            {
                StopCoroutine(feedbackCoroutine);
            }

            feedbackCoroutine = StartCoroutine(HideFeedbackAfterDelay());
        }
    }

    private IEnumerator HideFeedbackAfterDelay()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, feedbackDisplayDuration));

        if (feedbackPanel != null)
        {
            feedbackPanel.SetActive(false);
        }

        if (feedbackText != null)
        {
            feedbackText.text = string.Empty;
        }

        feedbackCoroutine = null;
    }

    private void HideFeedbackMessage()
    {
        if (feedbackCoroutine != null)
        {
            StopCoroutine(feedbackCoroutine);
            feedbackCoroutine = null;
        }

        if (feedbackPanel != null)
        {
            feedbackPanel.SetActive(false);
        }

        if (feedbackText != null)
        {
            feedbackText.text = string.Empty;
        }
    }

    private bool IsGearBlacklistedForTrader(GearItem gearItem, out string message)
    {
        if (gearItem == null)
        {
            message = "Invalid item.";
            return true;
        }

        if (ContainsItem(globalDisallowedGear, gearItem))
        {
            message = $"{gearItem.DisplayName} cannot be sold to any trader.";
            return true;
        }

        if (activeTrader != null && ContainsItem(activeTrader.DisallowedGear, gearItem))
        {
            message = $"{activeTrader.TraderName} is not interested in {gearItem.DisplayName}.";
            return true;
        }

        message = string.Empty;
        return false;
    }

    private bool IsResourceBlacklistedForTrader(ResourceTypeDef resourceType, out string message)
    {
        if (resourceType == null)
        {
            message = "Invalid resource.";
            return true;
        }

        if (ContainsItem(globalDisallowedResources, resourceType))
        {
            message = $"{resourceType.DisplayName} cannot be sold to any trader.";
            return true;
        }

        if (activeTrader != null && ContainsItem(activeTrader.DisallowedResources, resourceType))
        {
            message = $"{activeTrader.TraderName} is not interested in {resourceType.DisplayName}.";
            return true;
        }

        message = string.Empty;
        return false;
    }

    private static bool ContainsItem<T>(IEnumerable<T> collection, T value)
        where T : class
    {
        if (collection == null || value == null)
        {
            return false;
        }

        foreach (T element in collection)
        {
            if (element == value)
            {
                return true;
            }
        }

        return false;
    }

    private void RefreshFundsLabel()
    {
        if (traderFundsLabel != null)
        {
            if (activeTrader != null)
            {
                traderFundsLabel.text = activeTrader.AvailableCurrency.ToString();
            }
            else
            {
                traderFundsLabel.text = string.Empty;
            }
        }

        RefreshTraderCurrencyIcon();
    }

    private string FormatCurrency(int amount)
    {
        if (activeTrader == null)
        {
            return amount.ToString();
        }

        string currencyName = activeTrader.CurrencyType != null ? activeTrader.CurrencyType.DisplayName : "Currency";
        return $"{amount} {currencyName}";
    }

    private string BuildPriceValueText(int unitPrice, bool showEach)
    {
        int clamped = Mathf.Max(0, unitPrice);
        string priceText = clamped.ToString();

        if (showEach)
        {
            return $"{priceText} each";
        }

        return priceText;
    }

    private void RefreshTraderCurrencyIcon()
    {
        if (traderCurrencyIcon == null)
        {
            return;
        }

        if (activeTrader != null && activeTrader.CurrencyType != null && activeTrader.CurrencyType.Icon != null)
        {
            traderCurrencyIcon.sprite = activeTrader.CurrencyType.Icon;
            traderCurrencyIcon.color = Color.white;
            traderCurrencyIcon.enabled = true;
        }
        else
        {
            traderCurrencyIcon.sprite = traderCurrencyDefaultSprite;
            traderCurrencyIcon.color = traderCurrencyDefaultColor;
            traderCurrencyIcon.enabled = traderCurrencyDefaultEnabled;
        }
    }

    private void RefreshTradePromptCurrencyIcon()
    {
        if (tradePromptCurrencyIcon == null)
        {
            return;
        }

        if (activeTrader != null && activeTrader.CurrencyType != null && activeTrader.CurrencyType.Icon != null)
        {
            tradePromptCurrencyIcon.sprite = activeTrader.CurrencyType.Icon;
            tradePromptCurrencyIcon.color = Color.white;
            tradePromptCurrencyIcon.enabled = true;
        }
        else
        {
            tradePromptCurrencyIcon.sprite = tradePromptCurrencyDefaultSprite;
            tradePromptCurrencyIcon.color = tradePromptCurrencyDefaultColor;
            tradePromptCurrencyIcon.enabled = tradePromptCurrencyDefaultEnabled;
        }
    }

    private Vector3 GetPlayerWorldPosition()
    {
        if (playerWorldAnchor != null)
        {
            return playerWorldAnchor.position;
        }

        if (playerInventory != null)
        {
            return playerInventory.transform.position;
        }

        return activeTrader != null ? activeTrader.transform.position : Vector3.zero;
    }
}
}















