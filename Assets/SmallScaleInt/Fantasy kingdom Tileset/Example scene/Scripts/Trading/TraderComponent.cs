using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.EventSystems;
using SmallScale.FantasyKingdomTileset.Building;

namespace SmallScale.FantasyKingdomTileset
{
/// <summary>
/// Runtime trader component that can be attached to any NPC or interactable object.
/// Handles configurable stock, currency management, and interaction hooks so the
/// marketplace UI can open and process transactions.
/// </summary>
[DisallowMultipleComponent]
public class TraderComponent : MonoBehaviour
{
    [Serializable]
    public sealed class GearListing
    {
        public GearItem item;
        [Min(0)] public int quantity = 1;
        [Min(0)] public int playerBuyPrice = 10;   // price player pays to buy from trader
        [Min(0)] public int playerSellPrice = 5;   // price trader pays when buying from player
    }

    [Serializable]
    public sealed class ResourceListing
    {
        public ResourceTypeDef resource;
        [Min(0)] public int quantity = 0;              // total units available
        [Min(0)] public int playerBuyPricePerUnit = 1; // cost per unit when player buys
        [Min(0)] public int playerSellPricePerUnit = 1;// payout per unit when player sells
    }

    public readonly struct GearStockSnapshot
    {
        public GearStockSnapshot(GearItem item, int quantity, int buyPrice, int sellPrice)
        {
            Item = item;
            Quantity = quantity;
            PlayerBuyPrice = buyPrice;
            PlayerSellPrice = sellPrice;
        }

        public GearItem Item { get; }
        public int Quantity { get; }
        public int PlayerBuyPrice { get; }
        public int PlayerSellPrice { get; }
    }

    public readonly struct ResourceStockSnapshot
    {
        public ResourceStockSnapshot(ResourceTypeDef resource, int quantity, int buyPricePerUnit, int sellPricePerUnit)
        {
            Resource = resource;
            Quantity = quantity;
            PlayerBuyPricePerUnit = buyPricePerUnit;
            PlayerSellPricePerUnit = sellPricePerUnit;
        }

        public ResourceTypeDef Resource { get; }
        public int Quantity { get; }
        public int PlayerBuyPricePerUnit { get; }
        public int PlayerSellPricePerUnit { get; }
    }

    private sealed class GearRuntimeEntry
    {
        public GearRuntimeEntry(GearItem item)
        {
            Item = item;
        }

        public GearItem Item { get; }
        public int Quantity;
        public int PlayerBuyPrice;
        public int PlayerSellPrice;
    }

    private sealed class ResourceRuntimeEntry
    {
        public ResourceRuntimeEntry(ResourceTypeDef resource)
        {
            Resource = resource;
        }

        public ResourceTypeDef Resource { get; }
        public int Quantity;
        public int PlayerBuyPricePerUnit;
        public int PlayerSellPricePerUnit;
    }

    [Header("Trader Info")]
    [SerializeField]
    [Tooltip("Friendly display name shown in the UI header.")]
    private string traderName = "Trader";

    [SerializeField]
    [Tooltip("If enabled, left-clicking this object opens the trader panel.")]
    private bool openOnLeftClick = true;

    [Header("Currency")]
    [SerializeField]
    [Tooltip("Resource type used as currency (e.g., Coin).")]
    private ResourceTypeDef currencyType;

    [SerializeField]
    [Min(0)]
    [Tooltip("Starting amount of currency available for buying items from the player.")]
    private int startingCurrency = 100;

    [Header("Starting Stock")]
    [SerializeField]
    [Tooltip("Initial gear items offered for sale.")]
    private List<GearListing> startingGearStock = new List<GearListing>();

    [SerializeField]
    [Tooltip("Initial resources offered for sale (quantities in raw units).")]
    private List<ResourceListing> startingResourceStock = new List<ResourceListing>();

    [Header("Random Stock Generation")]
    [SerializeField]
    [Tooltip("If enabled, the trader ignores the configured starting stock and generates a random inventory when rebuilt.")]
    private bool generateRandomStockOnAwake = false;

