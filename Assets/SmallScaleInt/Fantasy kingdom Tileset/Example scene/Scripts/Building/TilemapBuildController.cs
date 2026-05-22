using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.Scripting.APIUpdating;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif
using SmallScale.FantasyKingdomTileset.Balance;
using Input = SmallScale.FantasyKingdomTileset.InputAdapter;

/// <summary>
/// Handles building placement by allowing the player to preview and place tiles on the
/// appropriate tilemap depending on the selected build part category.
/// </summary>
namespace SmallScale.FantasyKingdomTileset.Building
{
[MovedFrom(true, null, null, "TilemapBuildController")]
public sealed class TilemapBuildController : MonoBehaviour
{
    public static TilemapBuildController Instance { get; private set; }

    [Header("Dependencies")]
    [SerializeField]
    [Tooltip("Build menu that raises events when the player selects a part.")]
    private BuildMenuController buildMenu;

    [SerializeField]
    [Tooltip("Tilemap used for ground build parts.")]
    private Tilemap groundTilemap;

    [SerializeField]
    [Tooltip("Tilemap used for wall build parts.")]
    private Tilemap wallsTilemap;

    [SerializeField]
    [Tooltip("Tilemap used for roof build parts.")]
    private Tilemap roofTilemap;

    [SerializeField]
    [Tooltip("Tilemap used for object build parts.")]
    private Tilemap objectsTilemap;

    [SerializeField]
    [Tooltip("Tilemap dedicated to drawing the placement preview.")]
    private Tilemap previewTilemap;

    [SerializeField]
    [Tooltip("Tilemap containing tiles representing destroyed objects that should be cleared when rebuilding.")]
    private Tilemap brokenObjectsTilemap;

    [Header("Roof Visibility Sync")]
    [SerializeField]
    [Tooltip("Tilemap that stores the TileCheck markers used for roof visibility.")]
    private Tilemap roofTileCheckTilemap;

    [SerializeField]
    [Tooltip("Tile that will be placed on the TileCheck tilemap whenever the player builds a roof.")]
    private TileBase roofTileCheckMarkerTile;

    [SerializeField]
    [Tooltip("Cell offset (in roof tile coordinates) used to map a roof cell to the TileCheck tilemap.")]
    private Vector3Int roofTileCheckOffset = new Vector3Int(-2, -2, 0);

    [SerializeField]
    [Tooltip("Automatically create and remove TileCheck markers whenever roofs are placed or destroyed.")]
    private bool syncRoofTileCheckMarkers = true;

    [SerializeField]
    [Tooltip("If enabled, TileCheck markers are placed both at the offset location and the roof cell itself.")]
    private bool extendTileCheckPlacement = false;

    [Header("Collider Settings")]
    [SerializeField]
    [Tooltip("Mappings that link tilemaps to their collider auto placers for runtime updates.")]
    private List<TilemapColliderBinding> colliderBindings = new List<TilemapColliderBinding>();

    [Header("Preview Settings")]
    [SerializeField]
    [Tooltip("Colour tint applied to the preview tile.")]
    private Color previewColor = new Color(1f, 1f, 1f, 0.5f);

    [SerializeField]
    [Tooltip("Colour tint applied to the preview tile when placement is invalid.")]
    private Color invalidPreviewColor = new Color(1f, 0.3f, 0.3f, 0.5f);

    [SerializeField]
    [Tooltip("Sprite rendered under the preview tile to highlight the hovered cell.")]
    private Sprite previewTileSprite;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Alpha transparency applied to the hover tile sprite.")]
    private float previewTileAlpha = 0.25f;

    [SerializeField]
    [Tooltip("Camera used for translating screen positions to world positions. Defaults to Camera.main when omitted.")]
    private Camera sceneCamera;

    private readonly Dictionary<Tilemap, Dictionary<Vector3Int, DestructibleTileData>> playerPlacedTiles = new Dictionary<Tilemap, Dictionary<Vector3Int, DestructibleTileData>>();

    private DestructibleTileData activeDefinition;
    private Tilemap activeTargetTilemap;
    private TileColliderAutoPlacer activeColliderAutoPlacer;
    private readonly Dictionary<Tilemap, TileColliderAutoPlacer> colliderLookup = new Dictionary<Tilemap, TileColliderAutoPlacer>();
    private bool hasBuiltColliderLookup;
    private Vector3Int currentPreviewCell;
    private bool hasPreviewCell;
    private int currentVariantIndex;
    private SpriteRenderer previewTileRenderer;

