using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
// DestructibleTileData.cs
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Tilemaps;
using SmallScale.FantasyKingdomTileset.Building;

// Legacy resource enum and bundle removed


public enum CraftingStationType
{
    None,
    Furnace,
    Blacksmith,
    Tannery,
    StoneCutter,
    Anvil,
    EnchantingTable,
    AlchemyTable,
    CookingFire,
    Loom,
    Workbench,
    Kiln,
    Smelter,
    Trader,
    SpawnPoint
}

[CreateAssetMenu(menuName="Destruction/Destructible Tile Data", fileName="DT_StoneWall")]
[MovedFrom(true, null, null, "DestructibleTileData")]
public class DestructibleTileData : ScriptableObject
{
    [Header("Match")]
    public TileBase sourceTile;

    [Header("Build Settings")]
    [SerializeField]
    [Tooltip("Unique identifier used when referencing this tile from the build system.")]
    private string buildPartId = string.Empty;

    [SerializeField]
    [Tooltip("Name displayed for this tile inside the build menu.")]
    private string buildDisplayName = string.Empty;

    [SerializeField]
    [Tooltip("Category that determines which build menu tab this tile appears in.")]
    private BuildPartCategory buildCategory = BuildPartCategory.Objects;

    [SerializeField]
    [Tooltip("Icon shown on the build menu for this tile.")]
    private Sprite buildIcon;

    [SerializeField]
    [Tooltip("Descriptive text shown alongside the resource cost in the build menu.")]
    [TextArea]
    private string buildInfoText = string.Empty;

    [Header("Unlocking")]
    [SerializeField]
    [Tooltip("Resource the player must discover once before this tile becomes available in the build menu. Leave empty to make it available from the start.")]
    private ResourceTypeDef unlockResourceRequirement;

    // Legacy build cost removed

    [SerializeField]
    [Tooltip("Optional list of directional tile variants that can be cycled during placement.")]
    private TileBase[] buildTileVariants = System.Array.Empty<TileBase>();

    [Header("Build Availability")]
    [Tooltip("When false, this tile will not appear in the player's build menu.")]
    [SerializeField] bool isBuildable = true;

    [Header("HP")]
    [Min(1)] public int maxHP = 3;

    [Header("On Destroy")]
    public bool clearTile = true;
    public TileBase destroyedTile;
    public GameObject destroyVfxPrefab;
    public float destroyVfxCleanup = 2f;

    [Header("Swap/Clear Timing")]
    [Min(0f)] public float swapDelay = 0f;
    public bool placeDestroyedOnBrokenMap = true;

    [Header("On Hit (non-lethal)")]
    public bool flashOnHit = true;
    public Color flashColor = new Color(1,0.25f,0.25f,1);
    public float flashHold = 0.05f, flashFade = 0.1f;

    [Header("On Hit Shake")]
    public bool shakeOnHit = true;
    [Tooltip("Total shake time in seconds.")]
    [Min(0f)] public float shakeDuration = 0.18f;
    [Tooltip("Max local position offset applied to the tile (units).")]
    public Vector2 shakePosAmplitude = new Vector2(0.06f, 0.06f);
    [Tooltip("Max Z rotation (degrees) applied to the tile.")]
    [Min(0f)] public float shakeRotAmplitude = 2.5f;
    [Tooltip("Shake frequency in noise samples per second.")]
    [Min(0.1f)] public float shakeFrequency = 22f;
    [Tooltip("0 = constant amplitude, 1 = strong ease-out.")]
    [Range(0f, 1f)] public float shakeFalloff = 0.85f;

    // existing staged VFX (kept as-is)
    public GameObject stagedVfxPrefab;
    public float stagedVfxCleanup = 0f;

    // NEW: impact VFX (fires EVERY non-lethal hit)
    [Header("On Hit Impact VFX (instant)")]
    public GameObject impactVfxPrefab;      // e.g. tiny spark, dust puff, hit ring
    public float impactVfxCleanup = 0f;     // 0 = infer lifetime from ParticleSystems (fallback 2s)