    [SerializeField]
    [Tooltip("Resources eligible for random trader stock. If left null, falls back to the DynamicResourceManager database.")]
    private ResourceDatabase randomResourceDatabase;

    [SerializeField]
    [Tooltip("Gear items eligible for random trader stock. If left null, only the configured starting gear will be used.")]
    private GearItemDatabase randomGearDatabase;

    [Header("Trade Restrictions")]
    [SerializeField]
    [Tooltip("Specific gear items this trader will refuse to buy from the player.")]
    private List<GearItem> disallowedGear = new List<GearItem>();

    [SerializeField]
    [Tooltip("Specific resource types this trader will refuse to buy from the player.")]
    private List<ResourceTypeDef> disallowedResources = new List<ResourceTypeDef>();

    [Header("Fallback Pricing")]
    [SerializeField]
    [Min(0)]
    [Tooltip("Default resale price (what the player pays) if a newly acquired gear item had no prior listing.")]
    private int defaultGearPlayerBuyPrice = 25;

    [SerializeField]
    [Min(0)]
    [Tooltip("Default purchase price (what the trader pays the player) for gear without an existing listing.")]
    private int defaultGearPlayerSellPrice = 15;

    [SerializeField]
    [Min(0)]
    [Tooltip("Default resale price per unit for resources without an existing listing.")]
    private int defaultResourcePlayerBuyPricePerUnit = 3;

    [SerializeField]
    [Min(0)]
    [Tooltip("Default purchase price per unit for resources without an existing listing.")]
    private int defaultResourcePlayerSellPricePerUnit = 2;

    [SerializeField]
    [Min(1f)]
    [Tooltip("Markup applied when a newly acquired item has no resale price defined (must be >= 1).")]
    private float fallbackMarkupMultiplier = 1.2f;

    [Header("Selling Rules")]
    [SerializeField]
    [Tooltip("Allow the player to sell gear items that are not part of the starting stock.")]
    private bool allowUnknownGearSales = true;

    [SerializeField]
    [Tooltip("Allow the player to sell resource types that are not part of the starting stock.")]
    private bool allowUnknownResourceSales = true;

    private readonly Dictionary<GearItem, GearRuntimeEntry> gearRuntimeStock = new Dictionary<GearItem, GearRuntimeEntry>();
    private readonly Dictionary<ResourceTypeDef, ResourceRuntimeEntry> resourceRuntimeStock = new Dictionary<ResourceTypeDef, ResourceRuntimeEntry>();
    private int availableCurrency;

    private static readonly List<GearStockSnapshot> gearSnapshotBuffer = new List<GearStockSnapshot>();
    private static readonly List<ResourceStockSnapshot> resourceSnapshotBuffer = new List<ResourceStockSnapshot>();

    /// <summary>
    /// Raised whenever the trader's stock or funds change.
    /// </summary>
    public event Action<TraderComponent> InventoryChanged;

    /// <summary>
    /// Human-readable trader name.
    /// </summary>
    public string TraderName => string.IsNullOrWhiteSpace(traderName) ? name : traderName;

    /// <summary>
    /// Currency resource type.
    /// </summary>
    public ResourceTypeDef CurrencyType => currencyType;

    /// <summary>
    /// Current currency amount available to pay the player.
    /// </summary>
    public int AvailableCurrency => availableCurrency;

    /// <summary>
    /// Controls whether random stock generation is used when rebuilding.
    /// </summary>
    public bool UseRandomStockGeneration
    {
        get => generateRandomStockOnAwake;
        set => generateRandomStockOnAwake = value;
    }

    /// <summary>
    /// Enables or disables the automatic panel opening when this component is clicked in the scene.
    /// </summary>
    public bool OpenOnClick
    {
        get => openOnLeftClick;
        set => openOnLeftClick = value;
    }