    [Serializable]
    private sealed class TilemapColliderBinding
    {
        [SerializeField]
        private Tilemap tilemap;

        [SerializeField]
        private TileColliderAutoPlacer colliderAutoPlacer;

        public Tilemap Tilemap => tilemap;

        public TileColliderAutoPlacer ColliderAutoPlacer => colliderAutoPlacer;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[TilemapBuildController] Multiple instances detected. New instance will override the previous one.", this);
        }
        Instance = this;

        if (sceneCamera == null)
        {
            sceneCamera = Camera.main;
        }

        ApplyPreviewTint();
        EnsurePreviewTileRenderer();
        if (previewTileRenderer != null)
        {
            previewTileRenderer.enabled = false;
            UpdatePreviewTileRendererColor(previewColor);
        }

        RebuildColliderLookup();
    }

    private void OnEnable()
    {
        if (buildMenu != null)
        {
            buildMenu.OnPartSelected.AddListener(HandlePartSelected);
        }
    }

    private void OnDisable()
    {
        if (buildMenu != null)
        {
            buildMenu.OnPartSelected.RemoveListener(HandlePartSelected);
        }

        CancelPlacement();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        TryClearBrokenObjectsWithMiddleClick();

        if (activeDefinition == null || activeTargetTilemap == null)
        {
            return;
        }

        if (WasCancelPressed())
        {
            CancelPlacement();
            return;
        }

        if (IsPointerOverUiElement())
        {
            return;
        }

        HandleVariantScroll();

        if (!TryUpdatePreviewCell())
        {
            return;
        }

        if (WasConfirmPressed())
        {
            TryPlaceActiveTile();
        }
    }

    private void TryClearBrokenObjectsWithMiddleClick()
    {
        if (buildMenu == null || !buildMenu.IsOpen)
        {
            return;
        }

        if (!WasMiddleClickPressed())
        {
            return;
        }

        if (IsPointerOverUiElement())
        {
            return;
        }

        if (TryClearBrokenObjectsUnderPointer())
        {
            return;
        }

        TryRemovePlayerPlacedTileUnderPointer();
    }

    private bool TryClearBrokenObjectsUnderPointer()
    {
        if (brokenObjectsTilemap == null)
        {
            return false;
        }

        if (!TryGetPointerWorldPositionForTilemap(brokenObjectsTilemap, out Vector3 worldPosition))
        {
            return false;
        }

        Vector3Int cellPosition = brokenObjectsTilemap.WorldToCell(worldPosition);
        if (!brokenObjectsTilemap.HasTile(cellPosition))
        {
            return false;
        }

        brokenObjectsTilemap.SetTile(cellPosition, null);
        return true;
    }

    private bool TryRemovePlayerPlacedTileUnderPointer()
    {
        Tilemap[] tilemaps = { objectsTilemap, wallsTilemap, roofTilemap, groundTilemap };
        for (int i = 0; i < tilemaps.Length; i++)
        {
            Tilemap tilemap = tilemaps[i];
            if (tilemap == null)
            {
                continue;
            }

            if (!TryGetPointerWorldPositionForTilemap(tilemap, out Vector3 pointerWorldPosition))
            {
                continue;
            }

            Vector3Int cellPosition = tilemap.WorldToCell(pointerWorldPosition);
            if (!TryGetPlayerPlacedDefinition(tilemap, cellPosition, out DestructibleTileData definition))
            {
                continue;
            }

            Vector3 worldPosition = tilemap.CellToWorld(cellPosition);
            tilemap.SetTile(cellPosition, null);
            UpdateColliderTile(tilemap, cellPosition, null);
            HandleRoofTileRemoval(tilemap, cellPosition, definition);
            RemovePlayerPlacedTile(tilemap, cellPosition);
            RefundResources(definition, worldPosition);

            if (tilemap == activeTargetTilemap)
            {
                RefreshPreviewTile();
            }

            return true;
        }

        return false;
    }

