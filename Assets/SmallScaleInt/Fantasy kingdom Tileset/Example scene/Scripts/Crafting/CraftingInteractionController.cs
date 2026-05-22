using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

using SmallScale.FantasyKingdomTileset.Building;
using Input = SmallScale.FantasyKingdomTileset.InputAdapter;

namespace SmallScale.FantasyKingdomTileset
{
/// <summary>
/// Detects left-clicks on crafting stations placed on the objects tilemap and opens the crafting menu.
/// </summary>
public sealed class CraftingInteractionController : MonoBehaviour
{
    private static readonly HashSet<CraftingStationType> SupportedCraftingStations = new HashSet<CraftingStationType>
    {
        CraftingStationType.Blacksmith,
        CraftingStationType.Anvil,
        CraftingStationType.Furnace,
        CraftingStationType.Tannery,
        CraftingStationType.StoneCutter,
        CraftingStationType.Workbench,
        CraftingStationType.Smelter,
        CraftingStationType.Kiln,
        CraftingStationType.Loom,
        CraftingStationType.AlchemyTable,
        CraftingStationType.CookingFire
    };

    [Header("References")]
    [SerializeField]
    private Tilemap objectsTilemap;

    [SerializeField]
    private TilemapBuildController buildController;

    [SerializeField]
    private CraftingMenuController craftingMenu;

    [SerializeField]
    private Camera sceneCamera;


    [Header("Tile Database")]
    [SerializeField, Tooltip("Database used to resolve DestructibleTileData from placed tiles when they weren't placed via the build system.")]
    private DestructibleTileDatabase destructibleDatabase;
    [Header("Trader Stations")]
    [SerializeField, Tooltip("Trader component template used when interacting with trading post tiles.")]
    private TraderComponent tradingPostTraderTemplate;

    [SerializeField, Tooltip("Optional parent for instantiated trader templates. Defaults to this GameObject.")]
    private Transform tradingPostTraderContainer;

    [SerializeField, Tooltip("When enabled, regenerates the trader's stock each time the player interacts with a trading post.")]
    private bool randomizeTraderStockOnEachOpen = true;

    private TraderComponent cachedTradingPostTrader;
    private readonly Dictionary<Vector3Int, DestructibleTileData> spawnPointDefinitionOverrides = new Dictionary<Vector3Int, DestructibleTileData>();
    private bool hasActiveSpawnPoint;
    private Vector3Int activeSpawnPointCell;
    private TileBase activeSpawnPointOriginalTile;
    private Tilemap activeSpawnPointTilemap;
    private Vector3Int activeSpawnPointTilemapCell;

    [Header("Spawn Point Visuals")]
    [SerializeField, Tooltip("Animated tile set when a spawn point fireplace is active (e.g. WallDetail1).")]
    private TileBase spawnPointActiveTile;
    [SerializeField, Tooltip("Tilemap that hosts the animated fire tile (e.g. WallDetail1). When omitted we write directly to the objects tilemap.")]
    private Tilemap spawnPointVisualTilemap;

    [Header("Interaction Range")]
    [SerializeField, Tooltip("Player transform used to measure distance to crafting stations. When unset we try to auto-detect the player.")]
    private Transform playerTransform;

    [SerializeField, Tooltip("Maximum distance (in world units) from the player to interact with a crafting station or trader.")]
    private float interactionRange = 2f;
    private void Awake()
    {
        if (sceneCamera == null)
        {
            sceneCamera = Camera.main;
        }

        InitializeTradingPostTrader();
        EnsurePlayerTransform();
    }

