using System;
using System.Collections.Generic;
using UnityEngine;
using SmallScale.FantasyKingdomTileset;
using SmallScaleInc.TopDownPixelCharactersPack1;
using UnityEngine.Events;
using UnityEngine.Tilemaps;

namespace DungeonGeneration
{
    /// <summary>
    /// Runtime component that procedurally builds a dungeon using a profile-driven configuration.
    /// Handles tile placement, prefab spawning, and portal wiring outside the overworld bounds.
    /// </summary>
    [DisallowMultipleComponent]
    public partial class DungeonGenerator : MonoBehaviour
    {
        [SerializeField] private DungeonGenerationProfile profile;

        [Header("Tilemaps")]
        [SerializeField] private Tilemap groundTilemap;
        [SerializeField] private Tilemap groundDetailTilemap;
        [SerializeField] private Tilemap wallTilemap;
        [SerializeField] private Tilemap objectTilemap;

        [Header("Colliders")]
        [SerializeField] private TileColliderAutoPlacer[] colliderAutoPlacers;

        [Header("Runtime Roots")]
        [SerializeField] private Transform dungeonRoot;
        [SerializeField] private Transform overworldReturnAnchor;
        [SerializeField] private Transform previousLevelReturnAnchor;
        [SerializeField] private bool exitLeadsToNextLevel;
        [SerializeField] private Transform nextLevelEntryAnchor;

        [Header("Portal Offsets")]
        [SerializeField] private Vector3 startPortalReturnOffset = Vector3.zero;
        [SerializeField] private Vector3 exitPortalDestinationOffset = Vector3.zero;

        [Header("Lifecycle")]
        [SerializeField] private bool autoGenerateOnStart = true;
        [SerializeField] private bool clearBeforeGenerate = true;
        [SerializeField] private bool logDebugOutput;

        [Header("Events")]
        [SerializeField] private DungeonGeneratedEvent onDungeonGenerated;

        public event Action<DungeonRuntimeData> DungeonGenerated;

        public DungeonRuntimeData RuntimeData { get; private set; }

        public Transform OverworldReturnAnchor
        {
            get => overworldReturnAnchor;
            set => overworldReturnAnchor = value;
        }

        public Transform PreviousLevelReturnAnchor
        {
            get => previousLevelReturnAnchor;
            set => previousLevelReturnAnchor = value;
        }

        public Transform NextLevelEntryAnchor
        {
            get => nextLevelEntryAnchor;
            set => nextLevelEntryAnchor = value;
        }

        private readonly List<GameObject> spawnedPrefabs = new();
        private readonly List<Vector3Int> groundCells = new();
        private readonly List<Vector3Int> groundDetailCells = new();
        private readonly List<Vector3Int> wallCells = new();
        private readonly List<Vector3Int> objectCells = new();
        private readonly List<CorridorContact> corridorContactBuffer = new();

        private GameObject startPortalInstance;
        private GameObject exitPortalInstance;

        private DungeonLayoutResult lastLayout;
        private Vector3Int lastOrigin;
        private int lastSeed;
        private bool hasGenerated;
        private int runtimeCurrentLevel = 1;
        private bool runtimeLevelInitialized;
        private bool isAdvancingLevel;
        private GameObject preservedExitPortalDuringTransition;

        private static readonly Vector3Int[] PortalBlockingOffsets =
        {
            new Vector3Int(1, 0, 0),
            new Vector3Int(0, -1, 0),
            new Vector3Int(1, -1, 0)
        };

        private void Start()
        {
            if (autoGenerateOnStart)
            {
                EnsureDungeonReady();
            }
        }

        /// <summary>
        /// Generates a dungeon if needed. When forceRegenerate is true the existing dungeon is discarded.
        /// </summary>
        public void EnsureDungeonReady(bool forceRegenerate = false)
        {
            if (!hasGenerated || forceRegenerate)
            {
                GenerateDungeon(null, forceRegenerate);
            }
        }

        /// <summary>
        /// Rebuilds the dungeon explicitly, optionally overriding the random seed.
        /// </summary>
        public void RegenerateDungeon(int? overrideSeed = null)
        {
            GenerateDungeon(overrideSeed, true);
        }

        /// <summary>
        /// Main generation entry point.
        /// </summary>
        public void GenerateDungeon(int? overrideSeed, bool forceRegenerate)
        {
            if (profile == null)
            {
                Debug.LogWarning("[DungeonGenerator] No profile assigned. Generation aborted.", this);
                return;
            }

            EnsureRuntimeLevelInitialized();
            if (!isAdvancingLevel)
            {
                ResetRuntimeLevelFromProfile();
                preservedExitPortalDuringTransition = null;
            }
            else
            {
                ClampRuntimeLevelToBounds();
            }

            EnsureTilemapReference(ref groundTilemap, "Ground1");
            EnsureTilemapReference(ref groundDetailTilemap, "Ground2", optional: true);
            EnsureTilemapReference(ref wallTilemap, "Walls");
            EnsureTilemapReference(ref objectTilemap, "Objects", optional: true);

            int seed = ResolveSeed(overrideSeed);
            if (!forceRegenerate && hasGenerated && RuntimeData != null && RuntimeData.Seed == seed)
            {
                if (logDebugOutput)
                {
                    Debug.Log($"[DungeonGenerator] Seed {seed} already generated, skipping regeneration.", this);
                }
                return;
            }

            if (clearBeforeGenerate)
            {
                ClearExistingDungeon();
            }

            System.Random rng = new(seed);
            lastLayout = DungeonLayoutBuilder.Build(profile.Layout, rng);
            FillTightGaps(lastLayout);
            AssignRoomTemplates(lastLayout, rng);
            lastOrigin = DetermineOrigin(lastLayout);

            PaintDungeon(lastLayout, lastOrigin, rng);
            ApplyRoomTemplates(lastLayout, lastOrigin, rng);
            PopulateColliderTilemaps();
            SpawnDungeonContent(lastLayout, lastOrigin, rng);

            RuntimeData = BuildRuntimeData(lastLayout, lastOrigin, seed);
            hasGenerated = true;
            lastSeed = seed;

            onDungeonGenerated?.Invoke(RuntimeData);
            DungeonGenerated?.Invoke(RuntimeData);

            if (logDebugOutput)
            {
                Debug.Log($"[DungeonGenerator] Generated dungeon with seed {seed} ({lastLayout.Rooms.Count} rooms).", this);
            }
        }

        /// <summary>
        /// Clears tiles, portals, and spawned prefabs generated by the last run.
        /// </summary>
        public void ClearExistingDungeon()
        {
            ClearTilemapCells(groundTilemap, groundCells);
            ClearTilemapCells(groundDetailTilemap, groundDetailCells);
            ClearTilemapCells(wallTilemap, wallCells);
            ClearTilemapCells(objectTilemap, objectCells);

            groundCells.Clear();
            groundDetailCells.Clear();
            wallCells.Clear();
            objectCells.Clear();

            for (int i = 0; i < spawnedPrefabs.Count; i++)
            {
                DestroyRuntimeObject(spawnedPrefabs[i]);
            }
            spawnedPrefabs.Clear();

            DestroyRuntimeObject(startPortalInstance);
            startPortalInstance = null;

            if (exitPortalInstance != null)
            {
                if (preservedExitPortalDuringTransition != null && exitPortalInstance == preservedExitPortalDuringTransition)
                {
                    // Keep the portal alive until its teleport coroutine finishes.
                }
                else
                {
                    DetachExitPortalCallbacks(exitPortalInstance);
                    DestroyRuntimeObject(exitPortalInstance);
                }
            }
            exitPortalInstance = null;

            RuntimeData = null;
            hasGenerated = false;
        }

        private void EnsureTilemapReference(ref Tilemap tilemap, string expected, bool optional = false)
        {
            if (tilemap != null)
            {
                return;
            }

            tilemap = FindTilemapInChildren(expected);
            if (tilemap == null && !optional)
            {
                Debug.LogWarning($"[DungeonGenerator] Missing required tilemap reference for \"{expected}\".", this);
            }
        }

        private Tilemap FindTilemapInChildren(string nameFragment)
        {
            if (string.IsNullOrEmpty(nameFragment))
            {
                return null;
            }

            string fragment = nameFragment.ToLowerInvariant();
            Tilemap[] maps = GetComponentsInChildren<Tilemap>(true);
            foreach (Tilemap tm in maps)
            {
                if (tm != null && tm.name.ToLowerInvariant().Contains(fragment))
                {
                    return tm;
                }
            }

            return null;
        }

        private int ResolveSeed(int? overrideSeed)
        {
            if (overrideSeed.HasValue)
            {
                return overrideSeed.Value;
            }

            return profile.Layout.UseRandomSeed
                ? UnityEngine.Random.Range(int.MinValue, int.MaxValue)
                : profile.Layout.FixedSeed;
        }

        private Vector3Int DetermineOrigin(DungeonLayoutResult layout)
        {
            if (profile.Placement.UseAbsoluteOrigin)
            {
                return profile.Placement.AbsoluteOrigin + profile.Placement.ManualOffset;
            }

            Vector3Int origin = new(profile.Placement.HorizontalPadding, profile.Placement.VerticalOffset, 0);

            if (groundTilemap != null)
            {
                if (!TryGetUsedBounds(groundTilemap, out BoundsInt usedBounds))
                {
                    usedBounds = groundTilemap.cellBounds;
                }

                origin.x = usedBounds.xMax + profile.Placement.HorizontalPadding;
                origin.y = usedBounds.yMin + profile.Placement.VerticalOffset;
                origin.z = usedBounds.zMin;
            }

            origin += profile.Placement.ManualOffset;
            return origin;
        }

        private void AssignRoomTemplates(DungeonLayoutResult layout, System.Random rng)
        {
            DungeonTemplateSettings settings = profile.Templates;
            if (settings == null || (!settings.EnableTemplates && !settings.ForceTemplatesForAllRooms))
            {
                return;
            }

            IReadOnlyList<DungeonTemplateEntry> templateEntries = settings.Templates;
            if (templateEntries == null || templateEntries.Count == 0)
            {
                return;
            }

            bool forceTemplates = settings.ForceTemplatesForAllRooms;

            foreach (DungeonRoom room in layout.Rooms)
            {
                room.TemplatePlacement = null;

                bool isConnector = room.IsConnector;
                if (isConnector && !forceTemplates)
                {
                    continue;
                }

                bool isStart = room == layout.StartRoom;
                bool isEnd = room == layout.EndRoom;

                if (isStart && !settings.AllowStartRoom && !forceTemplates)
                {
                    continue;
                }

                if (isEnd && !settings.AllowEndRoom && !forceTemplates)
                {
                    continue;
                }

                bool requireTemplate = forceTemplates && !isConnector;
                if (!forceTemplates && rng.NextDouble() > settings.TemplateChance)
                {
                    continue;
                }

                Vector2Int roomSize = new Vector2Int(room.Bounds.width, room.Bounds.height);
                List<DungeonTemplateEntry> candidates = new List<DungeonTemplateEntry>();

                for (int i = 0; i < templateEntries.Count; i++)
                {
                    DungeonTemplateEntry entry = templateEntries[i];
                    if (entry == null)
                    {
                        continue;
                    }

                    DungeonRoomTemplate template = entry.Template;
                    if (template == null)
                    {
                        continue;
                    }

                    bool supports = forceTemplates || entry.SupportsRoom(roomSize);
                    if (!supports)
                    {
                        continue;
                    }

                    template.EnsureBaked();
                    if (!forceTemplates && !template.FitsRoom(roomSize))
                    {
                        continue;
                    }

                    candidates.Add(entry);
                }

                if (candidates.Count == 0)
                {
                    if (requireTemplate)
                    {
                        Debug.LogWarning($"[DungeonGenerator] Prefab-only mode: no template found for room {room.Id} ({room.Bounds.width}x{room.Bounds.height}). Falling back to procedural room.");
                    }
                    continue;
                }

                DungeonTemplateEntry chosen = SelectWeighted(candidates, e => e.Weight, rng);
                if (chosen?.Template == null)
                {
                    if (requireTemplate)
                    {
                        Debug.LogWarning($"[DungeonGenerator] Prefab-only mode: failed to pick template for room {room.Id}.");
                    }
                    continue;
                }

                room.TemplatePlacement = new RoomTemplatePlacement
                {
                    Template = chosen.Template
                };

                if (forceTemplates)
                {
                    ResizeRoomToTemplate(room, chosen.Template, layout);
                }
            }
        }