    private void HandlePartSelected(DestructibleTileData definition)
    {
        activeDefinition = null;
        activeTargetTilemap = null;
        activeColliderAutoPlacer = null;
        currentVariantIndex = 0;

        ClearPreview();

        if (definition == null)
        {
            return;
        }

        if (!BuildUnlockService.IsUnlocked(definition))
        {
            Debug.LogWarning($"Build part '{definition.DisplayName}' is locked and cannot be selected yet.");
            return;
        }

        if (definition.Tile == null)
        {
            Debug.LogWarning($"Build part '{definition.name}' does not have a tile assigned and cannot be placed.");
            return;
        }

        Tilemap targetTilemap = ResolveTargetTilemap(definition.Category);
        if (targetTilemap == null)
        {
            Debug.LogWarning($"No tilemap assigned for build part category '{definition.Category}'.");
            return;
        }

        activeDefinition = definition;
        activeTargetTilemap = targetTilemap;
        activeColliderAutoPlacer = ResolveColliderAutoPlacer(targetTilemap);

        TryUpdatePreviewCell(true);
    }

    private Tilemap ResolveTargetTilemap(BuildPartCategory category)
    {
        switch (category)
        {
            case BuildPartCategory.Ground:
                return groundTilemap;
            case BuildPartCategory.Walls:
                return wallsTilemap;
            case BuildPartCategory.Roof:
                return roofTilemap;
            case BuildPartCategory.Objects:
            case BuildPartCategory.CraftingStations:
                return objectsTilemap;
            default:
                return null;
        }
    }

    private TileColliderAutoPlacer ResolveColliderAutoPlacer(Tilemap targetTilemap)
    {
        if (targetTilemap == null)
        {
            return null;
        }

        EnsureColliderLookup();

        if (colliderLookup.TryGetValue(targetTilemap, out TileColliderAutoPlacer cachedPlacer))
        {
            if (cachedPlacer != null)
            {
                return cachedPlacer;
            }

            colliderLookup.Remove(targetTilemap);
        }

        TileColliderAutoPlacer autoPlacer = FindColliderAutoPlacerForTilemap(targetTilemap);
        if (autoPlacer != null)
        {
            RegisterAutoPlacer(autoPlacer);
        }

        return autoPlacer;
    }

    private bool TryUpdatePreviewCell(bool forceUpdate = false)
    {
        if (!TryGetPointerWorldPosition(out Vector3 worldPosition))
        {
            return false;
        }

        Vector3Int cellPosition = activeTargetTilemap.WorldToCell(worldPosition);
        if (!forceUpdate && hasPreviewCell && cellPosition == currentPreviewCell)
        {
            RefreshPreviewTile();
            return true;
        }

        SetPreviewCell(cellPosition);
        return true;
    }

    private void SetPreviewCell(Vector3Int cellPosition)
    {
        if (previewTilemap == null)
        {
            hasPreviewCell = true;
            currentPreviewCell = cellPosition;
            RefreshPreviewTile();
            return;
        }

        if (hasPreviewCell)
        {
            previewTilemap.SetTile(currentPreviewCell, null);
        }

        hasPreviewCell = true;
        currentPreviewCell = cellPosition;

        RefreshPreviewTile();
    }

    private void HandleVariantScroll()
    {
        if (activeDefinition == null || activeDefinition.VariantCount <= 1)
        {
            return;
        }

        float scrollDelta = GetScrollDelta();
        if (Mathf.Approximately(scrollDelta, 0f))
        {
            return;
        }

        int direction = scrollDelta > 0f ? 1 : -1;
        CycleVariant(direction);
    }

    private void CycleVariant(int direction)
    {
        if (activeDefinition == null)
        {
            return;
        }

        int variantCount = activeDefinition.VariantCount;
        if (variantCount <= 1)
        {
            return;
        }

        currentVariantIndex = Mod(currentVariantIndex + direction, variantCount);
        RefreshPreviewTile();
    }

    private void RefreshPreviewTile()
    {
        if (!hasPreviewCell)
        {
            HidePreviewTileSprite();
            return;
        }

        bool canPlace = CanPlaceAtCell(currentPreviewCell);

        if (previewTilemap != null)
        {
            TileBase previewTile = GetActiveVariantTile();
            previewTilemap.SetTile(currentPreviewCell, previewTile);

            if (previewTile != null)
            {
                previewTilemap.SetTileFlags(currentPreviewCell, TileFlags.None);

                Color targetColor = canPlace ? previewColor : invalidPreviewColor;
                previewTilemap.SetColor(currentPreviewCell, targetColor);
            }
        }

        UpdatePreviewTileSprite(canPlace);
    }

    private TileBase GetActiveVariantTile()
    {
        if (activeDefinition == null)
        {
            return null;
        }

        return activeDefinition.GetVariant(currentVariantIndex);
    }