    /// <summary>
    /// Overrides the trader display name shown in the UI header.
    /// </summary>
    public void SetTraderName(string displayName)
    {
        traderName = string.IsNullOrWhiteSpace(displayName) ? "Trader" : displayName;
    }

    public IReadOnlyList<GearItem> DisallowedGear => disallowedGear;
    public IReadOnlyList<ResourceTypeDef> DisallowedResources => disallowedResources;

    private void Awake()
    {
        RebuildRuntimeStock();
    }

    private void OnValidate()
    {
        fallbackMarkupMultiplier = Mathf.Max(1f, fallbackMarkupMultiplier);
        startingCurrency = Mathf.Max(0, startingCurrency);
        defaultGearPlayerBuyPrice = Mathf.Max(0, defaultGearPlayerBuyPrice);
        defaultGearPlayerSellPrice = Mathf.Max(0, defaultGearPlayerSellPrice);
        defaultResourcePlayerBuyPricePerUnit = Mathf.Max(0, defaultResourcePlayerBuyPricePerUnit);
        defaultResourcePlayerSellPricePerUnit = Mathf.Max(0, defaultResourcePlayerSellPricePerUnit);

        if (disallowedGear == null)
        {
            disallowedGear = new List<GearItem>();
        }

        if (disallowedResources == null)
        {
            disallowedResources = new List<ResourceTypeDef>();
        }

        if (startingGearStock != null)
        {
            for (int i = 0; i < startingGearStock.Count; i++)
            {
                GearListing listing = startingGearStock[i];
                if (listing == null)
                {
                    continue;
                }
                listing.quantity = Mathf.Max(0, listing.quantity);
                listing.playerBuyPrice = Mathf.Max(0, listing.playerBuyPrice);
                listing.playerSellPrice = Mathf.Max(0, listing.playerSellPrice);
            }
        }

        if (startingResourceStock != null)
        {
            for (int i = 0; i < startingResourceStock.Count; i++)
            {
                ResourceListing listing = startingResourceStock[i];
                if (listing == null)
                {
                    continue;
                }
                listing.quantity = Mathf.Max(0, listing.quantity);
                listing.playerBuyPricePerUnit = Mathf.Max(0, listing.playerBuyPricePerUnit);
                listing.playerSellPricePerUnit = Mathf.Max(0, listing.playerSellPricePerUnit);
            }
        }
    }

    /// <summary>
    /// Call to completely reset the runtime state back to the starting configuration.
    /// </summary>
    public void RebuildRuntimeStock()
    {
        gearRuntimeStock.Clear();
        resourceRuntimeStock.Clear();

        bool generated = false;
        if (generateRandomStockOnAwake)
        {
            generated = TryGenerateRandomStock();
            if (!generated)
            {
                Debug.LogWarning($"Trader '{TraderName}' failed to generate random stock. Falling back to configured starting stock.", this);
            }
        }

        if (!generated)
        {
            PopulateStockFromConfiguredLists();
        }

        availableCurrency = Mathf.Max(0, startingCurrency);
        NotifyInventoryChanged();
    }

    /// <summary>
    /// Forces a random stock rebuild regardless of the current generation flag.
    /// </summary>
    /// <param name="keepRandomFlag">When false, restores the previous random generation toggle after rebuilding.</param>
    public void RebuildRandomStock(bool keepRandomFlag = true)
    {
        bool previous = generateRandomStockOnAwake;
        generateRandomStockOnAwake = true;
        RebuildRuntimeStock();
        if (!keepRandomFlag)
        {
            generateRandomStockOnAwake = previous;
        }
    }

