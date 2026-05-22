// Cleaned and consolidated profile definition
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using SmallScale.FantasyKingdomTileset.Building;
using SmallScale.FantasyKingdomTileset;

namespace SmallScale.FantasyKingdomTileset.EditorTools
{
[CreateAssetMenu(menuName = "Destruction/Destructible Tile Batch Profile", fileName = "DestructibleTileBatchProfile")]
public class DestructibleTileBatchProfile : ScriptableObject
{
    [Serializable]
    public class Rule
    {
        public string prefix = "Flora";
        public bool enabled = true;

        [Header("Core")]
        [Min(1)] public int maxHP = 3;
        public bool clearTile = true;
        public TileBase destroyedTile;
        public GameObject destroyVfxPrefab;
        [Min(0f)] public float destroyVfxCleanup = 2f;

        [Header("Swap/Clear Timing")]
        [Min(0f)] public float swapDelay = 0f;
        public bool placeDestroyedOnBrokenMap = true;

        [Header("On Hit (non-lethal)")]
        public bool flashOnHit = true;
        public Color flashColor = new Color(1, 0.25f, 0.25f, 1);
        [Min(0f)] public float flashHold = 0.05f;
        [Min(0f)] public float flashFade = 0.1f;

        public GameObject stagedVfxPrefab;
        [Min(0f)] public float stagedVfxCleanup = 0f;

        public GameObject impactVfxPrefab;
        [Min(0f)] public float impactVfxCleanup = 0f;

        [Header("Gear Drops")]
        public bool enableGearDrops = false;
        [Range(0f, 1f)] public float gearDropChance = 0f;
        [Range(0f, 1f)] public float gearDropChainMultiplier = 0.35f;
        [Range(1, 3)] public int gearDropMaxCount = 1;
        public bool useLocalGearPool = false;
        public Vector2 lootScatter = new Vector2(0.25f, 0.25f);
        public List<GearItem> guaranteedGearDrops = new List<GearItem>();
        public List<GearItem> localRandomGearDrops = new List<GearItem>();
        [Tooltip("Additional tile asset names that should inherit this rule's settings.")]
        public List<string> aliasTileNames = new List<string>();

        [Header("Resource Drops (Dynamic)")]
        public ResourceSet resourceDrops;

        [Header("Build Settings")]
        public BuildPartCategory buildCategory = BuildPartCategory.Objects;
        public bool isBuildable = true;
        [Tooltip("Optional prefix appended to the generated build part identifier.")]
        public string partIdPrefix = string.Empty;
        [Tooltip("Optional override applied when generating the display name. Leave empty to use the tile base name.")]
        public string displayNameOverride = string.Empty;
        [Tooltip("Icon used for tiles matching this rule inside the build menu. Overrides can still be assigned per asset later.")]
        public Sprite buildIcon;
        [Tooltip("Info text displayed when hovering the tile in the build menu.")]
        [TextArea] public string buildInfoText = string.Empty;
        [Tooltip("Resource the player must acquire to unlock this tile in the build menu. Leave empty to unlock immediately.")]
        public ResourceTypeDef unlockResource;

        [Header("Crafting Station")]
        public bool isCraftingStation;
        public CraftingStationType stationType = CraftingStationType.None;
    }

    [Serializable]
    public class Defaults : Rule
    {
        public Defaults()
        {
            prefix = "<DEFAULT>";
        }
    }

    [Header("Folders & DB")]
    public UnityEditor.DefaultAsset sourceFolder;
    public UnityEditor.DefaultAsset outputFolder;

    public DestructibleTileDatabase database;
    public ResourceDatabase resourceDatabase;
    [Header("Gear Drop Defaults")]
    public LootPickup defaultLootPickupPrefab;
    public GearItemDatabase defaultGearDatabase;

    [Header("Behavior")]
    public bool overwriteExisting = true;
    public bool dryRun = false;

    [Header("Rules")]
    public List<Rule> rules = new List<Rule>();
    public Defaults defaultRule = new Defaults();
}
}
#endif




