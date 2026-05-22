using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using SmallScale.FantasyKingdomTileset.Building;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Tilemap))]
[MovedFrom(true, null, null, "TreeSwayAnimator")]
public class TreeSwayAnimator : MonoBehaviour
{
    private struct TreeTileData
    {
        public Vector3Int position;
        public float phase;
        public float speed;
        public float rotationAmplitude;
        public Vector3 pivotOffset;
        public Vector3 worldCenter;
        public float sortOffset;
    }

    [SerializeField]
    private Tilemap tilemap;
    private TilemapRenderer tilemapRenderer;

    [Header("Sway Settings")]
    [SerializeField]
    [Tooltip("Enable or disable the tree sway animation globally.")]
    private bool swayEnabled = true;

    [SerializeField]
    [Tooltip("Randomized range (in degrees) for the sway bend amplitude (used for shearing deformation).")]
    private Vector2 rotationAmplitudeRange = new Vector2(2.2f, 4.8f);

    [SerializeField]
    [Tooltip("Randomized range for the sway speed multiplier.")]
    private Vector2 swaySpeedRange = new Vector2(0.85f, 1.25f);

    [SerializeField]
    [Tooltip("Randomize the starting sway phase for each tree tile.")]
    private bool randomizePhase = true;

    [SerializeField]
    [Tooltip("Additional offset above the tile bottom to use as the sway pivot point.")]
    private float pivotHeightOffset = 0.0f;

    [Header("Visibility Settings")]
    [SerializeField]
    [Tooltip("Only animate trees that are currently within the camera view.")]
    private bool animateOnlyWhenVisible = true;

    [SerializeField]
    [Tooltip("Camera used to determine visible trees. Defaults to Camera.main when not assigned.")]
    private Camera visibilityCamera;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Extra padding (in world units) added to the camera view when determining which trees are visible.")]
    private float visibilityMargin = 0.5f;

    [Header("Sorting Settings")]
    [SerializeField]
    [Tooltip("Scale applied to the calculated z-offset so that trees keep a consistent draw order while swaying.")]
    private float sortOffsetScale = 0.001f;

    [SerializeField]
    [Tooltip("Tie breaker factor applied to the x-position when calculating the z-offset. Helps avoid z-fighting between trees in the same row.")]
    private float sortOffsetTieBreaker = 0.01f;

    private readonly List<TreeTileData> treeTiles = new List<TreeTileData>();
    private Vector3 sortPrimaryAxis = Vector3.up;
    private Vector3 sortSecondaryAxis = Vector3.right;
    private bool transformsDirty;
    private bool runtimeRefreshQueued;
#if UNITY_EDITOR
    private bool editorRefreshQueued;
#endif

    private void Reset()
    {
        tilemap = GetComponent<Tilemap>();
    }

    private void CacheReferences()
    {
        if (tilemap == null)
        {
            tilemap = GetComponent<Tilemap>();
        }

        if (tilemap != null && tilemapRenderer == null)
        {
            tilemapRenderer = tilemap.GetComponent<TilemapRenderer>();
        }

        UpdateSortAxes();
    }

    private void UpdateSortAxes()
    {
        if (tilemap == null)
        {
            sortPrimaryAxis = Vector3.up;
            sortSecondaryAxis = Vector3.right;
            return;
        }

        Vector3Int origin = Vector3Int.zero;
        Vector3 worldOrigin = tilemap.CellToWorld(origin);
        Vector3 worldRight = tilemap.CellToWorld(origin + Vector3Int.right) - worldOrigin;
        Vector3 worldUp = tilemap.CellToWorld(origin + Vector3Int.up) - worldOrigin;

        if (worldRight.sqrMagnitude < Mathf.Epsilon)
        {
            worldRight = Vector3.right;
        }

        if (worldUp.sqrMagnitude < Mathf.Epsilon)
        {
            worldUp = Vector3.up;
        }

        TilemapRenderer renderer = tilemapRenderer != null ? tilemapRenderer : tilemap.GetComponent<TilemapRenderer>();
        if (renderer != null)
        {
            tilemapRenderer = renderer;
        }

        Vector3 primary = worldUp;
        if (tilemapRenderer != null)
        {
            switch (tilemapRenderer.sortOrder)
            {
                case TilemapRenderer.SortOrder.TopLeft:
                    primary = worldUp + worldRight;
                    break;
                case TilemapRenderer.SortOrder.TopRight:
                    primary = worldUp - worldRight;
                    break;
                case TilemapRenderer.SortOrder.BottomLeft:
                    primary = -worldUp + worldRight;
                    break;
                case TilemapRenderer.SortOrder.BottomRight:
                    primary = -worldUp - worldRight;
                    break;
                default:
                    primary = worldUp;
                    break;
            }
        }

        if (primary.sqrMagnitude < Mathf.Epsilon)
        {
            primary = worldUp;
        }

        sortPrimaryAxis = primary.normalized;

        Vector3 secondary = worldRight - Vector3.Project(worldRight, sortPrimaryAxis);
        if (secondary.sqrMagnitude < Mathf.Epsilon)
        {
            secondary = Vector3.Cross(sortPrimaryAxis, Vector3.forward);
            if (secondary.sqrMagnitude < Mathf.Epsilon)
            {
                secondary = Vector3.Cross(sortPrimaryAxis, Vector3.up);
            }
        }

        sortSecondaryAxis = secondary.normalized;
    }

