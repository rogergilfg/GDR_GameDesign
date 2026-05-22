using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
// TileDestructionManager.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using SmallScale.FantasyKingdomTileset.Balance;
using SmallScale.FantasyKingdomTileset.Building;
using SmallScaleInc.TopDownPixelCharactersPack1;

[DisallowMultipleComponent]
[MovedFrom(true, null, null, "TileDestructionManager")]
public class TileDestructionManager : MonoBehaviour
{
    public static TileDestructionManager I { get; private set; }

    [Header("Tilemaps")]
    [Tooltip("Primary destructible layer used for hit lookup & HP tracking (also drives extras, collider/shadow clears).")]
    public Tilemap wallsTilemap;  // assign your "Walls" tilemap

    [Tooltip("Tilemaps that can be hit directly if Walls has no tile at the same cell (e.g., details/objects that should use same-cell hit).")]
    public Tilemap[] directHitTilemaps; // same-cell layers (NOT roofs)

    [Header("Roofs")]
    [Tooltip("Tilemaps treated as 'above' the hit cell; checked at (originCell + roofHitCellOffset). Put Roof1, Roof2 here.")]
    public Tilemap[] roofDirectHitTilemaps;

    [Tooltip("Cell offset applied when targeting roofs from the origin cell. For isometric 'up': (2,2).")]
    public Vector2Int roofHitCellOffset = new Vector2Int(2, 2);


    [Tooltip("Additional destructible layers to process when a Walls cell breaks (e.g., WallsDetails1, WallsDetails2, Objects).")]
    public Tilemap[] extraDestructibleTilemaps;

    public Tilemap brokenObjectsTilemap;
    public Tilemap colliderTilemap;
    public Tilemap shadowTilemap;

    TilemapCollider2D _colliderTilemapCollider;
    CompositeCollider2D _colliderComposite;

    [Tooltip("Optional: ground tilemaps (not used, just here for clarity).")]
    public Tilemap[] groundTilemaps;

    [Header("Data")]
    public DestructibleTileDatabase database;

    [Header("Camera Shake")]
    public bool enableCameraShake = true;
    [Range(0f, 1f)] public float cameraShakeAmplitude = 0.55f;
    [Min(0f)] public float cameraShakeDuration = 0.22f;
    [Tooltip("Optional explicit camera follow reference; defaults to Camera.main if omitted.")]
    public SmoothCameraFollow cameraFollow;

    [Header("Routing")]
    [Tooltip("If true, rubble from extra layers (WallsDetails1/2, Objects) is also placed on BrokenObjects tilemap.")]
    public bool routeExtrasToBrokenObjects = true;

    [Header("Hit Flash (overlay)")]
    [Tooltip("Optional tiny sprite overlay used as a hit flash cue on non-lethal hits.")]
    public GameObject flashOverlayPrefab;

    // ---------------------- Per-map+cell state ----------------------
    static readonly List<GearItem> s_tileEligibleGear = new List<GearItem>(32);
    PlayerInventory _cachedInventory;
    Transform _cachedPlayerTransform;
    readonly struct MapCell
    {
        public readonly Tilemap map;
        public readonly Vector3Int cell;
        public MapCell(Tilemap m, Vector3Int c) { map = m; cell = c; }
        public override int GetHashCode()
        {
            int h = map ? map.GetInstanceID() : 0;
            unchecked { return (h * 486187739) ^ cell.GetHashCode(); }
        }
        public override bool Equals(object obj)
        {
            if (obj is MapCell other) return other.map == map && other.cell == cell;
            return false;
        }
    }

    readonly Dictionary<MapCell, int> _hpByCell = new();
    readonly Dictionary<MapCell, GameObject> _stagedVfxByCell = new();
    readonly Dictionary<MapCell, float> _cellNextDamageTime = new();
    readonly Dictionary<MapCell, Coroutine> _flashRoutines = new();
    readonly Dictionary<MapCell, Coroutine> _shakeRoutines = new();
    readonly Dictionary<MapCell, Matrix4x4> _shakeBaseMatrices = new();
    readonly HashSet<MapCell> _swapPending = new();

