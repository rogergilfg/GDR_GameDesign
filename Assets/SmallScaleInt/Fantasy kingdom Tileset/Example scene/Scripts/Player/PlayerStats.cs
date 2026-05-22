using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using System;
using System.Collections.Generic;
using UnityEngine;
using SmallScale.FantasyKingdomTileset;
using SmallScale.FantasyKingdomTileset.Balance;

/// <summary>
/// Central authority responsible for tracking the player's stats coming from
/// base configuration values and all equipped gear.  Other systems can
/// subscribe to <see cref="StatsChanged"/> to react whenever the totals are
/// recomputed, ensuring gameplay values (damage, health, mana, etc.) stay in
/// sync with equipment changes.
/// </summary>
[DisallowMultipleComponent]
[MovedFrom(true, null, null, "PlayerStats")]
public class PlayerStats : MonoBehaviour
{
    /// <summary>
    /// Snapshot of the currently computed player stats.
    /// </summary>
    public readonly struct StatSnapshot
    {
        public readonly int Strength;
        public readonly int Defense;
        public readonly int Health;
        public readonly int Intelligence;
        public readonly int Knowledge;
        public readonly int WeaponDamage;
        public readonly int TileDamageBonus;

        public StatSnapshot(int strength, int defense, int health, int intelligence, int knowledge, int weaponDamage, int tileDamageBonus)
        {
            Strength = strength;
            Defense = defense;
            Health = health;
            Intelligence = intelligence;
            Knowledge = knowledge;
            WeaponDamage = weaponDamage;
            TileDamageBonus = tileDamageBonus;
        }
    }

    public static PlayerStats Instance { get; private set; }

    // Event for damage dealt (used by ability system procs)
    public event System.Action<int, bool> OnDamageDealt; // damage, wasCritical

    public enum AttributeStat
    {
        Strength,
        Defense,
        Health,
        Intelligence,
        Knowledge
    }

    [Serializable]
    public struct PlayerStatBonus
    {
        public int Strength;
        public int Defense;
        public int Health;
        public int Intelligence;
        public int Knowledge;
        public int WeaponDamage;
        public int TileDamage;

        public bool IsZero =>
            Strength == 0 &&
            Defense == 0 &&
            Health == 0 &&
            Intelligence == 0 &&
            Knowledge == 0 &&
            WeaponDamage == 0 &&
            TileDamage == 0;

        public PlayerStatBonus Negated()
        {
            return new PlayerStatBonus
            {
                Strength = -Strength,
                Defense = -Defense,
                Health = -Health,
                Intelligence = -Intelligence,
                Knowledge = -Knowledge,
                WeaponDamage = -WeaponDamage,
                TileDamage = -TileDamage
            };
        }
    }

    [Header("Dependencies")]
    [SerializeField]
    [Tooltip("Gear manager that keeps track of the player's equipped items.")]
    private PlayerGearManager gearManager;

    [SerializeField]
    [Tooltip("Component handling the player's health pool.")]
    private PlayerHealth playerHealth;

    [SerializeField]
    [Tooltip("Component handling the player's mana pool.")]
    private PlayerMana playerMana;

    [Header("Base Stats")]
    [SerializeField]
    [Tooltip("Strength available before any gear is equipped.")]
    private int baseStrength = 0;

    [SerializeField]
    [Tooltip("Defense available before any gear is equipped.")]
    private int baseDefense = 0;

    [SerializeField]
    [Tooltip("Health stat available before any gear is equipped.")]
    private int baseHealth = 0;

    [SerializeField]
    [Tooltip("Intelligence available before any gear is equipped.")]
    private int baseIntelligence = 0;

    [SerializeField]
    [Tooltip("Knowledge available before any gear is equipped.")]
    private int baseKnowledge = 0;

    [SerializeField]
    [Tooltip("Flat weapon damage granted even when no weapon is equipped.")]
    private int baseWeaponDamage = 0;

    [SerializeField]
    [Tooltip("Flat tile damage bonus applied even when no gear is equipped.")]
    private int baseTileDamageBonus = 0;

