using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using System.Collections.Generic;
using UnityEngine;
using SmallScaleInc.CharacterCreatorFantasy;
using SmallScale.FantasyKingdomTileset;

/// <summary>
/// Spawns random gear loot when the owning enemy dies.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth2D))]
[AddComponentMenu("Inventory/Enemy Random Gear Dropper")]
[MovedFrom(true, null, null, "EnemyRandomGearDropper")]
public sealed class EnemyRandomGearDropper : MonoBehaviour
{
    [Header("Random Drops")]
    [SerializeField, Range(0f, 1f)]
    [Tooltip("Base probability that at least one random gear item is dropped.")]
    private float baseDropChance = 0.08f;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("Multiplier applied to the drop chance after each successful roll (keeps follow-up drops rare).")]
    private float subsequentDropChanceMultiplier = 0.35f;

    [SerializeField, Range(1, 3)]
    [Tooltip("Upper bound on how many items this enemy can drop in a single death.")]
    private int maxDropCount = 3;

    [Header("Loot Presentation")]
    [SerializeField]
    [Tooltip("Prefab spawned for each dropped gear item.")]
    private LootPickup lootPickupPrefab;

    [SerializeField]
    [Tooltip("Optional offset added to the enemy position when spawning loot.")]
    private Vector3 dropSpawnOffset = Vector3.zero;

    [SerializeField]
    [Tooltip("Random scatter applied in X/Y around the spawn point for each drop.")]
    private Vector2 dropScatterRadius = new Vector2(0.5f, 0.5f);

    [Header("Specific Drops")]
    [SerializeField]
    [Tooltip("Gear items that are guaranteed to drop when this enemy is defeated.")]
    private List<GearItem> guaranteedDrops = new List<GearItem>();

    [SerializeField]
    [Tooltip("When enabled, random drops are taken from the local list below instead of the shared database.")]
    private bool useLocalRandomPool = false;

    [SerializeField]
    [Tooltip("Optional local list consulted when 'Use Local Random Pool' is enabled.")]
    private List<GearItem> localRandomDrops = new List<GearItem>();

    [Header("Item Source")]
    [SerializeField]
    [Tooltip("Database that lists every gear item eligible for random drops.")]
    private GearItemDatabase gearDatabase;

    private EnemyHealth2D trackedHealth;
    private EnemyAI enemyAI;
    private PlayerInventory cachedInventory;
    private Transform cachedPlayerTransform;
    private bool subscribed;

    static readonly List<GearItem> EligibleItems = new List<GearItem>(32);

    void Awake()
    {
        trackedHealth = GetComponent<EnemyHealth2D>();
        enemyAI = GetComponent<EnemyAI>();
    }

    void OnEnable()
    {
        Subscribe();
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    void Subscribe()
    {
        if (trackedHealth == null || subscribed) return;
        trackedHealth.OnDied += HandleEnemyDied;
        subscribed = true;
    }

    void Unsubscribe()
    {
        if (trackedHealth == null || !subscribed) return;
        trackedHealth.OnDied -= HandleEnemyDied;
        subscribed = false;
    }

    void HandleEnemyDied()
    {
        if (lootPickupPrefab == null) return;

        SpawnGuaranteedDrops();

        bool allowRandomDrops = enemyAI == null || enemyAI.enableRandomGearDrops;
        if (!allowRandomDrops) return;

        IReadOnlyList<GearItem> sourceList = useLocalRandomPool
            ? (localRandomDrops != null && localRandomDrops.Count > 0 ? localRandomDrops : null)
            : (gearDatabase != null ? gearDatabase.Items : null);

        if (sourceList == null || sourceList.Count == 0) return;

        BuildEligibleList(sourceList, !useLocalRandomPool);
        if (EligibleItems.Count == 0) return;

        float chance = Mathf.Clamp01(baseDropChance);
        int dropsSpawned = 0;
        int dropLimit = Mathf.Clamp(maxDropCount, 1, 3);

        while (dropsSpawned < dropLimit && EligibleItems.Count > 0 && chance > 0f)
        {
            if (Random.value > chance)
            {
                break;
            }

            GearItem selected = PickWeightedItem(EligibleItems);
            if (selected == null)
            {
                break;
            }

            SpawnLoot(selected);
            EligibleItems.Remove(selected);
            dropsSpawned++;

            chance *= Mathf.Clamp01(subsequentDropChanceMultiplier);
        }
    }

    void SpawnGuaranteedDrops()
    {
        if (guaranteedDrops == null || guaranteedDrops.Count == 0) return;

        for (int i = 0; i < guaranteedDrops.Count; i++)
        {
            GearItem gear = guaranteedDrops[i];
            if (gear == null) continue;
            SpawnLoot(gear);
        }
    }

    void BuildEligibleList(IReadOnlyList<GearItem> source, bool respectAvailabilityFlag)
    {
        EligibleItems.Clear();

        if (source == null) return;

        int playerLevel = PlayerExperience.Instance != null ? PlayerExperience.Instance.CurrentLevel : 0;
        for (int i = 0; i < source.Count; i++)
        {
            GearItem gear = source[i];
            if (gear == null) continue;
            if (respectAvailabilityFlag && !gear.CanAppearInRandomDrops) continue;
            if (gear.RandomDropWeight <= 0f) continue;
            if (gear.RequiredLevel > playerLevel && gear.RequiredLevel > 0) continue;
            EligibleItems.Add(gear);
        }
    }

    GearItem PickWeightedItem(List<GearItem> pool)
    {
        float totalWeight = 0f;
        for (int i = 0; i < pool.Count; i++)
        {
            totalWeight += Mathf.Max(0f, pool[i].RandomDropWeight);
        }

        if (totalWeight <= 0f) return null;

        float roll = Random.Range(0f, totalWeight);
        for (int i = 0; i < pool.Count; i++)
        {
            GearItem gear = pool[i];
            float weight = Mathf.Max(0f, gear.RandomDropWeight);
            if (weight <= 0f) continue;

            if (roll <= weight)
            {
                return gear;
            }

            roll -= weight;
        }

        return pool[pool.Count - 1];
    }

    void SpawnLoot(GearItem gear)
    {
        if (gear == null) return;

        Vector3 origin = transform.position + dropSpawnOffset;
        Vector2 scatter = SampleScatter();
        Vector3 finalPosition = origin + new Vector3(scatter.x, scatter.y, 0f);

        LootPickup pickup = Instantiate(lootPickupPrefab, origin, Quaternion.identity);
        pickup.Initialize(gear, ResolveInventory(), finalPosition, ResolvePlayerTransform());
    }

    Vector2 SampleScatter()
    {
        if (dropScatterRadius == Vector2.zero) return Vector2.zero;
        float x = dropScatterRadius.x == 0f ? 0f : Random.Range(-dropScatterRadius.x, dropScatterRadius.x);
        float y = dropScatterRadius.y == 0f ? 0f : Random.Range(-dropScatterRadius.y, dropScatterRadius.y);
        return new Vector2(x, y);
    }

    PlayerInventory ResolveInventory()
        {
            if (cachedInventory != null) return cachedInventory;
            cachedInventory = FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
            return cachedInventory;
        }

    Transform ResolvePlayerTransform()
    {
        if (cachedPlayerTransform != null) return cachedPlayerTransform;

        PlayerInventory inventory = ResolveInventory();
        if (inventory != null)
        {
            cachedPlayerTransform = inventory.transform;
            return cachedPlayerTransform;
        }

        if (PlayerExperience.Instance != null)
        {
            cachedPlayerTransform = PlayerExperience.Instance.transform;
            return cachedPlayerTransform;
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            cachedPlayerTransform = playerObj.transform;
        }

        return cachedPlayerTransform;
    }
}


}





