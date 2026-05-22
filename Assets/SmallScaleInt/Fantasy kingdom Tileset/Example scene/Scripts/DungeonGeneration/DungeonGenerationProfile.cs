using System;
using System.Collections.Generic;
using UnityEngine;
using SmallScale.FantasyKingdomTileset;
using UnityEngine.Tilemaps;

namespace DungeonGeneration
{
    /// <summary>
    /// Scriptable object encapsulating all tunable settings for procedural dungeon generation.
    /// Profiles allow designers to author different dungeon experiences without touching code.
    /// </summary>
    [CreateAssetMenu(menuName = "Dungeon Generation/Dungeon Generation Profile", fileName = "DungeonGenerationProfile")]
    public class DungeonGenerationProfile : ScriptableObject
    {
        [Header("Layout")]
        [SerializeField] private DungeonLayoutSettings layout = new DungeonLayoutSettings();

        [Header("Placement")]
        [SerializeField] private DungeonPlacementSettings placement = new DungeonPlacementSettings();

        [Header("Tiles")]
        [SerializeField] private DungeonTilePalette tilePalette = new DungeonTilePalette();

        [Header("Floor Details")]
        [SerializeField] private DungeonDetailSettings details = new DungeonDetailSettings();

        [Header("Object Tiles")]
        [SerializeField] private DungeonObjectTileSettings objectTiles = new DungeonObjectTileSettings();

        [Header("Spawns")]
        [SerializeField] private DungeonSpawnSettings spawns = new DungeonSpawnSettings();

        [Header("Portals")]
        [SerializeField] private DungeonPortalSettings portals = new DungeonPortalSettings();

        [Header("Templates")]
        [SerializeField] private DungeonTemplateSettings templates = new DungeonTemplateSettings();

        [Header("Levels (Optional)")]
        [SerializeField] private DungeonLevelsTable levels = new DungeonLevelsTable();

        [Header("Progression (Optional)")]
        [SerializeField] private DungeonProgressionSettings progression = new DungeonProgressionSettings();

        public DungeonLayoutSettings Layout => layout;
        public DungeonPlacementSettings Placement => placement;
        public DungeonTilePalette TilePalette => tilePalette;
        public DungeonDetailSettings Details => details;
        public DungeonObjectTileSettings ObjectTiles => objectTiles;
        public DungeonSpawnSettings Spawns => spawns;
        public DungeonPortalSettings Portals => portals;
        public DungeonTemplateSettings Templates => templates;
        public DungeonLevelsTable Levels => levels;
        public DungeonProgressionSettings Progression => progression;
    }

    [Serializable]
    public class DungeonLayoutSettings
    {
        [Tooltip("Overall width and height of the generated layout measured in cells.")]
        [SerializeField] private Vector2Int gridSize = new Vector2Int(96, 96);

        [Tooltip("Number of primary rooms to carve before connectors and corridors are added.")]
        [SerializeField, Min(1)] private int targetRoomCount = 10;

        [Tooltip("Minimum inclusive room size in cells (width, height).")]
        [SerializeField] private Vector2Int minRoomSize = new Vector2Int(8, 6);

        [Tooltip("Maximum inclusive room size in cells (width, height).")]
        [SerializeField] private Vector2Int maxRoomSize = new Vector2Int(18, 14);

        [Tooltip("Corridor width measured in tiles.")]
        [SerializeField, Range(1, 5)] private int corridorWidth = 2;

        [Tooltip("Safety padding reserved inside each BSP partition before carving a room.")]
        [SerializeField, Range(0, 6)] private int roomPadding = 1;

        [Tooltip("Chance per room pair to create an additional connector beyond the minimum spanning tree.")]
        [SerializeField, Range(0f, 1f)] private float extraConnectorChance = 0.2f;

        [Tooltip("Maximum number of extra loops that can be injected to avoid huge fully-connected layouts.")]
        [SerializeField, Range(0, 16)] private int maxExtraConnectors = 4;

        [Header("Corridor Control")]
        [Tooltip("Attempt to break very long corridors by inserting extra rooms along their length.")]
        [SerializeField] private bool enableCorridorBreakRooms = true;

        [Tooltip("Maximum corridor length (in tiles) before forcing an additional room.")]
        [SerializeField, Min(4)] private int maxCorridorLength = 40;

        [Tooltip("Minimum size for rooms inserted to break up long corridors.")]
        [SerializeField] private Vector2Int corridorBreakRoomMinSize = new Vector2Int(6, 5);