    private void PopulateStockFromConfiguredLists()
    {
        if (startingGearStock != null)
        {
            for (int i = 0; i < startingGearStock.Count; i++)
            {
                GearListing listing = startingGearStock[i];
                if (listing == null || listing.item == null || listing.quantity <= 0)
                {
                    continue;
                }

                int resolvedBuy = ResolveGearBuyPrice(listing.item, listing.playerBuyPrice);
                int resolvedSell = ResolveGearSellPrice(listing.item, listing.playerSellPrice);

                var entry = new GearRuntimeEntry(listing.item)
                {
                    Quantity = Mathf.Max(0, listing.quantity),
                    PlayerBuyPrice = resolvedBuy,
                    PlayerSellPrice = resolvedSell
                };
                gearRuntimeStock[listing.item] = entry;
            }
        }

        if (startingResourceStock != null)
        {
            for (int i = 0; i < startingResourceStock.Count; i++)
            {
                ResourceListing listing = startingResourceStock[i];
                if (listing == null || listing.resource == null || listing.quantity <= 0)
                {
                    continue;
                }

                int resolvedBuyPerUnit = ResolveResourceBuyPricePerUnit(listing.resource, listing.playerBuyPricePerUnit);
                int resolvedSellPerUnit = ResolveResourceSellPricePerUnit(listing.resource, listing.playerSellPricePerUnit);

                var entry = new ResourceRuntimeEntry(listing.resource)
                {
                    Quantity = Mathf.Max(0, listing.quantity),
                    PlayerBuyPricePerUnit = resolvedBuyPerUnit,
                    PlayerSellPricePerUnit = resolvedSellPerUnit
                };
                resourceRuntimeStock[listing.resource] = entry;
            }
        }
    }

    private bool TryGenerateRandomStock()
    {
        bool addedResources = GenerateRandomResources();
        bool addedGear = GenerateRandomGear();
        return addedResources || addedGear;
    }

    private bool GenerateRandomResources()
    {
        ResourceDatabase database = randomResourceDatabase;
        if (database == null)
        {
            DynamicResourceManager manager = DynamicResourceManager.Instance;
            if (manager != null)
            {
                database = manager.Database;
            }
        }

        IReadOnlyList<ResourceTypeDef> resources = database != null ? database.Resources : null;
        if (resources == null || resources.Count == 0)
        {
            return false;
        }

        bool addedAny = false;
        for (int i = 0; i < resources.Count; i++)
        {
            ResourceTypeDef resource = resources[i];
            if (resource == null || resource.IsCurrency)
            {
                continue;
            }

            float chance = GetRandomInclusionChance(resource.Rarity);
            if (chance <= 0f)
            {
                continue;
            }

            if (UnityEngine.Random.value > chance)
            {
                continue;
            }

            int quantity = GetRandomResourceQuantity(resource.Rarity);
            if (quantity <= 0)
            {
                continue;
            }

            int buyPerUnit = ResolveResourceBuyPricePerUnit(resource, 0);
            int sellPerUnit = ResolveResourceSellPricePerUnit(resource, 0);

            var entry = new ResourceRuntimeEntry(resource)
            {
                Quantity = quantity,
                PlayerBuyPricePerUnit = buyPerUnit,
                PlayerSellPricePerUnit = sellPerUnit
            };

            resourceRuntimeStock[resource] = entry;
            addedAny = true;
        }

        return addedAny;
    }

    private bool GenerateRandomGear()
    {
        GearItemDatabase database = randomGearDatabase;
        IReadOnlyList<GearItem> items = database != null ? database.Items : null;
        if (items == null || items.Count == 0)
        {
            return false;
        }

        bool addedAny = false;
        for (int i = 0; i < items.Count; i++)
        {
            GearItem gear = items[i];
            if (gear == null || !gear.CanAppearInRandomDrops)
            {
                continue;
            }

            float chance = GetRandomInclusionChance(gear.Rarity);
            if (chance <= 0f)
            {
                continue;
            }

            if (UnityEngine.Random.value > chance)
            {
                continue;
            }

            int quantity = GetRandomGearQuantity(gear.Rarity);
            if (quantity <= 0)
            {
                continue;
            }

            int buyPrice = ResolveGearBuyPrice(gear, 0);
            int sellPrice = ResolveGearSellPrice(gear, 0);

            var entry = new GearRuntimeEntry(gear)
            {
                Quantity = quantity,
                PlayerBuyPrice = buyPrice,
                PlayerSellPrice = sellPrice
            };

            gearRuntimeStock[gear] = entry;
            addedAny = true;
        }

        return addedAny;
    }