    private void Update()
    {
        if (!WasLeftClickPressed())
        {
            return;
        }


        if (IsPointerOverUiElement())
        {
            return; // ignore UI clicks
        }

        if (BuildMenuController.IsAnyMenuOpen)
        {
            return;
        }


        if (sceneCamera == null || objectsTilemap == null || buildController == null)
        {
            return;
        }

        if (!TryGetPointerWorldPositionOnTilemap(objectsTilemap, out Vector3 worldPosition))
        {
            return;
        }

        Vector3Int hoverCell = objectsTilemap.WorldToCell(worldPosition);

        // Resolve hovered definition
        if (!buildController.TryGetPlacedDefinitionAtWorld(objectsTilemap, worldPosition, out DestructibleTileData definition) || definition == null)
        {
            // Fallback: resolve from tile if not placed via build system
            TileBase tile = objectsTilemap.GetTile(hoverCell);
            if (tile == null || destructibleDatabase == null || !destructibleDatabase.TryGet(tile, out definition) || definition == null)
            {
                if (!spawnPointDefinitionOverrides.TryGetValue(hoverCell, out definition))
                {
                    return;
                }
            }
        }

        if (definition == null)
        {
            return;
        }

        // If placement map still contains a stale definition, make sure the current cell still holds it.
        CraftingStationType stationType = definition.craftingStationType;
        bool cellMatchesDefinition = DoesCellContainDefinition(hoverCell, definition);
        if (!cellMatchesDefinition && stationType != CraftingStationType.SpawnPoint)
        {
            return;
        }

        if (!definition.isCraftingStation)
        {
            return;
        }

        Vector3 cellCenter = objectsTilemap.GetCellCenterWorld(hoverCell);
        if (!IsWithinInteractionRange(cellCenter))
        {
            ShowOutOfRangeText(cellCenter);
            return;
        }

        if (stationType == CraftingStationType.Trader)
        {
            HandleTraderStation(definition, worldPosition);
            return;
        }

        if (stationType == CraftingStationType.SpawnPoint)
        {
            spawnPointDefinitionOverrides[hoverCell] = definition;
            HandleSpawnPoint(hoverCell, definition, cellCenter);
            return;
        }

        if (!IsSupportedCraftingStation(stationType))
        {
            return;
        }

        if (craftingMenu == null)
        {
            Debug.LogWarning("Crafting station clicked but no CraftingMenuController is assigned.", this);
            return;
        }

        craftingMenu.OpenForStation(stationType, worldPosition);
    }

    private static bool IsSupportedCraftingStation(CraftingStationType stationType)
    {
        return SupportedCraftingStations.Contains(stationType);
    }

    private void InitializeTradingPostTrader()
    {
        if (cachedTradingPostTrader != null)
        {
            return;
        }

        if (tradingPostTraderTemplate == null)
        {
            Debug.LogWarning("CraftingInteractionController has no trader template assigned. Trading post tiles will not function until one is provided.", this);
            return;
        }

        Transform parent = tradingPostTraderContainer != null ? tradingPostTraderContainer : null;
        TraderComponent instance = Instantiate(tradingPostTraderTemplate, parent);
        if (instance == null)
        {
            return;
        }

        bool wasActive = instance.gameObject.activeSelf;
        if (!wasActive)
        {
            instance.gameObject.SetActive(true);
        }

        instance.OpenOnClick = false;
        instance.UseRandomStockGeneration = true;
        instance.RebuildRandomStock();

        if (!wasActive)
        {
            instance.gameObject.SetActive(false);
        }

        cachedTradingPostTrader = instance;
    }

    private void HandleTraderStation(DestructibleTileData definition, Vector3 worldPosition)
    {
        TraderPanelController panel = TraderPanelController.Instance;
        if (panel == null)
        {
            Debug.LogWarning("Trading post clicked but no TraderPanelController is active in the scene.", this);
            return;
        }

        TraderComponent trader = ResolveTradingPostTrader();
        if (trader == null)
        {
            Debug.LogWarning("Trading post clicked but no trader template was assigned.", this);
            return;
        }

        if (!string.IsNullOrWhiteSpace(definition?.BuildDisplayName))
        {
            trader.SetTraderName(definition.BuildDisplayName);
        }

        Vector3Int cell = objectsTilemap != null ? objectsTilemap.WorldToCell(worldPosition) : Vector3Int.zero;
        Vector3 traderPosition = objectsTilemap != null ? objectsTilemap.GetCellCenterWorld(cell) : worldPosition;
        trader.transform.position = traderPosition;

        if (!trader.gameObject.activeSelf)
        {
            trader.gameObject.SetActive(true);
        }

        Debug.Log($"Opening trading post '{trader.TraderName}' at {traderPosition}", trader);

        if (randomizeTraderStockOnEachOpen)
        {
            trader.RebuildRandomStock();
        }
        else if (!trader.UseRandomStockGeneration)
        {
            trader.UseRandomStockGeneration = true;
        }

        trader.OpenTraderPanel();
    }