        private void ResizeRoomToTemplate(DungeonRoom room, DungeonRoomTemplate template, DungeonLayoutResult layout)
        {
            if (room == null || template == null || layout == null)
            {
                return;
            }

            Vector2Int desiredSize = template.Size;
            if (desiredSize.x <= 0 || desiredSize.y <= 0)
            {
                return;
            }

            RectInt current = room.Bounds;
            Vector2 center = current.center;
            int width = desiredSize.x;
            int height = desiredSize.y;

            int xMin = Mathf.RoundToInt(center.x - width / 2f);
            int yMin = Mathf.RoundToInt(center.y - height / 2f);
            RectInt newBounds = new RectInt(xMin, yMin, width, height);
            room.Bounds = newBounds;

            // Remove old floor cells from global lookup
            if (room.FloorCells != null)
            {
                for (int i = 0; i < room.FloorCells.Count; i++)
                {
                    layout.AllFloorCells.Remove(room.FloorCells[i]);
                }
            }

            room.FloorCells.Clear();
            room.FloorLookup.Clear();

            for (int y = newBounds.yMin; y < newBounds.yMax; y++)
            {
                for (int x = newBounds.xMin; x < newBounds.xMax; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    room.FloorCells.Add(cell);
                    room.FloorLookup.Add(cell);
                    layout.AllFloorCells.Add(cell);
                }
            }
        }

        private static bool TryGetUsedBounds(Tilemap tilemap, out BoundsInt usedBounds)
        {
            usedBounds = default;
            if (tilemap == null)
            {
                return false;
            }

            BoundsInt bounds = tilemap.cellBounds;
            TileBase[] tiles = tilemap.GetTilesBlock(bounds);

            Vector3Int min = new(int.MaxValue, int.MaxValue, bounds.zMin);
            Vector3Int max = new(int.MinValue, int.MinValue, bounds.zMax);
            bool any = false;

            int index = 0;
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                for (int x = bounds.xMin; x < bounds.xMax; x++)
                {
                    TileBase tile = tiles[index++];
                    if (tile == null)
                    {
                        continue;
                    }

                    any = true;
                    if (x < min.x) min.x = x;
                    if (y < min.y) min.y = y;
                    if (x > max.x) max.x = x;
                    if (y > max.y) max.y = y;
                }
            }

            if (!any)
            {
                return false;
            }

            Vector3Int size = new(max.x - min.x + 1, max.y - min.y + 1, Math.Max(1, bounds.size.z));
            usedBounds = new BoundsInt(min, size);
            return true;
        }

        private void PaintDungeon(DungeonLayoutResult layout, Vector3Int origin, System.Random rng)
        {
            if (layout == null || groundTilemap == null)
            {
                return;
            }

            HashSet<Vector2Int> templateFloorLookup = null;
            bool skipRoomPainting = profile.Templates != null && profile.Templates.ForceTemplatesForAllRooms;
            if (skipRoomPainting)
            {
                templateFloorLookup = BuildTemplateFloorLookup(layout);
            }

            PaintFloors(layout, origin, rng, templateFloorLookup);
            PaintWalls(layout, origin, templateFloorLookup);
            PaintObjectTiles(layout, origin, rng, templateFloorLookup);
        }

        private void PaintFloors(DungeonLayoutResult layout, Vector3Int origin, System.Random rng, HashSet<Vector2Int> templateFloorLookup)
        {
            DungeonTilePalette palette = profile.TilePalette;
            foreach (Vector2Int cell in layout.AllFloorCells)
            {
                if (templateFloorLookup != null && templateFloorLookup.Contains(cell))
                {
                    continue;
                }

                Vector3Int worldCell = new(origin.x + cell.x, origin.y + cell.y, origin.z);
                TileBase tile = palette.FloorTiles.GetRandomTile(rng);
                if (tile == null)
                {
                    continue;
                }

                groundTilemap.SetTile(worldCell, tile);
                groundCells.Add(worldCell);

                if (palette.FloorAccentTiles.HasTiles && rng.NextDouble() < 0.2)
                {
                    TileBase accent = palette.FloorAccentTiles.GetRandomTile(rng);
                    if (accent != null)
                    {
                        groundTilemap.SetTile(worldCell, accent);
                    }
                }

                TryPaintDetailTile(cell, worldCell, layout, rng, templateFloorLookup);
            }
        }

        private void TryPaintDetailTile(Vector2Int localCell, Vector3Int worldCell, DungeonLayoutResult layout, System.Random rng, HashSet<Vector2Int> templateFloorLookup)
        {
            if (groundDetailTilemap == null)
            {
                return;
            }

            DungeonDetailSettings settings = profile.Details;
            if (!settings.DetailTiles.HasTiles || settings.DetailChance <= 0f)
            {
                return;
            }

            float normalized = layout.NormalizedDistanceLookup.TryGetValue(localCell, out float value) ? value : 0f;
            float curveMultiplier = settings.DetailDensityByDepth != null
                ? settings.DetailDensityByDepth.Evaluate(Mathf.Clamp01(normalized))
                : 1f;

            float chance = settings.DetailChance * Mathf.Clamp01(curveMultiplier);
            if (rng.NextDouble() > chance)
            {
                return;
            }

            TileBase detail = settings.DetailTiles.GetRandomTile(rng);
            if (detail == null)
            {
                return;
            }

            groundDetailTilemap.SetTile(worldCell, detail);
            groundDetailCells.Add(worldCell);
        }

        private void PaintWalls(DungeonLayoutResult layout, Vector3Int origin, HashSet<Vector2Int> templateFloorLookup)
        {
            if (wallTilemap == null)
            {
                return;
            }

            DungeonTilePalette palette = profile.TilePalette;
            foreach (WallPlacement placement in layout.WallPlacements)
            {
                DungeonDirection facing = placement.Facing.RotateCounterClockwise();

                TileBase tile = placement.Type switch
                {
                    WallPlacementType.Corner => palette.InnerCorners.Get(placement.Corner) ?? palette.StraightWalls.Get(facing),
                    WallPlacementType.EndCap => palette.EndCapWalls.Get(facing) ?? palette.StraightWalls.Get(facing),
                    _ => palette.StraightWalls.Get(facing)
                };

                if (tile == null)
                {
                    continue;
                }

                if (templateFloorLookup != null && ShouldSkipWall(placement.Position, layout, templateFloorLookup))
                {
                    continue;
                }

                Vector3Int cell = new(origin.x + placement.Position.x, origin.y + placement.Position.y, origin.z);
                wallTilemap.SetTile(cell, tile);
                wallCells.Add(cell);
            }
        }

        private void PaintObjectTiles(DungeonLayoutResult layout, Vector3Int origin, System.Random rng, HashSet<Vector2Int> templateFloorLookup)
        {
            if (objectTilemap == null)
            {
                return;
            }

            DungeonObjectTileSettings settings = profile.ObjectTiles;
            if (!settings.ObjectTiles.HasTiles || settings.PlacementChance <= 0f)
            {
                return;
            }

            foreach (DungeonRoom room in layout.Rooms)
            {
                foreach (Vector2Int cell in room.FloorCells)
                {
                    if (templateFloorLookup != null && templateFloorLookup.Contains(cell))
                    {
                        continue;
                    }

                    if (cell == layout.StartCell || cell == layout.EndCell)
                    {
                        continue;
                    }

                    if (rng.NextDouble() > settings.PlacementChance)
                    {
                        continue;
                    }

                    TileBase tile = settings.ObjectTiles.GetRandomTile(rng);
                    if (tile == null)
                    {
                        continue;
                    }

                    Vector3Int worldCell = new(origin.x + cell.x, origin.y + cell.y, origin.z);
                    objectTilemap.SetTile(worldCell, tile);
                    objectCells.Add(worldCell);
                }
            }
        }

        private void ApplyRoomTemplates(DungeonLayoutResult layout, Vector3Int origin, System.Random rng)
        {
            DungeonTemplateSettings settings = profile.Templates;
            if (settings == null || (!settings.EnableTemplates && !settings.ForceTemplatesForAllRooms))
            {
                return;
            }

            TileBase fallbackFloor = GetDefaultFloorTile();

            foreach (DungeonRoom room in layout.Rooms)
            {
                RoomTemplatePlacement placement = room.TemplatePlacement;
                if (placement == null || placement.Template == null)
                {
                    continue;
                }

                placement.Template.EnsureBaked();
                CaptureCorridorContacts(room, layout);
                placement.TemplateGroundCells.Clear();

                ClearTemplateArea(room, origin);
                Vector3Int anchorCell = new Vector3Int(origin.x + room.Bounds.xMin, origin.y + room.Bounds.yMin, origin.z);

                placement.Template.ApplyToTilemaps(groundTilemap, groundDetailTilemap, wallTilemap, objectTilemap, anchorCell);
                placement.Template.SpawnDecoration(dungeonRoot != null ? dungeonRoot : transform, groundTilemap, anchorCell, spawnedPrefabs);

                IReadOnlyList<DungeonRoomTemplate.TemplateTilemapLayer> layers = placement.Template.Layers;
                if (layers == null)
                {
                    continue;
                }

                for (int i = 0; i < layers.Count; i++)
                {
                    DungeonRoomTemplate.TemplateTilemapLayer layer = layers[i];
                    Tilemap target = ResolveTemplateTarget(layer.TilemapName);
                    if (target == null)
                    {
                        continue;
                    }

                    List<Vector3Int> positions = layer.Positions;
                    for (int j = 0; j < positions.Count; j++)
                    {
                        Vector3Int cell = anchorCell + positions[j];

                        if (target == groundTilemap)
                        {
                            groundCells.Add(cell);
                            Vector3Int localCell = positions[j];
                            Vector2Int worldLocal = new Vector2Int(room.Bounds.xMin + localCell.x, room.Bounds.yMin + localCell.y);
                            placement.TemplateGroundCells.Add(worldLocal);
                        }
                        else if (target == groundDetailTilemap)
                        {
                            groundDetailCells.Add(cell);
                        }
                        else if (target == wallTilemap)
                        {
                            wallCells.Add(cell);
                        }
                        else if (target == objectTilemap)
                        {
                            objectCells.Add(cell);
                        }
                    }
                }

                FillEmptyGroundCells(room, origin, fallbackFloor);
                RebuildRoomFloorCells(room, layout, origin);
                EnsureTemplateDoorways(room, layout, origin, fallbackFloor);
            }
        }