    void Awake()
    {
        if (I && I != this) { Destroy(gameObject); return; }
        I = this;

        if (!wallsTilemap) Debug.LogWarning("[TileDestruction] Walls Tilemap not assigned.", this);
        if (!database)     Debug.LogWarning("[TileDestruction] Database not assigned.", this);

        if (colliderTilemap)
        {
            _colliderTilemapCollider = colliderTilemap.GetComponent<TilemapCollider2D>();
            if (!_colliderTilemapCollider)
            {
                Debug.LogWarning("[TileDestruction] Collider Tilemap is assigned but has no TilemapCollider2D component.", colliderTilemap);
            }
            else
            {
                _colliderComposite = _colliderTilemapCollider.GetComponent<CompositeCollider2D>();
            }
        }
    }

    void TriggerCameraShake()
    {
        if (!enableCameraShake)
            return;

        if (!cameraFollow)
        {
            var mainCam = Camera.main;
            if (mainCam)
                cameraFollow = mainCam.GetComponent<SmoothCameraFollow>();
        }

        if (cameraFollow && cameraShakeAmplitude > 0f && cameraShakeDuration > 0f)
        {
            cameraFollow.Shake(Mathf.Clamp01(cameraShakeAmplitude), cameraShakeDuration);
        }
    }

    // ---------- Static API ----------
    public static bool TryHitAtWorld(Vector2 worldPos, int damage = 1)
        => I ? I.HitAtWorld(worldPos, damage) : false;

    public static int HitCircle(Vector2 center, float radius, int damage)
        => I ? I.DoCircle(center, radius, damage) : 0;

    public static int HitCone(Vector2 center, Vector2 forward, float radius, float coneDegrees, int damage)
        => I ? I.DoCone(center, forward, radius, coneDegrees, damage) : 0;

    // ---------- Resolve which map+cell to hit ----------
    // Order: Walls (same cell) -> directHitTilemaps (same cell) -> roofDirectHitTilemaps (cell + roofHitYOffset)
    bool TryGetHitTarget(Vector3Int originCell, out Tilemap hitMap, out TileBase hitTile, out Vector3Int targetCell)
    {
        // 1) Walls first
        if (wallsTilemap)
        {
            var t = wallsTilemap.GetTile(originCell);
            if (t)
            {
                hitMap = wallsTilemap; hitTile = t; targetCell = originCell;
                return true;
            }
        }

        // 2) Other direct-hit maps at SAME cell (NOT roofs)
        if (directHitTilemaps != null)
        {
            for (int i = 0; i < directHitTilemaps.Length; i++)
            {
                var map = directHitTilemaps[i];
                if (!map) continue;
                var t = map.GetTile(originCell);
                if (t)
                {
                    hitMap = map; hitTile = t; targetCell = originCell;
                    return true;
                }
            }
        }

        // 3) Roofs at OFFSET cell (x + dx, y + dy), e.g. (2,2) for iso 'north'
        if (roofDirectHitTilemaps != null && (roofHitCellOffset.x != 0 || roofHitCellOffset.y != 0))
        {
            var roofCell = new Vector3Int(
                originCell.x + roofHitCellOffset.x,
                originCell.y + roofHitCellOffset.y,
                originCell.z
            );

            for (int i = 0; i < roofDirectHitTilemaps.Length; i++)
            {
                var map = roofDirectHitTilemaps[i];
                if (!map) continue;
                var t = map.GetTile(roofCell);
                if (t)
                {
                    hitMap = map; hitTile = t; targetCell = roofCell;
                    return true;
                }
            }
        }


        hitMap = null; hitTile = null; targetCell = originCell;
        return false;
    }

    // ---------- Single point ----------
    bool HitAtWorld(Vector2 world, int damage)
    {
        if (!wallsTilemap || damage <= 0) return false;

        var originCell = wallsTilemap.WorldToCell(world);
        if (!TryGetHitTarget(originCell, out var map, out var tile, out var targetCell))
            return false;

        Vector3 worldCenter = map.GetCellCenterWorld(targetCell);
        return HitCellOnMap(map, tile, targetCell, damage, worldCenter);
    }