        [Tooltip("Maximum size for rooms inserted to break up long corridors.")]
        [SerializeField] private Vector2Int corridorBreakRoomMaxSize = new Vector2Int(10, 8);

        [Header("Seeding")]
        [Tooltip("Use a random seed each time the generator runs (otherwise the fixed seed below is used).")]
        [SerializeField] private bool useRandomSeed = true;

        [Tooltip("Fixed seed used when Use Random Seed is disabled.")]
        [SerializeField] private int fixedSeed = 12345;

        public Vector2Int GridSize => new Vector2Int(Mathf.Max(32, gridSize.x), Mathf.Max(32, gridSize.y));
        public int TargetRoomCount => Mathf.Max(1, targetRoomCount);
        public Vector2Int MinRoomSize => new Vector2Int(Mathf.Max(3, minRoomSize.x), Mathf.Max(3, minRoomSize.y));
        public Vector2Int MaxRoomSize => new Vector2Int(Mathf.Max(MinRoomSize.x, maxRoomSize.x), Mathf.Max(MinRoomSize.y, maxRoomSize.y));
        public int CorridorWidth => Mathf.Clamp(corridorWidth, 1, 5);
        public int RoomPadding => Mathf.Clamp(roomPadding, 0, 6);
        public float ExtraConnectorChance => Mathf.Clamp01(extraConnectorChance);
        public int MaxExtraConnectors => Mathf.Max(0, maxExtraConnectors);
        public bool EnableCorridorBreakRooms => enableCorridorBreakRooms;
        public int MaxCorridorLength => Mathf.Max(4, maxCorridorLength);
        public Vector2Int CorridorBreakRoomMinSize => new Vector2Int(Mathf.Max(3, corridorBreakRoomMinSize.x), Mathf.Max(3, corridorBreakRoomMinSize.y));
        public Vector2Int CorridorBreakRoomMaxSize => new Vector2Int(Mathf.Max(CorridorBreakRoomMinSize.x, corridorBreakRoomMaxSize.x), Mathf.Max(CorridorBreakRoomMinSize.y, corridorBreakRoomMaxSize.y));
        public bool UseRandomSeed => useRandomSeed;
        public int FixedSeed => fixedSeed;
    }

    [Serializable]
    public class DungeonPlacementSettings
    {
        [Tooltip("Horizontal padding (cells) between the existing overworld map bounds and the dungeon.")]
        [SerializeField, Range(4, 128)] private int horizontalPadding = 32;

        [Tooltip("Vertical offset applied after padding. Positive values move the dungeon upwards in grid space.")]
        [SerializeField] private int verticalOffset;

        [Tooltip("Additional manual offset applied after padding and vertical offset.")]
        [SerializeField] private Vector3Int manualOffset = Vector3Int.zero;

        [Tooltip("Optional override for the dungeon origin. When enabled the dungeon ignores map bounds.")]
        [SerializeField] private bool useAbsoluteOrigin;

        [Tooltip("Absolute cell coordinate used when Use Absolute Origin is enabled.")]
        [SerializeField] private Vector3Int absoluteOrigin = new Vector3Int(256, 0, 0);

        public int HorizontalPadding => Mathf.Max(0, horizontalPadding);
        public int VerticalOffset => verticalOffset;
        public Vector3Int ManualOffset => manualOffset;
        public bool UseAbsoluteOrigin => useAbsoluteOrigin;
        public Vector3Int AbsoluteOrigin => absoluteOrigin;
    }

    [Serializable]
    public class DungeonTilePalette
    {
        [Tooltip("Weighted list of floor tiles used on the Ground1 tilemap.")]
        [SerializeField] private WeightedTileCollection floorTiles = new WeightedTileCollection();

        [Tooltip("Weighted list of optional floor accent tiles (placed sparingly on Ground1).")]
        [SerializeField] private WeightedTileCollection floorAccentTiles = new WeightedTileCollection();

        [Tooltip("Directional wall tiles for straight segments (Walls tilemap).")]
        [SerializeField] private DirectionalTileSet straightWalls = new DirectionalTileSet();

        [Tooltip("Directional tiles for wall end caps (Walls tilemap). Optional; falls back to straight walls when empty).")]
        [SerializeField] private DirectionalTileSet endCapWalls = new DirectionalTileSet();