        private HashSet<Vector2Int> BuildTemplateFloorLookup(DungeonLayoutResult layout)
        {
            var lookup = new HashSet<Vector2Int>();
            if (layout == null || layout.Rooms == null)
            {
                return lookup;
            }

            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                DungeonRoom room = layout.Rooms[i];
                if (room?.TemplatePlacement?.Template == null)
                {
                    continue;
                }

                foreach (Vector2Int cell in room.FloorCells)
                {
                    lookup.Add(cell);
                }
            }

            return lookup;
        }

        private static readonly Vector2Int[] NeighborOffsets =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1)
        };

        private bool ShouldSkipWall(Vector2Int wallCell, DungeonLayoutResult layout, HashSet<Vector2Int> templateFloorLookup)
        {
            if (templateFloorLookup == null || templateFloorLookup.Count == 0 || layout?.AllFloorCells == null)
            {
                return false;
            }

            bool adjacentTemplate = false;
            bool adjacentNonTemplate = false;

            for (int i = 0; i < NeighborOffsets.Length; i++)
            {
                Vector2Int neighbor = wallCell + NeighborOffsets[i];
                if (!layout.AllFloorCells.Contains(neighbor))
                {
                    continue;
                }

                if (templateFloorLookup.Contains(neighbor))
                {
                    adjacentTemplate = true;
                }
                else
                {
                    adjacentNonTemplate = true;
                }

                if (adjacentTemplate && adjacentNonTemplate)
                {
                    break;
                }
            }

            return adjacentTemplate && !adjacentNonTemplate;
        }

        private void ClearTemplateArea(DungeonRoom room, Vector3Int origin)
        {
            RectInt bounds = room.Bounds;

            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                for (int x = bounds.xMin; x < bounds.xMax; x++)
                {
                    Vector3Int cell = new Vector3Int(origin.x + x, origin.y + y, origin.z);
                    groundTilemap?.SetTile(cell, null);
                    groundDetailTilemap?.SetTile(cell, null);
                    wallTilemap?.SetTile(cell, null);
                    objectTilemap?.SetTile(cell, null);
                }
            }

            RemoveTrackedCells(groundCells, bounds, origin);
            RemoveTrackedCells(groundDetailCells, bounds, origin);
            RemoveTrackedCells(wallCells, bounds, origin);
            RemoveTrackedCells(objectCells, bounds, origin);
        }

        private void CaptureCorridorContacts(DungeonRoom room, DungeonLayoutResult layout)
        {
            RoomTemplatePlacement placement = room.TemplatePlacement;
            if (placement == null)
            {
                return;
            }

            placement.CorridorContacts.Clear();
            CollectCorridorContacts(room.Bounds, layout, placement.CorridorContacts);
        }

        private void CollectCorridorContacts(RectInt bounds, DungeonLayoutResult layout, ICollection<CorridorContact> output)
        {
            Vector2Int[] directions =
            {
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0),
                new Vector2Int(0, 1),
                new Vector2Int(0, -1)
            };

            HashSet<Vector2Int> seenInterior = new HashSet<Vector2Int>();

            foreach (Vector2Int corridor in layout.CorridorCells)
            {
                if (bounds.Contains(corridor))
                {
                    continue;
                }

                for (int i = 0; i < directions.Length; i++)
                {
                    Vector2Int interior = corridor + directions[i];
                    if (!bounds.Contains(interior))
                    {
                        continue;
                    }

                    if (!seenInterior.Add(interior))
                    {
                        continue;
                    }

                    output.Add(new CorridorContact
                    {
                        CorridorLocal = corridor,
                        InteriorLocal = interior
                    });
                }
            }
        }

        private void FillTightGaps(DungeonLayoutResult layout)
        {
            if (layout == null)
            {
                return;
            }

            HashSet<Vector2Int> additions = new HashSet<Vector2Int>();
            HashSet<Vector2Int> corridorAdditions = new HashSet<Vector2Int>();

            BoundsInt bounds = layout.UsedBounds;
            int minX = bounds.xMin - 2;
            int maxX = bounds.xMax + 2;
            int minY = bounds.yMin - 2;
            int maxY = bounds.yMax + 2;

            Vector2Int east = new Vector2Int(1, 0);
            Vector2Int west = new Vector2Int(-1, 0);
            Vector2Int north = new Vector2Int(0, 1);
            Vector2Int south = new Vector2Int(0, -1);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    if (layout.AllFloorCells.Contains(pos))
                    {
                        continue;
                    }

                    bool hasEast = layout.AllFloorCells.Contains(pos + east);
                    bool hasWest = layout.AllFloorCells.Contains(pos + west);
                    bool hasNorth = layout.AllFloorCells.Contains(pos + north);
                    bool hasSouth = layout.AllFloorCells.Contains(pos + south);

                    int neighborCount = 0;
                    if (hasEast) neighborCount++;
                    if (hasWest) neighborCount++;
                    if (hasNorth) neighborCount++;
                    if (hasSouth) neighborCount++;

                    bool fill = false;
                    if (neighborCount >= 3)
                    {
                        fill = true;
                    }
                    else if ((hasEast && hasWest && !hasNorth && !hasSouth) || (hasNorth && hasSouth && !hasEast && !hasWest))
                    {
                        fill = true;
                    }

                    if (!fill)
                    {
                        continue;
                    }

                    additions.Add(pos);

                    if (!corridorAdditions.Contains(pos))
                    {
                        if (layout.CorridorCells.Contains(pos + east) ||
                            layout.CorridorCells.Contains(pos + west) ||
                            layout.CorridorCells.Contains(pos + north) ||
                            layout.CorridorCells.Contains(pos + south))
                        {
                            corridorAdditions.Add(pos);
                        }
                    }
                }
            }

            if (additions.Count == 0)
            {
                return;
            }

            int newMinX = bounds.xMin;
            int newMinY = bounds.yMin;
            int newMaxX = bounds.xMax;
            int newMaxY = bounds.yMax;

            foreach (Vector2Int cell in additions)
            {
                layout.AllFloorCells.Add(cell);
                if (corridorAdditions.Contains(cell))
                {
                    layout.CorridorCells.Add(cell);
                }
                else
                {
                    layout.CorridorCells.Remove(cell);
                }

                for (int i = 0; i < layout.Rooms.Count; i++)
                {
                    DungeonRoom room = layout.Rooms[i];
                    if (!room.Bounds.Contains(cell))
                    {
                        continue;
                    }

                    if (room.FloorLookup.Add(cell))
                    {
                        room.FloorCells.Add(cell);
                    }
                }

                if (cell.x < newMinX) newMinX = cell.x;
                if (cell.y < newMinY) newMinY = cell.y;
                if (cell.x > newMaxX) newMaxX = cell.x;
                if (cell.y > newMaxY) newMaxY = cell.y;
            }

            layout.UsedBounds = new BoundsInt(new Vector3Int(newMinX, newMinY, bounds.zMin), new Vector3Int(newMaxX - newMinX + 1, newMaxY - newMinY + 1, bounds.size.z));
        }

        private void EnsureTemplateDoorways(DungeonRoom room, DungeonLayoutResult layout, Vector3Int origin, TileBase fallbackFloor)
        {
            EnsureDoorwaysFromCorridors(room, layout, origin, fallbackFloor);
        }

        private TileBase GetDefaultFloorTile()
        {
            DungeonTilePalette palette = profile.TilePalette;
            if (palette == null)
            {
                return null;
            }

            TileBase tile = palette.FloorTiles.FirstTile;
            if (tile != null)
            {
                return tile;
            }

            tile = palette.FloorAccentTiles.FirstTile;
            if (tile != null)
            {
                return tile;
            }

            return null;
        }

        private void RemoveTrackedCells(List<Vector3Int> list, RectInt bounds, Vector3Int origin)
        {
            if (list == null)
            {
                return;
            }

            for (int i = list.Count - 1; i >= 0; i--)
            {
                Vector3Int cell = list[i];
                int localX = cell.x - origin.x;
                int localY = cell.y - origin.y;
                if (localX >= bounds.xMin && localX < bounds.xMax && localY >= bounds.yMin && localY < bounds.yMax)
                {
                    list.RemoveAt(i);
                }
            }
        }

        private void RemoveTrackedCell(List<Vector3Int> list, Vector3Int cell)
        {
            if (list == null)
            {
                return;
            }

            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] == cell)
                {
                    list.RemoveAt(i);
                    break;
                }
            }
        }

        private void AddTrackedCell(List<Vector3Int> list, Vector3Int cell)
        {
            if (list == null)
            {
                return;
            }

            if (!list.Contains(cell))
            {
                list.Add(cell);
            }
        }

        private void ClearNonFloorAt(Vector3Int cell)
        {
            if (groundDetailTilemap != null && groundDetailTilemap.HasTile(cell))
            {
                groundDetailTilemap.SetTile(cell, null);
                RemoveTrackedCell(groundDetailCells, cell);
            }

            if (objectTilemap != null && objectTilemap.HasTile(cell))
            {
                objectTilemap.SetTile(cell, null);
                RemoveTrackedCell(objectCells, cell);
            }

            if (wallTilemap != null && wallTilemap.HasTile(cell))
            {
                wallTilemap.SetTile(cell, null);
                RemoveTrackedCell(wallCells, cell);
            }
        }

        private void FillEmptyGroundCells(DungeonRoom room, Vector3Int origin, TileBase fallbackFloor)
        {
            if (groundTilemap == null || fallbackFloor == null)
            {
                return;
            }

            HashSet<Vector2Int> templateGround = room.TemplatePlacement?.TemplateGroundCells;
            RectInt bounds = room.Bounds;
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                for (int x = bounds.xMin; x < bounds.xMax; x++)
                {
                    Vector2Int worldLocal = new Vector2Int(x, y);
                    if (templateGround != null && templateGround.Contains(worldLocal))
                    {
                        continue;
                    }

                    Vector3Int worldCell = new Vector3Int(origin.x + x, origin.y + y, origin.z);
                    if (groundTilemap.GetTile(worldCell) == null)
                    {
                        groundTilemap.SetTile(worldCell, fallbackFloor);
                        AddTrackedCell(groundCells, worldCell);
                    }
                }
            }
        }

        private void EnsureDoorwaysFromCorridors(DungeonRoom room, DungeonLayoutResult layout, Vector3Int origin, TileBase fallbackFloor)
        {
            RectInt bounds = room.Bounds;
            if (bounds.width <= 0 || bounds.height <= 0)
            {
                return;
            }

            List<CorridorContact> contacts = room.TemplatePlacement?.CorridorContacts;
            bool usingBuffer = false;
            if (contacts == null || contacts.Count == 0)
            {
                corridorContactBuffer.Clear();
                CollectCorridorContacts(bounds, layout, corridorContactBuffer);
                contacts = corridorContactBuffer;
                usingBuffer = true;
            }

            for (int i = 0; i < contacts.Count; i++)
            {
                CorridorContact contact = contacts[i];

                if (!layout.CorridorCells.Contains(contact.CorridorLocal))
                {
                    continue;
                }

                Vector3Int insideCell = new Vector3Int(origin.x + contact.InteriorLocal.x, origin.y + contact.InteriorLocal.y, origin.z);
                Vector3Int corridorCell = new Vector3Int(origin.x + contact.CorridorLocal.x, origin.y + contact.CorridorLocal.y, origin.z);

                ClearNonFloorAt(insideCell);

                if (groundTilemap != null)
                {
                    if (groundTilemap.GetTile(insideCell) == null && fallbackFloor != null)
                    {
                        groundTilemap.SetTile(insideCell, fallbackFloor);
                    }

                    if (groundTilemap.GetTile(corridorCell) == null && fallbackFloor != null)
                    {
                        groundTilemap.SetTile(corridorCell, fallbackFloor);
                    }

                    if (groundTilemap.GetTile(insideCell) != null)
                    {
                        AddTrackedCell(groundCells, insideCell);
                    }

                    if (groundTilemap.GetTile(corridorCell) != null)
                    {
                        AddTrackedCell(groundCells, corridorCell);
                    }
                }

                layout.AllFloorCells.Add(contact.InteriorLocal);
                layout.CorridorCells.Remove(contact.InteriorLocal);

                if (room.FloorLookup.Add(contact.InteriorLocal))
                {
                    room.FloorCells.Add(contact.InteriorLocal);
                }
            }

            if (usingBuffer)
            {
                contacts.Clear();
            }
        }


        private void RebuildRoomFloorCells(DungeonRoom room, DungeonLayoutResult layout, Vector3Int origin)
        {
            if (groundTilemap == null)
            {
                return;
            }

            RectInt bounds = room.Bounds;
            room.FloorCells.Clear();
            room.FloorLookup.Clear();

            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                for (int x = bounds.xMin; x < bounds.xMax; x++)
                {
                    Vector2Int local = new Vector2Int(x, y);
                    Vector3Int cell = new Vector3Int(origin.x + x, origin.y + y, origin.z);

                    TileBase tile = groundTilemap.GetTile(cell);
                    if (tile != null)
                    {
                        room.FloorCells.Add(local);
                        room.FloorLookup.Add(local);
                        layout.AllFloorCells.Add(local);
                        layout.CorridorCells.Remove(local);
                    }
                    else
                    {
                        layout.AllFloorCells.Remove(local);
                        layout.CorridorCells.Remove(local);
                    }
                }
            }
        }

        private Tilemap ResolveTemplateTarget(string tilemapName)
        {
            if (string.IsNullOrEmpty(tilemapName))
            {
                return null;
            }

            string lower = tilemapName.ToLowerInvariant();

            Tilemap result = MatchTilemapExact(lower);
            if (result != null)
            {
                return result;
            }

            if (groundTilemap != null && groundTilemap.name.ToLowerInvariant().Contains(lower)) return groundTilemap;
            if (groundDetailTilemap != null && groundDetailTilemap.name.ToLowerInvariant().Contains(lower)) return groundDetailTilemap;
            if (wallTilemap != null && wallTilemap.name.ToLowerInvariant().Contains(lower)) return wallTilemap;
            if (objectTilemap != null && objectTilemap.name.ToLowerInvariant().Contains(lower)) return objectTilemap;

            return null;
        }

        private Tilemap MatchTilemapExact(string lower)
        {
            if (groundTilemap != null && groundTilemap.name.Equals(lower, StringComparison.OrdinalIgnoreCase))
            {
                return groundTilemap;
            }

            if (groundDetailTilemap != null && groundDetailTilemap.name.Equals(lower, StringComparison.OrdinalIgnoreCase))
            {
                return groundDetailTilemap;
            }

            if (wallTilemap != null && wallTilemap.name.Equals(lower, StringComparison.OrdinalIgnoreCase))
            {
                return wallTilemap;
            }

            if (objectTilemap != null && objectTilemap.name.Equals(lower, StringComparison.OrdinalIgnoreCase))
            {
                return objectTilemap;
            }

            return null;
        }

        private void SpawnDungeonContent(DungeonLayoutResult layout, Vector3Int origin, System.Random rng)
        {
            SpawnPortals(layout, origin);
            SpawnEnemies(layout, origin, rng);
            SpawnProps(layout, origin, rng);
        }

        #region Portal & Spawn Helpers (implemented in partial section)

        partial void SpawnPortals(DungeonLayoutResult layout, Vector3Int origin);
        partial void SpawnEnemies(DungeonLayoutResult layout, Vector3Int origin, System.Random rng);
        partial void SpawnProps(DungeonLayoutResult layout, Vector3Int origin, System.Random rng);
        private partial DungeonRuntimeData BuildRuntimeData(DungeonLayoutResult layout, Vector3Int origin, int seed);

        #endregion

        private static void ClearTilemapCells(Tilemap tilemap, List<Vector3Int> cells)
        {
            if (tilemap == null || cells == null || cells.Count == 0)
            {
                return;
            }

            for (int i = 0; i < cells.Count; i++)
            {
                tilemap.SetTile(cells[i], null);
            }
        }

        private void DestroyRuntimeObject(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(instance);
            }
            else
            {
                DestroyImmediate(instance);
            }
        }

        private void DetachExitPortalCallbacks(GameObject portalInstance)
        {
            if (portalInstance == null)
            {
                return;
            }

            if (portalInstance.TryGetComponent(out Interactable interactable))
            {
                interactable.PortalTeleported -= HandleExitPortalTeleported;
                interactable.PortalSequenceCompleted -= HandleExitPortalSequenceCompleted;
            }
        }

        private void PopulateColliderTilemaps()
        {
            if (colliderAutoPlacers == null || colliderAutoPlacers.Length == 0)
            {
                return;
            }

            for (int i = 0; i < colliderAutoPlacers.Length; i++)
            {
                TileColliderAutoPlacer autoPlacer = colliderAutoPlacers[i];
                if (autoPlacer == null)
                {
                    continue;
                }

                autoPlacer.PopulateColliders();
            }
        }

        [Serializable]
        public class DungeonGeneratedEvent : UnityEvent<DungeonRuntimeData>
        {
        }
    }
}