    // ---------- AoE circle ----------
    int DoCircle(Vector2 center, float r, int damage)
    {
        if (!wallsTilemap || damage <= 0) return 0;

        Vector3Int min = wallsTilemap.WorldToCell(center - Vector2.one * r);
        Vector3Int max = wallsTilemap.WorldToCell(center + Vector2.one * r);
        float r2 = r * r;
        int count = 0;

        for (int y = min.y; y <= max.y; y++)
        for (int x = min.x; x <= max.x; x++)
        {
            var originCell = new Vector3Int(x, y, 0);

            // Circle coverage based on the origin cell position
            Vector3 wcOrigin = wallsTilemap.GetCellCenterWorld(originCell);
            if (((Vector2)wcOrigin - center).sqrMagnitude > r2) continue;

            if (!TryGetHitTarget(originCell, out var map, out var tile, out var targetCell)) continue;

            Vector3 worldCenter = map.GetCellCenterWorld(targetCell);
            if (HitCellOnMap(map, tile, targetCell, damage, worldCenter)) count++;
        }
        return count;
    }

    // ---------- Cone (circle filter + dot check) ----------
    int DoCone(Vector2 center, Vector2 forward, float r, float coneDeg, int damage)
    {
        if (!wallsTilemap || damage <= 0) return 0;

        Vector3Int min = wallsTilemap.WorldToCell(center - Vector2.one * r);
        Vector3Int max = wallsTilemap.WorldToCell(center + Vector2.one * r);
        float cosThresh = Mathf.Cos(Mathf.Max(1f, coneDeg * 0.5f) * Mathf.Deg2Rad);
        forward = (forward.sqrMagnitude < 0.0001f) ? Vector2.right : forward.normalized;

        int count = 0;
        for (int y = min.y; y <= max.y; y++)
        for (int x = min.x; x <= max.x; x++)
        {
            var originCell = new Vector3Int(x, y, 0);
            Vector3 wcOrigin = wallsTilemap.GetCellCenterWorld(originCell);

            Vector2 to = (Vector2)wcOrigin - center;
            float m = to.magnitude;
            if (m < 0.0001f || m > r) continue;
            if (Vector2.Dot(forward, to / m) < cosThresh) continue;

            if (!TryGetHitTarget(originCell, out var map, out var tile, out var targetCell)) continue;

            Vector3 worldCenter = map.GetCellCenterWorld(targetCell);
            if (HitCellOnMap(map, tile, targetCell, damage, worldCenter)) count++;
        }
        return count;
    }

    // ---------- Core per-cell damage (per map+cell) ----------
    bool HitCellOnMap(Tilemap map, TileBase tile, Vector3Int cell, int damage, Vector3 worldCenter)
    {
        float now = Time.time;
        var key = new MapCell(map, cell);

        if (_swapPending.Contains(key)) return false;
        if (_cellNextDamageTime.TryGetValue(key, out var next) && now < next) return false;

        if (!database || !database.TryGet(tile, out var data) || data == null)
            return false; // unmapped tile: ignore

        int curHP = _hpByCell.TryGetValue(key, out int stored) ? stored : Mathf.Max(1, data.maxHP);

        curHP -= Mathf.Max(1, damage);

        if (curHP > 0)
        {
            _hpByCell[key] = curHP;

            TriggerCameraShake();

            // --- NEW: Impact VFX (fires every non-lethal hit) ---
            if (data.impactVfxPrefab)
            {
                var impact = Instantiate(data.impactVfxPrefab, worldCenter, Quaternion.identity, GetSpawnParent());
                if (data.impactVfxCleanup > 0f) Destroy(impact, data.impactVfxCleanup);
                else
                {
                    var ad = impact.AddComponent<AutoDestroyVfx>();
                    ad.lifetimeOverride = 0f; // infer or fallback
                }
            }

            // --- Existing staged VFX (appears once per cell, as before) ---
            if (data.stagedVfxPrefab && !_stagedVfxByCell.ContainsKey(key))
            {
                var inst = Instantiate(data.stagedVfxPrefab, worldCenter, Quaternion.identity, GetSpawnParent());
                _stagedVfxByCell[key] = inst;
                if (data.stagedVfxCleanup > 0f) Destroy(inst, data.stagedVfxCleanup);
            }

            if (data.flashOnHit)
                StartFlashForCell(map, cell, data.flashColor, data.flashHold, data.flashFade);

            StartShakeForCell(map, cell, data);

            _cellNextDamageTime[key] = now + 0.12f;
            return true;
        }


        // --- lethal branch ---
        StopShakeForCell(map, cell, true);
        TriggerCameraShake();

        CancelFlash(map, cell);
        map.SetColor(cell, Color.white);
        _hpByCell.Remove(key);

        if (_stagedVfxByCell.TryGetValue(key, out var staged) && staged)
            Destroy(staged);
        _stagedVfxByCell.Remove(key);

        // Process lethal on the hit map
        bool tileCleared = ProcessLethalForMap(map, cell, worldCenter, tile);

        if (tileCleared && ShouldClearColliderFor(map))
        {
            TryClearColliderCell(cell);
        }

        if (map == wallsTilemap)
        {
            // Also process extras at same cell
            if (extraDestructibleTilemaps != null)
            {
                for (int i = 0; i < extraDestructibleTilemaps.Length; i++)
                {
                    var ex = extraDestructibleTilemaps[i];
                    if (!ex) continue;
                    var t = ex.GetTile(cell);
                    if (!t) continue;
                    ProcessLethalForMap(ex, cell, worldCenter, t);
                }
            }

            if (shadowTilemap)
            {
                shadowTilemap.SetTile(cell, null);
                shadowTilemap.SetColor(cell, Color.white);
                shadowTilemap.SetTransformMatrix(cell, Matrix4x4.identity);
            }

            _swapPending.Add(key);
            StartCoroutine(SwapAfterDelay(cell, data));
        }
        else
        {
            if (!data.clearTile && data.destroyedTile)
                StartCoroutine(SwapAfterDelayForMap(map, cell, data));
        }

        return true;
    }