    private float GetRandomInclusionChance(GearRarity rarity)
    {
        switch (rarity)
        {
            case GearRarity.Common:
                return 0.80f;
            case GearRarity.Uncommon:
                return 0.10f;
            case GearRarity.Rare:
                return 0.05f;
            case GearRarity.Epic:
                return 0.01f;
            case GearRarity.Legendary:
                return 0.0001f; // 0.01%
            default:
                return 0.05f;
        }
    }

    private int GetRandomResourceQuantity(GearRarity rarity)
    {
        switch (rarity)
        {
            case GearRarity.Common:
                return UnityEngine.Random.Range(40, 61);   // ~50 on average
            case GearRarity.Uncommon:
                return UnityEngine.Random.Range(15, 31);
            case GearRarity.Rare:
                return UnityEngine.Random.Range(5, 11);
            case GearRarity.Epic:
                return UnityEngine.Random.Range(1, 4);
            case GearRarity.Legendary:
                return 1;
            default:
                return UnityEngine.Random.Range(10, 21);
        }
    }

    private int GetRandomGearQuantity(GearRarity rarity)
    {
        switch (rarity)
        {
            case GearRarity.Common:
                return UnityEngine.Random.Range(1, 4);
            case GearRarity.Uncommon:
                return UnityEngine.Random.Range(1, 3);
            case GearRarity.Rare:
            case GearRarity.Epic:
            case GearRarity.Legendary:
                return 1;
            default:
                return 1;
        }
    }

    /// <summary>
    /// Provides a snapshot of gear stock for UI rendering.
    /// </summary>
    public IReadOnlyList<GearStockSnapshot> GetGearStockSnapshot()
    {
        gearSnapshotBuffer.Clear();
        foreach (KeyValuePair<GearItem, GearRuntimeEntry> pair in gearRuntimeStock)
        {
            GearRuntimeEntry entry = pair.Value;
            if (entry == null || entry.Quantity <= 0 || entry.Item == null)
            {
                continue;
            }

            gearSnapshotBuffer.Add(new GearStockSnapshot(entry.Item, entry.Quantity, entry.PlayerBuyPrice, entry.PlayerSellPrice));
        }
        return gearSnapshotBuffer;
    }

    /// <summary>
    /// Provides a snapshot of resource stock for UI rendering.
    /// </summary>
    public IReadOnlyList<ResourceStockSnapshot> GetResourceStockSnapshot()
    {
        resourceSnapshotBuffer.Clear();
        foreach (KeyValuePair<ResourceTypeDef, ResourceRuntimeEntry> pair in resourceRuntimeStock)
        {
            ResourceRuntimeEntry entry = pair.Value;
            if (entry == null || entry.Quantity <= 0 || entry.Resource == null)
            {
                continue;
            }

            resourceSnapshotBuffer.Add(new ResourceStockSnapshot(entry.Resource, entry.Quantity, entry.PlayerBuyPricePerUnit, entry.PlayerSellPricePerUnit));
        }
        return resourceSnapshotBuffer;
    }

    /// <summary>
    /// Attempts to reserve and deduct gear stock when the player buys from the trader.
    /// </summary>
    public bool TryProcessPlayerPurchase(GearItem item, int quantity, out int totalPrice)
    {
        totalPrice = 0;
        if (item == null || quantity <= 0)
        {
            return false;
        }

        if (!gearRuntimeStock.TryGetValue(item, out GearRuntimeEntry entry) || entry == null || entry.Quantity < quantity)
        {
            return false;
        }

        int pricePerItem = Mathf.Max(0, entry.PlayerBuyPrice);
        totalPrice = pricePerItem * quantity;

        entry.Quantity -= quantity;
        if (entry.Quantity <= 0)
        {
            gearRuntimeStock.Remove(item);
        }

        AddCurrency(totalPrice);
        NotifyInventoryChanged();
        return true;
    }