namespace DungeonGeneration
{
    public partial class DungeonGenerator
    {
        partial void SpawnPortals(DungeonLayoutResult layout, Vector3Int origin)
        {
            if (groundTilemap == null)
            {
                return;
            }

            DungeonPortalSettings portals = profile.Portals;

            Vector2Int startLocal = ResolvePortalPlacement(layout.StartCell, layout.StartRoom, layout, origin, padding: 2);
            Vector2Int endLocal = ResolvePortalPlacement(layout.EndCell, layout.EndRoom, layout, origin, padding: 2);

            layout.StartCell = startLocal;
            layout.EndCell = endLocal;

            Vector3Int startCell = new(origin.x + startLocal.x, origin.y + startLocal.y, origin.z);
            Vector3Int endCell = new(origin.x + endLocal.x, origin.y + endLocal.y, origin.z);

            Vector3 startWorld = groundTilemap != null ? groundTilemap.GetCellCenterWorld(startCell) : (Vector3)startCell;
            Vector3 endWorld = groundTilemap != null ? groundTilemap.GetCellCenterWorld(endCell) : (Vector3)endCell;

            Transform parent = dungeonRoot != null ? dungeonRoot : transform;

            if (portals.SpawnStartPortal && portals.StartPortalPrefab != null)
            {
                startPortalInstance = InstantiateRuntime(portals.StartPortalPrefab, startWorld + portals.StartPortalOffset, parent);
                if (startPortalInstance != null && startPortalInstance.TryGetComponent(out Interactable interactable))
                {
                    // Forward-only progression: return to overworld instead of previous level
                    Transform returnTarget = profile.Progression != null && profile.Progression.ForwardOnly
                        ? overworldReturnAnchor
                        : (previousLevelReturnAnchor != null ? previousLevelReturnAnchor : overworldReturnAnchor);
                    interactable.ConfigurePortalDestination(returnTarget, startPortalReturnOffset);
                }

                if (startPortalInstance != null)
                {
                    nextLevelEntryAnchor = startPortalInstance.transform;
                }
            }
            else
            {
                startPortalInstance = null;
                if (nextLevelEntryAnchor == null)
                {
                    nextLevelEntryAnchor = overworldReturnAnchor;
                }
            }

            if (portals.SpawnExitPortal && portals.ExitPortalPrefab != null)
            {
                exitPortalInstance = InstantiateRuntime(portals.ExitPortalPrefab, endWorld + portals.ExitPortalOffset, parent);
                if (exitPortalInstance != null && exitPortalInstance.TryGetComponent(out Interactable interactable))
                {
                    bool chainToNextLevel = ShouldChainExitToNextLevel();
                    exitLeadsToNextLevel = chainToNextLevel;
                    Transform destination = chainToNextLevel && nextLevelEntryAnchor != null
                        ? nextLevelEntryAnchor
                        : overworldReturnAnchor;
                    interactable.ConfigurePortalDestination(destination, exitPortalDestinationOffset);
                    if (chainToNextLevel)
                    {
                        interactable.PortalTeleported += HandleExitPortalTeleported;
                        interactable.PortalSequenceCompleted += HandleExitPortalSequenceCompleted;
                    }
                    else
                    {
                        interactable.PortalTeleported -= HandleExitPortalTeleported;
                        interactable.PortalSequenceCompleted -= HandleExitPortalSequenceCompleted;
                    }
                }

                // Optional per-level end chest next to the exit
                var levels = profile.Levels;
                var levelDef = levels != null ? levels.ResolveLevel(GetRuntimeDungeonLevel()) : null;
                if (levelDef != null && levelDef.SpawnEndChest)
                {
                    Vector2Int chestLocal;
                    if (!TryFindClearAdjacentCell(endLocal, origin, 2, out chestLocal))
                    {
                        chestLocal = endLocal;
                    }
                    Vector3Int chestCell = new(origin.x + chestLocal.x, origin.y + chestLocal.y, origin.z);
                    Vector3 chestPos = groundTilemap != null ? groundTilemap.GetCellCenterWorld(chestCell) : (Vector3)chestCell;
                    chestPos += levelDef.ChestOffset;
                    GameObject chestInstance = SpawnPrefab(levelDef.ChestPrefab, chestPos, parent);
                    if (chestInstance != null && levelDef.Loot != null && levelDef.Loot.Length > 0)
                    {
                        if (chestInstance.TryGetComponent(out Interactable chestInteractable))
                        {
                            chestInteractable.ConfigureLootDrops(levelDef.Loot, true);
                        }
                    }
                }
            }
        }

