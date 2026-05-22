using System;
using UnityEngine;
using SmallScaleInc.CharacterCreatorFantasy;
using SmallScale.FantasyKingdomTileset;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    /// <summary>
    /// Identifies the broad category of an actor that owns an ability runner.
    /// </summary>
    public enum AbilityActorKind
    {
        Unknown = 0,
        Player = 1,
        Enemy = 2,
        Neutral = 3
    }

    /// <summary>
    /// Bitmask flag used by requirements to express which actor kinds they care about.
    /// </summary>
    [Flags]
    public enum AbilityActorKindMask
    {
        None = 0,
        Player = 1 << 0,
        Enemy = 1 << 1,
        Neutral = 1 << 2,
        Unknown = 1 << 3,
        All = Player | Enemy | Neutral | Unknown
    }

    static class AbilityActorKindExtensions
    {
        public static AbilityActorKindMask ToMask(this AbilityActorKind kind)
        {
            return kind switch
            {
                AbilityActorKind.Player => AbilityActorKindMask.Player,
                AbilityActorKind.Enemy => AbilityActorKindMask.Enemy,
                AbilityActorKind.Neutral => AbilityActorKindMask.Neutral,
                AbilityActorKind.Unknown => AbilityActorKindMask.Unknown,
                _ => AbilityActorKindMask.Unknown
            };
        }
    }

    /// <summary>
    /// Activation payload provided by callers when an ability is requested.
    /// </summary>
    [Serializable]
    public struct AbilityActivationParameters
    {
        /// <summary>
        /// Explicit target transform (optional). When null the runner attempts to infer a target from the owner.
        /// </summary>
        public Transform Target;

        /// <summary>
        /// Optional world position override used by some abilities (e.g., targeted AoE).
        /// </summary>
        public Vector3? TargetPoint;

        /// <summary>
        /// Desired direction supplied by input. When zero the runner can fall back to other heuristics.
        /// </summary>
        public Vector2 DesiredDirection;

        /// <summary>
        /// Custom payload exposed for advanced abilities.
        /// </summary>
        public object CustomData;
    }

    /// <summary>
    /// Rich context passed to steps and requirements so they can interact with the world.
    /// </summary>
    public sealed class AbilityRuntimeContext
    {
        public AbilityRunner Runner { get; }
        public AbilityDefinition Definition { get; }
        public AbilityRuntimeState State { get; }
        public AbilityActivationParameters ActivationParameters { get; }

        public GameObject Owner { get; }
        public Transform Transform { get; }
        public Rigidbody2D Rigidbody2D { get; }
        public Animator Animator { get; }
        public GenericTopDownController TopDownController { get; }
        public EnemyAI EnemyAI { get; }
        public NeutralNpcAI NeutralAI { get; }
        public EnemyHealth2D EnemyHealth { get; }
        public PlayerMana PlayerMana { get; }
        public PlayerHealth PlayerHealth { get; }

        public AbilityActorKind OwnerKind => Runner.OwnerKind;
        public bool IsPlayerControlled => OwnerKind == AbilityActorKind.Player;
        public bool IsEnemyControlled => OwnerKind == AbilityActorKind.Enemy;
        public bool IsNeutralControlled => OwnerKind == AbilityActorKind.Neutral;

        public Vector2 DesiredDirection => ActivationParameters.DesiredDirection;
        public Transform Target => ActivationParameters.Target != null ? ActivationParameters.Target : Runner.CachedTarget;
        public Vector3 TargetPosition
        {
            get
            {
                if (ActivationParameters.TargetPoint.HasValue) return ActivationParameters.TargetPoint.Value;
                if (Target != null) return Target.position;
                return Transform.position;
            }
        }

        public bool CancelRequested { get; private set; }

        public int CurrentRank => 1;
        public int MaxRank => 1;

        // Max range set by WaitForTargetConfirmationStep for subsequent steps to use
        public float? ConfirmedTargetMaxRange { get; set; }

        // World position confirmed by WaitForTargetConfirmationStep for later steps (e.g. summons/projectiles)
        public Vector3? ConfirmedTargetPosition { get; set; }

        public AbilityRuntimeContext(
            AbilityRunner runner,
            AbilityDefinition definition,
            AbilityRuntimeState state,
            in AbilityActivationParameters activation)
        {
            Runner = runner;
            Definition = definition;
            State = state;
            ActivationParameters = activation;

            Owner = runner.gameObject;
            Transform = runner.transform;
            Rigidbody2D = runner.CachedRigidbody2D;
            Animator = runner.CachedAnimator;
            TopDownController = runner.CachedTopDownController;
            EnemyAI = runner.CachedEnemyAI;
            NeutralAI = runner.CachedNeutralNpcAI;
            EnemyHealth = runner.CachedEnemyHealth;
            PlayerMana = runner.CachedPlayerMana;
            if (!PlayerMana && runner.IsPlayerControlled)
            {
                PlayerMana = PlayerMana.Instance;
            }

            PlayerHealth = runner.CachedPlayerHealth;
            if (!PlayerHealth && runner.IsPlayerControlled)
            {
                PlayerHealth = PlayerHealth.Instance;
            }
        }

        public void RequestCancel() => CancelRequested = true;

        /// <summary>
        /// Gets the owner's base damage value before ability modifiers.
        /// Returns 0 if the owner has no damage source.
        /// </summary>
        public int GetOwnerBaseDamage()
        {
            // Player damage is based on stats (Strength + WeaponDamage)
            if (IsPlayerControlled && PlayerStats.Instance != null)
            {
                // Get player's total damage contribution (Strength + WeaponDamage)
                var stats = PlayerStats.Instance.CurrentStats;
                return stats.Strength + stats.WeaponDamage;
            }

            // Enemy damage
            if (IsEnemyControlled && EnemyAI != null)
            {
                return EnemyAI.CurrentMeleeDamage;
            }

            // Neutral NPC damage
            if (IsNeutralControlled && NeutralAI != null)
            {
                return Mathf.Max(1, NeutralAI.attackDamage);
            }

            return 0;
        }

        public Vector2 GetAimDirection(bool fallbackToTarget = true, bool use2D = true)
        {
            Vector2 dir = DesiredDirection;
            if (dir.sqrMagnitude < 0.0001f && fallbackToTarget && Target != null)
            {
                dir = (Vector2)(Target.position - Transform.position);
            }

            return dir.sqrMagnitude < 0.0001f ? Vector2.right : dir.normalized;
        }
    }
}