        [Tooltip("Corner tiles used when a wall cell touches two orthogonal floor cells (Walls tilemap).")]
        [SerializeField] private CornerTileSet innerCorners = new CornerTileSet();

        [Tooltip("Optional tiles to use for outward facing corners (Walls tilemap).")]
        [SerializeField] private CornerTileSet outerCorners = new CornerTileSet();

        public WeightedTileCollection FloorTiles => floorTiles;
        public WeightedTileCollection FloorAccentTiles => floorAccentTiles;
        public DirectionalTileSet StraightWalls => straightWalls;
        public DirectionalTileSet EndCapWalls => endCapWalls;
        public CornerTileSet InnerCorners => innerCorners;
        public CornerTileSet OuterCorners => outerCorners;
    }

    [Serializable]
    public class DungeonDetailSettings
    {
        [Tooltip("Chance per floor cell to place a detail tile on the Ground2 tilemap.")]
        [SerializeField, Range(0f, 1f)] private float detailChance = 0.35f;

        [Tooltip("Optional modulation curve evaluating depth (0=start, 1=end) to control detail density.")]
        [SerializeField] private AnimationCurve detailDensityByDepth = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        [Tooltip("Weighted set of detail tiles painted on Ground2.")]
        [SerializeField] private WeightedTileCollection detailTiles = new WeightedTileCollection();

        public float DetailChance => Mathf.Clamp01(detailChance);
        public AnimationCurve DetailDensityByDepth => detailDensityByDepth;
        public WeightedTileCollection DetailTiles => detailTiles;
    }

    [Serializable]
    public class DungeonObjectTileSettings
    {
        [Tooltip("Chance per eligible cell to place a decorative object tile on the Objects tilemap.")]
        [SerializeField, Range(0f, 1f)] private float placementChance = 0.15f;

        [Tooltip("Weighted selection of object tiles painted on the Objects tilemap.")]
        [SerializeField] private WeightedTileCollection objectTiles = new WeightedTileCollection();

        public float PlacementChance => Mathf.Clamp01(placementChance);
        public WeightedTileCollection ObjectTiles => objectTiles;
    }

    [Serializable]
    public class DungeonSpawnSettings
    {
        [Tooltip("Chance that a non-critical room spawns enemies.")]
        [SerializeField, Range(0f, 1f)] private float enemyRoomChance = 0.6f;

        [Tooltip("Range of enemy packs to spawn when a room is populated.")]
        [SerializeField] private Vector2Int enemyPackSizeRange = new Vector2Int(2, 5);

        [Tooltip("Weighted pool of enemy prefabs to select from.")]
        [SerializeField] private List<DungeonEnemySpawnEntry> enemyPrefabs = new List<DungeonEnemySpawnEntry>();

        [Tooltip("Chance for a room to spawn interactive props (destructible barrels, traps, etc.).")]
        [SerializeField, Range(0f, 1f)] private float interactableChance = 0.4f;

        [Tooltip("Range for interactive props per eligible room.")]
        [SerializeField] private Vector2Int interactableCountRange = new Vector2Int(0, 2);

        [Tooltip("Weighted pool for interactive prop prefabs.")]
        [SerializeField] private List<DungeonPrefabSpawnRule> interactablePrefabs = new List<DungeonPrefabSpawnRule>();

        [Tooltip("Chance per room to spawn static props (non-interactive).")]
        [SerializeField, Range(0f, 1f)] private float propChance = 0.45f;

        [Tooltip("Range for static prop prefabs per eligible room.")]
        [SerializeField] private Vector2Int propCountRange = new Vector2Int(1, 3);

        [Tooltip("Weighted pool for static prop prefabs (furniture, clutter).")]
        [SerializeField] private List<DungeonPrefabSpawnRule> propPrefabs = new List<DungeonPrefabSpawnRule>();

        public float EnemyRoomChance => Mathf.Clamp01(enemyRoomChance);
        public Vector2Int EnemyPackSizeRange => ClampRange(enemyPackSizeRange, 0, 16);
        public IReadOnlyList<DungeonEnemySpawnEntry> EnemyPrefabs => enemyPrefabs;
        public float InteractableChance => Mathf.Clamp01(interactableChance);
        public Vector2Int InteractableCountRange => ClampRange(interactableCountRange, 0, 8);
        public IReadOnlyList<DungeonPrefabSpawnRule> InteractablePrefabs => interactablePrefabs;
        public float PropChance => Mathf.Clamp01(propChance);
        public Vector2Int PropCountRange => ClampRange(propCountRange, 0, 12);
        public IReadOnlyList<DungeonPrefabSpawnRule> PropPrefabs => propPrefabs;

