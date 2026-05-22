using System;
using System.Collections.Generic;
using UnityEngine;
using SmallScale.FantasyKingdomTileset;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Input = SmallScale.FantasyKingdomTileset.InputAdapter;

namespace FantasyKingdoms.Minimap
{
    /// <summary>
    /// Central controller for the minimap system. Builds a simplified texture representation
    /// of configured tilemaps and overlays dynamic objects such as the player and props.
    /// </summary>
    [DisallowMultipleComponent]
    public class MinimapController : MonoBehaviour
    {
        public static MinimapController Instance { get; private set; }

        [Serializable]
        private class TilemapLayer
        {
            [SerializeField] private string name;
            [SerializeField] private Tilemap tilemap;
            [SerializeField] private bool include = true;
            [SerializeField] private Color color = Color.gray;

            public Tilemap Tilemap => tilemap;
            public bool Include => include && tilemap != null;
            public Color Color => color;

            public string Name => string.IsNullOrEmpty(name) && tilemap != null
                ? tilemap.name
                : name;
        }

        [Serializable]
        private class OverlayMarkerOverride
        {
            [SerializeField] private string description;
            [SerializeField] private LayerMask layers;
            [SerializeField] private Color color = Color.white;
            [SerializeField, Range(1, 6)] private int markerSize = 2;

            public bool Matches(int layer) => (layers.value & (1 << layer)) != 0;
            public Color Color => color;
            public int MarkerSize => Mathf.Clamp(markerSize, 1, 6);

            public void ClampValues()
            {
                markerSize = Mathf.Clamp(markerSize, 1, 6);
            }
        }

        [Header("Tilemaps")]
        [SerializeField] private GridLayout grid;
        [SerializeField] private List<TilemapLayer> tilemapLayers = new();
        [SerializeField, Tooltip("Color used when no tilemap occupies a pixel.")]
        private Color backgroundColor = Color.black;
        [SerializeField, Range(1, 8), Tooltip("Number of pixels used to represent a single tile.")]
        private int pixelsPerTile = 2;

        [Header("View")]
        [SerializeField, Min(1), Tooltip("Base width of the minimap view in tiles before zoom is applied.")]
        private int viewWidthInTiles = 32;
        [SerializeField, Min(1), Tooltip("Base height of the minimap view in tiles before zoom is applied.")]
        private int viewHeightInTiles = 32;
        [SerializeField, Min(0.01f), Tooltip("Smallest zoom level allowed. Larger numbers show a smaller area (zoomed in).")]
        private float minZoom = 0.5f;
        [SerializeField, Min(0.01f), Tooltip("Largest zoom level allowed. Larger numbers show a smaller area (zoomed in).")]
        private float maxZoom = 3f;
        [SerializeField, Min(0.01f), Tooltip("Amount zoom level changes each time the zoom buttons are pressed.")]
        private float zoomStep = 0.25f;
        [SerializeField, Tooltip("Initial zoom level when the minimap is created. Larger numbers show a smaller area (zoomed in).")]
        private float initialZoom = 1f;
        [SerializeField, Tooltip("Rotation (in degrees) applied to the minimap display.")]
        private float rotationAngle;

