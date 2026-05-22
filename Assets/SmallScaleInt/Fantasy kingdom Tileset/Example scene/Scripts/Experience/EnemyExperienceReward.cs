using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using SmallScale.FantasyKingdomTileset.Balance;
using SmallScaleInc.CharacterCreatorFantasy;

namespace SmallScale.FantasyKingdomTileset
{
/// <summary>
/// Grants the player experience when the attached enemy dies.
/// Keeps the logic modular so individual enemies can tune their reward values.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth2D))]
[AddComponentMenu("Experience/Enemy Experience Reward")]
public sealed class EnemyExperienceReward : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Experience awarded to the player when this enemy dies.")]
    [Min(0)]
    private int experienceOnDeath = 25;

    [SerializeField]
    [Tooltip("Offset applied to the spawn position for experience feedback (e.g., combat text).")]
    private Vector3 experienceSpawnOffset = Vector3.zero;

    private EnemyHealth2D trackedHealth;
    private bool isSubscribed;

    private void Awake()
    {
        trackedHealth = GetComponent<EnemyHealth2D>();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (trackedHealth == null || isSubscribed)
        {
            return;
        }

        trackedHealth.OnDied += HandleEnemyDied;
        isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (trackedHealth == null || !isSubscribed)
        {
            return;
        }

        trackedHealth.OnDied -= HandleEnemyDied;
        isSubscribed = false;
    }

    private void HandleEnemyDied()
    {
        if (experienceOnDeath <= 0)
        {
            return;
        }

        int adjustedExperience = experienceOnDeath;
        if (GameBalanceManager.Instance != null)
        {
            adjustedExperience = GameBalanceManager.Instance.GetAdjustedEnemyExperience(adjustedExperience);
        }

        Vector3 awardPosition = transform.position + experienceSpawnOffset;
        bool hasPlayer = PlayerExperience.Instance != null;
        if (trackedHealth != null && trackedHealth.LastAttacker != null && hasPlayer)
        {
            var attacker = trackedHealth.LastAttacker;
            bool companionKill = attacker.GetComponentInParent<CompanionAI>() != null;
            bool turretKill = attacker.GetComponentInParent<TurretAI>() != null;
            if (companionKill || turretKill)
            {
                awardPosition = PlayerExperience.Instance.transform.position;
            }
        }
        if (adjustedExperience > 0)
        {
            PlayerExperience.GrantStatic(adjustedExperience, awardPosition);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (experienceOnDeath < 0)
        {
            experienceOnDeath = 0;
        }
    }
#endif
}
}