    [NonSerialized] private int bonusStrength;
    [NonSerialized] private int bonusDefense;
    [NonSerialized] private int bonusHealth;
    [NonSerialized] private int bonusIntelligence;
    [NonSerialized] private int bonusKnowledge;
    [NonSerialized] private int bonusWeaponDamage;
    [NonSerialized] private int bonusTileDamage;
    [NonSerialized] private float bonusThornDamagePercent;
    [NonSerialized] private int bonusThornDamageFlat;
    [NonSerialized] private float bonusLifestealPercent;
    [NonSerialized] private int bonusLifestealFlat;
    [NonSerialized] private int bonusLifestealCap;
    [NonSerialized] private bool bonusLifestealLivingOnly;

    [Header("Derived Stat Scaling")]
    [SerializeField]
    [Tooltip("Number of health points granted per point invested in the Health stat.")]
    private float healthPerHealthPoint = 1f;

    [SerializeField]
    [Tooltip("Number of mana points granted per point invested in the Intelligence stat.")]
    private float manaPerIntelligencePoint = 1f;

    [SerializeField]
    [Tooltip("Mana regenerated per second granted for each point invested in Knowledge.")]
    private float regenPerKnowledgePoint = 0.2f;

    /// <summary>
    /// Invoked whenever the aggregated stats have been recalculated.
    /// </summary>
    public event Action<StatSnapshot> StatsChanged;

    /// <summary>
    /// Invoked whenever the player's unspent attribute points change.
    /// </summary>
    public event Action<int> AttributePointsChanged;

    private StatSnapshot currentStats;
    private bool hasCompletedInitialStart;
    private bool hasCachedBaseHealth;
    private int cachedBaseMaxHealth;
    private bool hasCachedBaseMana;
    private float cachedBaseMaxMana;
    private float cachedBaseRegenPerSecond;

    [Header("Attribute Points")]
    [SerializeField]
    [Tooltip("Attribute points available to spend on stats.")]
    private int unspentAttributePoints = 0;

    [SerializeField]
    [Tooltip("Strength points granted permanently via attribute allocation.")]
    private int allocatedStrengthPoints = 0;

    [SerializeField]
    [Tooltip("Defense points granted permanently via attribute allocation.")]
    private int allocatedDefensePoints = 0;

    [SerializeField]
    [Tooltip("Health stat points granted permanently via attribute allocation.")]
    private int allocatedHealthPoints = 0;

    [SerializeField]
    [Tooltip("Intelligence points granted permanently via attribute allocation.")]
    private int allocatedIntelligencePoints = 0;

    [SerializeField]
    [Tooltip("Knowledge points granted permanently via attribute allocation.")]
    private int allocatedKnowledgePoints = 0;

    private void Reset()
    {
        if (gearManager == null)
        {
            gearManager = GetComponent<PlayerGearManager>();
        }

        if (playerHealth == null)
        {
            playerHealth = GetComponent<PlayerHealth>();
        }

        if (playerMana == null)
        {
            playerMana = GetComponent<PlayerMana>();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple PlayerStats instances detected. Destroying duplicate.", this);
            Destroy(this);
            return;
        }

        Instance = this;
        CacheBaseResources();
    }

    private void OnEnable()
    {
        if (gearManager == null)
        {
            gearManager = GetComponent<PlayerGearManager>();
        }

        if (playerHealth == null)
        {
            playerHealth = GetComponent<PlayerHealth>();
        }

        if (playerMana == null)
        {
            playerMana = GetComponent<PlayerMana>();
        }

        if (gearManager != null)
        {
            gearManager.GearChanged += HandleGearChanged;
        }

        CacheBaseResources();

        if (hasCompletedInitialStart)
        {
            RecalculateStats();
        }
    }

    private void OnDisable()
    {
        if (gearManager != null)
        {
            gearManager.GearChanged -= HandleGearChanged;
        }
    }