        [Header("Dynamic Overlays")]
        [SerializeField, Tooltip("Player transform to display on the minimap.")]
        private Transform followTarget;
        [SerializeField] private Color followTargetColor = Color.white;
        [SerializeField, Range(1, 6)] private int followTargetMarkerSize = 2;
        [SerializeField, Tooltip("Camera used to translate the mouse cursor position into world space. Defaults to Camera.main if omitted.")]
        private Camera worldCamera;
        [SerializeField, Tooltip("Draw an indicator that shows the follow target's facing direction based on the mouse cursor.")]
        private bool showFollowTargetFacing = true;
        [SerializeField] private Color followTargetFacingColor = Color.white;
        [SerializeField, Range(1, 24)] private int followTargetFacingLengthInPixels = 8;
        [SerializeField, Range(1, 5)] private int followTargetFacingThickness = 2;
        [SerializeField, Tooltip("Additional rotation (in degrees) applied to the follow target's facing indicator.")]
        private float followTargetFacingAngleOffset;
        [SerializeField, Tooltip("World layers that should appear on the minimap (e.g. Props).")]
        private LayerMask overlayLayers;
        [SerializeField] private Color overlayColor = new Color(1f, 0.8f, 0f);
        [SerializeField, Range(1, 6)] private int overlayMarkerSize = 2;
        [SerializeField, Tooltip("Per-layer overrides for overlay marker color and size.")]
        private List<OverlayMarkerOverride> overlayMarkerOverrides = new();

        [Header("UI")]
        [SerializeField] private RawImage minimapDisplay;
        [SerializeField, Tooltip("Optional button used to zoom the minimap in.")]
        private Button zoomInButton;
        [SerializeField, Tooltip("Optional button used to zoom the minimap out.")]
        private Button zoomOutButton;
        [SerializeField, Tooltip("Slider that controls the transparency of the follow target facing indicator.")]
        private Slider followTargetFacingAlphaSlider;

        [Header("Performance")]
        [SerializeField, Range(0.1f, 2f)]
        private float minimapSyncInterval = 0.5f;

        private float minimapSyncTimer;
        private Texture2D minimapTexture;
        private Color32[] pixelBuffer;
        private int textureWidth;
        private int textureHeight;
        private GridLayout resolvedGrid;
        private float currentZoom;
        private static readonly List<GameObject> OverlayObjects = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("Multiple MinimapController instances detected. Destroying duplicate.");
                Destroy(this);
                return;
            }