    // --- Handle VFX/loot/clear/swap for a single tilemap & tile at this cell ---
    bool ProcessLethalForMap(Tilemap map, Vector3Int cell, Vector3 worldCenter, TileBase tile)
    {
        DestructibleTileData d = null;
        if (database && tile && database.TryGet(tile, out var found)) d = found;

        StopShakeForCell(map, cell, true);

        if (d != null && d.destroyVfxPrefab)
        {
            var v = Instantiate(d.destroyVfxPrefab, worldCenter, Quaternion.identity, GetSpawnParent());
            if (d.destroyVfxCleanup > 0f) Destroy(v, d.destroyVfxCleanup);
        }

        TrySpawnGearDrops(d, worldCenter);

        if (d != null && d.resourceDropsSet != null && !d.resourceDropsSet.IsEmpty && DynamicResourceManager.Instance)
        {
            DynamicResourceManager.Instance.GrantResources(d.resourceDropsSet, worldCenter);
        }

        if (d != null && d.experienceReward > 0 && PlayerExperience.Instance != null)
        {
            int adjustedExperience = d.experienceReward;
            if (GameBalanceManager.Instance != null)
            {
                adjustedExperience = GameBalanceManager.Instance.GetAdjustedTileExperience(adjustedExperience);
            }

            if (adjustedExperience > 0)
            {
                PlayerExperience.Instance.GrantExperience(adjustedExperience, worldCenter);
            }
        }

        // Clear the current tile immediately (so VFX is visible)
        if (map)
        {
            map.SetTile(cell, null);
        }

        if (d != null && d.BuildCategory == BuildPartCategory.Roof && TilemapBuildController.Instance != null)
        {
            TilemapBuildController.Instance.HandleRoofTileDestroyed(map, cell);
        }

        if (d != null && !d.clearTile && d.destroyedTile)
        {
            StartCoroutine(SwapAfterDelayForMap(map, cell, d));
        }

        return tile != null;
    }

    Transform GetSpawnParent() => wallsTilemap ? wallsTilemap.transform : null;