    private void Awake()
    {
        CacheReferences();
        CacheTreeTiles();
        transformsDirty = false;
        if (tilemap != null)
        {
            tilemap.transform.hasChanged = false;
        }
    }

    private void OnEnable()
    {
        CacheReferences();
        if (treeTiles.Count == 0)
        {
            CacheTreeTiles();
        }
    }

    private void OnDisable()
    {
        RestoreTileMatrices();
    }

    private void OnDestroy()
    {
        RestoreTileMatrices();
    }

    private void Update()
    {
        ApplyQueuedRuntimeRefresh();

        if (treeTiles.Count == 0)
        {
            return;
        }

        if (!swayEnabled)
        {
            if (transformsDirty)
            {
                RestoreTileMatrices();
            }

            return;
        }

        if (tilemap == null)
        {
            return;
        }

        if (tilemap.transform.hasChanged)
        {
            UpdateSortAxes();
            UpdateWorldCenters();
            tilemap.transform.hasChanged = false;
        }

        Rect visibilityRect = default;
        bool restrictByVisibility = animateOnlyWhenVisible && TryGetVisibilityRect(out visibilityRect);

        float time = Time.time;
        bool anyApplied = false;

        for (int i = 0; i < treeTiles.Count; i++)
        {
            TreeTileData data = treeTiles[i];

            if (restrictByVisibility && !IsVisible(data.worldCenter, visibilityRect))
            {
                continue;
            }

            float swayFactor = Mathf.Sin(time * data.speed + data.phase);

            float rotation = swayFactor * data.rotationAmplitude;
            // Convert the desired rotation angle into an equivalent shear amount so that the
            // base of the sprite stays locked in place while the top deforms.
            float shearAmount = Mathf.Tan(rotation * Mathf.Deg2Rad);

            Matrix4x4 shearMatrix = Matrix4x4.identity;
            shearMatrix.m01 = shearAmount;

            Matrix4x4 matrix = Matrix4x4.Translate(new Vector3(0f, 0f, data.sortOffset)) *
                               Matrix4x4.Translate(data.pivotOffset) *
                               shearMatrix *
                               Matrix4x4.Translate(-data.pivotOffset);

            tilemap.SetTransformMatrix(data.position, matrix);
            anyApplied = true;
        }

        transformsDirty |= anyApplied;
    }