    [Header("Gear Drops")]
    public bool enableGearDrops = false;
    [Range(0f, 1f)] public float gearDropChance = 0.05f;
    [Range(0f, 1f)] public float gearDropChainMultiplier = 0.35f;
    [Range(1, 3)] public int gearDropMaxCount = 1;
    public LootPickup lootPickupPrefab;
    public GearItemDatabase gearDropDatabase;
    public bool useLocalGearPool = false;
    public List<GearItem> guaranteedGearDrops = new List<GearItem>();
    public List<GearItem> localRandomGearDrops = new List<GearItem>();
    public Vector2 lootScatter = new Vector2(0.25f, 0.25f);

    [Header("Resource Drops (Dynamic)")]
    public ResourceSet resourceDropsSet;

    [Header("Crafting Station")]
    public bool isCraftingStation;
    public CraftingStationType craftingStationType = CraftingStationType.None;

    [Header("Leveling")]
    [Tooltip("Experience awarded to the player when this tile is destroyed.")]
    public int experienceReward = 0;
    /// <summary>
    /// Gets the unique identifier associated with this tile for building purposes.
    /// A fallback identifier is generated from the source tile name when unspecified.
    /// </summary>
    public string BuildPartId
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(buildPartId))
            {
                return buildPartId;
            }

            if (sourceTile == null)
            {
                return name;
            }

            return SanitizeIdentifier(sourceTile.name);
        }
    }

    /// <summary>
    /// Gets the display name shown inside the build menu for this tile.
    /// Falls back to the base tile name without directional suffixes when not provided.
    /// </summary>
    public string BuildDisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(buildDisplayName))
            {
                return buildDisplayName;
            }

            if (sourceTile != null)
            {
                return ExtractBaseTileName(sourceTile.name);
            }

            return name;
        }
    }

    /// <summary>
    /// Gets the category this tile belongs to within the build menu.
    /// </summary>
    public BuildPartCategory BuildCategory => buildCategory;

    /// <summary>
    /// Backwards-compatible alias exposing the build category.
    /// </summary>
    public BuildPartCategory Category => buildCategory;

    /// <summary>
    /// Gets the icon that should represent this tile within the build menu.
    /// </summary>
    public Sprite BuildIcon => buildIcon;

    /// <summary>
    /// Backwards-compatible alias exposing the build icon.
    /// </summary>
    public Sprite Icon => buildIcon;

    /// <summary>
    /// Gets the descriptive text shown when hovering this tile in the build menu.
    /// </summary>
    public string BuildInfoText => buildInfoText;

    /// <summary>
    /// Backwards-compatible alias exposing the info text.
    /// </summary>
    public string InfoText => buildInfoText;

    /// <summary>
    /// Resource that must be acquired once to unlock this tile for building. Null means unlocked by default.
    /// </summary>
    public ResourceTypeDef UnlockResourceRequirement => unlockResourceRequirement;

    /// <summary>
    /// Gets a value indicating whether this tile requires an unlock before it can be built.
    /// </summary>
    public bool RequiresUnlock => unlockResourceRequirement != null;

    // Legacy cost properties removed; use ResourceCostSet

    /// <summary>
    /// Dynamic resource cost set. If defined (non-empty), dynamic systems should use this instead
    /// of the legacy ResourceBundle cost.
    /// </summary>
    public ResourceSet ResourceCostSet
    {
        get
        {
            if (resourceDropsSet != null && !resourceDropsSet.IsEmpty)
            {
                return resourceDropsSet;
            }

            return null;
        }
    }

    /// <summary>
    /// Backwards-compatible alias exposing the display name.
    /// </summary>
    public string DisplayName => BuildDisplayName;

    /// <summary>
    /// Gets whether this tile can be placed by the player via the build menu.
    /// </summary>
    public bool IsBuildable => isBuildable;

    /// <summary>
    /// Updates whether this tile should appear in the player's build menu.
    /// </summary>
    /// <param name="buildable">True to allow placement; false to hide from the menu.</param>
    public void SetBuildable(bool buildable)
    {
        isBuildable = buildable;
    }

    /// <summary>
    /// Backwards-compatible alias exposing the part identifier.
    /// </summary>
    public string PartId => BuildPartId;

    /// <summary>
    /// Backwards-compatible alias exposing the primary tile.
    /// </summary>
    public TileBase Tile => PrimaryTile;

    /// <summary>
    /// Gets the number of directional variants available for this tile.
    /// </summary>
    public int VariantCount
    {
        get
        {
            if (buildTileVariants != null && buildTileVariants.Length > 0)
            {
                return buildTileVariants.Length;
            }

            return sourceTile != null ? 1 : 0;
        }
    }

    /// <summary>
    /// Retrieves the tile variant at the provided index. When no variants are configured the source tile is returned.
    /// </summary>
    /// <param name="index">Index of the desired variant.</param>
    public TileBase GetVariant(int index)
    {
        int count = VariantCount;
        if (count == 0)
        {
            return null;
        }

        if (buildTileVariants != null && buildTileVariants.Length > 0)
        {
            int wrappedIndex = Mod(index, buildTileVariants.Length);
            return buildTileVariants[wrappedIndex];
        }

        return sourceTile;
    }

    /// <summary>
    /// Returns the primary tile variant used when placing this tile.
    /// </summary>
    public TileBase PrimaryTile => GetVariant(0);

    private void OnEnable() { }

