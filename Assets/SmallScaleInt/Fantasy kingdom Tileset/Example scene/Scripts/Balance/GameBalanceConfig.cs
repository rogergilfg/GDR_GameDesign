using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace SmallScale.FantasyKingdomTileset.Balance
{
/// <summary>
/// Centralised set of balance parameters that designers can tweak from a single asset.
/// </summary>
[CreateAssetMenu(fileName = "GameBalanceConfig", menuName = "Game/Balance Config", order = 0)]
[MovedFrom(true, null, null, "GameBalanceConfig")]
public sealed class GameBalanceConfig : ScriptableObject
{
    [Header("Experience")]
    [Tooltip("Multiplier applied to all experience the player earns.")]
    [Min(0f)] public float playerExperienceGainMultiplier = 1f;

    [Tooltip("Multiplier applied to experience granted when the player destroys tiles.")]
    [Min(0f)] public float tileExperienceMultiplier = 1f;

    [Tooltip("Multiplier applied to experience gained from resources (e.g., harvesting, salvaging).")]
    [Min(0f)] public float resourceExperienceMultiplier = 1f;

    [Header("Resources")]
    [Tooltip("Multiplier applied to all resources granted to the player (e.g., tile drops, loot payouts).")]
    [Min(0f)] public float resourceGainMultiplier = 1f;

    [Header("Player Damage")]
    [Tooltip("Multiplier applied to the player's ability damage after stat contributions.")]
    [Min(0f)] public float playerAbilityDamageMultiplier = 1f;

    [Tooltip("Multiplier applied to the player's melee damage (including stat contributions).")]
    [Min(0f)] public float playerMeleeDamageMultiplier = 1f;

    [Tooltip("Multiplier applied to the player's damage against destructible tiles.")]
    [Min(0f)] public float playerTileDamageMultiplier = 1f;

    [Header("Enemy Scaling")]
    [Tooltip("Multiplier applied to the max health of enemies when they spawn.")]
    [Min(0f)] public float enemyHealthMultiplier = 1f;

    [Tooltip("Multiplier applied to enemy damage output (melee, projectiles, etc.).")]
    [Min(0f)] public float enemyDamageMultiplier = 1f;

    [Tooltip("Multiplier applied to the experience enemies award when defeated.")]
    [Min(0f)] public float enemyExperienceRewardMultiplier = 1f;

    [Header("Building")]
    [Tooltip("Multiplier applied to the resource cost when placing build tiles.")]
    [Min(0f)] public float buildCostMultiplier = 1f;
}
}