    /// <summary>
    /// Attempts to reserve and deduct resource stock when the player buys from the trader.
    /// </summary>
    public bool TryProcessPlayerPurchase(ResourceTypeDef resource, int amount, out int totalPrice)
    {
        totalPrice = 0;
        if (resource == null || amount <= 0)
        {
            return false;
        }

        if (!resourceRuntimeStock.TryGetValue(resource, out ResourceRuntimeEntry entry) || entry == null || entry.Quantity < amount)
        {
            return false;
        }

        int pricePerUnit = Mathf.Max(0, entry.PlayerBuyPricePerUnit);
        totalPrice = pricePerUnit * amount;

        entry.Quantity -= amount;
        if (entry.Quantity <= 0)
        {
            resourceRuntimeStock.Remove(resource);
        }

        AddCurrency(totalPrice);
        NotifyInventoryChanged();
        return true;
    }

    /// <summary>
    /// Attempts to buy gear from the player and add it to stock.
    /// </summary>
    public bool TryProcessPlayerSale(GearItem item, int quantity, out int totalPayout, out int resalePricePerItem)
    {
        totalPayout = 0;
        resalePricePerItem = 0;

        if (item == null || quantity <= 0)
        {
            return false;
        }

        if (IsGearDisallowed(item))
        {
            return false;
        }

        int payoutPerItem;
        GearRuntimeEntry existingEntry = null;
        if (gearRuntimeStock.TryGetValue(item, out existingEntry) && existingEntry != null)
        {
            payoutPerItem = Mathf.Max(0, existingEntry.PlayerSellPrice);
        }
        else
        {
            if (!allowUnknownGearSales)
            {
                return false;
            }

            payoutPerItem = ResolveGearSellPrice(item, 0);
            if (payoutPerItem <= 0)
            {
                return false;
            }
        }

        totalPayout = payoutPerItem * quantity;
        if (totalPayout > availableCurrency)
        {
            return false;
        }

        availableCurrency -= totalPayout;

        int buyPrice = existingEntry != null ? existingEntry.PlayerBuyPrice : ResolveGearBuyPrice(item, 0);
        if (buyPrice <= 0)
        {
            buyPrice = Mathf.CeilToInt(payoutPerItem * fallbackMarkupMultiplier);
        }
        if (buyPrice < payoutPerItem)
        {
            buyPrice = payoutPerItem;
        }

        if (existingEntry != null)
        {
            existingEntry.Quantity += quantity;
            existingEntry.PlayerSellPrice = payoutPerItem;
            existingEntry.PlayerBuyPrice = buyPrice;
        }
        else
        {
            existingEntry = new GearRuntimeEntry(item)
            {
                Quantity = quantity,
                PlayerBuyPrice = buyPrice,
                PlayerSellPrice = payoutPerItem
            };
            gearRuntimeStock[item] = existingEntry;
        }

        resalePricePerItem = existingEntry.PlayerBuyPrice;
        NotifyInventoryChanged();
        return true;
    }