    private bool CanPlaceAtCell(Vector3Int cellPosition)
    {
        if (activeDefinition == null || activeTargetTilemap == null)
        {
            return false;
        }

        bool cellOccupied = activeTargetTilemap.HasTile(cellPosition);
        if (!cellOccupied)
        {
            return HasResourcesForActiveDefinition();
        }

        bool isPlayerPlaced = IsPlayerPlacedTile(activeTargetTilemap, cellPosition);
        if (isPlayerPlaced)
        {
            return HasResourcesForReplacement(cellPosition);
        }

        if (activeDefinition.BuildCategory == BuildPartCategory.Ground)
        {
            // Allow ground tiles to overwrite any existing tile so terrain can be repainted.
            return HasResourcesForActiveDefinition();
        }

        return false;
    }

    private bool HasResourcesForActiveDefinition()
    {
        if (activeDefinition == null)
        {
            return false;
        }

        if (!BuildUnlockService.IsUnlocked(activeDefinition))
        {
            return false;
        }

        ResourceSet cost = ResolveBuildCost(activeDefinition.ResourceCostSet);
        if (cost == null || cost.IsEmpty)
        {
            return true;
        }

        DynamicResourceManager manager = DynamicResourceManager.Instance;
        if (manager == null)
        {
            return true;
        }

        return manager.HasResources(cost);
    }

    private bool HasResourcesForReplacement(Vector3Int cellPosition)
    {
        if (activeDefinition == null)
        {
            return false;
        }

        if (HasResourcesForActiveDefinition())
        {
            return true;
        }

        DynamicResourceManager manager = DynamicResourceManager.Instance;
        if (manager == null)
        {
            return true;
        }

        if (!TryGetPlayerPlacedDefinition(activeTargetTilemap, cellPosition, out DestructibleTileData definition) || definition == null)
        {
            return false;
        }

        ResourceSet cost = ResolveBuildCost(activeDefinition.ResourceCostSet);
        if (cost == null || cost.IsEmpty)
        {
            return true;
        }

        ResourceSet refund = ResolveBuildCost(definition.ResourceCostSet);
        ResourceSet current = manager.CurrentResources;
        IReadOnlyList<ResourceAmount> amounts = cost.Amounts;
        for (int i = 0; i < amounts.Count; i++)
        {
            ResourceAmount amount = amounts[i];
            if (amount.type == null || amount.amount <= 0)
            {
                continue;
            }

            int available = current.Get(amount.type);
            if (refund != null)
            {
                available += refund.Get(amount.type);
            }

            if (available < amount.amount)
            {
                return false;
            }
        }

        return true;
    }    private static int Mod(int value, int modulus)
    {
        if (modulus == 0)
        {
            return 0;
        }

        int result = value % modulus;
        return result < 0 ? result + modulus : result;
    }

    private float GetScrollDelta()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            Vector2 scrollValue = Mouse.current.scroll.ReadValue();
            if (!Mathf.Approximately(scrollValue.y, 0f))
            {
                return scrollValue.y;
            }
        }