    private void Start()
    {
        hasCompletedInitialStart = true;
        RecalculateStats();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Returns the current stat snapshot.
    /// </summary>
    public StatSnapshot CurrentStats => currentStats;

    /// <summary>
    /// Gets the total tile damage bonus currently applied to the player.
    /// </summary>
    public int CurrentTileDamageBonus => Mathf.Max(0, currentStats.TileDamageBonus);

    /// <summary>
    /// Returns the current thorn damage percentage (0.0 to 1.0+) from passive abilities.
    /// </summary>
    public float CurrentThornDamagePercent => Mathf.Max(0f, bonusThornDamagePercent);

    /// <summary>
    /// Returns the current flat thorn damage from passive abilities.
    /// </summary>
    public int CurrentThornDamageFlat => Mathf.Max(0, bonusThornDamageFlat);

    /// <summary>
    /// Returns the current lifesteal percentage (0.0 to 1.0) from passive abilities.
    /// </summary>
    public float CurrentLifestealPercent => Mathf.Max(0f, bonusLifestealPercent);

    /// <summary>
    /// Returns the current flat lifesteal amount from passive abilities.
    /// </summary>
    public int CurrentLifestealFlat => Mathf.Max(0, bonusLifestealFlat);

    /// <summary>
    /// Returns the maximum health that can be restored per hit (0 = no limit).
    /// </summary>
    public int CurrentLifestealCap => Mathf.Max(0, bonusLifestealCap);

    /// <summary>
    /// Returns whether lifesteal only works on living targets.
    /// </summary>
    public bool CurrentLifestealLivingOnly => bonusLifestealLivingOnly;

    /// <summary>
    /// Calculates the base damage (before variance and crits) after applying the
    /// player's strength and any equipped weapon damage to the provided value.
    /// </summary>
    public int GetModifiedBaseDamage(int abilityBaseDamage)
    {
        long total = abilityBaseDamage;
        total += currentStats.Strength;
        total += currentStats.WeaponDamage;
        total = Math.Min(int.MaxValue, Math.Max(int.MinValue, total));
        float abilityMultiplier = GameBalanceManager.Instance != null ? GameBalanceManager.Instance.PlayerAbilityDamageMultiplier : 1f;
        total = (long)Mathf.RoundToInt(Mathf.Max(0f, abilityMultiplier) * Mathf.Max(1, (int)total));
        return Mathf.Max(1, (int)Mathf.Clamp(total, 1, int.MaxValue));
    }

    /// <summary>
    /// Applies the player's defense to incoming damage and returns the adjusted
    /// value. Uses a diminishing returns formula so that defense always helps but
    /// never makes the player invulnerable.
    /// </summary>
    public int ApplyIncomingDamageReduction(int incomingDamage)
    {
        float defense = Mathf.Max(0f, currentStats.Defense);
        float multiplier = 100f / (100f + defense);
        float adjusted = incomingDamage * multiplier;
        return Mathf.Max(1, Mathf.RoundToInt(adjusted));
    }

    private void HandleGearChanged(GearType gearType, GearItem newGear, GearItem previousGear)
    {
        RecalculateStats();
    }

    private void CacheBaseResources()
    {
        if (playerHealth != null && !hasCachedBaseHealth)
        {
            hasCachedBaseHealth = true;
            cachedBaseMaxHealth = Mathf.Max(1, playerHealth.maxHealth);
        }

        if (playerMana != null && !hasCachedBaseMana)
        {
            hasCachedBaseMana = true;
            cachedBaseMaxMana = Mathf.Max(1f, playerMana.maxMana);
            cachedBaseRegenPerSecond = Mathf.Max(0f, playerMana.regenPerSecond);
        }
    }

    /// <summary>
    /// Rebuilds the stat totals from scratch.
    /// </summary>
    public void RecalculateStats()
    {
        int strength = Mathf.Max(0, baseStrength);
        int defense = Mathf.Max(0, baseDefense);
        int health = Mathf.Max(0, baseHealth);
        int intelligence = Mathf.Max(0, baseIntelligence);
        int knowledge = Mathf.Max(0, baseKnowledge);
        int weaponDamage = Mathf.Max(0, baseWeaponDamage);
        int tileDamageBonus = baseTileDamageBonus;

        strength += Mathf.Max(0, GetAttributeContribution(AttributeStat.Strength));
        defense += Mathf.Max(0, GetAttributeContribution(AttributeStat.Defense));
        health += Mathf.Max(0, GetAttributeContribution(AttributeStat.Health));
        intelligence += Mathf.Max(0, GetAttributeContribution(AttributeStat.Intelligence));
        knowledge += Mathf.Max(0, GetAttributeContribution(AttributeStat.Knowledge));

        strength += bonusStrength;
        defense += bonusDefense;
        health += bonusHealth;
        intelligence += bonusIntelligence;
        knowledge += bonusKnowledge;
        weaponDamage += bonusWeaponDamage;
        tileDamageBonus += bonusTileDamage;

        if (gearManager != null)
        {
            IReadOnlyDictionary<GearType, GearItem> equipped = gearManager.EquippedGear;
            foreach (GearItem gear in equipped.Values)
            {
                if (gear == null)
                {
                    continue;
                }

                strength += gear.Strength;
                defense += gear.Defense;
                health += gear.Health;
                intelligence += gear.Intelligence;
                knowledge += gear.Knowledge;
                weaponDamage += gear.Damage;
                tileDamageBonus += gear.TileDamageBonus;
            }
        }

        currentStats = new StatSnapshot(
            Mathf.Max(0, strength),
            Mathf.Max(0, defense),
            Mathf.Max(0, health),
            Mathf.Max(0, intelligence),
            Mathf.Max(0, knowledge),
            Mathf.Max(0, weaponDamage),
            Mathf.Max(0, tileDamageBonus));

        ApplyDerivedStats(currentStats);
        StatsChanged?.Invoke(currentStats);
    }

    /// <summary>
    /// Called when external systems (e.g., Settings panel) change balance multipliers.
    /// Forces a stat recalculation so damage and tile bonuses update immediately.
    /// </summary>
    public void RecalculateDamageFromSettings()
    {
        RecalculateStats();
    }

    public void ApplyTemporaryStatBonus(in PlayerStatBonus bonus)
    {
        bool changed = false;
        changed |= ApplyBonus(ref bonusStrength, bonus.Strength);
        changed |= ApplyBonus(ref bonusDefense, bonus.Defense);
        changed |= ApplyBonus(ref bonusHealth, bonus.Health);
        changed |= ApplyBonus(ref bonusIntelligence, bonus.Intelligence);
        changed |= ApplyBonus(ref bonusKnowledge, bonus.Knowledge);
        changed |= ApplyBonus(ref bonusWeaponDamage, bonus.WeaponDamage);
        changed |= ApplyBonus(ref bonusTileDamage, bonus.TileDamage);

        if (changed)
        {
            RecalculateStats();
        }
    }

    /// <summary>
    /// Sets the current thorn damage percentage from passive abilities.
    /// </summary>
    public void SetThornDamagePercent(float percent)
    {
        bonusThornDamagePercent = Mathf.Max(0f, percent);
    }

    /// <summary>
    /// Sets the current flat thorn damage from passive abilities.
    /// </summary>
    public void SetThornDamageFlat(int amount)
    {
        bonusThornDamageFlat = Mathf.Max(0, amount);
    }

    /// <summary>
    /// Sets the current lifesteal percentage from passive abilities.
    /// </summary>
    public void SetLifestealPercent(float percent)
    {
        bonusLifestealPercent = Mathf.Max(0f, percent);
    }

    /// <summary>
    /// Sets the current flat lifesteal amount from passive abilities.
    /// </summary>
    public void SetLifestealFlat(int amount)
    {
        bonusLifestealFlat = Mathf.Max(0, amount);
    }

    /// <summary>
    /// Sets the maximum health that can be restored per hit from passive abilities.
    /// </summary>
    public void SetLifestealCap(int cap)
    {
        bonusLifestealCap = Mathf.Max(0, cap);
    }

    /// <summary>
    /// Sets whether lifesteal only works on living targets from passive abilities.
    /// </summary>
    public void SetLifestealLivingOnly(bool livingOnly)
    {
        bonusLifestealLivingOnly = livingOnly;
    }

    /// <summary>
    /// Invokes the OnDamageDealt event. Called by AbilityEffectUtility when player deals damage.
    /// </summary>
    public void NotifyDamageDealt(int damage, bool wasCritical)
    {
        OnDamageDealt?.Invoke(damage, wasCritical);
    }

    static bool ApplyBonus(ref int field, int delta)
    {
        if (delta == 0)
        {
            return false;
        }

        long next = (long)field + delta;
        if (next > int.MaxValue) next = int.MaxValue;
        if (next < int.MinValue) next = int.MinValue;

        int result = (int)next;
        if (result == field)
        {
            return false;
        }

        field = result;
        return true;
    }

    /// <summary>
    /// Returns the number of unspent attribute points.
    /// </summary>
    public int UnspentAttributePoints => Mathf.Max(0, unspentAttributePoints);

    /// <summary>
    /// Returns how many attribute points have been permanently allocated to the given stat.
    /// </summary>
    public int GetAllocatedAttributePoints(AttributeStat stat)
    {
        switch (stat)
        {
            case AttributeStat.Strength: return Mathf.Max(0, allocatedStrengthPoints);
            case AttributeStat.Defense: return Mathf.Max(0, allocatedDefensePoints);
            case AttributeStat.Health: return Mathf.Max(0, allocatedHealthPoints);
            case AttributeStat.Intelligence: return Mathf.Max(0, allocatedIntelligencePoints);
            case AttributeStat.Knowledge: return Mathf.Max(0, allocatedKnowledgePoints);
            default: return 0;
        }
    }

    /// <summary>
    /// Grants attribute points that can later be spent on stats.
    /// </summary>
    public void GrantAttributePoints(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        long next = (long)unspentAttributePoints + amount;
        unspentAttributePoints = (int)Mathf.Clamp(next, 0, int.MaxValue);
        AttributePointsChanged?.Invoke(unspentAttributePoints);
    }

    /// <summary>
    /// Attempts to spend attribute points on the provided stats.
    /// </summary>
    public bool TrySpendAttributePoints(int strength, int defense, int health, int intelligence, int knowledge)
    {
        if (strength < 0 || defense < 0 || health < 0 || intelligence < 0 || knowledge < 0)
        {
            Debug.LogWarning("Attribute allocations must be non-negative.");
            return false;
        }

        int total = strength + defense + health + intelligence + knowledge;
        if (total <= 0)
        {
            return false;
        }

        if (total > UnspentAttributePoints)
        {
            Debug.LogWarning("Not enough attribute points available.");
            return false;
        }

        allocatedStrengthPoints += strength;
        allocatedDefensePoints += defense;
        allocatedHealthPoints += health;
        allocatedIntelligencePoints += intelligence;
        allocatedKnowledgePoints += knowledge;
        unspentAttributePoints -= total;
        RecalculateStats();
        AttributePointsChanged?.Invoke(unspentAttributePoints);
        return true;
    }

    private int GetAttributeContribution(AttributeStat stat)
    {
        switch (stat)
        {
            case AttributeStat.Strength: return allocatedStrengthPoints;
            case AttributeStat.Defense: return allocatedDefensePoints;
            case AttributeStat.Health: return allocatedHealthPoints;
            case AttributeStat.Intelligence: return allocatedIntelligencePoints;
            case AttributeStat.Knowledge: return allocatedKnowledgePoints;
            default: return 0;
        }
    }

    private void ApplyDerivedStats(StatSnapshot stats)
    {
        if (playerHealth != null)
        {
            CacheBaseResources();
            int bonusHealth = Mathf.RoundToInt(stats.Health * Mathf.Max(0f, healthPerHealthPoint));
            int newMax = Mathf.Max(1, cachedBaseMaxHealth + bonusHealth);
            int previousMax = Mathf.Max(1, playerHealth.maxHealth);
            float healthRatio = previousMax > 0 ? playerHealth.currentHealth / (float)previousMax : 1f;
            playerHealth.maxHealth = newMax;
            int newCurrent = Mathf.RoundToInt(newMax * healthRatio);
            if (PlayerHealth.IsPlayerDead || playerHealth.currentHealth <= 0)
            {
                newCurrent = 0;
            }

            int minValue = (PlayerHealth.IsPlayerDead || playerHealth.currentHealth <= 0) ? 0 : 1;
            playerHealth.currentHealth = Mathf.Clamp(newCurrent, minValue, newMax);
        }

        if (playerMana != null)
        {
            CacheBaseResources();
            float bonusMana = stats.Intelligence * Mathf.Max(0f, manaPerIntelligencePoint);
            float newMaxMana = Mathf.Max(1f, cachedBaseMaxMana + bonusMana);
            playerMana.SetMaxMana(newMaxMana, refill: false);

            float bonusRegen = stats.Knowledge * Mathf.Max(0f, regenPerKnowledgePoint);
            playerMana.regenPerSecond = Mathf.Max(0f, cachedBaseRegenPerSecond + bonusRegen);
        }
    }
}




}