    /// <summary>
    /// Attempts to buy resources from the player and add them to stock.
    /// </summary>
    public bool TryProcessPlayerSale(ResourceTypeDef resource, int amount, out int totalPayout, out int resalePricePerUnit)
    {
        totalPayout = 0;
        resalePricePerUnit = 0;

        if (resource == null || amount <= 0)
        {
            return false;
        }

        if (IsResourceDisallowed(resource))
        {
            return false;
        }

        int payoutPerUnit;
        ResourceRuntimeEntry existingEntry = null;
        if (resourceRuntimeStock.TryGetValue(resource, out existingEntry) && existingEntry != null)
        {
            payoutPerUnit = Mathf.Max(0, existingEntry.PlayerSellPricePerUnit);
        }
        else
        {
            if (!allowUnknownResourceSales)
            {
                return false;
            }

            payoutPerUnit = ResolveResourceSellPricePerUnit(resource, 0);
            if (payoutPerUnit <= 0)
            {
                return false;
            }
        }

        totalPayout = payoutPerUnit * amount;
        if (totalPayout > availableCurrency)
        {
            return false;
        }

        availableCurrency -= totalPayout;

        int buyPricePerUnit = existingEntry != null ? existingEntry.PlayerBuyPricePerUnit : ResolveResourceBuyPricePerUnit(resource, 0);
        if (buyPricePerUnit <= 0)
        {
            buyPricePerUnit = Mathf.CeilToInt(payoutPerUnit * fallbackMarkupMultiplier);
        }
        if (buyPricePerUnit < payoutPerUnit)
        {
            buyPricePerUnit = payoutPerUnit;
        }

        if (existingEntry != null)
        {
            existingEntry.Quantity += amount;
            existingEntry.PlayerSellPricePerUnit = payoutPerUnit;
            existingEntry.PlayerBuyPricePerUnit = buyPricePerUnit;
        }
        else
        {
            existingEntry = new ResourceRuntimeEntry(resource)
            {
                Quantity = amount,
                PlayerBuyPricePerUnit = buyPricePerUnit,
                PlayerSellPricePerUnit = payoutPerUnit
            };
            resourceRuntimeStock[resource] = existingEntry;
        }

        resalePricePerUnit = existingEntry.PlayerBuyPricePerUnit;
        NotifyInventoryChanged();
        return true;
    }

    /// <summary>
    /// Returns the price the player would pay per gear item when buying from the trader.
    /// </summary>
    public bool TryGetPlayerBuyPrice(GearItem item, out int pricePerItem)
    {
        if (item == null)
        {
            pricePerItem = 0;
            return false;
        }

        if (gearRuntimeStock.TryGetValue(item, out GearRuntimeEntry entry) && entry != null && entry.Quantity > 0)
        {
            pricePerItem = Mathf.Max(0, entry.PlayerBuyPrice);
            return true;
        }

        pricePerItem = 0;
        return false;
    }

    /// <summary>
    /// Returns the price the trader would pay per gear item when buying from the player.
    /// </summary>
    public bool TryGetPlayerSellPrice(GearItem item, out int pricePerItem)
    {
        if (item == null)
        {
            pricePerItem = 0;
            return false;
        }

        if (IsGearDisallowed(item))
        {
            pricePerItem = 0;
            return false;
        }

        if (gearRuntimeStock.TryGetValue(item, out GearRuntimeEntry entry) && entry != null)
        {
            pricePerItem = Mathf.Max(0, entry.PlayerSellPrice);
            return pricePerItem > 0 || allowUnknownGearSales;
        }

        if (!allowUnknownGearSales)
        {
            pricePerItem = 0;
            return false;
        }

        pricePerItem = ResolveGearSellPrice(item, 0);
        return pricePerItem > 0;
    }

    /// <summary>
    /// Returns the price the player would pay per resource unit when buying from the trader.
    /// </summary>
    public bool TryGetPlayerBuyPrice(ResourceTypeDef resource, out int pricePerUnit)
    {
        if (resource == null)
        {
            pricePerUnit = 0;
            return false;
        }

        if (resourceRuntimeStock.TryGetValue(resource, out ResourceRuntimeEntry entry) && entry != null && entry.Quantity > 0)
        {
            pricePerUnit = Mathf.Max(0, entry.PlayerBuyPricePerUnit);
            return true;
        }

        pricePerUnit = 0;
        return false;
    }

