using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using UnityEngine;
using UnityEngine.Tilemaps;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[MovedFrom(true, null, null, "IsometricSpriteSorter")]
public sealed class IsometricSpriteSorter : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Grid used to convert world positions into isometric cell coordinates.")]
    [SerializeField] private Grid gridOverride;

    [Tooltip("Optional TilemapRenderer used to mirror its sort order configuration.")]
    [SerializeField] private TilemapRenderer referenceTilemapRenderer;

    [Header("Sorting")]
    [SerializeField] private TilemapRenderer.SortOrder sortOrder = TilemapRenderer.SortOrder.TopRight;

    [Tooltip("Additional offset applied after calculating the automatic sorting order.")]
    [SerializeField] private int sortingOrderOffset;

    [Tooltip("Scales the distance between consecutive sorting orders.")]
    [SerializeField] private float sortingStep = 100f;

    [Tooltip("Invert the automatically calculated sorting direction if objects appear behind the tilemap.")]
    [SerializeField] private bool invertSortOrder;

    [Tooltip("Secondary tie breaker applied using the grid X coordinate to reduce z-fighting.")]
    [SerializeField] private float tieBreaker = 0.01f;

    [Header("Foot Position")]
    [Tooltip("Optional manual offset (in world units) applied to the sampled foot position.")]
    [SerializeField] private Vector3 worldFootOffset = Vector3.zero;

    private SpriteRenderer spriteRenderer;
    private Grid cachedGrid;
    private Transform cachedGridTransform;
    private Vector2 cellRight2D;
    private Vector2 cellUp2D;
    private Vector3 cellOriginWorld;
    private float inverseDeterminant;
    private bool gridConfigured;

    private void Reset()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (gridOverride == null)
        {
            gridOverride = GetComponentInParent<Grid>();
        }

        if (referenceTilemapRenderer == null)
        {
            referenceTilemapRenderer = GetComponentInParent<TilemapRenderer>();
        }
    }

    private void Awake()
    {
        CacheRenderer();
        CacheGridData();
        UpdateSortingOrder();
    }

    private void OnEnable()
    {
        CacheRenderer();
        CacheGridData();
        UpdateSortingOrder();
    }

    private void OnValidate()
    {
        CacheRenderer();

        if (referenceTilemapRenderer != null)
        {
            sortOrder = referenceTilemapRenderer.sortOrder;
            if (gridOverride == null)
            {
                gridOverride = referenceTilemapRenderer.GetComponentInParent<Grid>();
            }
        }

        CacheGridData();
        UpdateSortingOrder();
    }

    private void LateUpdate()
    {
        if (cachedGridTransform != null && cachedGridTransform.hasChanged)
        {
            CacheGridData();
            cachedGridTransform.hasChanged = false;
        }

        UpdateSortingOrder();
    }

    private void OnTransformParentChanged()
    {
        CacheGridData();
        UpdateSortingOrder();
    }

    private void OnDidApplyAnimationProperties()
    {
        UpdateSortingOrder();
    }

    private void CacheRenderer()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
    }

    private void CacheGridData()
    {
        Grid resolvedGrid = gridOverride != null ? gridOverride : GetComponentInParent<Grid>();

        if (resolvedGrid == cachedGrid && gridConfigured)
        {
            return;
        }

        cachedGrid = resolvedGrid;
        cachedGridTransform = cachedGrid != null ? cachedGrid.transform : null;
        gridConfigured = false;

        if (cachedGrid == null)
        {
            return;
        }

        cellOriginWorld = cachedGrid.CellToWorld(Vector3Int.zero);
        Vector3 cellRight = cachedGrid.CellToWorld(Vector3Int.right) - cellOriginWorld;
        Vector3 cellUp = cachedGrid.CellToWorld(Vector3Int.up) - cellOriginWorld;

        cellRight2D = new Vector2(cellRight.x, cellRight.y);
        cellUp2D = new Vector2(cellUp.x, cellUp.y);

        float determinant = cellRight2D.x * cellUp2D.y - cellRight2D.y * cellUp2D.x;
        if (Mathf.Approximately(determinant, 0f))
        {
            return;
        }

        inverseDeterminant = 1f / determinant;
        gridConfigured = true;
    }

    private void UpdateSortingOrder()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        Vector3 footPosition = ResolveFootWorldPosition();
        Vector2 gridCoordinates = gridConfigured ? WorldToGridCoords(footPosition) : new Vector2(footPosition.x, footPosition.y);

        float primary = EvaluatePrimarySortValue(gridCoordinates);
        float secondary = gridCoordinates.x * tieBreaker;

        float step = Mathf.Approximately(sortingStep, 0f) ? 1f : Mathf.Abs(sortingStep);
        float direction = invertSortOrder ? 1f : -1f;
        float rawOrder = (primary * step * direction) + secondary;
        int finalOrder = Mathf.RoundToInt(rawOrder) + sortingOrderOffset;

        if (spriteRenderer.sortingOrder != finalOrder)
        {
            spriteRenderer.sortingOrder = finalOrder;
        }
    }

    private Vector3 ResolveFootWorldPosition()
    {
        if (spriteRenderer.sprite == null)
        {
            return transform.position + worldFootOffset;
        }

        Bounds bounds = spriteRenderer.bounds;
        Vector3 bottomCenter = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        return bottomCenter + worldFootOffset;
    }

    private Vector2 WorldToGridCoords(Vector3 worldPosition)
    {
        Vector2 delta = new Vector2(worldPosition.x - cellOriginWorld.x, worldPosition.y - cellOriginWorld.y);

        float gridX = (delta.x * cellUp2D.y - delta.y * cellUp2D.x) * inverseDeterminant;
        float gridY = (delta.y * cellRight2D.x - delta.x * cellRight2D.y) * inverseDeterminant;

        return new Vector2(gridX, gridY);
    }

    private float EvaluatePrimarySortValue(Vector2 gridCoordinates)
    {
        switch (sortOrder)
        {
            case TilemapRenderer.SortOrder.BottomLeft:
                return -(gridCoordinates.x + gridCoordinates.y);
            case TilemapRenderer.SortOrder.BottomRight:
                return gridCoordinates.x - gridCoordinates.y;
            case TilemapRenderer.SortOrder.TopLeft:
                return gridCoordinates.y - gridCoordinates.x;
            case TilemapRenderer.SortOrder.TopRight:
            default:
                return gridCoordinates.x + gridCoordinates.y;
        }
    }
}



}





