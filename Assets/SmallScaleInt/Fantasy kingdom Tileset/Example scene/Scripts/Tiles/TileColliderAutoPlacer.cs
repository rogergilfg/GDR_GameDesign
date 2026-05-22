using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;


/// <summary>
/// Automates the placement of collider tiles on a collider tilemap based on the names of the
/// visual tiles present on one or more source tilemaps. This allows setting up default collider pairings for
/// large tile sets without having to place each collider manually.
/// </summary>
[ExecuteAlways]
[MovedFrom(true, null, null, "TileColliderAutoPlacer")]
public class TileColliderAutoPlacer : MonoBehaviour
{
    [SerializeField]
    private Tilemap sourceTilemap;

    [SerializeField]
    private Tilemap secondarySourceTilemap;

    [SerializeField]
    private Tilemap tertiarySourceTilemap;

    [SerializeField]
    private Tilemap colliderTilemap;

    [SerializeField]
    private bool clearColliderTilemapBeforePopulate = true;

    [SerializeField]
    private List<NameBasedIgnoreRule> ignoreRules = new List<NameBasedIgnoreRule>();

    [SerializeField]
    private List<NameBasedColliderRule> specialRules = new List<NameBasedColliderRule>();

    [SerializeField]
    private List<ColliderRuleGroup> defaultGroups = new List<ColliderRuleGroup>();

    [SerializeField]
    private List<CornerColliderRuleGroup> cornerGroups = new List<CornerColliderRuleGroup>();

    private readonly Dictionary<Vector3Int, Vector3Int> appliedColliderOffsets = new Dictionary<Vector3Int, Vector3Int>();

    public Tilemap SourceTilemap => sourceTilemap;

    public Tilemap SecondarySourceTilemap => secondarySourceTilemap;

    public Tilemap TertiarySourceTilemap => tertiarySourceTilemap;

    public Tilemap ColliderTilemap => colliderTilemap;

    /// <summary>
    /// Clears and repopulates the collider tilemap so that each tile on the source tilemap gets a
    /// corresponding collider tile when a matching rule is found.
    /// </summary>
    [ContextMenu("Populate Colliders")]
    public void PopulateColliders()
    {
        List<Tilemap> orderedSourceTilemaps = GetOrderedSourceTilemaps();
        if (orderedSourceTilemaps.Count == 0)
        {
            Debug.LogWarning($"{nameof(TileColliderAutoPlacer)} on {name} is missing a source tilemap reference.");
            return;
        }

        if (colliderTilemap == null)
        {
            Debug.LogWarning($"{nameof(TileColliderAutoPlacer)} on {name} is missing a collider tilemap reference.");
            return;
        }

        if (clearColliderTilemapBeforePopulate)
        {
            colliderTilemap.ClearAllTiles();
        }
        appliedColliderOffsets.Clear();

        BoundsInt bounds = GetCombinedBounds(orderedSourceTilemaps);
        foreach (Vector3Int position in bounds.allPositionsWithin)
        {
            TileBase sourceTile = GetSourceTileAt(orderedSourceTilemaps, position);
            if (sourceTile == null)
            {
                ClearExistingColliderAt(position);
                continue;
            }

            if (!TryResolveColliderTile(sourceTile, out TileBase colliderTile, out Vector3Int placementOffset))
            {
                ClearExistingColliderAt(position);
                continue;
            }

            Vector3Int targetPosition = position + placementOffset;
            colliderTilemap.SetTile(targetPosition, colliderTile);
            appliedColliderOffsets[position] = placementOffset;
        }

        MarkColliderTilemapDirty();
    }

    private void Reset()
    {
        EnsureColliderTilemapReference();
    }

    private void Awake()
    {
        EnsureColliderTilemapReference();
    }

    private void OnValidate()
    {
        EnsureColliderTilemapReference();
    }

    private void EnsureColliderTilemapReference()
    {
        if (colliderTilemap == null)
        {
            colliderTilemap = GetComponent<Tilemap>();
        }
    }