        partial void SpawnEnemies(DungeonLayoutResult layout, Vector3Int origin, System.Random rng)
        {
            DungeonSpawnSettings spawns = profile.Spawns;
            if (spawns.EnemyPrefabs == null || spawns.EnemyPrefabs.Count == 0)
            {
                return;
            }

            Transform parent = dungeonRoot != null ? dungeonRoot : transform;
            var levels = profile.Levels;
            int currentLevel = levels != null ? GetRuntimeDungeonLevel() : 1;
            var levelDef = levels != null ? levels.ResolveLevel(currentLevel) : null;
            var pool = levels != null ? levels.GetEnemyPoolForLevel(currentLevel, spawns.EnemyPrefabs) : spawns.EnemyPrefabs;

            foreach (DungeonRoom room in layout.Rooms)
            {
                if (room == layout.StartRoom || room == layout.EndRoom)
                {
                    continue;
                }

                if (rng.NextDouble() > spawns.EnemyRoomChance)
                {
                    continue;
                }

                int packCount = RandomRangeInclusive(rng, spawns.EnemyPackSizeRange.x, spawns.EnemyPackSizeRange.y);
                for (int pack = 0; pack < packCount; pack++)
                {
                    DungeonEnemySpawnEntry entry = SelectWeighted(pool, e => e.Weight, rng);
                    if (entry == null || entry.Prefab == null)
                    {
                        continue;
                    }

                    int enemyCount = RandomRangeInclusive(rng, entry.CountRange.x, entry.CountRange.y);
                    for (int i = 0; i < enemyCount; i++)
                    {
                        if (!TryPickWorldSpawnPosition(room, layout, origin, rng, entry.AllowCorridors, out Vector3Int cell, out Vector3 world))
                        {
                            continue;
                        }

                        GameObject enemy = SpawnPrefab(entry.Prefab, world, parent);
                        ApplyLevelScaling(enemy);
                    }
                }
            }

            if (levelDef != null)
            {
                SpawnGuaranteedEnemies(layout, origin, rng, parent, levelDef.GuaranteedEnemies);
            }
        }

        partial void SpawnProps(DungeonLayoutResult layout, Vector3Int origin, System.Random rng)
        {
            DungeonSpawnSettings spawns = profile.Spawns;
            if ((spawns.PropPrefabs == null || spawns.PropPrefabs.Count == 0) &&
                (spawns.InteractablePrefabs == null || spawns.InteractablePrefabs.Count == 0))
            {
                return;
            }

            Transform parent = dungeonRoot != null ? dungeonRoot : transform;

            foreach (DungeonRoom room in layout.Rooms)
            {
                if (room == layout.StartRoom || room == layout.EndRoom)
                {
                    continue;
                }

                TrySpawnPrefabGroup(room, layout, origin, rng, spawns.PropChance, spawns.PropCountRange, spawns.PropPrefabs, parent);
                TrySpawnPrefabGroup(room, layout, origin, rng, spawns.InteractableChance, spawns.InteractableCountRange, spawns.InteractablePrefabs, parent);
            }
        }

        private void TrySpawnPrefabGroup(
            DungeonRoom room,
            DungeonLayoutResult layout,
            Vector3Int origin,
            System.Random rng,
            float chance,
            Vector2Int countRange,
            IReadOnlyList<DungeonPrefabSpawnRule> pool,
            Transform parent)
        {
            if (pool == null || pool.Count == 0 || chance <= 0f)
            {
                return;
            }

            if (rng.NextDouble() > chance)
            {
                return;
            }

            int targetCount = RandomRangeInclusive(rng, countRange.x, countRange.y);
            targetCount = Mathf.Min(targetCount, room.FloorCells.Count);

            for (int i = 0; i < targetCount; i++)
            {
                DungeonPrefabSpawnRule rule = SelectWeighted(pool, r => r.Weight, rng);
                if (rule == null || rule.Prefab == null)
                {
                    continue;
                }

                if (!TryPickWorldSpawnPosition(room, layout, origin, rng, rule.AllowCorridors, out Vector3Int cell, out Vector3 centerWorld))
                {
                    continue;
                }

                Vector3 position;
                if (groundTilemap != null)
                {
                    position = rule.AlignToCellCenter
                        ? centerWorld
                        : groundTilemap.CellToWorld(cell);
                }
                else
                {
                    position = rule.AlignToCellCenter ? centerWorld : (Vector3)cell;
                }

                position += rule.PositionOffset;

                SpawnPrefab(rule.Prefab, position, parent);
            }
        }

        private void SpawnGuaranteedEnemies(
            DungeonLayoutResult layout,
            Vector3Int origin,
            System.Random rng,
            Transform parent,
            IReadOnlyList<DungeonGuaranteedEnemy> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                DungeonGuaranteedEnemy entry = entries[i];
                if (entry == null || entry.Prefab == null)
                {
                    continue;
                }

                int targetCount = Mathf.Max(0, entry.Count);
                if (targetCount == 0)
                {
                    continue;
                }

                int spawned = 0;
                int maxAttempts = Mathf.Max(16, targetCount * 8);

                while (spawned < targetCount && maxAttempts-- > 0)
                {
                    DungeonRoom room = GetRandomCombatRoom(layout, rng);
                    if (room == null)
                    {
                        break;
                    }

                    if (!TryPickWorldSpawnPosition(room, layout, origin, rng, entry.AllowCorridors, out _, out Vector3 world))
                    {
                        continue;
                    }

                    GameObject enemy = SpawnPrefab(entry.Prefab, world, parent);
                    ApplyLevelScaling(enemy);
                    ApplyGuaranteedStatModifiers(enemy, entry);
                    spawned++;
                }
            }
        }

        private DungeonRoom GetRandomCombatRoom(DungeonLayoutResult layout, System.Random rng)
        {
            if (layout == null || layout.Rooms == null || layout.Rooms.Count == 0)
            {
                return null;
            }

            const int Attempts = 64;
            for (int i = 0; i < Attempts; i++)
            {
                DungeonRoom candidate = layout.Rooms[rng.Next(layout.Rooms.Count)];
                if (candidate == null || candidate == layout.StartRoom || candidate == layout.EndRoom)
                {
                    continue;
                }

                if (candidate.FloorCells == null || candidate.FloorCells.Count == 0)
                {
                    continue;
                }

                return candidate;
            }

            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                DungeonRoom candidate = layout.Rooms[i];
                if (candidate != null && candidate != layout.StartRoom && candidate != layout.EndRoom && candidate.FloorCells.Count > 0)
                {
                    return candidate;
                }
            }