        private static Vector2Int ClampRange(Vector2Int value, int min, int max)
        {
            int x = Mathf.Clamp(value.x, min, max);
            int y = Mathf.Clamp(Mathf.Max(value.y, x), min, max);
            return new Vector2Int(x, y);
        }
    }

    [Serializable]
    public class DungeonPortalSettings
    {
        [Tooltip("Prefab spawned at the dungeon start. Should include an Interactable configured as a portal.")]
        [SerializeField] private GameObject startPortalPrefab;

        [Tooltip("Prefab spawned at the dungeon exit. Should include an Interactable configured as a portal.")]
        [SerializeField] private GameObject exitPortalPrefab;

        [Tooltip("Offset applied to the start portal after converting the cell to world space.")]
        [SerializeField] private Vector3 startPortalOffset = new Vector3(0f, 0.25f, 0f);

        [Tooltip("Offset applied to the exit portal after converting the cell to world space.")]
        [SerializeField] private Vector3 exitPortalOffset = new Vector3(0f, 0.25f, 0f);

        [Tooltip("When enabled, the generator spawns the start portal automatically.")]
        [SerializeField] private bool spawnStartPortal = true;

        [Tooltip("When enabled, the generator spawns the exit portal automatically.")]
        [SerializeField] private bool spawnExitPortal = true;

        public GameObject StartPortalPrefab => startPortalPrefab;
        public GameObject ExitPortalPrefab => exitPortalPrefab;
        public Vector3 StartPortalOffset => startPortalOffset;
        public Vector3 ExitPortalOffset => exitPortalOffset;
        public bool SpawnStartPortal => spawnStartPortal;
        public bool SpawnExitPortal => spawnExitPortal;
    }

    [Serializable]
    public class DungeonLevelsTable
    {
        [Tooltip("Enable multi-level configuration and scaling.")]
        [SerializeField] private bool enableLevels = false;

        [Tooltip("Total number of levels in this dungeon run.")]
        [SerializeField, Min(1)] private int totalLevels = 1;

        [Tooltip("Current dungeon level (1-based).")]
        [SerializeField, Min(1)] private int currentLevel = 1;

        [Header("Default Per-Level Scaling")]
        [Tooltip("Health multiplier applied per level (exponentially). 1.15 = +15% per level.")]
        [SerializeField, Min(1f)] private float defaultHealthMultiplierPerLevel = 1.15f;
        [Tooltip("Damage multiplier applied per level (exponentially). 1.10 = +10% per level.")]
        [SerializeField, Min(1f)] private float defaultDamageMultiplierPerLevel = 1.10f;

        [Header("Per-Level Overrides")]
        [SerializeField] private List<DungeonLevelDefinition> levelDefinitions = new List<DungeonLevelDefinition>();

        public bool EnableLevels => enableLevels;
        public int TotalLevels => Mathf.Max(1, totalLevels);
        public int CurrentLevel
        {
            get => Mathf.Clamp(currentLevel, 1, TotalLevels);
            set => currentLevel = Mathf.Clamp(value, 1, TotalLevels);
        }

        public float GetHealthScale(int level)
        {
            if (!EnableLevels) return 1f;
            var def = ResolveLevel(level);
            float mult = def != null && def.OverrideMultipliers ? Mathf.Max(1f, def.HealthMultiplierPerLevel) : Mathf.Max(1f, defaultHealthMultiplierPerLevel);
            return Mathf.Pow(mult, Mathf.Max(0, level - 1));
        }

        public float GetDamageScale(int level)
        {
            if (!EnableLevels) return 1f;
            var def = ResolveLevel(level);
            float mult = def != null && def.OverrideMultipliers ? Mathf.Max(1f, def.DamageMultiplierPerLevel) : Mathf.Max(1f, defaultDamageMultiplierPerLevel);
            return Mathf.Pow(mult, Mathf.Max(0, level - 1));
        }

        public DungeonLevelDefinition FindLevel(int level)
        {
            if (levelDefinitions == null) return null;
            for (int i = 0; i < levelDefinitions.Count; i++)
            {
                var def = levelDefinitions[i];
                if (def != null && def.Level == level)
                {
                    return def;
                }
            }
            return null;
        }