    /// <summary>
    /// Returns the price the trader would pay per resource unit when buying from the player.
    /// </summary>
    public bool TryGetPlayerSellPrice(ResourceTypeDef resource, out int pricePerUnit)
    {
        if (resource == null)
        {
            pricePerUnit = 0;
            return false;
        }

        if (IsResourceDisallowed(resource))
        {
            pricePerUnit = 0;
            return false;
        }

        if (resourceRuntimeStock.TryGetValue(resource, out ResourceRuntimeEntry entry) && entry != null)
        {
            pricePerUnit = Mathf.Max(0, entry.PlayerSellPricePerUnit);
            return pricePerUnit > 0 || allowUnknownResourceSales;
        }

        if (!allowUnknownResourceSales)
        {
            pricePerUnit = 0;
            return false;
        }

        pricePerUnit = ResolveResourceSellPricePerUnit(resource, 0);
        return pricePerUnit > 0;
    }

    /// <summary>
    /// Tests if the trader has enough currency to cover the supplied payout.
    /// </summary>
    public bool HasFunds(int requiredAmount)
    {
        return requiredAmount <= availableCurrency;
    }

    public bool IsGearDisallowed(GearItem item)
    {
        return item != null && disallowedGear != null && disallowedGear.Contains(item);
    }

    public bool IsResourceDisallowed(ResourceTypeDef resource)
    {
        return resource != null && disallowedResources != null && disallowedResources.Contains(resource);
    }

    /// <summary>
    /// Opens the linked trader UI if available.
    /// </summary>
    public void OpenTraderPanel()
    {
        TraderPanelController panel = TraderPanelController.Instance;
        if (panel != null)
        {
            panel.OpenTrader(this);
        }
        else
        {
            Debug.LogWarning($"No TraderPanelController in scene to open for trader '{TraderName}'.", this);
        }
    }

    private void OnMouseDown()
    {
        if (!openOnLeftClick || !enabled || !gameObject.activeInHierarchy)
        {
            return;
        }

        if (BuildMenuController.IsAnyMenuOpen)
        {
            return;
        }

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem != null && eventSystem.IsPointerOverGameObject())
        {
            return;
        }

        // OnMouseDown always corresponds to left button for 2D/3D colliders.
        OpenTraderPanel();
    }

    private void AddCurrency(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        long next = (long)availableCurrency + amount;
        availableCurrency = next > int.MaxValue ? int.MaxValue : (int)next;
    }

    private void NotifyInventoryChanged()
    {
        InventoryChanged?.Invoke(this);
    }

    private int ResolveGearBuyPrice(GearItem item, int configuredPrice)
    {
        if (configuredPrice > 0)
        {
            return configuredPrice;
        }

        if (item != null)
        {
            int itemDefault = item.DefaultPlayerBuyPrice;
            if (itemDefault > 0)
            {
                return itemDefault;
            }
        }

        return Mathf.Max(0, defaultGearPlayerBuyPrice);
    }

    private int ResolveGearSellPrice(GearItem item, int configuredPrice)
    {
        if (configuredPrice > 0)
        {
            return configuredPrice;
        }

        if (item != null)
        {
            int itemDefault = item.DefaultPlayerSellPrice;
            if (itemDefault > 0)
            {
                return itemDefault;
            }
        }

        return Mathf.Max(0, defaultGearPlayerSellPrice);
    }

    private int ResolveResourceBuyPricePerUnit(ResourceTypeDef resource, int configuredPrice)
    {
        if (configuredPrice > 0)
        {
            return configuredPrice;
        }

        if (resource != null)
        {
            int resourceDefault = resource.DefaultPlayerBuyPricePerUnit;
            if (resourceDefault > 0)
            {
                return resourceDefault;
            }
        }

        return Mathf.Max(0, defaultResourcePlayerBuyPricePerUnit);
    }

    private int ResolveResourceSellPricePerUnit(ResourceTypeDef resource, int configuredPrice)
    {
        if (configuredPrice > 0)
        {
            return configuredPrice;
        }

        if (resource != null)
        {
            int resourceDefault = resource.DefaultPlayerSellPricePerUnit;
            if (resourceDefault > 0)
            {
                return resourceDefault;
            }
        }

        return Mathf.Max(0, defaultResourcePlayerSellPricePerUnit);
    }
}
}