            return layout.EndRoom ?? layout.StartRoom ?? layout.Rooms[0];
        }

        private void ApplyGuaranteedStatModifiers(GameObject enemy, DungeonGuaranteedEnemy entry)
        {
            if (enemy == null || entry == null || !entry.ApplyStatModifiers)
            {
                return;
            }

            if (entry.HealthMultiplier > 0f)
            {
                var health = enemy.GetComponentInChildren<EnemyHealth2D>();
                if (health != null)
                {
                    health.ApplyExternalHealthMultiplier(entry.HealthMultiplier, entry.FillScaledEnemiesToMaxHealth);
                }
            }

            if (entry.DamageMultiplier > 0f)
            {
                var ai = enemy.GetComponentInChildren<SmallScaleInc.CharacterCreatorFantasy.EnemyAI>();
                if (ai != null)
                {
                    ai.damageMultiplier *= entry.DamageMultiplier;
                }
            }
        }

        private partial DungeonRuntimeData BuildRuntimeData(DungeonLayoutResult layout, Vector3Int origin, int seed)
        {
            var data = new DungeonRuntimeData
            {
                Seed = seed,
                Origin = origin,
                DungeonBounds = layout.UsedBounds,
                StartCell = layout.StartCell,
                EndCell = layout.EndCell,
                NormalizedDistanceLookup = layout.NormalizedDistanceLookup
            };

            if (groundTilemap != null)
            {
                Vector3Int startCell = new(origin.x + layout.StartCell.x, origin.y + layout.StartCell.y, origin.z);
                Vector3Int endCell = new(origin.x + layout.EndCell.x, origin.y + layout.EndCell.y, origin.z);
                data.StartWorldPosition = groundTilemap.GetCellCenterWorld(startCell);
                data.EndWorldPosition = groundTilemap.GetCellCenterWorld(endCell);
            }

            data.StartPortal = startPortalInstance != null ? startPortalInstance.transform : null;
            data.ExitPortal = exitPortalInstance != null ? exitPortalInstance.transform : null;

            foreach (DungeonRoom room in layout.Rooms)
            {
                var runtimeRoom = new DungeonRuntimeRoom
                {
                    Id = room.Id,
                    Bounds = room.Bounds,
                    IsCorridor = room.IsConnector,
                    TemplateName = room.TemplatePlacement?.Template != null ? room.TemplatePlacement.Template.name : string.Empty
                };

                foreach (Vector2Int local in room.FloorCells)
                {
                    Vector3Int cell = new(origin.x + local.x, origin.y + local.y, origin.z);
                    Vector3 world = groundTilemap != null ? groundTilemap.GetCellCenterWorld(cell) : (Vector3)cell;
                    runtimeRoom.WorldPositions.Add(world);
                }

                data.RoomsInternal.Add(runtimeRoom);
            }

            return data;
        }

        private GameObject SpawnPrefab(GameObject prefab, Vector3 position, Transform parent)
        {
            GameObject instance = InstantiateRuntime(prefab, position, parent);
            if (instance != null)
            {
                spawnedPrefabs.Add(instance);
            }
            return instance;
        }

        private void ApplyLevelScaling(GameObject enemy)
        {
            if (enemy == null || profile == null) return;
            var levels = profile.Levels;
            if (levels == null || !levels.EnableLevels) return;
            int currentLevel = GetRuntimeDungeonLevel();
            float hpScale = Mathf.Max(1f, levels.GetHealthScale(currentLevel));
            float dmgScale = Mathf.Max(1f, levels.GetDamageScale(currentLevel));

            // Health
            var health = enemy.GetComponentInChildren<EnemyHealth2D>();
            if (health != null)
            {
                int baseMax = Mathf.Max(1, health.maxHealth);
                int scaled = Mathf.Max(1, Mathf.RoundToInt(baseMax * hpScale));
                health.maxHealth = scaled;
                // Best-effort set current to max on spawn
                var field = typeof(EnemyHealth2D).GetField("currentHealth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(health, scaled);
                }
            }

            // Damage
            var ai = enemy.GetComponentInChildren<SmallScaleInc.CharacterCreatorFantasy.EnemyAI>();
            if (ai != null)
            {
                ai.meleeDamage = Mathf.Max(1, Mathf.RoundToInt(ai.meleeDamage * dmgScale));
                ai.projectileDamage = Mathf.Max(1, Mathf.RoundToInt(ai.projectileDamage * dmgScale));
            }
        }

        private GameObject InstantiateRuntime(GameObject prefab, Vector3 position, Transform parent)
        {
            if (prefab == null)
            {
                return null;
            }

            Quaternion rotation = prefab.transform.rotation;
            if (Application.isPlaying)
            {
                return Instantiate(prefab, position, rotation, parent);
            }

            GameObject instance = Instantiate(prefab, position, rotation, parent);
            instance.name = $"{prefab.name} (DungeonRuntime)";
            return instance;
        }

        private void EnsureRuntimeLevelInitialized()
        {
            if (runtimeLevelInitialized)
            {
                return;
            }

            runtimeLevelInitialized = true;
            ResetRuntimeLevelFromProfile();
        }

        private void ResetRuntimeLevelFromProfile()
        {
            var levels = profile != null ? profile.Levels : null;
            if (levels == null)
            {
                runtimeCurrentLevel = 1;
                return;
            }

            runtimeCurrentLevel = Mathf.Clamp(levels.CurrentLevel, 1, levels.TotalLevels);
        }

        private void ClampRuntimeLevelToBounds()
        {
            var levels = profile != null ? profile.Levels : null;
            if (levels == null)
            {
                runtimeCurrentLevel = 1;
                return;
            }

            runtimeCurrentLevel = Mathf.Clamp(runtimeCurrentLevel, 1, levels.TotalLevels);
        }

        private int GetRuntimeDungeonLevel()
        {
            ClampRuntimeLevelToBounds();
            return runtimeCurrentLevel;
        }

        private bool ShouldChainExitToNextLevel()
        {
            var levels = profile != null ? profile.Levels : null;
            if (levels == null || !levels.EnableLevels)
            {
                return false;
            }

            ClampRuntimeLevelToBounds();
            return runtimeCurrentLevel < levels.TotalLevels;
        }

        private void AdvanceToNextLevel(Transform player)
        {
            var levels = profile != null ? profile.Levels : null;
            if (levels == null || !levels.EnableLevels)
            {
                return;
            }

            if (runtimeCurrentLevel >= levels.TotalLevels)
            {
                return;
            }

            isAdvancingLevel = true;
            try
            {
                runtimeCurrentLevel = Mathf.Clamp(runtimeCurrentLevel + 1, 1, levels.TotalLevels);
                GenerateDungeon(null, true);
                MovePlayerToNextLevelStart(player);
                AnnounceLevelEntry(player);
            }
            finally
            {
                isAdvancingLevel = false;
            }
        }

        private void MovePlayerToNextLevelStart(Transform player)
        {
            if (player == null)
            {
                return;
            }

            Vector3 destination = player.position;
            if (RuntimeData != null)
            {
                destination = RuntimeData.StartWorldPosition;
            }
            else if (startPortalInstance != null)
            {
                destination = startPortalInstance.transform.position;
            }

            player.position = destination;

            Camera mainCamera = Camera.main;
            if (mainCamera != null && mainCamera.TryGetComponent(out SmoothCameraFollow cameraFollow) && cameraFollow.target == player)
            {
                cameraFollow.SnapToTargetImmediately();
            }
        }

        public void AnnounceCurrentLevelAtPlayer(Transform player)
        {
            AnnounceLevelEntry(player);
        }

        private void HandleExitPortalTeleported(Interactable portal, Transform player)
        {
            if (portal == null)
            {
                return;
            }

            if (!ShouldChainExitToNextLevel() || isAdvancingLevel)
            {
                return;
            }

            preservedExitPortalDuringTransition = portal.gameObject;
            portal.PortalTeleported -= HandleExitPortalTeleported;
            AdvanceToNextLevel(player);
        }

        private void HandleExitPortalSequenceCompleted(Interactable portal, Transform player)
        {
            if (portal == null)
            {
                return;
            }

            portal.PortalTeleported -= HandleExitPortalTeleported;
            portal.PortalSequenceCompleted -= HandleExitPortalSequenceCompleted;

            if (preservedExitPortalDuringTransition == portal.gameObject)
            {
                DestroyRuntimeObject(portal.gameObject);
                preservedExitPortalDuringTransition = null;
            }
        }

        private void AnnounceLevelEntry(Transform player)
        {
            if (player == null || profile == null)
            {
                return;
            }

            EnsureRuntimeLevelInitialized();

            var levels = profile.Levels;
            if (levels == null || !levels.EnableLevels)
            {
                return;
            }

            string label = GetCurrentLevelDisplayName();
            if (string.IsNullOrWhiteSpace(label))
            {
                return;
            }

            CombatTextManager manager = CombatTextManager.Instance;
            if (manager == null)
            {
                return;
            }

            manager.SpawnStatus(label, player.position);
        }

        private string GetCurrentLevelDisplayName()
        {
            var levels = profile.Levels;
            if (levels == null)
            {
                return string.Empty;
            }

            return levels.GetLevelDisplayName(GetRuntimeDungeonLevel());
        }

        private static int RandomRangeInclusive(System.Random rng, int min, int max)
        {
            if (max < min)
            {
                (min, max) = (max, min);
            }

            return rng.Next(min, max + 1);
        }

        private static T SelectWeighted<T>(IReadOnlyList<T> entries, Func<T, float> weightSelector, System.Random rng) where T : class
        {
            if (entries == null || entries.Count == 0)
            {
                return null;
            }

            float total = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                T entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                total += Mathf.Max(0f, weightSelector(entry));
            }

            if (total <= 0f)
            {
                return null;
            }

            double roll = rng.NextDouble() * total;
            float cumulative = 0f;

            for (int i = 0; i < entries.Count; i++)
            {
                T entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                cumulative += Mathf.Max(0f, weightSelector(entry));
                if (roll <= cumulative)
                {
                    return entry;
                }
            }

            return entries[^1];
        }

        private Vector2Int ResolvePortalPlacement(Vector2Int preferred, DungeonRoom room, DungeonLayoutResult layout, Vector3Int origin, int padding = 2)
{
    for (int pad = Mathf.Max(0, padding); pad >= 0; pad--)
    {
        if (HasPortalClearance(preferred, origin, pad))
        {
            return preferred;
        }

        Vector2Int best = preferred;
        int bestScore = int.MaxValue;
        bool found = false;

        if (room != null)
        {
            for (int i = 0; i < room.FloorCells.Count; i++)
            {
                Vector2Int candidate = room.FloorCells[i];
                if (!HasPortalClearance(candidate, origin, pad))
                {
                    continue;
                }
                int score = DistanceSquared(preferred, candidate);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                    found = true;
                }
            }
        }

        if (!found)
        {
            foreach (Vector2Int candidate in layout.AllFloorCells)
            {
                if (!HasPortalClearance(candidate, origin, pad))
                {
                    continue;
                }
                int score = DistanceSquared(preferred, candidate);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                    found = true;
                }
            }
        }

        if (found)
        {
            return best;
        }
    }

    return preferred;
}
        private bool HasPortalClearance(Vector2Int localCell, Vector3Int origin, int padding = 2)
{
    Vector3Int worldCell = new(origin.x + localCell.x, origin.y + localCell.y, origin.z);

    if (groundTilemap != null && !groundTilemap.HasTile(worldCell))
    {
        return false;
    }

    if (wallTilemap != null)
    {
        int pad = Mathf.Max(0, padding);
        for (int dy = -pad; dy <= pad; dy++)
        {
            for (int dx = -pad; dx <= pad; dx++)
            {
                Vector3Int check = new Vector3Int(worldCell.x + dx, worldCell.y + dy, worldCell.z);
                if (wallTilemap.HasTile(check))
                {
                    return false;
                }
            }
        }
    }

    return true;
}
        private bool TryFindClearAdjacentCell(Vector2Int centerLocal, Vector3Int origin, int padding, out Vector2Int result)
        {
            Vector2Int[] cardinals = { new Vector2Int(1,0), new Vector2Int(0,1), new Vector2Int(-1,0), new Vector2Int(0,-1) };
            for (int dist = 1; dist <= 2; dist++)
            {
                for (int i = 0; i < cardinals.Length; i++)
                {
                    Vector2Int candidate = centerLocal + cardinals[i] * dist;
                    Vector3Int worldCell = new Vector3Int(origin.x + candidate.x, origin.y + candidate.y, origin.z);
                    if (groundTilemap != null && !groundTilemap.HasTile(worldCell))
                    {
                        continue;
                    }
                    if (HasPortalClearance(candidate, origin, padding))
                    {
                        result = candidate;
                        return true;
                    }
                }
            }
            result = centerLocal;
            return false;
        }

        private static int DistanceSquared(Vector2Int a, Vector2Int b)
        {
            int dx = a.x - b.x;
            int dy = a.y - b.y;
            return dx * dx + dy * dy;
        }

        private bool TryPickWorldSpawnPosition(
            DungeonRoom room,
            DungeonLayoutResult layout,
            Vector3Int origin,
            System.Random rng,
            bool allowCorridors,
            out Vector3Int cell,
            out Vector3 worldPosition)
        {
            cell = default;
            worldPosition = default;

            const int MaxAttempts = 48;
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                Vector2Int local = room.GetRandomFloorCell(rng, allowCorridors, layout.CorridorCells);
                if (local == layout.StartCell || local == layout.EndCell)
                {
                    continue;
                }

                Vector3Int candidateCell = new(origin.x + local.x, origin.y + local.y, origin.z);
                if (groundTilemap != null && !groundTilemap.HasTile(candidateCell))
                {
                    continue;
                }

                cell = candidateCell;
                worldPosition = groundTilemap != null
                    ? groundTilemap.GetCellCenterWorld(candidateCell)
                    : (Vector3)candidateCell;
                return true;
            }

            return false;
        }
    }

    [Serializable]
    public class DungeonRuntimeData
    {
        public int Seed { get; internal set; }
        public Vector3Int Origin { get; internal set; }
        public BoundsInt DungeonBounds { get; internal set; }
        public Vector2Int StartCell { get; internal set; }
        public Vector2Int EndCell { get; internal set; }
        public Vector3 StartWorldPosition { get; internal set; }
        public Vector3 EndWorldPosition { get; internal set; }
        public Transform StartPortal { get; internal set; }
        public Transform ExitPortal { get; internal set; }
        public Dictionary<Vector2Int, float> NormalizedDistanceLookup { get; internal set; }

        internal List<DungeonRuntimeRoom> RoomsInternal { get; } = new();
        public IReadOnlyList<DungeonRuntimeRoom> Rooms => RoomsInternal;
    }

    [Serializable]
    public class DungeonRuntimeRoom
    {
        public int Id { get; internal set; }
        public RectInt Bounds { get; internal set; }
        public bool IsCorridor { get; internal set; }
        public string TemplateName { get; internal set; }
        public List<Vector3> WorldPositions { get; } = new();
    }
}

namespace DungeonGeneration
{
    internal class DungeonLayoutResult
    {
        public HashSet<Vector2Int> AllFloorCells { get; } = new();
        public HashSet<Vector2Int> CorridorCells { get; } = new();
        public List<DungeonRoom> Rooms { get; } = new();
        public Dictionary<Vector2Int, float> NormalizedDistanceLookup { get; } = new();
        public List<WallPlacement> WallPlacements { get; } = new();
        public List<CorridorSegment> CorridorSegments { get; } = new();
        public Vector2Int StartCell { get; internal set; }
        public Vector2Int EndCell { get; internal set; }
        public DungeonRoom StartRoom { get; internal set; }
        public DungeonRoom EndRoom { get; internal set; }
        public BoundsInt UsedBounds { get; internal set; }
    }

