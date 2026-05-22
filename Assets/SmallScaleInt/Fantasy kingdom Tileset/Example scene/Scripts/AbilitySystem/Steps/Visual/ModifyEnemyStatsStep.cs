using System.Collections;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using SmallScaleInc.CharacterCreatorFantasy;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [System.Serializable]
    [AbilityComponentDescription("Temporarily tweaks EnemyAI combat stats (speed, damage, etc.) and restores them when the step ends.")]
    [MovedFrom("AbilitySystem")]
    public sealed class ModifyEnemyStatsStep : AbilityStep
    {
        [SerializeField]
        [Tooltip("Duration the modifiers remain active (seconds)." )]
        private float duration = 4f;

        [Header("Damage Multiplier")]
        [SerializeField]
        [Tooltip("Apply a temporary damage multiplier to EnemyAI.damageMultiplier.")]
        private bool applyDamageMultiplier = true;

        [SerializeField]
        [Tooltip("Multiplier applied to EnemyAI.damageMultiplier when enabled.")]
        private float damageMultiplier = 1.5f;

        [Header("Knockback Suppression")]
        [SerializeField]
        [Tooltip("Temporarily suppress knockback on EnemyHealth2D.")]
        private bool suppressKnockback = true;

        public override IEnumerator Execute(AbilityRuntimeContext context)
        {
            EnemyAI enemyAi = context.EnemyAI;
            EnemyHealth2D enemyHealth = context.EnemyHealth;

            float originalDamageMultiplier = enemyAi ? enemyAi.damageMultiplier : 1f;

            if (applyDamageMultiplier && enemyAi && damageMultiplier > 0f)
            {
                enemyAi.damageMultiplier = originalDamageMultiplier * damageMultiplier;
            }

            if (suppressKnockback && enemyHealth)
            {
                enemyHealth.SetKnockbackSuppressed(true);
            }

            float remaining = Mathf.Max(0f, duration);
            float end = Time.time + remaining;
            while (Time.time < end)
            {
                if (context.CancelRequested)
                {
                    break;
                }
                yield return null;
            }

            if (applyDamageMultiplier && enemyAi)
            {
                enemyAi.damageMultiplier = originalDamageMultiplier;
            }

            if (suppressKnockback && enemyHealth)
            {
                enemyHealth.SetKnockbackSuppressed(false);
            }
        }
    }
}