    void TrySpawnGearDrops(DestructibleTileData data, Vector3 worldCenter)
    {
        if (data == null)
            return;

        bool hasGuaranteed = data.guaranteedGearDrops != null && data.guaranteedGearDrops.Count > 0;
        if (!data.enableGearDrops && !hasGuaranteed)
            return;

        var lootPrefab = data.lootPickupPrefab;
        if (!lootPrefab)
            return;

        var inventory = ResolvePlayerInventory();
        var playerTransform = ResolvePlayerTransform(inventory);

        if (hasGuaranteed)
        {
            for (int i = 0; i < data.guaranteedGearDrops.Count; i++)
            {
                var gear = data.guaranteedGearDrops[i];
                if (!gear) continue;
                SpawnGearLoot(lootPrefab, gear, worldCenter, data.lootScatter, inventory, playerTransform);
            }
        }

        if (!data.enableGearDrops)
            return;

        IReadOnlyList<GearItem> sourceList = data.useLocalGearPool
            ? (data.localRandomGearDrops != null && data.localRandomGearDrops.Count > 0 ? data.localRandomGearDrops : null)
            : (data.gearDropDatabase != null ? data.gearDropDatabase.Items : null);

        if (sourceList == null || sourceList.Count == 0)
            return;

        BuildEligibleGearList(sourceList, !data.useLocalGearPool);
        if (s_tileEligibleGear.Count == 0)
            return;

        float chance = Mathf.Clamp01(data.gearDropChance);
        int dropLimit = Mathf.Clamp(data.gearDropMaxCount, 1, 3);
        int dropsSpawned = 0;

        while (dropsSpawned < dropLimit && s_tileEligibleGear.Count > 0 && chance > 0f)
        {
            if (Random.value > chance)
                break;

            var selected = PickWeightedGearItem(s_tileEligibleGear);
            if (!selected)
                break;

            SpawnGearLoot(lootPrefab, selected, worldCenter, data.lootScatter, inventory, playerTransform);
            s_tileEligibleGear.Remove(selected);
            dropsSpawned++;

            chance *= Mathf.Clamp01(data.gearDropChainMultiplier);
        }
    }

    void SpawnGearLoot(LootPickup prefab, GearItem gear, Vector3 worldCenter, Vector2 scatter, PlayerInventory inventory, Transform playerTransform)
    {
        if (!prefab || !gear)
            return;

        Vector2 jitter = new Vector2(
            Random.Range(-scatter.x, scatter.x),
            Random.Range(-scatter.y, scatter.y)
        );
        Vector3 spawnPos = worldCenter + (Vector3)jitter;
        var pickup = Instantiate(prefab, spawnPos, Quaternion.identity, GetSpawnParent());
        pickup.Initialize(gear, inventory, spawnPos, playerTransform);
    }

    void BuildEligibleGearList(IReadOnlyList<GearItem> source, bool respectAvailabilityFlag)
    {
        s_tileEligibleGear.Clear();
        if (source == null)
            return;

        int playerLevel = PlayerExperience.Instance != null ? PlayerExperience.Instance.CurrentLevel : 0;
        for (int i = 0; i < source.Count; i++)
        {
            var gear = source[i];
            if (!gear)
                continue;
            if (respectAvailabilityFlag && !gear.CanAppearInRandomDrops)
                continue;
            if (gear.RandomDropWeight <= 0f)
                continue;
            if (gear.RequiredLevel > playerLevel && gear.RequiredLevel > 0)
                continue;
            s_tileEligibleGear.Add(gear);
        }
    }

    GearItem PickWeightedGearItem(List<GearItem> pool)
    {
        float totalWeight = 0f;
        for (int i = 0; i < pool.Count; i++)
        {
            totalWeight += Mathf.Max(0f, pool[i].RandomDropWeight);
        }
        if (totalWeight <= 0f)
            return null;

        float roll = Random.Range(0f, totalWeight);
        for (int i = 0; i < pool.Count; i++)
        {
            var gear = pool[i];
            float weight = Mathf.Max(0f, gear.RandomDropWeight);
            if (weight <= 0f)
                continue;
            if (roll <= weight)
                return gear;
            roll -= weight;
        }
        return pool[pool.Count - 1];
    }

    PlayerInventory ResolvePlayerInventory()
    {
        if (_cachedInventory)
            return _cachedInventory;
        _cachedInventory = FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
        return _cachedInventory;
    }