    public bool TryResolveColliderTile(TileBase sourceTile, out TileBase colliderTile, out Vector3Int placementOffset)
    {
        colliderTile = null;
        placementOffset = Vector3Int.zero;
        string tileName = sourceTile != null ? sourceTile.name : string.Empty;
        if (string.IsNullOrEmpty(tileName))
        {
            return false;
        }

        foreach (NameBasedIgnoreRule ignoreRule in ignoreRules)
        {
            if (ignoreRule == null)
            {
                continue;
            }

            if (ignoreRule.IsMatch(tileName))
            {
                return false;
            }
        }

        foreach (NameBasedColliderRule rule in specialRules)
        {
            if (rule == null || rule.ColliderTile == null)
            {
                continue;
            }

            if (rule.IsMatch(tileName))
            {
                colliderTile = rule.ColliderTile;
                placementOffset = rule.PlacementOffset;
                return true;
            }
        }

        foreach (CornerColliderRuleGroup cornerGroup in cornerGroups)
        {
            if (cornerGroup == null)
            {
                continue;
            }

            if (cornerGroup.TryResolve(tileName, out TileBase cornerCollider, out Vector3Int cornerOffset))
            {
                colliderTile = cornerCollider;
                placementOffset = cornerOffset;
                return true;
            }
        }

        foreach (ColliderRuleGroup group in defaultGroups)
        {
            if (group == null)
            {
                continue;
            }

            if (group.TryResolve(tileName, out TileBase resolvedCollider, out Vector3Int groupOffset))
            {
                colliderTile = resolvedCollider;
                placementOffset = groupOffset;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Updates the collider tile at the given cell position so that it matches the supplied source
    /// tile according to the configured rules. When no matching rule is found, any collider at the
    /// position is cleared.
    /// </summary>
    /// <param name="cellPosition">Grid position on the collider tilemap to update.</param>
    /// <param name="sourceTile">Tile placed on the source tilemap that should dictate the collider.</param>
    public void ApplyColliderForTile(Vector3Int cellPosition, TileBase sourceTile)
    {
        if (colliderTilemap == null)
        {
            return;
        }

        bool modified = ClearExistingColliderAt(cellPosition);

        if (sourceTile == null || !TryResolveColliderTile(sourceTile, out TileBase colliderTile, out Vector3Int placementOffset))
        {
            if (modified)
            {
                MarkColliderTilemapDirty();
            }
            return;
        }

        Vector3Int targetCell = cellPosition + placementOffset;
        TileBase existingTile = colliderTilemap.GetTile(targetCell);
        if (existingTile != colliderTile)
        {
            colliderTilemap.SetTile(targetCell, colliderTile);
            modified = true;
        }

        appliedColliderOffsets[cellPosition] = placementOffset;

        if (modified)
        {
            MarkColliderTilemapDirty();
        }
    }

    private bool ClearExistingColliderAt(Vector3Int sourcePosition)
    {
        if (!appliedColliderOffsets.TryGetValue(sourcePosition, out Vector3Int previousOffset))
        {
            return false;
        }

        Vector3Int previousCell = sourcePosition + previousOffset;
        if (colliderTilemap != null && colliderTilemap.GetTile(previousCell) != null)
        {
            colliderTilemap.SetTile(previousCell, null);
        }

        appliedColliderOffsets.Remove(sourcePosition);
        return true;
    }

    private void MarkColliderTilemapDirty()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && colliderTilemap != null)
        {
            UnityEditor.EditorUtility.SetDirty(colliderTilemap);
        }
#endif
    }

    private List<Tilemap> GetOrderedSourceTilemaps()
    {
        List<Tilemap> orderedTilemaps = new List<Tilemap>(2);

        void AddUnique(Tilemap tilemap)
        {
            if (tilemap != null && !orderedTilemaps.Contains(tilemap))
            {
                orderedTilemaps.Add(tilemap);
            }
        }

        AddWallsTilemapIfAvailable(orderedTilemaps);
        AddUnique(sourceTilemap);
        AddUnique(secondarySourceTilemap);
        AddUnique(tertiarySourceTilemap);

        return orderedTilemaps;
    }

    private void AddWallsTilemapIfAvailable(List<Tilemap> orderedTilemaps)
    {
        Tilemap wallsTilemap = null;
        if (IsWallsTilemap(sourceTilemap))
        {
            wallsTilemap = sourceTilemap;
        }
        else if (IsWallsTilemap(secondarySourceTilemap))
        {
            wallsTilemap = secondarySourceTilemap;
        }

        if (wallsTilemap != null && !orderedTilemaps.Contains(wallsTilemap))
        {
            orderedTilemaps.Add(wallsTilemap);
        }
    }

    private static bool IsWallsTilemap(Tilemap tilemap)
    {
        return tilemap != null && string.Equals(tilemap.name, "Walls", StringComparison.OrdinalIgnoreCase);
    }

    private static BoundsInt GetCombinedBounds(List<Tilemap> tilemaps)
    {
        if (tilemaps == null || tilemaps.Count == 0)
        {
            return new BoundsInt(Vector3Int.zero, Vector3Int.zero);
        }

        BoundsInt combinedBounds = default;
        bool hasBounds = false;

        foreach (Tilemap tilemap in tilemaps)
        {
            if (tilemap == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = tilemap.cellBounds;
                hasBounds = true;
                continue;
            }

            BoundsInt tilemapBounds = tilemap.cellBounds;
            Vector3Int min = Vector3Int.Min(combinedBounds.min, tilemapBounds.min);
            Vector3Int max = Vector3Int.Max(combinedBounds.max, tilemapBounds.max);
            combinedBounds.SetMinMax(min, max);
        }

        return combinedBounds;
    }

    private static TileBase GetSourceTileAt(List<Tilemap> tilemaps, Vector3Int position)
    {
        if (tilemaps == null)
        {
            return null;
        }

        foreach (Tilemap tilemap in tilemaps)
        {
            if (tilemap == null)
            {
                continue;
            }

            TileBase tile = tilemap.GetTile(position);
            if (tile != null)
            {
                return tile;
            }
        }

        return null;
    }

    [Serializable]
    private class CornerColliderRuleGroup
    {
        [SerializeField]
        private string nameStartsWith = string.Empty;

        [SerializeField]
        private bool caseSensitive = false;

        [SerializeField]
        private CornerDirectionCollider eastCollider = new CornerDirectionCollider("_E");

        [SerializeField]
        private CornerDirectionCollider southCollider = new CornerDirectionCollider("_S");

        [SerializeField]
        private CornerDirectionCollider westCollider = new CornerDirectionCollider("_W");

        [SerializeField]
        private CornerDirectionCollider northCollider = new CornerDirectionCollider("_N");

        public bool TryResolve(string tileName, out TileBase colliderTile, out Vector3Int placementOffset)
        {
            colliderTile = null;
            placementOffset = Vector3Int.zero;
            if (string.IsNullOrEmpty(tileName))
            {
                return false;
            }

            StringComparison comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            if (!string.IsNullOrEmpty(nameStartsWith) && !tileName.StartsWith(nameStartsWith, comparison))
            {
                return false;
            }

            if (ResolveDirection(tileName, comparison, eastCollider, out colliderTile, out placementOffset))
            {
                return true;
            }

            if (ResolveDirection(tileName, comparison, southCollider, out colliderTile, out placementOffset))
            {
                return true;
            }

            if (ResolveDirection(tileName, comparison, westCollider, out colliderTile, out placementOffset))
            {
                return true;
            }

            return ResolveDirection(tileName, comparison, northCollider, out colliderTile, out placementOffset);
        }

        private static bool ResolveDirection(string tileName, StringComparison comparison, CornerDirectionCollider directionCollider, out TileBase colliderTile, out Vector3Int placementOffset)
        {
            colliderTile = null;
            placementOffset = Vector3Int.zero;
            if (directionCollider == null || directionCollider.ColliderTile == null)
            {
                return false;
            }

            return directionCollider.TryResolve(tileName, comparison, out colliderTile, out placementOffset);
        }
    }

    [Serializable]
    private class CornerDirectionCollider
    {
        [SerializeField]
        private string suffix = string.Empty;

        [SerializeField]
        private TileBase colliderTile;

        [SerializeField]
        private Vector3Int placementOffset = Vector3Int.zero;

        public CornerDirectionCollider()
        {
        }

        public CornerDirectionCollider(string suffix)
        {
            this.suffix = suffix;
        }

        public TileBase ColliderTile => colliderTile;
        public bool TryResolve(string tileName, StringComparison comparison, out TileBase resolvedTile, out Vector3Int offset)
        {
            resolvedTile = null;
            offset = placementOffset;
            if (string.IsNullOrEmpty(tileName))
            {
                return false;
            }

            if (string.IsNullOrEmpty(suffix))
            {
                return false;
            }

            if (tileName.EndsWith(suffix, comparison))
            {
                resolvedTile = colliderTile;
                return true;
            }

            offset = Vector3Int.zero;
            return false;
        }
    }

    [Serializable]
    private class ColliderRuleGroup
    {
        [SerializeField]
        private string nameStartsWith = string.Empty;

        [SerializeField]
        private bool caseSensitive = false;

        [SerializeField]
        private List<DirectionCollider> directionColliders = new List<DirectionCollider>();

        [SerializeField]
        private TileBase defaultCollider;

        [SerializeField]
        private Vector3Int defaultOffset = Vector3Int.zero;

        public bool TryResolve(string tileName, out TileBase colliderTile, out Vector3Int placementOffset)
        {
            colliderTile = null;
            placementOffset = Vector3Int.zero;
            if (string.IsNullOrEmpty(tileName))
            {
                return false;
            }

            StringComparison comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            if (!string.IsNullOrEmpty(nameStartsWith) && !tileName.StartsWith(nameStartsWith, comparison))
            {
                return false;
            }

            foreach (DirectionCollider entry in directionColliders)
            {
                if (entry == null || entry.ColliderTile == null)
                {
                    continue;
                }

                if (entry.TryResolve(tileName, comparison, out colliderTile, out placementOffset))
                {
                    return true;
                }
            }

            if (defaultCollider != null)
            {
                colliderTile = defaultCollider;
                placementOffset = defaultOffset;
                return true;
            }

            return false;
        }
    }

    [Serializable]
    private class DirectionCollider
    {
        [SerializeField]
        private string suffix = string.Empty;

        [SerializeField]
        private TileBase colliderTile;

        [SerializeField]
        private Vector3Int placementOffset = Vector3Int.zero;

        public TileBase ColliderTile => colliderTile;

        public bool TryResolve(string tileName, StringComparison comparison, out TileBase resolvedTile, out Vector3Int offset)
        {
            resolvedTile = null;
            offset = Vector3Int.zero;
            if (string.IsNullOrEmpty(tileName))
            {
                return false;
            }

            if (string.IsNullOrEmpty(suffix))
            {
                return false;
            }

            if (tileName.EndsWith(suffix, comparison))
            {
                resolvedTile = colliderTile;
                offset = placementOffset;
                return true;
            }

            return false;
        }
    }

    [Serializable]
    private class NameBasedColliderRule
    {
        [SerializeField]
        private string nameStartsWith = string.Empty;

        [SerializeField]
        private string nameContains = string.Empty;

        [SerializeField]
        private string nameEndsWith = string.Empty;

        [SerializeField]
        private bool caseSensitive = false;

        [SerializeField]
        private TileBase colliderTile;

        [SerializeField]
        private Vector3Int placementOffset = Vector3Int.zero;

        public TileBase ColliderTile => colliderTile;
        public Vector3Int PlacementOffset => placementOffset;

        public bool IsMatch(string tileName)
        {
            if (colliderTile == null || string.IsNullOrEmpty(tileName))
            {
                return false;
            }

            StringComparison comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            if (!string.IsNullOrEmpty(nameStartsWith) && !tileName.StartsWith(nameStartsWith, comparison))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(nameContains) && tileName.IndexOf(nameContains, comparison) < 0)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(nameEndsWith) && !tileName.EndsWith(nameEndsWith, comparison))
            {
                return false;
            }

            return true;
        }
    }

    [Serializable]
    private class NameBasedIgnoreRule
    {
        [SerializeField]
        private string nameContains = string.Empty;

        [SerializeField]
        private bool caseSensitive = false;

        public bool IsMatch(string tileName)
        {
            if (string.IsNullOrEmpty(tileName) || string.IsNullOrEmpty(nameContains))
            {
                return false;
            }

            StringComparison comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            return tileName.IndexOf(nameContains, comparison) >= 0;
        }
    }
}



}




