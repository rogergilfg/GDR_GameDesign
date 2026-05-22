using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace SmallScaleInc.CharacterCreatorFantasy
{
    /// <summary>
    /// Robust tilemap based A* pathfinder used to move characters around blocking collider tiles.
    /// </summary>
    public static class TilemapPathfinder
    {
        const int kMaxSearchIterations = 8192;
        const int kFallbackSearchDepth = 16;
        const float kCardinalCost = 1f;
        const float kDiagonalCost = 1.41421356f; // sqrt(2)

        static Tilemap s_ColliderMap;
        static readonly Dictionary<Vector3Int, float> s_temporaryBlocked = new Dictionary<Vector3Int, float>();
        static readonly List<Vector3Int> s_tempRemoval = new List<Vector3Int>();

        struct Neighbor
        {
            public readonly Vector3Int Offset;
            public readonly float Cost;
            public readonly bool IsDiagonal;

            public Neighbor(int x, int y)
            {
                Offset = new Vector3Int(x, y, 0);
                IsDiagonal = x != 0 && y != 0;
                Cost = IsDiagonal ? kDiagonalCost : kCardinalCost;
            }
        }

        static readonly Neighbor[] s_Neighbors =
        {
            new Neighbor( 1, 0),
            new Neighbor(-1, 0),
            new Neighbor( 0, 1),
            new Neighbor( 0,-1),
            new Neighbor( 1, 1),
            new Neighbor( 1,-1),
            new Neighbor(-1, 1),
            new Neighbor(-1,-1),
        };

        class Node
        {
            public Vector3Int Cell;
            public float G;
            public float H;
            public Node Parent;
            public int HeapIndex = -1;
            public float F => G + H;
        }

        class NodeHeap
        {
            readonly List<Node> _items = new List<Node>();

            public int Count => _items.Count;

            public void Clear() => _items.Clear();

            public void Push(Node node)
            {
                node.HeapIndex = _items.Count;
                _items.Add(node);
                SortUp(node);
            }

            public Node Pop()
            {
                int lastIndex = _items.Count - 1;
                Node first = _items[0];
                Node last = _items[lastIndex];
                _items.RemoveAt(lastIndex);

                if (_items.Count > 0)
                {
                    _items[0] = last;
                    last.HeapIndex = 0;
                    SortDown(last);
                }

                first.HeapIndex = -1;
                return first;
            }

            public void Update(Node node)
            {
                SortUp(node);
            }

            static int Compare(Node a, Node b)
            {
                if (a.F < b.F) return -1;
                if (a.F > b.F) return 1;
                if (a.H < b.H) return -1;
                if (a.H > b.H) return 1;
                return 0;
            }

            void SortUp(Node node)
            {
                while (node.HeapIndex > 0)
                {
                    int parentIndex = (node.HeapIndex - 1) >> 1;
                    Node parent = _items[parentIndex];
                    if (Compare(node, parent) < 0)
                    {
                        _items[node.HeapIndex] = parent;
                        parent.HeapIndex = node.HeapIndex;
                        node.HeapIndex = parentIndex;
                        _items[parentIndex] = node;
                        continue;
                    }
                    break;
                }
            }

            void SortDown(Node node)
            {
                while (true)
                {
                    int left = (node.HeapIndex << 1) + 1;
                    if (left >= _items.Count) break;

                    int right = left + 1;
                    int swap = left;
                    if (right < _items.Count && Compare(_items[right], _items[left]) < 0)
                        swap = right;

                    if (Compare(_items[swap], node) < 0)
                    {
                        _items[node.HeapIndex] = _items[swap];
                        _items[swap].HeapIndex = node.HeapIndex;
                        node.HeapIndex = swap;
                        _items[swap] = node;
                        continue;
                    }
                    break;
                }
            }
        }

        /// <summary>Returns true if a collider tilemap has been located.</summary>
        public static bool HasColliderMap => s_ColliderMap != null;

        /// <summary>Explicitly assigns the tilemap containing blocking tiles.</summary>
        public static void Configure(Tilemap tilemap)
        {
            s_ColliderMap = tilemap;
        }

        /// <summary>Attempts to locate a tilemap on the "World" layer that contains the collider tiles.</summary>
        public static void EnsureInitialized()
        {
            if (s_ColliderMap != null) return;

            int worldLayer = LayerMask.NameToLayer("World");

            // Prefer tilemaps that have a TilemapCollider2D on the requested layer.
            var colliderMaps = Object.FindObjectsByType<TilemapCollider2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var collider in colliderMaps)
            {
                if (collider == null) continue;
                var map = collider.GetComponent<Tilemap>();
                if (map == null) continue;
                if (worldLayer != -1 && collider.gameObject.layer != worldLayer) continue;
                s_ColliderMap = map;
                return;
            }

            // Fall back to any tilemap that lives on the requested layer, or one named "Colliders".
            var maps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var map in maps)
            {
                if (map == null) continue;
                if (worldLayer != -1 && map.gameObject.layer == worldLayer)
                {
                    s_ColliderMap = map;
                    return;
                }
                if (map.name == "Colliders")
                {
                    s_ColliderMap = map;
                    return;
                }
            }
        }

        /// <summary>Returns true if the given world position is considered walkable.</summary>
        public static bool IsWalkable(Vector2 worldPosition)
        {
            EnsureInitialized();
            ExpireTemporaryObstacles();
            Vector3Int cell = s_ColliderMap ? s_ColliderMap.WorldToCell(worldPosition) : Vector3Int.FloorToInt(worldPosition);
            return IsCellWalkable(cell);
        }

        /// <summary>
        /// Finds a path between two world-space positions, expressed as a list of world-space waypoints.
        /// Returns true when a path was found; false when the destination could not be reached.
        /// </summary>
        public static bool TryFindPath(Vector2 worldStart, Vector2 worldGoal, List<Vector2> results)
        {
            EnsureInitialized();
            ExpireTemporaryObstacles();

            results.Clear();
            if (s_ColliderMap == null)
            {
                results.Add(worldGoal);
                return true;
            }

            Vector3Int startCell = s_ColliderMap.WorldToCell(worldStart);
            Vector3Int goalCell = s_ColliderMap.WorldToCell(worldGoal);

            if (!IsCellWalkable(startCell)) startCell = FindNearestWalkable(startCell, worldStart);
            if (!IsCellWalkable(goalCell)) goalCell = FindNearestWalkable(goalCell, worldGoal);

            var pathCells = RunAStar(startCell, goalCell);
            if (pathCells.Count == 0)
                return false;

            for (int i = 0; i < pathCells.Count; i++)
            {
                Vector3 world = s_ColliderMap.GetCellCenterWorld(pathCells[i]);
                results.Add(world);
            }

            // Remove the first waypoint if it is essentially at the start position.
            if (results.Count > 0)
            {
                float sqrDist = ((Vector2)results[0] - worldStart).sqrMagnitude;
                if (sqrDist < 0.04f) results.RemoveAt(0);
            }

            if (results.Count == 0) results.Add(worldGoal);
            return true;
        }

        static List<Vector3Int> RunAStar(Vector3Int start, Vector3Int goal)
        {
            var openHeap = new NodeHeap();
            var openLookup = new Dictionary<Vector3Int, Node>();
            var closedSet = new HashSet<Vector3Int>();

            var startNode = new Node
            {
                Cell = start,
                G = 0f,
                H = Heuristic(start, goal),
                Parent = null
            };

            openHeap.Push(startNode);
            openLookup[start] = startNode;

            int iterations = 0;
            while (openHeap.Count > 0 && iterations++ < kMaxSearchIterations)
            {
                Node current = openHeap.Pop();
                openLookup.Remove(current.Cell);
                closedSet.Add(current.Cell);

                if (current.Cell == goal)
                    return Reconstruct(current);

                for (int i = 0; i < s_Neighbors.Length; i++)
                {
                    Neighbor neighbor = s_Neighbors[i];
                    Vector3Int nextCell = current.Cell + neighbor.Offset;

                    if (closedSet.Contains(nextCell)) continue;
                    if (!IsCellWalkable(nextCell)) continue;
                    if (neighbor.IsDiagonal && !IsDiagonalWalkable(current.Cell, neighbor.Offset)) continue;

                    float tentativeG = current.G + neighbor.Cost;

                    if (!openLookup.TryGetValue(nextCell, out Node node))
                    {
                        node = new Node
                        {
                            Cell = nextCell,
                            G = tentativeG,
                            H = Heuristic(nextCell, goal),
                            Parent = current
                        };
                        openLookup[nextCell] = node;
                        openHeap.Push(node);
                    }
                    else if (tentativeG + 0.0001f < node.G)
                    {
                        node.G = tentativeG;
                        node.Parent = current;
                        node.H = Heuristic(nextCell, goal);
                        openHeap.Update(node);
                    }
                }
            }

            return new List<Vector3Int>();
        }

        static List<Vector3Int> Reconstruct(Node node)
        {
            var list = new List<Vector3Int>();
            for (var n = node; n != null; n = n.Parent)
                list.Add(n.Cell);
            list.Reverse();
            return list;
        }

        static float Heuristic(Vector3Int a, Vector3Int b)
        {
            int dx = Mathf.Abs(a.x - b.x);
            int dy = Mathf.Abs(a.y - b.y);
            int min = Mathf.Min(dx, dy);
            int max = Mathf.Max(dx, dy);
            return kDiagonalCost * min + kCardinalCost * (max - min);
        }

        static bool IsDiagonalWalkable(Vector3Int cell, Vector3Int offset)
        {
            Vector3Int horizontal = new Vector3Int(cell.x + offset.x, cell.y, cell.z);
            Vector3Int vertical = new Vector3Int(cell.x, cell.y + offset.y, cell.z);
            return IsCellWalkable(horizontal) && IsCellWalkable(vertical);
        }

        static bool IsCellWalkable(Vector3Int cell)
        {
            if (s_ColliderMap == null) return true;
            return !s_ColliderMap.HasTile(cell) && !IsTempBlocked(cell);
        }

        public static void RegisterTemporaryObstacle(Vector2 worldPosition, float lifetimeSeconds = 1.5f)
        {
            EnsureInitialized();
            if (s_ColliderMap == null) return;
            Vector3Int cell = s_ColliderMap.WorldToCell(worldPosition);
            float expiry = Time.time + Mathf.Max(0.1f, lifetimeSeconds);
            if (s_temporaryBlocked.TryGetValue(cell, out float existing) && existing > expiry)
            {
                return;
            }
            s_temporaryBlocked[cell] = expiry;
        }

        static bool IsTempBlocked(Vector3Int cell)
        {
            if (s_temporaryBlocked.Count == 0) return false;
            return s_temporaryBlocked.TryGetValue(cell, out float expiry) && expiry > Time.time;
        }

        static void ExpireTemporaryObstacles()
        {
            if (s_temporaryBlocked.Count == 0) return;
            s_tempRemoval.Clear();
            float now = Time.time;
            foreach (var kvp in s_temporaryBlocked)
            {
                if (kvp.Value <= now)
                {
                    s_tempRemoval.Add(kvp.Key);
                }
            }

            for (int i = 0; i < s_tempRemoval.Count; i++)
            {
                s_temporaryBlocked.Remove(s_tempRemoval[i]);
            }
            s_tempRemoval.Clear();
        }

        static Vector3Int FindNearestWalkable(Vector3Int fromCell, Vector2 fallbackWorld)
        {
            if (IsCellWalkable(fromCell)) return fromCell;

            var queue = new Queue<Vector3Int>();
            var visited = new HashSet<Vector3Int>();
            queue.Enqueue(fromCell);
            visited.Add(fromCell);

            int depth = 0;
            while (queue.Count > 0 && depth++ < kFallbackSearchDepth)
            {
                int breadth = queue.Count;
                for (int i = 0; i < breadth; i++)
                {
                    var cell = queue.Dequeue();
                    for (int n = 0; n < s_Neighbors.Length; n++)
                    {
                        Vector3Int next = cell + s_Neighbors[n].Offset;
                        if (!visited.Add(next)) continue;
                        if (!IsCellWalkable(next))
                        {
                            queue.Enqueue(next);
                            continue;
                        }

                        if (!s_Neighbors[n].IsDiagonal || IsDiagonalWalkable(cell, s_Neighbors[n].Offset))
                            return next;
                    }
                }
            }

            // Give up: return whichever cell contains the fallback world position.
            return s_ColliderMap.WorldToCell(fallbackWorld);
        }

        public static bool TryFindBlockingCell(Vector2 worldStart, Vector2 worldGoal, out Vector3Int blockingCell, out Vector2 worldCenter)
        {
            blockingCell = default;
            worldCenter = default;
            EnsureInitialized();
            if (s_ColliderMap == null) return false;

            Vector3Int startCell = s_ColliderMap.WorldToCell(worldStart);
            Vector3Int goalCell = s_ColliderMap.WorldToCell(worldGoal);

            int x0 = startCell.x;
            int y0 = startCell.y;
            int x1 = goalCell.x;
            int y1 = goalCell.y;

            int dx = Mathf.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;

            bool skippedStart = false;

            while (true)
            {
                if (skippedStart)
                {
                    Vector3Int cell = new Vector3Int(x0, y0, startCell.z);
                    if (!IsCellWalkable(cell))
                    {
                        blockingCell = cell;
                        worldCenter = s_ColliderMap.GetCellCenterWorld(cell);
                        return true;
                    }
                }
                else
                {
                    skippedStart = true;
                }

                if (x0 == x1 && y0 == y1)
                {
                    break;
                }

                int e2 = 2 * err;
                if (e2 >= dy)
                {
                    err += dy;
                    x0 += sx;
                }
                if (e2 <= dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }

            return false;
        }
    }
}






