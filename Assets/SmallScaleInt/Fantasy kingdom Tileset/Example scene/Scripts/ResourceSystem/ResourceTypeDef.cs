using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using UnityEngine;
using SmallScale.FantasyKingdomTileset;

/// <summary>
/// Defines a resource type in a data-driven way. Create assets of this type for
/// each material (e.g., Wood, Stone, Copper) you want to support.
/// </summary>
public enum CraftableResourceCategory
{
    Metal,
    Wood,
    Leather,
    Cloth,
    Stone
}

[CreateAssetMenu(fileName = "ResourceType", menuName = "Resources/Resource Type")]
[MovedFrom(true, null, null, "ResourceTypeDef")]
public class ResourceTypeDef : ScriptableObject
{
    [SerializeField]
    [Tooltip("Stable unique identifier. Auto-assigned if empty.")]
    private string id = System.Guid.NewGuid().ToString();

    [SerializeField]
    [Tooltip("Display name used in UI.")]
    private string displayName = "New Resource";

    [SerializeField]
    [Tooltip("Icon used in UI panels.")]
    private Sprite icon;

    [SerializeField]
    [TextArea]
    [Tooltip("Optional note shown in the resource tooltip.")]
    private string note = string.Empty;

    [Header("Appearance")]
    [SerializeField]
    [Tooltip("Legacy default color used for UI text or effects. Not used for rarity tinting.")]
    private Color color = Color.white;

    [SerializeField]
    [Tooltip("Rarity tier used for UI coloring. For resources, Common is white; other tiers match gear.")]
    private GearRarity rarity = GearRarity.Common;

    [Header("Stacking")]
    [SerializeField]
    [Min(1)]
    [Tooltip("Max amount per inventory stack when shown in the inventory UI.")]
    private int maxStackSize = 100;

    [Header("Experience")]
    [SerializeField]
    [Tooltip("Experience awarded per unit when this resource is gathered or otherwise granted to the player.")]
    [Min(0)]
    private int experiencePerUnit = 0;

    [Header("Currency")]
    [SerializeField]
    [Tooltip("If enabled, this resource will be treated as a currency and shown in the currency panel instead of the standard resource list.")]
    private bool isCurrency;

    [Header("Trading")]
    [SerializeField, Min(0)]
    [Tooltip("Default price per unit the player pays when purchasing this resource from most traders. Traders can override by configuring a price above zero.")]
    private int defaultPlayerBuyPricePerUnit = 0;

    [SerializeField, Min(0)]
    [Tooltip("Default price per unit the player receives when selling this resource to most traders. Traders can override by configuring a price above zero.")]
    private int defaultPlayerSellPricePerUnit = 0;

    [Header("Crafting")]
    [SerializeField]
    [Tooltip("When enabled the resource can be crafted at the appropriate crafting station.")]
    private bool isCraftable = false;

    [SerializeField]
    [Tooltip("Category used to determine which crafting stations can make this resource.")]
    private CraftableResourceCategory craftCategory = CraftableResourceCategory.Metal;

    [SerializeField]
    [Tooltip("Resources required to craft one unit of this resource.")]
    private ResourceSet craftingCost = new ResourceSet();

    [SerializeField]
    [Min(1)]
    [Tooltip("How many units of this resource are produced per craft action.")]
    private int craftYield = 1;

    public string Id => id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public Sprite Icon => icon;
    public Color Color => color;
    public GearRarity Rarity => rarity;
    public Color RarityColor => rarity == GearRarity.Common ? Color.white : GearItem.GetColorForRarity(rarity);
    public string Note => note ?? string.Empty;
    public int MaxStackSize => Mathf.Max(1, maxStackSize);
    public int ExperiencePerUnit => Mathf.Max(0, experiencePerUnit);
    public bool IsCurrency => isCurrency;
    public int DefaultPlayerBuyPricePerUnit => Mathf.Max(0, defaultPlayerBuyPricePerUnit);
    public int DefaultPlayerSellPricePerUnit => Mathf.Max(0, defaultPlayerSellPricePerUnit);
    public bool IsCraftable => isCraftable;
    public CraftableResourceCategory CraftCategory => craftCategory;
    public ResourceSet CraftingCost => craftingCost;
    public int CraftYield => Mathf.Max(1, craftYield);

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            id = System.Guid.NewGuid().ToString();
        }
        if (maxStackSize < 1)
        {
            maxStackSize = 1;
        }
        if (experiencePerUnit < 0)
        {
            experiencePerUnit = 0;
        }
        if (defaultPlayerBuyPricePerUnit < 0)
        {
            defaultPlayerBuyPricePerUnit = 0;
        }
        if (defaultPlayerSellPricePerUnit < 0)
        {
            defaultPlayerSellPricePerUnit = 0;
        }
        if (craftYield < 1)
        {
            craftYield = 1;
        }
        if (craftingCost == null)
        {
            craftingCost = new ResourceSet();
        }
    }
}



}