#endif

        return Input.mouseScrollDelta.y;
    }

    private void TryPlaceActiveTile()
    {
        if (!hasPreviewCell || activeDefinition == null || activeTargetTilemap == null)
        {
            return;
        }

        if (!CanPlaceAtCell(currentPreviewCell))
        {
            return;
        }

        TileBase tileToPlace = GetActiveVariantTile();
        if (tileToPlace == null)
        {
            return;
        }

        DynamicResourceManager manager = DynamicResourceManager.Instance;
        ResourceSet cost = ResolveBuildCost(activeDefinition.ResourceCostSet);
        Vector3 worldPosition = activeTargetTilemap.CellToWorld(currentPreviewCell);

        bool isReplacing = TryGetPlayerPlacedDefinition(activeTargetTilemap, currentPreviewCell, out DestructibleTileData replacedDefinition);
        ResourceSet refund = isReplacing && replacedDefinition != null ? ResolveBuildCost(replacedDefinition.ResourceCostSet) : null;

        if (isReplacing)
        {
            RemovePlayerPlacedTile(activeTargetTilemap, currentPreviewCell);
        }

        if (manager != null && refund != null && !refund.IsEmpty)
        {
            manager.GrantResources(refund, worldPosition, showFeedback: true, awardExperience: false);
        }

        if (manager != null && cost != null && !cost.IsEmpty)
        {
            if (!manager.TrySpendResources(cost, worldPosition))
            {
                if (manager != null && refund != null && !refund.IsEmpty)
                {
                    manager.TrySpendResources(refund, worldPosition, showFeedback: false);
                }

                if (isReplacing && replacedDefinition != null)
                {
                    RegisterPlayerPlacedTile(activeTargetTilemap, currentPreviewCell, replacedDefinition);
                }

                RefreshPreviewTile();
                return;
            }
        }

        if (isReplacing && replacedDefinition != null)
        {
            HandleRoofTileRemoval(activeTargetTilemap, currentPreviewCell, replacedDefinition);
        }

        ClearBrokenObjectsAtCell(currentPreviewCell);
        activeTargetTilemap.SetTile(currentPreviewCell, tileToPlace);
        ApplyColliderForActivePlacement(currentPreviewCell, tileToPlace);
        if (activeDefinition != null && (activeDefinition.Category == BuildPartCategory.Objects || activeDefinition.Category == BuildPartCategory.CraftingStations))
        {
            TileColliderAutoPlacer ap = ResolveColliderAutoPlacer(activeTargetTilemap);
            if (ap != null)
            {
                ap.PopulateColliders();
            }
        }

        HandleRoofTilePlacement(activeTargetTilemap, currentPreviewCell, activeDefinition);
        RegisterPlayerPlacedTile(activeTargetTilemap, currentPreviewCell, activeDefinition);
        RefreshPreviewTile();
    }
    private void ApplyColliderForActivePlacement(Vector3Int cellPosition, TileBase sourceTile)
    {
        if (activeTargetTilemap == null)
        {
            return;
        }

        if (activeDefinition != null)
        {
            BuildPartCategory cat = activeDefinition.Category;
            if (cat != BuildPartCategory.Walls && cat != BuildPartCategory.CraftingStations && cat != BuildPartCategory.Objects)
            {
                return;
            }
        }

        TileColliderAutoPlacer autoPlacer = activeColliderAutoPlacer;
        if (autoPlacer == null)
        {
            autoPlacer = ResolveColliderAutoPlacer(activeTargetTilemap);
            activeColliderAutoPlacer = autoPlacer;
        }

        autoPlacer?.ApplyColliderForTile(cellPosition, sourceTile);
    }

    private void RegisterPlayerPlacedTile(Tilemap tilemap, Vector3Int cellPosition, DestructibleTileData definition)
    {
        if (tilemap == null || definition == null)
        {
            return;
        }

        Dictionary<Vector3Int, DestructibleTileData> placements = GetOrCreatePlacementMap(tilemap);
        placements[cellPosition] = definition;
    }

    private void RemovePlayerPlacedTile(Tilemap tilemap, Vector3Int cellPosition)
    {
        if (tilemap == null)
        {
            return;
        }

        if (!playerPlacedTiles.TryGetValue(tilemap, out Dictionary<Vector3Int, DestructibleTileData> placements))
        {
            return;
        }

        placements.Remove(cellPosition);
        if (placements.Count == 0)
        {
            playerPlacedTiles.Remove(tilemap);
        }
    }

    private bool RoofTileCheckSyncEnabled => syncRoofTileCheckMarkers && roofTileCheckTilemap != null;

    private bool CanPlaceRoofTileCheck => RoofTileCheckSyncEnabled && roofTileCheckMarkerTile != null;

    private bool IsConfiguredRoofTilemap(Tilemap tilemap)
    {
        if (roofTilemap == null)
        {
            return tilemap != null;
        }

        return tilemap == roofTilemap;
    }

    private bool ShouldSyncRoofTileCheck(DestructibleTileData definition, Tilemap sourceTilemap)
    {
        if (!RoofTileCheckSyncEnabled || definition == null)
        {
            return false;
        }

        if (definition.BuildCategory != BuildPartCategory.Roof)
        {
            return false;
        }

        return IsConfiguredRoofTilemap(sourceTilemap);
    }

    private Vector3Int ConvertRoofCellToTileCheckCell(Vector3Int roofCell)
    {
        return new Vector3Int(
            roofCell.x + roofTileCheckOffset.x,
            roofCell.y + roofTileCheckOffset.y,
            roofCell.z + roofTileCheckOffset.z);
    }

    private IEnumerable<Vector3Int> EnumerateTileCheckCells(Vector3Int roofCell)
    {
        Vector3Int primary = ConvertRoofCellToTileCheckCell(roofCell);
        yield return primary;

        if (!extendTileCheckPlacement)
        {
            yield break;
        }

        if (roofCell != primary)
        {
            yield return roofCell;
        }

        Vector3Int extra = new Vector3Int(roofCell.x - 1, roofCell.y - 1, roofCell.z);
        if (extra != primary && extra != roofCell)
        {
            yield return extra;
        }
    }

    private void PlaceRoofTileCheckAt(Vector3Int roofCell)
    {
        if (!CanPlaceRoofTileCheck)
        {
            return;
        }

        foreach (Vector3Int checkCell in EnumerateTileCheckCells(roofCell))
        {
            roofTileCheckTilemap.SetTile(checkCell, roofTileCheckMarkerTile);
        }
    }

    private void RemoveRoofTileCheckAt(Vector3Int roofCell)
    {
        if (!RoofTileCheckSyncEnabled)
        {
            return;
        }

        foreach (Vector3Int checkCell in EnumerateTileCheckCells(roofCell))
        {
            if (roofTileCheckTilemap.HasTile(checkCell))
            {
                roofTileCheckTilemap.SetTile(checkCell, null);
            }
        }
    }

    private void HandleRoofTilePlacement(Tilemap tilemap, Vector3Int cellPosition, DestructibleTileData definition)
    {
        if (!ShouldSyncRoofTileCheck(definition, tilemap))
        {
            return;
        }

        PlaceRoofTileCheckAt(cellPosition);
    }

    private void HandleRoofTileRemoval(Tilemap tilemap, Vector3Int cellPosition, DestructibleTileData definition)
    {
        if (!ShouldSyncRoofTileCheck(definition, tilemap))
        {
            return;
        }

        RemoveRoofTileCheckAt(cellPosition);
    }

    public void HandleRoofTileDestroyed(Tilemap sourceTilemap, Vector3Int cellPosition)
    {
        if (!RoofTileCheckSyncEnabled)
        {
            return;
        }

        if (!IsConfiguredRoofTilemap(sourceTilemap))
        {
            return;
        }

        RemoveRoofTileCheckAt(cellPosition);
    }

    public bool IsPlayerPlacedTile(Tilemap tilemap, Vector3Int cellPosition)
    {
        return TryGetPlayerPlacedDefinition(tilemap, cellPosition, out DestructibleTileData _);
    }

    private bool TryGetPlayerPlacedDefinition(Tilemap tilemap, Vector3Int cellPosition, out DestructibleTileData definition)
    {
        if (tilemap != null && playerPlacedTiles.TryGetValue(tilemap, out Dictionary<Vector3Int, DestructibleTileData> placements))
        {
            return placements.TryGetValue(cellPosition, out definition);
        }

        definition = null;
        return false;
    }

    public bool TryGetPlacedDefinitionAt(Tilemap tilemap, Vector3Int cellPosition, out DestructibleTileData definition)
    {
        return TryGetPlayerPlacedDefinition(tilemap, cellPosition, out definition);
    }

    public bool TryGetPlacedDefinitionAtWorld(Tilemap tilemap, Vector3 worldPosition, out DestructibleTileData definition)
    {
        if (tilemap == null)
        {
            definition = null;
            return false;
        }

        Vector3Int cell = tilemap.WorldToCell(worldPosition);
        return TryGetPlayerPlacedDefinition(tilemap, cell, out definition);
    }
    private Dictionary<Vector3Int, DestructibleTileData> GetOrCreatePlacementMap(Tilemap tilemap)
    {
        if (tilemap == null)
        {
            return null;
        }

        if (!playerPlacedTiles.TryGetValue(tilemap, out Dictionary<Vector3Int, DestructibleTileData> placements))
        {
            placements = new Dictionary<Vector3Int, DestructibleTileData>();
            playerPlacedTiles[tilemap] = placements;
        }

        return placements;
    }

    private void RefundResources(DestructibleTileData definition, Vector3 worldPosition)
    {
        if (definition == null)
        {
            return;
        }

        ResourceSet refund = ResolveBuildCost(definition.ResourceCostSet);
        if (refund == null || refund.IsEmpty)
        {
            return;
        }

        DynamicResourceManager manager = DynamicResourceManager.Instance;
        if (manager == null)
        {
            return;
        }

        manager.GrantResources(refund, worldPosition, showFeedback: true, awardExperience: false);
    }
        private void ClearBrokenObjectsAtCell(Vector3Int cellPosition)
    {
        if (brokenObjectsTilemap == null)
        {
            return;
        }

        Vector3 worldPosition = activeTargetTilemap.CellToWorld(cellPosition);
        Vector3Int brokenObjectsCell = brokenObjectsTilemap.WorldToCell(worldPosition);

        if (brokenObjectsTilemap.HasTile(brokenObjectsCell))
        {
            brokenObjectsTilemap.SetTile(brokenObjectsCell, null);
        }
    }

    private void UpdateColliderTile(Tilemap tilemap, Vector3Int cellPosition, TileBase sourceTile)
    {
        TileColliderAutoPlacer autoPlacer = ResolveColliderAutoPlacer(tilemap);
        autoPlacer?.ApplyColliderForTile(cellPosition, sourceTile);
    }

    private static ResourceSet ResolveBuildCost(ResourceSet baseCost)
    {
        if (GameBalanceManager.Instance == null)
        {
            return baseCost;
        }

        return GameBalanceManager.Instance.GetAdjustedBuildCost(baseCost);
    }

    private void CancelPlacement()
    {
        activeDefinition = null;
        activeTargetTilemap = null;
        activeColliderAutoPlacer = null;
        ClearPreview();
    }

    private void ClearPreview()
    {
        if (previewTilemap != null && hasPreviewCell)
        {
            previewTilemap.SetTile(currentPreviewCell, null);
        }

        hasPreviewCell = false;
        currentPreviewCell = Vector3Int.zero;
        currentVariantIndex = 0;
        HidePreviewTileSprite();
    }

    private void ApplyPreviewTint()
    {
        if (previewTilemap != null)
        {
            previewTilemap.color = Color.white;
        }
    }

    private void EnsurePreviewTileRenderer()
    {
        if (previewTileSprite == null)
        {
            return;
        }

        if (previewTileRenderer == null)
        {
            GameObject previewTileObject = new GameObject("PreviewTile");
            Transform parentTransform = previewTilemap != null ? previewTilemap.transform : transform;
            previewTileObject.transform.SetParent(parentTransform, false);
            previewTileRenderer = previewTileObject.AddComponent<SpriteRenderer>();
        }

        previewTileRenderer.sprite = previewTileSprite;
        ConfigurePreviewTileRenderer();
    }

    private void ConfigurePreviewTileRenderer()
    {
        if (previewTileRenderer == null)
        {
            return;
        }

        if (previewTilemap != null)
        {
            TilemapRenderer tilemapRenderer = previewTilemap.GetComponent<TilemapRenderer>();
            if (tilemapRenderer != null)
            {
                previewTileRenderer.sortingLayerID = tilemapRenderer.sortingLayerID;
                previewTileRenderer.sortingOrder = tilemapRenderer.sortingOrder - 1;
            }
        }
    }

    private void UpdatePreviewTileRendererColor(Color baseColor)
    {
        if (previewTileRenderer == null)
        {
            return;
        }

        baseColor.a = previewTileAlpha;
        previewTileRenderer.color = baseColor;
    }

    private void UpdatePreviewTileSprite(bool canPlace)
    {
        if (previewTileSprite == null || activeTargetTilemap == null)
        {
            HidePreviewTileSprite();
            return;
        }

        EnsurePreviewTileRenderer();

        if (previewTileRenderer == null)
        {
            return;
        }

        Transform targetParent = activeTargetTilemap.transform;
        if (previewTileRenderer.transform.parent != targetParent)
        {
            previewTileRenderer.transform.SetParent(targetParent, false);
        }

        Vector3 worldPosition = activeTargetTilemap.GetCellCenterWorld(currentPreviewCell);
        previewTileRenderer.transform.position = worldPosition;

        Color targetColor = canPlace ? previewColor : invalidPreviewColor;
        UpdatePreviewTileRendererColor(targetColor);

        previewTileRenderer.enabled = true;
    }

    private void HidePreviewTileSprite()
    {
        if (previewTileRenderer != null)
        {
            previewTileRenderer.enabled = false;
        }
    }

    private void OnValidate()
    {
        previewTileAlpha = Mathf.Clamp01(previewTileAlpha);

        if (previewTileRenderer != null)
        {
            Color color = previewTileRenderer.color;
            color.a = previewTileAlpha;
            previewTileRenderer.color = color;
        }

        RebuildColliderLookup();
    }

    private void EnsureColliderLookup()
    {
        if (hasBuiltColliderLookup)
        {
            return;
        }

        RebuildColliderLookup();
    }

    private void RebuildColliderLookup()
    {
        colliderLookup.Clear();
        hasBuiltColliderLookup = true;

        if (colliderBindings != null)
        {
            for (int i = 0; i < colliderBindings.Count; i++)
            {
                RegisterColliderBinding(colliderBindings[i]);
            }
        }

        RegisterAutoPlacersFromScene();
    }

    private void RegisterColliderBinding(TilemapColliderBinding binding)
    {
        if (binding == null)
        {
            return;
        }

        Tilemap tilemap = binding.Tilemap;
        TileColliderAutoPlacer autoPlacer = binding.ColliderAutoPlacer;
        if (tilemap == null || autoPlacer == null)
        {
            return;
        }

        colliderLookup[tilemap] = autoPlacer;
    }

    private void RegisterAutoPlacer(TileColliderAutoPlacer autoPlacer)
    {
        if (autoPlacer == null)
        {
            return;
        }

        Tilemap sourceTilemap = autoPlacer.SourceTilemap;
        if (sourceTilemap == null)
        {
            return;
        }

        if (colliderLookup.TryGetValue(sourceTilemap, out TileColliderAutoPlacer existing) && existing != null && existing != autoPlacer)
        {
            return;
        }

        colliderLookup[sourceTilemap] = autoPlacer;
    }

    private void RegisterAutoPlacersFromScene()
    {
        TileColliderAutoPlacer[] autoPlacers = FindAllColliderAutoPlacers();
        for (int i = 0; i < autoPlacers.Length; i++)
        {
            TileColliderAutoPlacer autoPlacer = autoPlacers[i];
            if (autoPlacer == null)
            {
                continue;
            }

            GameObject autoPlacerObject = autoPlacer.gameObject;
            if (autoPlacerObject == null || !autoPlacerObject.scene.IsValid())
            {
                continue;
            }

            RegisterAutoPlacer(autoPlacer);
        }
    }

    private static TileColliderAutoPlacer[] FindAllColliderAutoPlacers()
    {
#if UNITY_2020_1_OR_NEWER
        return UnityEngine.Object.FindObjectsByType<TileColliderAutoPlacer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        return Resources.FindObjectsOfTypeAll<TileColliderAutoPlacer>();
#endif
    }

    private TileColliderAutoPlacer FindColliderAutoPlacerForTilemap(Tilemap targetTilemap)
    {
        TileColliderAutoPlacer autoPlacer = GetColliderAutoPlacerFromBindings(targetTilemap);
        if (autoPlacer != null)
        {
            return autoPlacer;
        }

        TileColliderAutoPlacer[] autoPlacers = FindAllColliderAutoPlacers();
        for (int i = 0; i < autoPlacers.Length; i++)
        {
            autoPlacer = autoPlacers[i];
            if (autoPlacer == null)
            {
                continue;
            }

            GameObject autoPlacerObject = autoPlacer.gameObject;
            if (autoPlacerObject == null || !autoPlacerObject.scene.IsValid())
            {
                continue;
            }

            if (autoPlacer.SourceTilemap == targetTilemap)
            {
                return autoPlacer;
            }
        }

        return null;
    }

    private TileColliderAutoPlacer GetColliderAutoPlacerFromBindings(Tilemap targetTilemap)
    {
        if (colliderBindings == null)
        {
            return null;
        }

        for (int i = 0; i < colliderBindings.Count; i++)
        {
            TilemapColliderBinding binding = colliderBindings[i];
            if (binding == null)
            {
                continue;
            }

            if (binding.Tilemap == targetTilemap)
            {
                return binding.ColliderAutoPlacer;
            }
        }

        return null;
    }

    private bool TryGetPointerWorldPosition(out Vector3 worldPosition)
    {
        return TryGetPointerWorldPositionForTilemap(activeTargetTilemap, out worldPosition);
    }

    private bool TryGetPointerWorldPositionForTilemap(Tilemap targetTilemap, out Vector3 worldPosition)
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

    private bool TryGetPointerScreenPosition(out Vector2 screenPosition)
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

    private static bool WasConfirmPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            return true;
        }
#endif
        return Input.GetMouseButtonDown(0);
    }

    private static bool WasCancelPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current[Key.Escape].wasPressedThisFrame)
        {
            return true;
        }
#endif
        return Input.GetKeyDown(KeyCode.Escape);
    }

    private static bool WasMiddleClickPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.middleButton.wasPressedThisFrame)
        {
            return true;
        }
#endif
        return Input.GetMouseButtonDown(2);
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
}
}