    internal class DungeonRoom
    {
        public int Id { get; set; }
        public RectInt Bounds { get; set; }
        public bool IsConnector { get; set; }
        public List<Vector2Int> FloorCells { get; } = new();
        public HashSet<Vector2Int> FloorLookup { get; } = new();
        public RoomTemplatePlacement TemplatePlacement { get; set; }

        public bool Contains(Vector2Int cell) => FloorLookup.Contains(cell);

        public Vector2Int GetRandomFloorCell(System.Random rng, bool allowCorridors, HashSet<Vector2Int> corridors)
        {
            if (FloorCells.Count == 0)
            {
                return Vector2Int.RoundToInt(Bounds.center);
            }

            const int Attempts = 24;
            for (int i = 0; i < Attempts; i++)
            {
                Vector2Int candidate = FloorCells[rng.Next(FloorCells.Count)];
                if (allowCorridors || corridors == null || !corridors.Contains(candidate))
                {
                    return candidate;
                }
            }

            return FloorCells[0];
        }
    }

    internal class RoomTemplatePlacement
    {
        public DungeonRoomTemplate Template;
        public List<CorridorContact> CorridorContacts { get; } = new();
        public HashSet<Vector2Int> TemplateGroundCells { get; } = new();
    }

    internal struct CorridorContact
    {
        public Vector2Int CorridorLocal;
        public Vector2Int InteriorLocal;
    }

    internal class CorridorSegment
    {
        public DungeonRoom From;
        public DungeonRoom To;
        public List<Vector2Int> Cells { get; } = new();
    }

    internal enum WallPlacementType
    {
        Straight,
        EndCap,
        Corner
    }

    internal struct WallPlacement
    {
        public Vector2Int Position;
        public WallPlacementType Type;
        public DungeonDirection Facing;
        public DungeonCorner Corner;
    }

    internal static class DungeonLayoutBuilder
    {
        private static readonly Vector2Int[] CardinalOffsets =
        {
            new(1, 0),
            new(0, -1),
            new(-1, 0),
            new(0, 1)
        };

        public static DungeonLayoutResult Build(DungeonLayoutSettings settings, System.Random rng)
        {
            var result = new DungeonLayoutResult();
            RectInt rootRect = new RectInt(0, 0, settings.GridSize.x, settings.GridSize.y);
            List<BspNode> leaves = new() { new BspNode(rootRect) };

            SplitUntilTarget(leaves, settings, rng);

            for (int i = 0; i < leaves.Count; i++)
            {
                DungeonRoom room = CarveRoom(leaves[i], settings, rng, i);
                if (room == null)
                {
                    continue;
                }

                leaves[i].Room = room;
                result.Rooms.Add(room);
                for (int j = 0; j < room.FloorCells.Count; j++)
                {
                    result.AllFloorCells.Add(room.FloorCells[j]);
                }
            }

            ConnectRooms(result, leaves, settings, rng);
            BreakLongCorridors(result, settings, rng);
            ComputeBounds(result);
            DetermineStartEnd(result);
            BuildWalls(result);

            return result;
        }

        private static void SplitUntilTarget(List<BspNode> leaves, DungeonLayoutSettings settings, System.Random rng)
        {
            int safety = settings.TargetRoomCount * 8 + 16;

            while (leaves.Count < settings.TargetRoomCount && safety-- > 0)
            {
                BspNode candidate = SelectSplitCandidate(leaves, settings, rng);
                if (candidate == null || !TrySplit(candidate, settings, rng))
                {
                    break;
                }

                leaves.Remove(candidate);
                leaves.Add(candidate.Left);
                leaves.Add(candidate.Right);
            }
        }

