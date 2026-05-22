using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace DungeonGeneration
{
    /// <summary>
    /// Defines a handcrafted room that can be embedded within the procedural layout.
    /// Stores tile placements captured from a prefab along with optional decoration data.
    /// </summary>
    [CreateAssetMenu(menuName = "Dungeon Generation/Dungeon Room Template", fileName = "DungeonRoomTemplate")]
    public class DungeonRoomTemplate : ScriptableObject
    {
        [Header("Prefab Source")]
        [SerializeField] private GameObject tilemapPrefab;
#pragma warning disable 0414
        [SerializeField] private bool computeInPlayMode = true;
#pragma warning restore 0414

        [Header("Decorations")]
        [SerializeField] private GameObject decorationPrefab;
        [SerializeField] private Vector3 decorationOffset;

        [Header("Fitting")]
        [SerializeField] private bool requireExactSize = true;

        private bool isBaked;
        private Vector2Int cachedSize = Vector2Int.zero;
        private Vector3Int cachedMinCell = Vector3Int.zero;
        private List<TemplateTilemapLayer> cachedLayers;

        [Serializable]
        public class TemplateTilemapLayer
        {
            public string TilemapName;
            public bool OverrideExisting = true;
            public List<Vector3Int> Positions = new();
            public List<TileBase> Tiles = new();
        }

        public Vector2Int Size
        {
            get
            {
                EnsureBaked();
                return cachedSize;
            }
        }

        public bool RequireExactSize => requireExactSize;

        public IReadOnlyList<TemplateTilemapLayer> Layers
        {
            get
            {
                EnsureBaked();
                return cachedLayers;
            }
        }

        public void EnsureBaked()
        {
            if (isBaked)
            {
                return;
            }

            cachedLayers = new List<TemplateTilemapLayer>();
            cachedSize = Vector2Int.zero;
            cachedMinCell = Vector3Int.zero;

            if (tilemapPrefab == null)
            {
                isBaked = true;
                return;
            }

            GameObject instance = Instantiate(tilemapPrefab);
            if (instance == null)
            {
                isBaked = true;
                return;
            }

            instance.hideFlags = HideFlags.HideAndDontSave;
            if (!Application.isPlaying)
            {
                instance.SetActive(false);
            }

            try
            {
                BakeFromInstance(instance);
            }
            finally
            {
                if (Application.isPlaying)
                {
                    Destroy(instance);
                }
                else
                {
                    DestroyImmediate(instance);
                }
            }

            isBaked = true;
        }

        private void BakeFromInstance(GameObject instance)
        {
            Tilemap[] tilemaps = instance.GetComponentsInChildren<Tilemap>();
            if (tilemaps == null || tilemaps.Length == 0)
            {
                cachedSize = Vector2Int.zero;
                cachedMinCell = Vector3Int.zero;
                return;
            }

            bool hasTile = false;
            Vector3Int min = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
            Vector3Int max = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);

            foreach (Tilemap tilemap in tilemaps)
            {
                if (tilemap == null)
                {
                    continue;
                }

                var layer = new TemplateTilemapLayer
                {
                    TilemapName = tilemap.name,
                    OverrideExisting = true
                };

                BoundsInt bounds = tilemap.cellBounds;
                foreach (Vector3Int cell in bounds.allPositionsWithin)
                {
                    TileBase tile = tilemap.GetTile(cell);
                    if (tile == null)
                    {
                        continue;
                    }

                    hasTile = true;
                    if (cell.x < min.x) min.x = cell.x;
                    if (cell.y < min.y) min.y = cell.y;
                    if (cell.z < min.z) min.z = cell.z;
                    if (cell.x > max.x) max.x = cell.x;
                    if (cell.y > max.y) max.y = cell.y;
                    if (cell.z > max.z) max.z = cell.z;

                    layer.Positions.Add(cell);
                    layer.Tiles.Add(tile);
                }

                if (layer.Positions.Count > 0)
                {
                    cachedLayers.Add(layer);
                }
            }

            if (!hasTile)
            {
                cachedSize = Vector2Int.zero;
                cachedMinCell = Vector3Int.zero;
                return;
            }

            cachedMinCell = min;
            cachedSize = new Vector2Int(max.x - min.x + 1, max.y - min.y + 1);

            for (int layerIndex = 0; layerIndex < cachedLayers.Count; layerIndex++)
            {
                TemplateTilemapLayer layer = cachedLayers[layerIndex];
                for (int i = 0; i < layer.Positions.Count; i++)
                {
                    Vector3Int absolute = layer.Positions[i];
                    layer.Positions[i] = absolute - cachedMinCell;
                }
            }
        }

        public bool FitsRoom(Vector2Int roomSize)
        {
            EnsureBaked();

            if (requireExactSize)
            {
                return roomSize == cachedSize;
            }

            return roomSize.x >= cachedSize.x && roomSize.y >= cachedSize.y;
        }

        public void ApplyToTilemaps(Tilemap ground, Tilemap groundDetail, Tilemap wall, Tilemap objects, Vector3Int anchorCell)
        {
            EnsureBaked();

            if (cachedLayers == null || cachedLayers.Count == 0)
            {
                return;
            }

            for (int i = 0; i < cachedLayers.Count; i++)
            {
                TemplateTilemapLayer layer = cachedLayers[i];
                Tilemap target = ResolveTargetTilemap(layer.TilemapName, ground, groundDetail, wall, objects);
                if (target == null)
                {
                    continue;
                }

                for (int j = 0; j < layer.Positions.Count; j++)
                {
                    Vector3Int cell = anchorCell + layer.Positions[j];
                    TileBase tile = layer.Tiles[j];
                    if (layer.OverrideExisting || target.GetTile(cell) == null)
                    {
                        target.SetTile(cell, tile);
                    }
                }
            }
        }

        public void SpawnDecoration(Transform parent, Tilemap referenceTilemap, Vector3Int anchorCell, List<GameObject> spawnedPrefabs)
        {
            if (decorationPrefab == null)
            {
                return;
            }

            Vector3 baseWorld = referenceTilemap != null
                ? referenceTilemap.CellToWorld(anchorCell)
                : (Vector3)anchorCell;

            GameObject instance = Instantiate(decorationPrefab, baseWorld + decorationOffset, decorationPrefab.transform.rotation, parent);
            if (spawnedPrefabs != null && instance != null)
            {
                spawnedPrefabs.Add(instance);
            }
        }

        private static Tilemap ResolveTargetTilemap(string name, Tilemap ground, Tilemap detail, Tilemap wall, Tilemap objects)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            string lower = name.ToLowerInvariant();

            if (ground != null && ground.name.ToLowerInvariant() == lower) return ground;
            if (detail != null && detail.name.ToLowerInvariant() == lower) return detail;
            if (wall != null && wall.name.ToLowerInvariant() == lower) return wall;
            if (objects != null && objects.name.ToLowerInvariant() == lower) return objects;

            if (ground != null && ground.name.ToLowerInvariant().Contains(lower)) return ground;
            if (detail != null && detail.name.ToLowerInvariant().Contains(lower)) return detail;
            if (wall != null && wall.name.ToLowerInvariant().Contains(lower)) return wall;
            if (objects != null && objects.name.ToLowerInvariant().Contains(lower)) return objects;

            return null;
        }

    }
}