    private void HandleSpawnPoint(Vector3Int cell, DestructibleTileData definition, Vector3 cellCenterWorld)
    {
        bool clickedActive = hasActiveSpawnPoint && cell == activeSpawnPointCell;

        if (clickedActive)
        {
            RestoreActiveSpawnPointVisual(true, cellCenterWorld + Vector3.up * 0.25f);
            return;
        }

        PlayerHealth playerHealth = PlayerHealth.Instance;
        Transform player = EnsurePlayerTransform();

        if (playerHealth == null && player != null)
        {
            playerHealth = player.GetComponent<PlayerHealth>();
            if (playerHealth == null)
            {
                playerHealth = player.GetComponentInParent<PlayerHealth>();
            }
        }

        if (playerHealth == null)
        {
            Debug.LogWarning("Spawn point station clicked but no PlayerHealth instance could be found.", this);
            return;
        }

        // Clear any previously lit spawn point so only one fireplace is active at a time.
        RestoreActiveSpawnPointVisual(false, cellCenterWorld);
        ActivateSpawnPointVisual(cell, definition);

        Transform playerRoot = playerHealth.transform;
        Vector3 spawnPosition = playerRoot.position;
        Quaternion spawnRotation = playerRoot.rotation;
        playerHealth.SetSpawnPoint(spawnPosition, spawnRotation);

        ShowSpawnPointStatus("Spawn point updated", cellCenterWorld + Vector3.up * 0.35f);
    }

    private void ActivateSpawnPointVisual(Vector3Int cell, DestructibleTileData definition)
    {
        Tilemap targetTilemap = spawnPointVisualTilemap != null ? spawnPointVisualTilemap : objectsTilemap;
        if (targetTilemap == null)
        {
            hasActiveSpawnPoint = false;
            return;
        }

        Vector3 worldCenter = objectsTilemap != null ? objectsTilemap.GetCellCenterWorld(cell) : targetTilemap.CellToWorld(cell);
        Vector3Int targetCell = targetTilemap == objectsTilemap ? cell : targetTilemap.WorldToCell(worldCenter);

        TileBase existingTile = targetTilemap.GetTile(targetCell);
        if (existingTile == null && definition != null && targetTilemap == objectsTilemap)
        {
            existingTile = definition.PrimaryTile;
        }

        activeSpawnPointOriginalTile = existingTile;
        activeSpawnPointTilemap = targetTilemap;
        activeSpawnPointTilemapCell = targetCell;

        if (spawnPointActiveTile != null)
        {
            targetTilemap.SetTile(targetCell, spawnPointActiveTile);
        }

        hasActiveSpawnPoint = true;
        activeSpawnPointCell = cell;
    }

    private void RestoreActiveSpawnPointVisual(bool showStatus, Vector3 statusPosition)
    {
        if (!hasActiveSpawnPoint)
        {
            activeSpawnPointOriginalTile = null;
            activeSpawnPointCell = default;
            activeSpawnPointTilemap = null;
            activeSpawnPointTilemapCell = default;
            return;
        }

        Tilemap targetTilemap = activeSpawnPointTilemap != null ? activeSpawnPointTilemap : objectsTilemap;
        if (targetTilemap != null)
        {
            TileBase tileToRestore = activeSpawnPointOriginalTile;
            if (tileToRestore != null)
            {
                targetTilemap.SetTile(activeSpawnPointTilemapCell, tileToRestore);
            }
            else
            {
                targetTilemap.SetTile(activeSpawnPointTilemapCell, null);
            }
        }

        if (showStatus)
        {
            ShowSpawnPointStatus("Spawn point deactivated", statusPosition);
        }

        hasActiveSpawnPoint = false;
        activeSpawnPointOriginalTile = null;
        activeSpawnPointCell = default;
        activeSpawnPointTilemap = null;
        activeSpawnPointTilemapCell = default;
    }

    private void ShowSpawnPointStatus(string message, Vector3 worldPosition)
    {
        if (CombatTextManager.Instance == null || !CombatTextManager.Enabled)
        {
            return;
        }

        CombatTextManager.Instance.SpawnStatus(message, worldPosition);
    }