        public IReadOnlyList<DungeonEnemySpawnEntry> GetEnemyPoolForLevel(int level, IReadOnlyList<DungeonEnemySpawnEntry> defaultPool)
        {
            var def = ResolveLevel(level);
            if (def != null && def.EnemyPrefabs != null && def.EnemyPrefabs.Count > 0)
            {
                return def.EnemyPrefabs;
            }
            return defaultPool;
        }

        public DungeonLevelDefinition ResolveLevel(int level)
        {
            var def = FindLevel(level);
            if (def != null)
            {
                return def;
            }

            if (levelDefinitions == null || levelDefinitions.Count == 0)
            {
                return null;
            }

            int index = Mathf.Clamp(level - 1, 0, levelDefinitions.Count - 1);
            return levelDefinitions[Mathf.Clamp(index, 0, levelDefinitions.Count - 1)];
        }

        public string GetLevelDisplayName(int level)
        {
            level = Mathf.Clamp(level, 1, TotalLevels);
            var def = ResolveLevel(level);
            if (def != null)
            {
                string name = def.DisplayName;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }
            }
            return $"Level {level}";
        }
    }

    [Serializable]
    public class DungeonLevelDefinition
    {
        [SerializeField, Min(1)] private int level = 1;

        [Header("Presentation")]
        [SerializeField] private string displayName = string.Empty;

        [Header("Scaling Overrides")]
        [SerializeField] private bool overrideMultipliers = false;
        [SerializeField, Min(1f)] private float healthMultiplierPerLevel = 1.15f;
        [SerializeField, Min(1f)] private float damageMultiplierPerLevel = 1.10f;

        [Header("End Chest")]
        [SerializeField] private bool spawnEndChest = false;
        [SerializeField] private GameObject chestPrefab;
        [SerializeField] private Vector3 chestOffset = new Vector3(1f, 0f, 0f);
        [SerializeField] private LootDropDefinition[] loot = Array.Empty<LootDropDefinition>();

        [Header("Enemies (Override)")]
        [SerializeField] private List<DungeonEnemySpawnEntry> enemyPrefabs = new List<DungeonEnemySpawnEntry>();
        [SerializeField] private List<DungeonGuaranteedEnemy> guaranteedEnemies = new List<DungeonGuaranteedEnemy>();

        public int Level => Mathf.Max(1, level);
        public bool OverrideMultipliers => overrideMultipliers;
        public float HealthMultiplierPerLevel => Mathf.Max(1f, healthMultiplierPerLevel);
        public float DamageMultiplierPerLevel => Mathf.Max(1f, damageMultiplierPerLevel);
        public bool SpawnEndChest => spawnEndChest && chestPrefab != null;
        public GameObject ChestPrefab => chestPrefab;
        public Vector3 ChestOffset => chestOffset;
        public LootDropDefinition[] Loot => loot;
        public List<DungeonEnemySpawnEntry> EnemyPrefabs => enemyPrefabs;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? $"Level {Level}" : displayName;
        public IReadOnlyList<DungeonGuaranteedEnemy> GuaranteedEnemies => guaranteedEnemies;
    }

    [Serializable]
    public class DungeonGuaranteedEnemy
    {
        [SerializeField] private GameObject prefab;
        [SerializeField, Min(1)] private int count = 1;
        [SerializeField] private bool allowCorridors = false;
        [SerializeField] private bool applyStatModifiers = false;
        [SerializeField, Min(0f)] private float healthMultiplier = 1f;
        [SerializeField, Min(0f)] private float damageMultiplier = 1f;
        [SerializeField] private bool fillScaledEnemiesToMaxHealth = true;

        public GameObject Prefab => prefab;
        public int Count => Mathf.Max(0, count);
        public bool AllowCorridors => allowCorridors;
        public bool ApplyStatModifiers => applyStatModifiers;
        public float HealthMultiplier => Mathf.Max(0f, healthMultiplier);
        public float DamageMultiplier => Mathf.Max(0f, damageMultiplier);
        public bool FillScaledEnemiesToMaxHealth => fillScaledEnemiesToMaxHealth;
    }

    [Serializable]
    public class DungeonProgressionSettings
    {
        [Tooltip("When enabled, using the start portal returns to the overworld rather than the previous level (forward-only progression).")]
        [SerializeField] private bool forwardOnly = false;

        public bool ForwardOnly => forwardOnly;
    }

    [Serializable]
    public class DungeonTemplateSettings
    {
        [Tooltip("When enabled the generator can substitute authored templates for some rooms.")]
        [SerializeField] private bool enableTemplates = true;

        [Tooltip("Chance per eligible room to place a handcrafted template instead of the procedural layout.")]
        [SerializeField, Range(0f, 1f)] private float templateChance = 0.3f;

        [Tooltip("Allow templates to be used for the room that contains the dungeon entrance portal.")]
        [SerializeField] private bool allowStartRoom = true;

        [Tooltip("Allow templates to be used for the room that contains the dungeon exit portal.")]
        [SerializeField] private bool allowEndRoom = true;

        [Header("Prefab-Only Mode")]
        [Tooltip("When enabled, every non-corridor room is replaced by a template and procedural room painting is skipped for those rooms.")]
        [SerializeField] private bool forceTemplatesForAllRooms = false;

        [Tooltip("Available template entries with optional weighting and size constraints.")]
        [SerializeField] private List<DungeonTemplateEntry> templates = new List<DungeonTemplateEntry>();

        public bool EnableTemplates => enableTemplates;
        public float TemplateChance => Mathf.Clamp01(templateChance);
        public bool AllowStartRoom => allowStartRoom;
        public bool AllowEndRoom => allowEndRoom;
        public bool ForceTemplatesForAllRooms => forceTemplatesForAllRooms;
        public IReadOnlyList<DungeonTemplateEntry> Templates => templates;
    }

    [Serializable]
    public class DungeonTemplateEntry
    {
        [SerializeField] private DungeonRoomTemplate template;
        [SerializeField, Range(0.01f, 100f)] private float weight = 1f;

        [Tooltip("Optional minimum room dimensions (width,height) required for this template.")]
        [SerializeField] private Vector2Int minRoomSize = new Vector2Int(4, 4);

        [Tooltip("Optional maximum room dimensions (width,height) allowed for this template. Zero values are ignored.")]
        [SerializeField] private Vector2Int maxRoomSize = Vector2Int.zero;

        public DungeonRoomTemplate Template => template;
        public float Weight => Mathf.Max(0f, weight);
        public Vector2Int MinRoomSize => new Vector2Int(Mathf.Max(1, minRoomSize.x), Mathf.Max(1, minRoomSize.y));
        public Vector2Int MaxRoomSize => maxRoomSize;

        public bool SupportsRoom(Vector2Int roomSize)
        {
            Vector2Int min = MinRoomSize;
            if (roomSize.x < min.x || roomSize.y < min.y)
            {
                return false;
            }

            if (maxRoomSize.x > 0 && roomSize.x > maxRoomSize.x)
            {
                return false;
            }

            if (maxRoomSize.y > 0 && roomSize.y > maxRoomSize.y)
            {
                return false;
            }

            return true;
        }
    }

    [Serializable]
    public class DirectionalTileSet
    {
        [SerializeField] private TileBase east;
        [SerializeField] private TileBase south;
        [SerializeField] private TileBase west;
        [SerializeField] private TileBase north;

        public TileBase Get(DungeonDirection direction)
        {
            return direction switch
            {
                DungeonDirection.East => east,
                DungeonDirection.South => south,
                DungeonDirection.West => west,
                DungeonDirection.North => north,
                _ => east
            };
        }
    }

    [Serializable]
    public class CornerTileSet
    {
        [SerializeField] private TileBase northEast;
        [SerializeField] private TileBase southEast;
        [SerializeField] private TileBase southWest;
        [SerializeField] private TileBase northWest;

        public TileBase Get(DungeonCorner corner)
        {
            return corner switch
            {
                DungeonCorner.NorthEast => northEast,
                DungeonCorner.SouthEast => southEast,
                DungeonCorner.SouthWest => southWest,
                DungeonCorner.NorthWest => northWest,
                _ => northEast
            };
        }
    }

    [Serializable]
    public class WeightedTileCollection
    {
        [SerializeField] private List<WeightedTileEntry> tiles = new List<WeightedTileEntry>();

        public bool HasTiles => tiles != null && tiles.Count > 0;

        public TileBase FirstTile
        {
            get
            {
                if (!HasTiles)
                {
                    return null;
                }

                for (int i = 0; i < tiles.Count; i++)
                {
                    TileBase tile = tiles[i]?.Tile;
                    if (tile != null)
                    {
                        return tile;
                    }
                }

                return null;
            }
        }

        public TileBase GetRandomTile(System.Random rng)
        {
            if (!HasTiles)
            {
                return null;
            }

            float total = 0f;
            for (int i = 0; i < tiles.Count; i++)
            {
                var entry = tiles[i];
                if (entry?.Tile == null || entry.Weight <= 0f)
                {
                    continue;
                }

                total += entry.Weight;
            }

            if (total <= 0f)
            {
                return null;
            }

            double roll = rng.NextDouble() * total;
            float cumulative = 0f;

            for (int i = 0; i < tiles.Count; i++)
            {
                var entry = tiles[i];
                if (entry?.Tile == null || entry.Weight <= 0f)
                {
                    continue;
                }

                cumulative += entry.Weight;
                if (roll <= cumulative)
                {
                    return entry.Tile;
                }
            }

            return tiles[tiles.Count - 1]?.Tile;
        }
    }

    [Serializable]
    public class WeightedTileEntry
    {
        [SerializeField] private TileBase tile;
        [SerializeField, Range(0.01f, 100f)] private float weight = 1f;

        public TileBase Tile => tile;
        public float Weight => Mathf.Max(0f, weight);
    }

    [Serializable]
    public class DungeonEnemySpawnEntry
    {
        [SerializeField] private GameObject prefab;
        [SerializeField, Range(0.01f, 100f)] private float weight = 1f;
        [SerializeField] private Vector2Int countRange = new Vector2Int(1, 3);
        [SerializeField] private bool allowCorridors;

        public GameObject Prefab => prefab;
        public float Weight => Mathf.Max(0f, weight);
        public Vector2Int CountRange => new Vector2Int(Mathf.Max(0, countRange.x), Mathf.Max(countRange.x, countRange.y));
        public bool AllowCorridors => allowCorridors;
    }

    [Serializable]
    public class DungeonPrefabSpawnRule
    {
        [SerializeField] private GameObject prefab;
        [SerializeField, Range(0.01f, 100f)] private float weight = 1f;
        [SerializeField] private bool allowCorridors;
        [SerializeField] private bool alignToCellCenter = true;
        [SerializeField] private Vector3 positionOffset = new Vector3(0f, 0.1f, 0f);

        public GameObject Prefab => prefab;
        public float Weight => Mathf.Max(0f, weight);
        public bool AllowCorridors => allowCorridors;
        public bool AlignToCellCenter => alignToCellCenter;
        public Vector3 PositionOffset => positionOffset;
    }

    public enum DungeonDirection
    {
        East = 0,
        South = 1,
        West = 2,
        North = 3
    }

    public enum DungeonCorner
    {
        NorthEast = 0,
        SouthEast = 1,
        SouthWest = 2,
        NorthWest = 3
    }

    public static class DungeonDirectionUtility
    {
        private static readonly Vector2Int[] CardinalOffsets =
        {
            new Vector2Int(1, 0),
            new Vector2Int(0, -1),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1)
        };

        public static Vector2Int ToOffset(DungeonDirection direction)
        {
            return CardinalOffsets[(int)direction];
        }

        public static DungeonDirection FromOffset(Vector2Int offset)
        {
            if (offset.x > 0 && offset.y == 0) return DungeonDirection.East;
            if (offset.x < 0 && offset.y == 0) return DungeonDirection.West;
            if (offset.y > 0 && offset.x == 0) return DungeonDirection.North;
            return DungeonDirection.South;
        }

        public static DungeonCorner ToCorner(Vector2Int a, Vector2Int b)
        {
            bool north = a.y > 0 || b.y > 0;
            bool south = a.y < 0 || b.y < 0;
            bool east = a.x > 0 || b.x > 0;
            bool west = a.x < 0 || b.x < 0;

            if (north && east) return DungeonCorner.NorthEast;
            if (south && east) return DungeonCorner.SouthEast;
            if (south && west) return DungeonCorner.SouthWest;
            return DungeonCorner.NorthWest;
        }

        public static DungeonDirection Opposite(this DungeonDirection direction)
        {
            return (DungeonDirection)(((int)direction + 2) % 4);
        }

        public static DungeonDirection RotateClockwise(this DungeonDirection direction)
        {
            return (DungeonDirection)(((int)direction + 1) % 4);
        }

        public static DungeonDirection RotateCounterClockwise(this DungeonDirection direction)
        {
            return (DungeonDirection)(((int)direction + 3) % 4);
        }
    }
}