    Transform ResolvePlayerTransform(PlayerInventory inventory)
    {
        if (inventory)
            return inventory.transform;
        if (PlayerExperience.Instance)
            return PlayerExperience.Instance.transform;
        if (_cachedPlayerTransform)
            return _cachedPlayerTransform;
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj)
            _cachedPlayerTransform = playerObj.transform;
        return _cachedPlayerTransform;
    }

    bool ShouldClearColliderFor(Tilemap map)
    {
        if (!map) return false;
        if (map == wallsTilemap) return true;
        if (directHitTilemaps == null) return false;

        for (int i = 0; i < directHitTilemaps.Length; i++)
        {
            if (directHitTilemaps[i] == map) return true;
        }

        return false;
    }

    void TryClearColliderCell(Vector3Int cell)
    {
        if (!colliderTilemap) return;

        if (!colliderTilemap.GetTile(cell)) return;

        colliderTilemap.SetTile(cell, null);
        RefreshColliderTilemap();
    }

    // ---------- Flash (per map) ----------
    void EnsureTileWritable(Tilemap map, Vector3Int cell)
    {
        if (!map) return;
        map.SetTileFlags(cell, TileFlags.None);
    }

    void StartFlashForCell(Tilemap map, Vector3Int cell, Color flash, float hold, float fade)
    {
        var key = new MapCell(map, cell);
        CancelFlash(map, cell);
        _flashRoutines[key] = StartCoroutine(FlashTileRoutine(map, cell, flash, hold, fade));
    }

    void CancelFlash(Tilemap map, Vector3Int cell)
    {
        var key = new MapCell(map, cell);
        if (_flashRoutines.TryGetValue(key, out var co) && co != null)
            StopCoroutine(co);
        _flashRoutines.Remove(key);

        if (map)
        {
            EnsureTileWritable(map, cell);
            map.SetColor(cell, Color.white);
        }
    }

    System.Collections.IEnumerator FlashTileRoutine(Tilemap map, Vector3Int cell, Color flash, float hold, float fade)
    {
        if (!map) yield break;

        EnsureTileWritable(map, cell);
        Color original = map.GetColor(cell);
        if (original.a <= 0f) original = Color.white;

        map.SetColor(cell, flash);
        if (hold > 0f) yield return new WaitForSeconds(hold);

        if (fade > 0f)
        {
            float t = 0f;
            while (t < fade)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / fade);
                map.SetColor(cell, Color.Lerp(flash, original, k));
                yield return null;
            }
        }

        map.SetColor(cell, original);
        _flashRoutines.Remove(new MapCell(map, cell));
    }

    void StartShakeForCell(Tilemap map, Vector3Int cell, DestructibleTileData data)
    {
        if (!map || data == null) return;
        if (!data.shakeOnHit) return;

        float duration = Mathf.Max(0f, data.shakeDuration);
        Vector2 posAmp = data.shakePosAmplitude;
        float posMax = Mathf.Max(Mathf.Abs(posAmp.x), Mathf.Abs(posAmp.y));
        float rotAmp = Mathf.Max(0f, data.shakeRotAmplitude);

        if (duration <= 0f) return;
        if (posMax <= 0f && rotAmp <= 0f) return;
        if (!map.GetTile(cell)) return;

        var key = new MapCell(map, cell);
        StopShakeForCell(map, cell, true);

        EnsureTileWritable(map, cell);
        Matrix4x4 baseMatrix = map.GetTransformMatrix(cell);
        _shakeBaseMatrices[key] = baseMatrix;

        var co = StartCoroutine(ShakeTileRoutine(map, cell, data, baseMatrix));
        _shakeRoutines[key] = co;
    }

    void StopShakeForCell(Tilemap map, Vector3Int cell, bool restoreTransform)
    {
        var key = new MapCell(map, cell);
        if (_shakeRoutines.TryGetValue(key, out var co) && co != null)
            StopCoroutine(co);
        _shakeRoutines.Remove(key);

        if (restoreTransform && map && map.GetTile(cell) && _shakeBaseMatrices.TryGetValue(key, out var baseMatrix))
        {
            EnsureTileWritable(map, cell);
            map.SetTransformMatrix(cell, baseMatrix);
        }

        _shakeBaseMatrices.Remove(key);
    }

    System.Collections.IEnumerator ShakeTileRoutine(Tilemap map, Vector3Int cell, DestructibleTileData data, Matrix4x4 baseMatrix)
    {
        if (!map || data == null) yield break;

        var key = new MapCell(map, cell);
        float duration = Mathf.Max(0f, data.shakeDuration);
        float freq = Mathf.Max(0.1f, data.shakeFrequency);
        Vector2 posAmp = data.shakePosAmplitude;
        float rotAmp = Mathf.Max(0f, data.shakeRotAmplitude);
        float falloff = Mathf.Clamp01(data.shakeFalloff);

        float t = 0f;
        float phase = Random.value * 1000f;
        const float TAU = 6.2831853f;

        while (t < duration)
        {
            if (!map) break;
            if (!map.GetTile(cell)) break;

            t += Time.deltaTime;

            float k = duration <= 0f ? 1f : Mathf.Clamp01(t / duration);
            float fall = (falloff <= 0f) ? 1f : Mathf.Lerp(1f, 1f - falloff, k);
            float f = freq * (phase + t);

            float nx = Mathf.Sin(f * TAU * 0.73f);
            float ny = Mathf.Sin((f + 0.37f) * TAU * 0.91f);
            float nr = Mathf.Sin((f + 0.19f) * TAU * 1.13f);

            Vector3 offset = new Vector3(nx * posAmp.x, ny * posAmp.y, 0f) * fall;
            float rotZ = nr * rotAmp * fall;

            Matrix4x4 shakeMatrix = baseMatrix * Matrix4x4.TRS(offset, Quaternion.Euler(0f, 0f, rotZ), Vector3.one);
            map.SetTransformMatrix(cell, shakeMatrix);

            yield return null;
        }

        if (map && map.GetTile(cell))
        {
            EnsureTileWritable(map, cell);
            map.SetTransformMatrix(cell, baseMatrix);
        }

        _shakeRoutines.Remove(key);
        _shakeBaseMatrices.Remove(key);
    }

    // --- Swap to rubble: ALWAYS route to brokenObjectsTilemap ---
    private System.Collections.IEnumerator SwapAfterDelayForMap(Tilemap sourceMap, Vector3Int cell, DestructibleTileData data)
    {
        float d = Mathf.Max(0f, data.swapDelay);
        if (d > 0f) yield return new WaitForSeconds(d);

        if (!data.clearTile && data.destroyedTile)
        {
            if (brokenObjectsTilemap)
            {
                brokenObjectsTilemap.SetTile(cell, data.destroyedTile);
            }
            else
            {
                Debug.LogWarning("[TileDestruction] BrokenObjects tilemap is not assigned; destroyed tile not placed.", this);
            }
        }
    }

    private System.Collections.IEnumerator SwapAfterDelay(Vector3Int cell, DestructibleTileData data)
    {
        float d = Mathf.Max(0f, data.swapDelay);
        if (d > 0f) yield return new WaitForSeconds(d);

        if (!data.clearTile && data.destroyedTile)
        {
            if (brokenObjectsTilemap)
            {
                brokenObjectsTilemap.SetTile(cell, data.destroyedTile);
            }
            else
            {
                Debug.LogWarning("[TileDestruction] BrokenObjects tilemap is not assigned; destroyed tile not placed.", this);
            }
        }

        _swapPending.Remove(new MapCell(wallsTilemap, cell));
    }

    void RefreshColliderTilemap()
    {
        if (_colliderTilemapCollider)
        {
            _colliderTilemapCollider.ProcessTilemapChanges();
            if (_colliderComposite)
                _colliderComposite.GenerateGeometry();
        }

        Physics2D.SyncTransforms();
    }
}

[MovedFrom(true, null, null, "AutoDestroyVfx")]
public class AutoDestroyVfx : MonoBehaviour
{
    [Tooltip("If > 0, destroy after this many seconds. If 0, try to infer from ParticleSystems; else fallback to 2s.")]
    public float lifetimeOverride = 0f;

    void Start()
    {
        float t = lifetimeOverride;
        if (t <= 0f)
        {
            float longest = 0f;
            var psList = GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in psList)
            {
                var main = ps.main;
                float est = main.duration + main.startLifetimeMultiplier;
                if (est > longest) longest = est;
            }
            t = (longest > 0f) ? longest : 2f;
        }
        Destroy(gameObject, t);
    }
}




}