            Instance = this;
            ResolveZoomBounds();
            RefreshMinimapTexture();
        }

        private void OnEnable()
        {
            if (zoomInButton != null)
            {
                zoomInButton.onClick.AddListener(HandleZoomIn);
            }

            if (zoomOutButton != null)
            {
                zoomOutButton.onClick.AddListener(HandleZoomOut);
            }

            if (followTargetFacingAlphaSlider != null)
            {
                followTargetFacingAlphaSlider.onValueChanged.AddListener(HandleFollowTargetFacingAlphaChanged);
                followTargetFacingAlphaSlider.SetValueWithoutNotify(followTargetFacingColor.a);
            }
        }

        private void OnDisable()
        {
            if (zoomInButton != null)
            {
                zoomInButton.onClick.RemoveListener(HandleZoomIn);
            }

            if (zoomOutButton != null)
            {
                zoomOutButton.onClick.RemoveListener(HandleZoomOut);
            }

            if (followTargetFacingAlphaSlider != null)
            {
                followTargetFacingAlphaSlider.onValueChanged.RemoveListener(HandleFollowTargetFacingAlphaChanged);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (minimapTexture != null)
            {
                Destroy(minimapTexture);
                minimapTexture = null;
                pixelBuffer = null;
                textureWidth = 0;
                textureHeight = 0;
            }
        }

        private void LateUpdate()
        {
            ApplyDisplayTransform();

            minimapSyncTimer += Time.unscaledDeltaTime;
            if (minimapSyncTimer < minimapSyncInterval)
            {
                return;
            }

            minimapSyncTimer = 0f;
            RefreshMinimapTexture();
        }

        public void RegisterIcon(MinimapIcon icon)
        {
            // Icon support has been removed. Method retained to keep older
            // components from throwing null reference exceptions when they
            // attempt to register. No further action is required.
        }

        public void UnregisterIcon(MinimapIcon icon)
        {
            // Icon support has been removed.
        }

        public void RequestRefresh()
        {
            minimapSyncTimer = minimapSyncInterval;
        }

        public void ForceRefresh()
        {
            RefreshMinimapTexture();
            minimapSyncTimer = 0f;
        }

        private GridLayout GetGridLayout()
        {
            if (grid != null)
            {
                return grid;
            }

            if (resolvedGrid != null)
            {
                return resolvedGrid;
            }

            foreach (TilemapLayer layer in tilemapLayers)
            {
                if (layer.Tilemap == null)
                {
                    continue;
                }

                GridLayout layout = layer.Tilemap.layoutGrid;
                if (layout != null)
                {
                    resolvedGrid = layout;
                    return resolvedGrid;
                }
            }

            resolvedGrid = FindFirstObjectByType<GridLayout>();
            return resolvedGrid;
        }

        private void RefreshMinimapTexture()
        {
            if (minimapDisplay == null)
            {
                return;
            }

            if (!TryCalculateWorldBounds(out BoundsInt worldBounds))
            {
                return;
            }

            BoundsInt bounds = CalculateViewBounds(worldBounds);

            int widthInTiles = Mathf.Max(1, bounds.size.x);
            int heightInTiles = Mathf.Max(1, bounds.size.y);
            int width = widthInTiles * pixelsPerTile;
            int height = heightInTiles * pixelsPerTile;

            EnsureTexture(width, height);

            if (pixelBuffer == null || pixelBuffer.Length != width * height)
            {
                pixelBuffer = new Color32[width * height];
            }

            Color32 background = backgroundColor;
            for (int i = 0; i < pixelBuffer.Length; i++)
            {
                pixelBuffer[i] = background;
            }

            DrawTilemaps(bounds, width, height);
            DrawDynamicObjects(bounds, width, height);

            minimapTexture.SetPixels32(pixelBuffer);
            minimapTexture.Apply();

            if (minimapDisplay.texture != minimapTexture)
            {
                minimapDisplay.texture = minimapTexture;
            }
        }

        private bool TryCalculateWorldBounds(out BoundsInt bounds)
        {
            bounds = new BoundsInt();
            bool hasBounds = false;

            foreach (TilemapLayer layer in tilemapLayers)
            {
                if (!layer.Include)
                {
                    continue;
                }

                Tilemap tilemap = layer.Tilemap;
                if (tilemap == null)
                {
                    continue;
                }

                BoundsInt tileBounds = tilemap.cellBounds;

                if (!hasBounds)
                {
                    bounds = tileBounds;
                    hasBounds = true;
                }
                else
                {
                    int xMin = Mathf.Min(bounds.xMin, tileBounds.xMin);
                    int yMin = Mathf.Min(bounds.yMin, tileBounds.yMin);
                    int xMax = Mathf.Max(bounds.xMax, tileBounds.xMax);
                    int yMax = Mathf.Max(bounds.yMax, tileBounds.yMax);

                    Vector3Int min = new Vector3Int(xMin, yMin, 0);
                    Vector3Int max = new Vector3Int(xMax, yMax, 1);
                    bounds.SetMinMax(min, max);
                }
            }

            if (!hasBounds)
            {
                return false;
            }

            return true;
        }

        private BoundsInt CalculateViewBounds(BoundsInt worldBounds)
        {
            GridLayout layout = GetGridLayout();
            if (layout == null || followTarget == null)
            {
                return worldBounds;
            }

            Vector3Int centerCell = layout.WorldToCell(followTarget.position);

            int effectiveWidth = Mathf.Max(1, Mathf.RoundToInt(viewWidthInTiles / Mathf.Max(currentZoom, 0.0001f)));
            int effectiveHeight = Mathf.Max(1, Mathf.RoundToInt(viewHeightInTiles / Mathf.Max(currentZoom, 0.0001f)));

            effectiveWidth = Mathf.Min(effectiveWidth, worldBounds.size.x);
            effectiveHeight = Mathf.Min(effectiveHeight, worldBounds.size.y);

            int halfWidth = Mathf.Max(1, effectiveWidth) / 2;
            int halfHeight = Mathf.Max(1, effectiveHeight) / 2;

            int minX = centerCell.x - halfWidth;
            int minY = centerCell.y - halfHeight;

            int maxX = minX + effectiveWidth;
            int maxY = minY + effectiveHeight;

            int worldMinX = worldBounds.xMin;
            int worldMinY = worldBounds.yMin;
            int worldMaxX = worldBounds.xMax;
            int worldMaxY = worldBounds.yMax;

            if (minX < worldMinX)
            {
                maxX += worldMinX - minX;
                minX = worldMinX;
            }

            if (maxX > worldMaxX)
            {
                int offset = maxX - worldMaxX;
                minX -= offset;
                maxX = worldMaxX;
            }

            if (minY < worldMinY)
            {
                maxY += worldMinY - minY;
                minY = worldMinY;
            }

            if (maxY > worldMaxY)
            {
                int offset = maxY - worldMaxY;
                minY -= offset;
                maxY = worldMaxY;
            }

            Vector3Int min = new Vector3Int(Mathf.Clamp(minX, worldMinX, worldMaxX), Mathf.Clamp(minY, worldMinY, worldMaxY), 0);
            Vector3Int size = new Vector3Int(Mathf.Max(1, maxX - min.x), Mathf.Max(1, maxY - min.y), 1);
            return new BoundsInt(min, size);
        }

        private void EnsureTexture(int width, int height)
        {
            if (minimapTexture != null && textureWidth == width && textureHeight == height)
            {
                return;
            }

            if (minimapTexture != null)
            {
                Destroy(minimapTexture);
            }

            minimapTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Generated Minimap"
            };

            textureWidth = width;
            textureHeight = height;
        }

        private void DrawTilemaps(BoundsInt bounds, int width, int height)
        {
            foreach (TilemapLayer layer in tilemapLayers)
            {
                if (!layer.Include)
                {
                    continue;
                }

                Tilemap tilemap = layer.Tilemap;
                if (tilemap == null)
                {
                    continue;
                }

                Color32 layerColor = layer.Color;
                BoundsInt layerBounds = tilemap.cellBounds;
                BoundsInt intersection = Intersect(bounds, layerBounds);

                foreach (Vector3Int cell in intersection.allPositionsWithin)
                {
                    if (!tilemap.HasTile(cell))
                    {
                        continue;
                    }

                    WriteTileColor(cell, bounds, width, height, layerColor);
                }
            }
        }

        private void DrawDynamicObjects(BoundsInt bounds, int width, int height)
        {
            GridLayout layout = GetGridLayout();
            if (layout == null)
            {
                return;
            }

            if (followTarget != null)
            {
                Vector3Int cell = layout.WorldToCell(followTarget.position);
                WriteMarker(cell, bounds, width, height, followTargetColor, followTargetMarkerSize);
                DrawFollowTargetFacing(layout, bounds, width, height);
            }

            if (overlayLayers == 0)
            {
                return;
            }

            OverlayObjects.Clear();
            OverlayObjects.AddRange(FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));

            // Track which enemies weâ€™ve already drawn to avoid duplicate markers from children
            var drawnEnemyIds = new HashSet<int>();

            foreach (GameObject go in OverlayObjects)
            {
                if (go == null || !go.activeInHierarchy)
                {
                    continue;
                }

                // If this object belongs to an enemy hierarchy, use the enemy root for
                // inclusion checks, alive state, and position. This prevents drawing
                // markers for dead enemies and deduplicates children.
                var enemyHealth = go.GetComponentInParent<EnemyHealth2D>();
                GameObject target = enemyHealth ? enemyHealth.gameObject : go;

                // Resolve override and inclusion for the targetâ€™s layer
                OverlayMarkerOverride overrideSettings = GetOverlayOverride(target.layer);
                bool includeLayer = (overlayLayers.value & (1 << target.layer)) != 0;
                if (!includeLayer && overrideSettings == null)
                {
                    continue;
                }

                // Special rule: for Enemy layer overrides/inclusions, require an alive EnemyHealth2D
                string layerName = LayerMask.LayerToName(target.layer);
                bool isEnemyLayer = !string.IsNullOrEmpty(layerName) && string.Equals(layerName, "Enemy", StringComparison.OrdinalIgnoreCase);
                if (isEnemyLayer && (!enemyHealth || enemyHealth.IsDead))
                {
                    continue;
                }

                if (enemyHealth)
                {
                    if (enemyHealth.IsDead)
                    {
                        continue; // never draw dead enemies
                    }

                    int eid = enemyHealth.GetInstanceID();
                    if (!drawnEnemyIds.Add(eid))
                    {
                        continue; // already drew this enemy
                    }

                    Vector3Int ecell = layout.WorldToCell(enemyHealth.transform.position);
                    Color32 ecol = overrideSettings != null ? overrideSettings.Color : overlayColor;
                    int esize = overrideSettings != null ? overrideSettings.MarkerSize : overlayMarkerSize;
                    WriteMarker(ecell, bounds, width, height, ecol, esize);
                    continue;
                }

                // Non-enemy overlay object
                Vector3Int cell = layout.WorldToCell(go.transform.position);
                Color32 markerColor = overrideSettings != null ? overrideSettings.Color : overlayColor;
                int markerSize = overrideSettings != null ? overrideSettings.MarkerSize : overlayMarkerSize;
                WriteMarker(cell, bounds, width, height, markerColor, markerSize);
            }

            OverlayObjects.Clear();
        }

        private OverlayMarkerOverride GetOverlayOverride(int layer)
        {
            if (overlayMarkerOverrides == null || overlayMarkerOverrides.Count == 0)
            {
                return null;
            }

            foreach (OverlayMarkerOverride overrideSetting in overlayMarkerOverrides)
            {
                if (overrideSetting != null && overrideSetting.Matches(layer))
                {
                    return overrideSetting;
                }
            }

            return null;
        }

        private void WriteTileColor(Vector3Int cell, BoundsInt bounds, int width, int height, Color32 color)
        {
            int x = cell.x - bounds.xMin;
            int y = cell.y - bounds.yMin;

            if (x < 0 || y < 0 || x >= bounds.size.x || y >= bounds.size.y)
            {
                return;
            }

            int pixelX = x * pixelsPerTile;
            int pixelY = y * pixelsPerTile;

            for (int py = 0; py < pixelsPerTile; py++)
            {
                int targetY = pixelY + py;
                if (targetY < 0 || targetY >= height)
                {
                    continue;
                }

                for (int px = 0; px < pixelsPerTile; px++)
                {
                    int targetX = pixelX + px;
                    if (targetX < 0 || targetX >= width)
                    {
                        continue;
                    }

                    int index = targetX + targetY * width;
                    pixelBuffer[index] = color;
                }
            }
        }

        private void WriteMarker(Vector3Int cell, BoundsInt bounds, int width, int height, Color32 color, int size)
        {
            int x = cell.x - bounds.xMin;
            int y = cell.y - bounds.yMin;

            if (x < 0 || y < 0 || x >= bounds.size.x || y >= bounds.size.y)
            {
                return;
            }

            int pixelX = x * pixelsPerTile + pixelsPerTile / 2;
            int pixelY = y * pixelsPerTile + pixelsPerTile / 2;
            int diameter = Mathf.Max(1, size);
            int radius = (diameter - 1) / 2;

            for (int py = 0; py < diameter; py++)
            {
                int offsetY = py - radius;
                int targetY = pixelY + offsetY;
                if (targetY < 0 || targetY >= height)
                {
                    continue;
                }

                for (int px = 0; px < diameter; px++)
                {
                    int offsetX = px - radius;
                    int targetX = pixelX + offsetX;
                    if (targetX < 0 || targetX >= width)
                    {
                        continue;
                    }

                    int index = targetX + targetY * width;
                    pixelBuffer[index] = color;
                }
            }
        }

        private void DrawFollowTargetFacing(GridLayout layout, BoundsInt bounds, int width, int height)
        {
            if (!showFollowTargetFacing || followTarget == null || !Input.mousePresent)
            {
                return;
            }

            Camera camera = worldCamera != null ? worldCamera : Camera.main;
            if (camera == null)
            {
                return;
            }

            Vector3 followPosition = followTarget.position;
            Vector3 followScreen = camera.WorldToScreenPoint(followPosition);
            if (followScreen.z < 0f)
            {
                return;
            }

            Vector3 mouseScreen = Input.mousePosition;
            mouseScreen.z = followScreen.z;
            Vector3 mouseWorld = camera.ScreenToWorldPoint(mouseScreen);
            mouseWorld.z = followPosition.z;

            Vector2 direction = (Vector2)(mouseWorld - followPosition);
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            direction = ConvertWorldToMinimapDirection(layout, direction);
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            direction.Normalize();

            if (!Mathf.Approximately(followTargetFacingAngleOffset, 0f))
            {
                direction = RotateVector(direction, followTargetFacingAngleOffset * Mathf.Deg2Rad);
                direction.Normalize();
            }

            Vector3Int centerCell = layout.WorldToCell(followPosition);
            int x = centerCell.x - bounds.xMin;
            int y = centerCell.y - bounds.yMin;

            if (x < 0 || y < 0 || x >= bounds.size.x || y >= bounds.size.y)
            {
                return;
            }

            int centerPixelX = x * pixelsPerTile + pixelsPerTile / 2;
            int centerPixelY = y * pixelsPerTile + pixelsPerTile / 2;
            int length = Mathf.Max(1, followTargetFacingLengthInPixels);
            int thickness = Mathf.Max(1, followTargetFacingThickness);

            Color32 color = followTargetFacingColor;

            for (int i = 1; i <= length; i++)
            {
                int targetX = Mathf.RoundToInt(centerPixelX + direction.x * i);
                int targetY = Mathf.RoundToInt(centerPixelY + direction.y * i);
                WritePixel(targetX, targetY, width, height, color, thickness);
            }

            // Add a small arrow head for readability by widening the final pixel.
            int headX = Mathf.RoundToInt(centerPixelX + direction.x * length);
            int headY = Mathf.RoundToInt(centerPixelY + direction.y * length);
            WritePixel(headX, headY, width, height, color, thickness + 1);
        }

        private static Vector2 RotateVector(Vector2 value, float radians)
        {
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector2(
                value.x * cos - value.y * sin,
                value.x * sin + value.y * cos);
        }

        private static Vector2 ConvertWorldToMinimapDirection(GridLayout layout, Vector2 worldDirection)
        {
            Vector3 worldOrigin = layout.CellToWorld(Vector3Int.zero);
            Vector2 cellRight = (Vector2)(layout.CellToWorld(Vector3Int.right) - worldOrigin);
            Vector2 cellUp = (Vector2)(layout.CellToWorld(Vector3Int.up) - worldOrigin);

            float determinant = cellRight.x * cellUp.y - cellRight.y * cellUp.x;
            if (Mathf.Approximately(determinant, 0f))
            {
                return worldDirection;
            }

            float inverseDeterminant = 1f / determinant;
            Vector2 minimapDirection = new Vector2(
                (worldDirection.x * cellUp.y - worldDirection.y * cellUp.x) * inverseDeterminant,
                (-worldDirection.x * cellRight.y + worldDirection.y * cellRight.x) * inverseDeterminant);

            return minimapDirection;
        }

        private void WritePixel(int x, int y, int width, int height, Color32 color, int thickness)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                return;
            }

            int radius = Mathf.Max(0, thickness - 1);

            for (int offsetY = -radius; offsetY <= radius; offsetY++)
            {
                int targetY = y + offsetY;
                if (targetY < 0 || targetY >= height)
                {
                    continue;
                }

                for (int offsetX = -radius; offsetX <= radius; offsetX++)
                {
                    int targetX = x + offsetX;
                    if (targetX < 0 || targetX >= width)
                    {
                        continue;
                    }

                    int index = targetX + targetY * width;
                    pixelBuffer[index] = color;
                }
            }
        }

        private static BoundsInt Intersect(BoundsInt a, BoundsInt b)
        {
            int xMin = Mathf.Max(a.xMin, b.xMin);
            int yMin = Mathf.Max(a.yMin, b.yMin);
            int xMax = Mathf.Min(a.xMax, b.xMax);
            int yMax = Mathf.Min(a.yMax, b.yMax);

            if (xMax <= xMin || yMax <= yMin)
            {
                return new BoundsInt(Vector3Int.zero, Vector3Int.zero);
            }

            Vector3Int min = new Vector3Int(xMin, yMin, 0);
            Vector3Int size = new Vector3Int(xMax - xMin, yMax - yMin, 1);
            return new BoundsInt(min, size);
        }

        private void HandleZoomIn()
        {
            SetZoom(currentZoom + zoomStep);
        }

        private void HandleZoomOut()
        {
            SetZoom(currentZoom - zoomStep);
        }

        private void HandleFollowTargetFacingAlphaChanged(float alpha)
        {
            Color color = followTargetFacingColor;
            color.a = Mathf.Clamp01(alpha);
            if (color == followTargetFacingColor)
            {
                return;
            }

            followTargetFacingColor = color;
            RequestRefresh();
        }

        private void SetZoom(float zoom)
        {
            float clampedZoom = Mathf.Clamp(zoom, minZoom, maxZoom);
            if (Mathf.Approximately(clampedZoom, currentZoom))
            {
                return;
            }

            currentZoom = clampedZoom;
            initialZoom = currentZoom;
            RequestRefresh();
        }

        private void ApplyDisplayTransform()
        {
            if (minimapDisplay == null)
            {
                return;
            }

            RectTransform rectTransform = minimapDisplay.rectTransform;
            if (rectTransform != null)
            {
                rectTransform.localEulerAngles = new Vector3(0f, 0f, rotationAngle);
            }
        }

        private void ResolveZoomBounds()
        {
            if (maxZoom < minZoom)
            {
                maxZoom = minZoom;
            }

            currentZoom = Mathf.Clamp(initialZoom, minZoom, maxZoom);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            minimapSyncInterval = Mathf.Clamp(minimapSyncInterval, 0.1f, 2f);
            pixelsPerTile = Mathf.Clamp(pixelsPerTile, 1, 8);
            followTargetMarkerSize = Mathf.Clamp(followTargetMarkerSize, 1, 6);
            overlayMarkerSize = Mathf.Clamp(overlayMarkerSize, 1, 6);
            if (overlayMarkerOverrides != null)
            {
                foreach (OverlayMarkerOverride overrideSetting in overlayMarkerOverrides)
                {
                    overrideSetting?.ClampValues();
                }
            }
            viewWidthInTiles = Mathf.Max(1, viewWidthInTiles);
            viewHeightInTiles = Mathf.Max(1, viewHeightInTiles);
            minZoom = Mathf.Max(0.01f, minZoom);
            maxZoom = Mathf.Max(minZoom, maxZoom);
            zoomStep = Mathf.Max(0.01f, zoomStep);
            initialZoom = Mathf.Clamp(initialZoom, minZoom, maxZoom);
            followTargetFacingLengthInPixels = Mathf.Clamp(followTargetFacingLengthInPixels, 1, 24);
            followTargetFacingThickness = Mathf.Clamp(followTargetFacingThickness, 1, 5);
            ResolveZoomBounds();
            RefreshMinimapTexture();
        }
#endif
    }
}