#if UNITY_EDITOR
    private void OnValidate() { }
#endif

    // Legacy sync removed

    /// <summary>
    /// Assigns the available tile variants for this tile.
    /// </summary>
    /// <param name="variants">Variants to associate with this tile.</param>
    public void SetVariants(IReadOnlyList<TileBase> variants)
    {
        if (variants == null || variants.Count == 0)
        {
            buildTileVariants = System.Array.Empty<TileBase>();
            return;
        }

        buildTileVariants = new TileBase[variants.Count];
        for (int i = 0; i < variants.Count; i++)
        {
            buildTileVariants[i] = variants[i];
        }
    }

    /// <summary>
    /// Updates the build metadata stored on this tile.
    /// </summary>
    /// <param name="partIdValue">Identifier that should be used by the build system.</param>
    /// <param name="displayNameValue">Display name shown in the build menu.</param>
    /// <param name="categoryValue">Category assigned to the tile.</param>
    /// <param name="iconValue">Icon displayed in the build menu.</param>
    /// <param name="infoTextValue">Info text displayed alongside the resource cost.</param>
    /// <param name="costValue">Resources required to place the tile.</param>
    public void SetBuildMetadata(string partIdValue, string displayNameValue, BuildPartCategory categoryValue, Sprite iconValue, string infoTextValue, ResourceSet costValue)
    {
        buildPartId = partIdValue ?? string.Empty;
        buildDisplayName = displayNameValue ?? string.Empty;
        buildCategory = categoryValue;
        buildIcon = iconValue;
        buildInfoText = infoTextValue ?? string.Empty;
        resourceDropsSet = costValue;
    }

    /// <summary>
    /// Configures the resource required to unlock this tile for building.
    /// </summary>
    /// <param name="resource">Resource that must be discovered, or null to unlock by default.</param>
    public void SetUnlockResource(ResourceTypeDef resource)
    {
        unlockResourceRequirement = resource;
    }

    private static int Mod(int value, int modulus)
    {
        if (modulus == 0)
        {
            return 0;
        }

        int result = value % modulus;
        return result < 0 ? result + modulus : result;
    }

    private static string SanitizeIdentifier(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        raw = raw.Trim();
        StringBuilder builder = new StringBuilder(raw.Length);
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
            else if (char.IsWhiteSpace(c) || c == '-' || c == '_')
            {
                builder.Append('_');
            }
        }

        return builder.ToString();
    }

    private static string ExtractBaseTileName(string tileName)
    {
        if (string.IsNullOrWhiteSpace(tileName))
        {
            return string.Empty;
        }

        tileName = tileName.Trim();
        if (tileName.Length >= 2 && tileName[tileName.Length - 2] == '_' && IsDirectionLetter(tileName[tileName.Length - 1]))
        {
            return tileName.Substring(0, tileName.Length - 2);
        }

        return tileName;
    }

    private static bool IsDirectionLetter(char c)
    {
        c = char.ToUpperInvariant(c);
        return c == 'N' || c == 'S' || c == 'E' || c == 'W';
    }
}






}