        private static BspNode SelectSplitCandidate(List<BspNode> leaves, DungeonLayoutSettings settings, System.Random rng)
        {
            List<BspNode> candidates = new();
            for (int i = 0; i < leaves.Count; i++)
            {
                if (CanSplit(leaves[i], settings))
                {
                    candidates.Add(leaves[i]);
                }
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            return candidates[rng.Next(candidates.Count)];
        }

        private static bool CanSplit(BspNode node, DungeonLayoutSettings settings)
        {
            Vector2Int minRoom = settings.MinRoomSize;
            int padding = settings.RoomPadding * 2;
            return node.Area.width > minRoom.x + padding && node.Area.height > minRoom.y + padding;
        }

        private static bool TrySplit(BspNode node, DungeonLayoutSettings settings, System.Random rng)
        {
            float ratio = node.Area.width / (float)node.Area.height;
            bool vertical = ratio >= 1.35f ? true : ratio <= 0.65f ? false : rng.NextDouble() > 0.5;

            if (vertical)
            {
                int min = node.Area.xMin + settings.MinRoomSize.x + settings.RoomPadding;
                int max = node.Area.xMax - settings.MinRoomSize.x - settings.RoomPadding;
                if (max - min < 4)
                {
                    return false;
                }

                int split = rng.Next(min, max);
                node.Left = new BspNode(new RectInt(node.Area.xMin, node.Area.yMin, split - node.Area.xMin, node.Area.height)) { Parent = node };
                node.Right = new BspNode(new RectInt(split, node.Area.yMin, node.Area.xMax - split, node.Area.height)) { Parent = node };
            }
            else
            {
                int min = node.Area.yMin + settings.MinRoomSize.y + settings.RoomPadding;
                int max = node.Area.yMax - settings.MinRoomSize.y - settings.RoomPadding;
                if (max - min < 4)
                {
                    return false;
                }

                int split = rng.Next(min, max);
                node.Left = new BspNode(new RectInt(node.Area.xMin, node.Area.yMin, node.Area.width, split - node.Area.yMin)) { Parent = node };
                node.Right = new BspNode(new RectInt(node.Area.xMin, split, node.Area.width, node.Area.yMax - split)) { Parent = node };
            }

            return true;
        }

        private static DungeonRoom CarveRoom(BspNode leaf, DungeonLayoutSettings settings, System.Random rng, int id)
        {
            Vector2Int min = settings.MinRoomSize;
            Vector2Int max = settings.MaxRoomSize;

            int availableWidth = Mathf.Max(min.x, leaf.Area.width - settings.RoomPadding * 2);
            int availableHeight = Mathf.Max(min.y, leaf.Area.height - settings.RoomPadding * 2);

            if (availableWidth < min.x || availableHeight < min.y)
            {
                return null;
            }

            int width = Mathf.Clamp(rng.Next(min.x, max.x + 1), min.x, availableWidth);
            int height = Mathf.Clamp(rng.Next(min.y, max.y + 1), min.y, availableHeight);

            int xMin = leaf.Area.xMin + settings.RoomPadding;
            int yMin = leaf.Area.yMin + settings.RoomPadding;
            int xMax = leaf.Area.xMax - settings.RoomPadding - width;
            int yMax = leaf.Area.yMax - settings.RoomPadding - height;

            int x = rng.Next(xMin, Mathf.Max(xMin, xMax + 1));
            int y = rng.Next(yMin, Mathf.Max(yMin, yMax + 1));

            RectInt bounds = new RectInt(x, y, width, height);
            var room = new DungeonRoom
            {
                Id = id,
                Bounds = bounds
            };

            for (int iy = bounds.yMin; iy < bounds.yMax; iy++)
            {
                for (int ix = bounds.xMin; ix < bounds.xMax; ix++)
                {
                    Vector2Int cell = new(ix, iy);
                    room.FloorCells.Add(cell);
                    room.FloorLookup.Add(cell);
                }
            }

            return room;
        }

        private static void ConnectRooms(DungeonLayoutResult result, List<BspNode> leaves, DungeonLayoutSettings settings, System.Random rng)
        {
            HashSet<ulong> carved = new();
            Queue<BspNode> queue = new();
            if (leaves.Count > 0)
            {
                queue.Enqueue(GetRoot(leaves[0]));
            }

            while (queue.Count > 0)
            {
                BspNode node = queue.Dequeue();
                if (node == null || node.IsLeaf)
                {
                    continue;
                }

                queue.Enqueue(node.Left);
                queue.Enqueue(node.Right);

                DungeonRoom leftRoom = node.Left?.GetRoom();
                DungeonRoom rightRoom = node.Right?.GetRoom();

                if (leftRoom == null || rightRoom == null)
                {
                    continue;
                }

                ulong key = ComposeKey(leftRoom.Id, rightRoom.Id);
                if (carved.Contains(key))
                {
                    continue;
                }

                carved.Add(key);
                CarveCorridor(result, leftRoom, rightRoom, settings.CorridorWidth, rng);
            }

            // Optional loops
            if (settings.MaxExtraConnectors <= 0 || settings.ExtraConnectorChance <= 0f)
            {
                return;
            }

            List<(DungeonRoom a, DungeonRoom b, ulong key)> potential = new();
            for (int i = 0; i < result.Rooms.Count; i++)
            {
                for (int j = i + 1; j < result.Rooms.Count; j++)
                {
                    ulong key = ComposeKey(result.Rooms[i].Id, result.Rooms[j].Id);
                    if (carved.Contains(key))
                    {
                        continue;
                    }
                    potential.Add((result.Rooms[i], result.Rooms[j], key));
                }
            }

            int extra = 0;
            for (int i = 0; i < potential.Count && extra < settings.MaxExtraConnectors; i++)
            {
                if (rng.NextDouble() > settings.ExtraConnectorChance)
                {
                    continue;
                }

                var pair = potential[rng.Next(potential.Count)];
                if (carved.Contains(pair.key))
                {
                    continue;
                }

                carved.Add(pair.key);
                CarveCorridor(result, pair.a, pair.b, settings.CorridorWidth, rng, true);
                extra++;
            }
        }

        private static void BreakLongCorridors(DungeonLayoutResult result, DungeonLayoutSettings settings, System.Random rng)
        {
            if (!settings.EnableCorridorBreakRooms)
            {
                return;
            }

            int maxLength = settings.MaxCorridorLength;
            if (maxLength <= 0 || result.CorridorSegments.Count == 0)
            {
                return;
            }

            Vector2Int minSize = settings.CorridorBreakRoomMinSize;
            Vector2Int maxSize = settings.CorridorBreakRoomMaxSize;

            for (int i = 0; i < result.CorridorSegments.Count; i++)
            {
                CorridorSegment segment = result.CorridorSegments[i];
                if (segment == null || segment.Cells.Count == 0)
                {
                    continue;
                }

                List<Vector2Int> ordered = new List<Vector2Int>();
                HashSet<Vector2Int> seen = new HashSet<Vector2Int>();
                for (int c = 0; c < segment.Cells.Count; c++)
                {
                    Vector2Int cell = segment.Cells[c];
                    if (seen.Add(cell))
                    {
                        ordered.Add(cell);
                    }
                }

                if (ordered.Count <= maxLength)
                {
                    continue;
                }

                int breaks = ordered.Count / maxLength;
                if (ordered.Count % maxLength == 0)
                {
                    breaks = Mathf.Max(0, breaks - 1);
                }

                breaks = Mathf.Max(1, breaks);

                for (int insert = 1; insert <= breaks; insert++)
                {
                    int index = Mathf.Clamp((int)Mathf.Round((ordered.Count - 1) * (insert / (float)(breaks + 1))), 0, ordered.Count - 1);
                    Vector2Int center = ordered[index];

                    int width = rng.Next(minSize.x, maxSize.x + 1);
                    int height = rng.Next(minSize.y, maxSize.y + 1);

                    RectInt newBounds = new RectInt(center.x - width / 2, center.y - height / 2, width, height);

                    if (!CanPlaceBreakRoom(result, newBounds))
                    {
                        continue;
                    }

                    DungeonRoom newRoom = new DungeonRoom
                    {
                        Id = result.Rooms.Count,
                        Bounds = newBounds,
                        IsConnector = true
                    };

                    for (int y = newBounds.yMin; y < newBounds.yMax; y++)
                    {
                        for (int x = newBounds.xMin; x < newBounds.xMax; x++)
                        {
                            Vector2Int cell = new Vector2Int(x, y);
                            newRoom.FloorCells.Add(cell);
                            newRoom.FloorLookup.Add(cell);
                            result.AllFloorCells.Add(cell);
                            result.CorridorCells.Remove(cell);
                        }
                    }

                    result.Rooms.Add(newRoom);
                }
            }
        }

        private static bool CanPlaceBreakRoom(DungeonLayoutResult result, RectInt bounds)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                for (int x = bounds.xMin; x < bounds.xMax; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (result.AllFloorCells.Contains(cell) && !result.CorridorCells.Contains(cell))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static void CarveCorridor(DungeonLayoutResult result, DungeonRoom a, DungeonRoom b, int width, System.Random rng, bool connector = false)
        {
            Vector2Int start = Vector2Int.RoundToInt(a.Bounds.center);
            Vector2Int end = Vector2Int.RoundToInt(b.Bounds.center);

            bool horizontalFirst = rng.NextDouble() > 0.5;

            var segment = new CorridorSegment
            {
                From = a,
                To = b
            };
            List<Vector2Int> segmentCellList = new List<Vector2Int>();
            HashSet<Vector2Int> segmentCellSet = new HashSet<Vector2Int>();

            if (horizontalFirst)
            {
                CarveHorizontal(result, start, new Vector2Int(end.x, start.y), width, segmentCellList, segmentCellSet);
                CarveVertical(result, new Vector2Int(end.x, start.y), end, width, segmentCellList, segmentCellSet);
            }
            else
            {
                CarveVertical(result, start, new Vector2Int(start.x, end.y), width, segmentCellList, segmentCellSet);
                CarveHorizontal(result, new Vector2Int(start.x, end.y), end, width, segmentCellList, segmentCellSet);
            }

            if (connector)
            {
                a.IsConnector = true;
                b.IsConnector = true;
            }

            segment.Cells.AddRange(segmentCellList);
            result.CorridorSegments.Add(segment);
        }

        private static void CarveHorizontal(DungeonLayoutResult result, Vector2Int from, Vector2Int to, int width, List<Vector2Int> segmentCells = null, HashSet<Vector2Int> segmentSet = null)
        {
            int minX = Mathf.Min(from.x, to.x);
            int maxX = Mathf.Max(from.x, to.x);

            for (int x = minX; x <= maxX; x++)
            {
                for (int offset = -width / 2; offset <= width / 2; offset++)
                {
                    Vector2Int cell = new(x, from.y + offset);
                    result.AllFloorCells.Add(cell);
                    result.CorridorCells.Add(cell);
                    if (segmentSet == null || segmentSet.Add(cell))
                    {
                        segmentCells?.Add(cell);
                    }
                }
            }
        }

        private static void CarveVertical(DungeonLayoutResult result, Vector2Int from, Vector2Int to, int width, List<Vector2Int> segmentCells = null, HashSet<Vector2Int> segmentSet = null)
        {
            int minY = Mathf.Min(from.y, to.y);
            int maxY = Mathf.Max(from.y, to.y);

            for (int y = minY; y <= maxY; y++)
            {
                for (int offset = -width / 2; offset <= width / 2; offset++)
                {
                    Vector2Int cell = new(from.x + offset, y);
                    result.AllFloorCells.Add(cell);
                    result.CorridorCells.Add(cell);
                    if (segmentSet == null || segmentSet.Add(cell))
                    {
                        segmentCells?.Add(cell);
                    }
                }
            }
        }

        private static void ComputeBounds(DungeonLayoutResult result)
        {
            if (result.AllFloorCells.Count == 0)
            {
                result.UsedBounds = new BoundsInt(Vector3Int.zero, Vector3Int.one);
                return;
            }

            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;

            foreach (Vector2Int cell in result.AllFloorCells)
            {
                if (cell.x < minX) minX = cell.x;
                if (cell.y < minY) minY = cell.y;
                if (cell.x > maxX) maxX = cell.x;
                if (cell.y > maxY) maxY = cell.y;
            }

            Vector3Int min = new(minX, minY, 0);
            Vector3Int size = new(maxX - minX + 1, maxY - minY + 1, 1);
            result.UsedBounds = new BoundsInt(min, size);
        }

        private static void DetermineStartEnd(DungeonLayoutResult result)
        {
            if (result.AllFloorCells.Count == 0)
            {
                result.StartCell = Vector2Int.zero;
                result.EndCell = Vector2Int.zero;
                return;
            }

            Vector2Int start = default;
            foreach (Vector2Int cell in result.AllFloorCells)
            {
                start = cell;
                break;
            }

            Dictionary<Vector2Int, int> distances;
            Vector2Int extremeA = FindFurthest(start, result.AllFloorCells, out _);
            Vector2Int extremeB = FindFurthest(extremeA, result.AllFloorCells, out distances);

            result.StartCell = extremeA;
            result.EndCell = extremeB;

            float maxDistance = distances.TryGetValue(extremeB, out int max) ? Mathf.Max(1, max) : 1f;
            foreach (KeyValuePair<Vector2Int, int> kvp in distances)
            {
                result.NormalizedDistanceLookup[kvp.Key] = Mathf.Clamp01(kvp.Value / maxDistance);
            }

            foreach (DungeonRoom room in result.Rooms)
            {
                if (room.Contains(result.StartCell))
                {
                    result.StartRoom = room;
                }
                if (room.Contains(result.EndCell))
                {
                    result.EndRoom = room;
                }
            }
        }

        private static Vector2Int FindFurthest(Vector2Int origin, HashSet<Vector2Int> floor, out Dictionary<Vector2Int, int> distances)
        {
            distances = new Dictionary<Vector2Int, int>(floor.Count);
            Queue<Vector2Int> queue = new();
            queue.Enqueue(origin);
            distances[origin] = 0;
            Vector2Int furthest = origin;

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                int dist = distances[current];
                if (dist > distances[furthest])
                {
                    furthest = current;
                }

                for (int i = 0; i < CardinalOffsets.Length; i++)
                {
                    Vector2Int next = current + CardinalOffsets[i];
                    if (!floor.Contains(next) || distances.ContainsKey(next))
                    {
                        continue;
                    }

                    distances[next] = dist + 1;
                    queue.Enqueue(next);
                }
            }

            return furthest;
        }

        private static void BuildWalls(DungeonLayoutResult result)
        {
            Dictionary<Vector2Int, int> adjacency = new();

            foreach (Vector2Int floor in result.AllFloorCells)
            {
                for (int i = 0; i < CardinalOffsets.Length; i++)
                {
                    Vector2Int neighbor = floor + CardinalOffsets[i];
                    if (result.AllFloorCells.Contains(neighbor))
                    {
                        continue;
                    }

                    if (!adjacency.ContainsKey(neighbor))
                    {
                        adjacency[neighbor] = 0;
                    }
                    adjacency[neighbor]++;
                }
            }

            foreach (KeyValuePair<Vector2Int, int> kvp in adjacency)
            {
                Vector2Int position = kvp.Key;
                int count = kvp.Value;

                WallPlacement placement = new()
                {
                    Position = position
                };

                if (count >= 2)
                {
                    List<DungeonDirection> interiorDirs = new();
                    for (int i = 0; i < CardinalOffsets.Length; i++)
                    {
                        Vector2Int neighbor = position + CardinalOffsets[i];
                        if (result.AllFloorCells.Contains(neighbor))
                        {
                            interiorDirs.Add((DungeonDirection)i);
                        }
                    }

                    if (interiorDirs.Count == 2 && Math.Abs((int)interiorDirs[0] - (int)interiorDirs[1]) % 2 == 1)
                    {
                        placement.Type = WallPlacementType.Corner;
                        Vector2Int dirA = DungeonDirectionUtility.ToOffset(interiorDirs[0]);
                        Vector2Int dirB = DungeonDirectionUtility.ToOffset(interiorDirs[1]);
                        placement.Corner = DungeonDirectionUtility.ToCorner(dirA, dirB);
                    }
                    else
                    {
                        placement.Type = WallPlacementType.Straight;
                        placement.Facing = DetermineFacing(position, result.AllFloorCells, interiorDirs);
                    }
                }
                else if (count == 1)
                {
                    placement.Type = WallPlacementType.EndCap;
                    placement.Facing = DetermineFacing(position, result.AllFloorCells, null);
                }
                else
                {
                    placement.Type = WallPlacementType.Straight;
                    placement.Facing = DetermineFacing(position, result.AllFloorCells, null);
                }

                result.WallPlacements.Add(placement);
            }
        }

        private static DungeonDirection DetermineFacing(Vector2Int wallCell, HashSet<Vector2Int> floorCells, List<DungeonDirection> interior)
        {
            if (interior != null && interior.Count > 0)
            {
                return interior[0];
            }

            for (int i = 0; i < CardinalOffsets.Length; i++)
            {
                if (floorCells.Contains(wallCell + CardinalOffsets[i]))
                {
                    return (DungeonDirection)i;
                }
            }

            return DungeonDirection.East;
        }

        private static ulong ComposeKey(int a, int b)
        {
            if (a > b)
            {
                (a, b) = (b, a);
            }

            return ((ulong)a << 32) | (uint)b;
        }

        private static BspNode GetRoot(BspNode node)
        {
            while (node.Parent != null)
            {
                node = node.Parent;
            }
            return node;
        }

        private class BspNode
        {
            public RectInt Area;
            public BspNode Parent;
            public BspNode Left;
            public BspNode Right;
            public DungeonRoom Room;

            public bool IsLeaf => Left == null && Right == null;

            public BspNode(RectInt area)
            {
                Area = area;
            }

            public DungeonRoom GetRoom()
            {
                if (IsLeaf)
                {
                    return Room;
                }

                DungeonRoom leftRoom = Left?.GetRoom();
                if (leftRoom != null)
                {
                    return leftRoom;
                }

                return Right?.GetRoom();
            }
        }
    }
}