    private TraderComponent ResolveTradingPostTrader()
    {
        if (cachedTradingPostTrader != null)
        {
            return cachedTradingPostTrader;
        }

        if (tradingPostTraderTemplate == null)
        {
            return null;
        }

        InitializeTradingPostTrader();
        return cachedTradingPostTrader;
    }

    private Transform EnsurePlayerTransform()
    {
        if (playerTransform != null)
        {
            return playerTransform;
        }

        if (PlayerStats.Instance != null)
        {
            playerTransform = PlayerStats.Instance.transform;
        }
        else if (PlayerHealth.Instance != null)
        {
            playerTransform = PlayerHealth.Instance.transform;
        }

        return playerTransform;
    }

    private bool IsWithinInteractionRange(Vector3 targetPosition)
    {
        Transform player = EnsurePlayerTransform();
        if (player == null)
        {
            return false;
        }

        float range = Mathf.Max(0.1f, interactionRange);
        Vector3 playerPos = player.position;
        playerPos.z = targetPosition.z;
        return (targetPosition - playerPos).sqrMagnitude <= range * range;
    }

    private void ShowOutOfRangeText(Vector3 worldPosition)
    {
        if (CombatTextManager.Instance == null || !CombatTextManager.Enabled)
        {
            return;
        }

        Transform player = EnsurePlayerTransform();
        Vector3 spawnPos = player != null ? player.position : worldPosition;
        spawnPos += Vector3.up * 0.5f;
        CombatTextManager.Instance.SpawnStatus("Too far away", spawnPos);
    }

    private bool TryGetPointerWorldPositionOnTilemap(Tilemap targetTilemap, out Vector3 worldPosition)
    {
        Camera targetCamera = sceneCamera != null ? sceneCamera : Camera.main;
        if (targetCamera == null || targetTilemap == null)
        {
            worldPosition = Vector3.zero;
            return false;
        }

        if (!TryGetPointerScreenPosition(out Vector2 screenPosition))
        {
            worldPosition = Vector3.zero;
            return false;
        }

        Ray ray = targetCamera.ScreenPointToRay(screenPosition);
        Vector3 planePoint = targetTilemap.transform.position;
        Plane plane = new Plane(targetTilemap.transform.forward, planePoint);
        if (!plane.Raycast(ray, out float distance))
        {
            plane = new Plane(-targetTilemap.transform.forward, planePoint);
            if (!plane.Raycast(ray, out distance))
            {
                worldPosition = Vector3.zero;
                return false;
            }
        }

        worldPosition = ray.GetPoint(distance);
        return true;
    }

    private static bool IsPointerOverUiElement()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return false;
        }

        if (eventSystem.IsPointerOverGameObject())
        {
            return true;
        }

#if ENABLE_INPUT_SYSTEM
        const int MousePointerId = -1;
        if (eventSystem.IsPointerOverGameObject(MousePointerId))
        {
            return true;
        }
#endif

        return eventSystem.IsPointerOverGameObject(PointerInputModule.kMouseLeftId);
    }

    private static bool WasLeftClickPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            return true;
        }
#endif
        return Input.GetMouseButtonDown(0);
    }

    private static bool TryGetPointerScreenPosition(out Vector2 screenPosition)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            screenPosition = Mouse.current.position.ReadValue();
            return true;
        }
#endif
        if (Input.mousePresent)
        {
            Vector3 mousePosition = Input.mousePosition;
            screenPosition = new Vector2(mousePosition.x, mousePosition.y);
            return true;
        }

        screenPosition = Vector2.zero;
        return false;
    }

    private bool DoesCellContainDefinition(Vector3Int cell, DestructibleTileData definition)
    {
        if (objectsTilemap == null || definition == null)
        {
            return false;
        }
        TileBase tile = objectsTilemap.GetTile(cell);
        if (tile == null)
        {
            return false;
        }
        int variants = definition.VariantCount;
        for (int i = 0; i < variants; i++)
        {
            if (definition.GetVariant(i) == tile)
            {
                return true;
            }
        }
        return false;
    }
}
}