    private void CacheTreeTiles()
    {
        CacheReferences();
        if (tilemap == null)
        {
            treeTiles.Clear();
            transformsDirty = false;
            return;
        }

        if (treeTiles.Count > 0)
        {
            RestoreTileMatrices();
        }

        treeTiles.Clear();

        tilemap.CompressBounds();
        BoundsInt bounds = tilemap.cellBounds;
        Vector3 pivotOffset = CalculatePivotOffset();
        foreach (Vector3Int position in bounds.allPositionsWithin)
        {
            TileBase tile = tilemap.GetTile(position);
            if (tile == null)
            {
                continue;
            }

            string tileName = tile.name;
            if (string.IsNullOrEmpty(tileName) || !tileName.StartsWith("Tree", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Do not apply sway to tiles the player has placed via the build system.
            if (TilemapBuildController.Instance != null &&
                TilemapBuildController.Instance.IsPlayerPlacedTile(tilemap, position))
            {
                continue;
            }

            tilemap.SetTileFlags(position, TileFlags.None);

            float baseRandom = PseudoRandom(position, 0f);
            float speedRandom = PseudoRandom(position, 37.21f);
            float rotationRandom = PseudoRandom(position, 91.77f);

            TreeTileData data = new TreeTileData()
            {
                position = position,
                phase = CalculatePhase(baseRandom),
                speed = Mathf.Lerp(swaySpeedRange.x, swaySpeedRange.y, speedRandom),
                rotationAmplitude = Mathf.Lerp(rotationAmplitudeRange.x, rotationAmplitudeRange.y, rotationRandom),
                pivotOffset = pivotOffset,
                worldCenter = tilemap.GetCellCenterWorld(position),
                sortOffset = 0f
            };

            data.sortOffset = CalculateSortOffset(data.position, data.worldCenter);

            tilemap.SetTransformMatrix(position, Matrix4x4.identity);
            treeTiles.Add(data);
        }

        transformsDirty = false;
    }

    private void RestoreTileMatrices()
    {
        if (tilemap == null)
        {
            transformsDirty = false;
            return;
        }

        for (int i = 0; i < treeTiles.Count; i++)
        {
            tilemap.SetTransformMatrix(treeTiles[i].position, Matrix4x4.identity);
        }

        transformsDirty = false;
    }

    private void UpdateWorldCenters()
    {
        Vector3 pivotOffset = CalculatePivotOffset();
        for (int i = 0; i < treeTiles.Count; i++)
        {
            TreeTileData data = treeTiles[i];
            data.worldCenter = tilemap.GetCellCenterWorld(data.position);
            data.pivotOffset = pivotOffset;
            data.sortOffset = CalculateSortOffset(data.position, data.worldCenter);
            treeTiles[i] = data;
        }
    }

    private float CalculatePhase(float randomValue)
    {
        if (!randomizePhase)
        {
            return 0f;
        }

        return randomValue * Mathf.PI * 2f;
    }

    private Vector3 CalculatePivotOffset()
    {
        if (tilemap == null)
        {
            return Vector3.zero;
        }

        Vector3 cellSize = tilemap.cellSize;
        float offsetY = -cellSize.y * 0.5f + pivotHeightOffset;
        return new Vector3(0f, offsetY, 0f);
    }

    private float CalculateSortOffset(Vector3Int cellPosition, Vector3 worldCenter)
    {
        float scaledPrimary = Vector3.Dot(worldCenter, sortPrimaryAxis) * sortOffsetScale;
        float scaledSecondary = Vector3.Dot(worldCenter, sortSecondaryAxis) * sortOffsetScale * sortOffsetTieBreaker;

        const float cellStepX = 0.0001f;
        const float cellStepY = 0.00001f;
        const float cellStepZ = 0.000001f;
        float cellTieBreaker = (cellPosition.x * cellStepX) + (cellPosition.y * cellStepY) + (cellPosition.z * cellStepZ);

        return scaledPrimary + scaledSecondary + cellTieBreaker;
    }

    private bool TryGetVisibilityRect(out Rect rect)
    {
        Camera cam = visibilityCamera != null ? visibilityCamera : Camera.main;
        if (cam == null)
        {
            rect = default;
            return false;
        }

        float depth = cam.orthographic ? 0f : cam.nearClipPlane;
        Vector3 min = cam.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
        Vector3 max = cam.ViewportToWorldPoint(new Vector3(1f, 1f, depth));

        float minX = Mathf.Min(min.x, max.x) - visibilityMargin;
        float maxX = Mathf.Max(min.x, max.x) + visibilityMargin;
        float minY = Mathf.Min(min.y, max.y) - visibilityMargin;
        float maxY = Mathf.Max(min.y, max.y) + visibilityMargin;

        rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
        return true;
    }

    private static bool IsVisible(Vector3 position, Rect rect)
    {
        return rect.Contains(new Vector2(position.x, position.y));
    }

    private static float PseudoRandom(Vector3Int position, float offset)
    {
        float dot = Vector3.Dot(position, new Vector3(12.9898f, 78.233f, 37.719f));
        float value = Mathf.Sin(dot + offset) * 43758.5453f;
        return value - Mathf.Floor(value);
    }

    private void OnValidate()
    {
        rotationAmplitudeRange.x = Mathf.Max(0f, rotationAmplitudeRange.x);
        rotationAmplitudeRange.y = Mathf.Max(0f, rotationAmplitudeRange.y);
        if (rotationAmplitudeRange.x > rotationAmplitudeRange.y)
        {
            rotationAmplitudeRange = new Vector2(rotationAmplitudeRange.y, rotationAmplitudeRange.x);
        }

        swaySpeedRange.x = Mathf.Max(0f, swaySpeedRange.x);
        swaySpeedRange.y = Mathf.Max(0f, swaySpeedRange.y);
        if (swaySpeedRange.x > swaySpeedRange.y)
        {
            swaySpeedRange = new Vector2(swaySpeedRange.y, swaySpeedRange.x);
        }

        sortOffsetScale = Mathf.Max(0f, sortOffsetScale);
        sortOffsetTieBreaker = Mathf.Max(0f, sortOffsetTieBreaker);

        if (tilemap == null)
        {
            tilemap = GetComponent<Tilemap>();
        }

        if (tilemap == null)
        {
            return;
        }

        RequestRefresh();
    }

    public void SetSwayEnabled(bool enabled)
    {
        swayEnabled = enabled;

        if (!swayEnabled)
        {
            RestoreTileMatrices();
        }
    }

    private void RequestRefresh()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            QueueEditorRefresh();
            return;
        }
#endif

        runtimeRefreshQueued = true;
    }

    private void ApplyQueuedRuntimeRefresh()
    {
        if (!runtimeRefreshQueued || !isActiveAndEnabled)
        {
            return;
        }

        runtimeRefreshQueued = false;
        CacheReferences();
        CacheTreeTiles();
    }

#if UNITY_EDITOR
    private void QueueEditorRefresh()
    {
        if (editorRefreshQueued)
        {
            return;
        }

        editorRefreshQueued = true;
        EditorApplication.delayCall += PerformEditorRefresh;
    }

    private void PerformEditorRefresh()
    {
        editorRefreshQueued = false;

        if (this == null)
        {
            return;
        }

        CacheReferences();
        CacheTreeTiles();
    }
#endif
}



}




