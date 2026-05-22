using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace SmallScale.FantasyKingdomTileset.Balance
{
/// <summary>
/// Runtime access point for game-wide balance multipliers. Attach to a bootstrap object and assign a config asset.
/// </summary>
[DisallowMultipleComponent]
[MovedFrom(true, null, null, "GameBalanceManager")]
public sealed class GameBalanceManager : MonoBehaviour
{
    public static GameBalanceManager Instance { get; private set; }

    [SerializeField]
    [Tooltip("Balance configuration asset that drives global multipliers.")]
    private GameBalanceConfig config;

    private bool appliedExperienceMultiplier;

    public GameBalanceConfig Config => config;

    public float ExperienceGainMultiplier => config != null ? Mathf.Max(0f, config.playerExperienceGainMultiplier) : 1f;
    public float ResourceGainMultiplier => config != null ? Mathf.Max(0f, config.resourceGainMultiplier) : 1f;
    public float PlayerAbilityDamageMultiplier => config != null ? Mathf.Max(0f, config.playerAbilityDamageMultiplier) : 1f;
    public float PlayerMeleeDamageMultiplier => config != null ? Mathf.Max(0f, config.playerMeleeDamageMultiplier) : 1f;
    public float PlayerTileDamageMultiplier => config != null ? Mathf.Max(0f, config.playerTileDamageMultiplier) : 1f;
    public float TileExperienceMultiplier => config != null ? Mathf.Max(0f, config.tileExperienceMultiplier) : 1f;
    public float ResourceExperienceMultiplier => config != null ? Mathf.Max(0f, config.resourceExperienceMultiplier) : 1f;
    public float EnemyHealthMultiplier => config != null ? Mathf.Max(0f, config.enemyHealthMultiplier) : 1f;
    public float EnemyDamageMultiplier => config != null ? Mathf.Max(0f, config.enemyDamageMultiplier) : 1f;
    public float EnemyExperienceRewardMultiplier => config != null ? Mathf.Max(0f, config.enemyExperienceRewardMultiplier) : 1f;
    public float BuildCostMultiplier => config != null ? Mathf.Max(0f, config.buildCostMultiplier) : 1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple GameBalanceManager instances detected. Destroying duplicate.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ApplyExperienceMultiplierIfReady();
    }

    private void OnEnable()
    {
        appliedExperienceMultiplier = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (!appliedExperienceMultiplier)
        {
            ApplyExperienceMultiplierIfReady();
        }
    }

    public void RefreshExperienceMultiplier()
    {
        appliedExperienceMultiplier = false;
        ApplyExperienceMultiplierIfReady();
    }

    private void ApplyExperienceMultiplierIfReady()
    {
        if (PlayerExperience.Instance == null)
        {
            return;
        }

        PlayerExperience.Instance.SetExternalMultiplier(ExperienceGainMultiplier);
        appliedExperienceMultiplier = true;
    }

    /// <summary>
    /// Returns a resource cost adjusted by the current build cost multiplier. When the multiplier is 0, null is returned to indicate a free build.
    /// </summary>
    public ResourceSet GetAdjustedBuildCost(ResourceSet baseCost)
    {
        if (baseCost == null || baseCost.IsEmpty)
        {
            return null;
        }

        float multiplier = BuildCostMultiplier;
        multiplier = Mathf.Max(0f, multiplier);

        if (multiplier <= 0f)
        {
            return null;
        }

        if (Mathf.Approximately(multiplier, 1f))
        {
            return baseCost;
        }

        ResourceSet adjusted = new ResourceSet();
        IReadOnlyList<ResourceAmount> list = baseCost.Amounts;
        for (int i = 0; i < list.Count; i++)
        {
            ResourceAmount amount = list[i];
            if (amount.type == null || amount.amount <= 0)
            {
                continue;
            }

            int scaled = Mathf.RoundToInt(amount.amount * multiplier);
            if (multiplier > 0f && scaled <= 0 && amount.amount > 0)
            {
                scaled = 1;
            }

            if (scaled <= 0)
            {
                continue;
            }

            adjusted.Set(amount.type, scaled);
        }

        return adjusted.IsEmpty ? null : adjusted;
    }

    private int AdjustExperienceAmount(int baseAmount, float multiplier)
    {
        if (baseAmount <= 0)
        {
            return 0;
        }

        multiplier = Mathf.Max(0f, multiplier);
        if (multiplier <= 0f)
        {
            return 0;
        }

        int adjusted = Mathf.RoundToInt(baseAmount * multiplier);
        if (adjusted <= 0)
        {
            adjusted = 1;
        }

        return adjusted;
    }

    public int GetAdjustedEnemyExperience(int baseAmount) => AdjustExperienceAmount(baseAmount, EnemyExperienceRewardMultiplier);

    public int GetAdjustedTileExperience(int baseAmount) => AdjustExperienceAmount(baseAmount, TileExperienceMultiplier);

    public int GetAdjustedResourceExperience(int baseAmount) => AdjustExperienceAmount(baseAmount, ResourceExperienceMultiplier);

    public void RefreshEnemyStats()
    {
        var enemies = FindObjectsByType<global::SmallScale.FantasyKingdomTileset.EnemyHealth2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] == null)
                continue;
            enemies[i].RefreshBalanceFromManager();
        }
    }
}
}



