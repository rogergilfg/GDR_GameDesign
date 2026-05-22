using System.Collections;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [System.Serializable]
    [AbilityComponentDescription("Restores the caster's own health using flat or percentage-based calculations with flexible clamps.")]
    [MovedFrom("AbilitySystem")]
    public sealed class HealSelfStep : AbilityStep
    {
        private enum HealAmountMode
        {
            Flat,
            PercentOfMax,
            PercentOfMissing
        }

        [Header("Amount")]
        [SerializeField]
        [Tooltip("How the heal amount is determined.")]
        private HealAmountMode mode = HealAmountMode.Flat;

        [SerializeField]
        [Tooltip("Flat amount of HP restored when Mode is Flat.")]
        private int flatAmount = 10;

        [SerializeField]
        [Tooltip("Percent (0..1) used when Mode is PercentOfMax/PercentOfMissing.")]
        [Range(0f, 1f)]
        private float percent = 0.25f;

        [Header("Clamping & Rules")]
        [SerializeField]
        [Tooltip("Minimum clamp for the computed heal amount. 0 disables.")]
        private int minAmount = 0;

        [SerializeField]
        [Tooltip("Maximum clamp for the computed heal amount. 0 disables.")]
        private int maxAmount = 0;

        [SerializeField]
        [Tooltip("Skip if the caster is already at full health.")]
        private bool ignoreIfFullHealth = true;

        [SerializeField]
        [Tooltip("If no compatible health component is found, do nothing instead of failing.")]
        private bool allowNoHealthComponent = true;

        [Header("Feedback")]
        [SerializeField]
        [Tooltip("Optional VFX spawned on the caster when the heal succeeds.")]
        private GameObject healVfxPrefab;

        [SerializeField]
        [Tooltip("Lifetime of the heal VFX (seconds). 0 = destroy next frame.")]
        private float vfxCleanupDelay = 2f;

        [SerializeField]
        [Tooltip("Show combat text when the heal succeeds.")]
        private bool showCombatText = true;

        public override IEnumerator Execute(AbilityRuntimeContext context)
        {
            if (!context.Transform)
            {
                yield break;
            }

            if (!TryGetHealth(context, out int current, out int max))
            {
                if (!allowNoHealthComponent)
                {
                    yield break;
                }
                yield break;
            }

            if (max <= 0)
            {
                yield break;
            }

            if (ignoreIfFullHealth && current >= max)
            {
                yield break;
            }

            int toHeal = ComputeAmount(current, max);
            if (toHeal > 0)
            {
                bool applied = AbilityEffectUtility.TryHeal(context.Transform, toHeal);
                if (applied)
                {
                    SpawnFeedback(context.Transform, toHeal);
                }
            }

            yield break;
        }

        int ComputeAmount(int current, int max)
        {
            int amount = 0;
            switch (mode)
            {
                case HealAmountMode.Flat:
                    amount = Mathf.Max(0, flatAmount);
                    break;
                case HealAmountMode.PercentOfMax:
                    amount = Mathf.RoundToInt(Mathf.Clamp01(percent) * max);
                    break;
                case HealAmountMode.PercentOfMissing:
                    int missing = Mathf.Max(0, max - current);
                    amount = Mathf.RoundToInt(Mathf.Clamp01(percent) * missing);
                    break;
            }

            if (minAmount > 0) amount = Mathf.Max(minAmount, amount);
            if (maxAmount > 0) amount = Mathf.Min(maxAmount, amount);
            return amount;
        }

        bool TryGetHealth(AbilityRuntimeContext context, out int current, out int max)
        {
            current = 0;
            max = 0;

            var enemy = context.EnemyHealth;
            if (enemy)
            {
                // EnemyHealth2D exposes CurrentHealth/MaxHealth used elsewhere in the codebase
                current = Mathf.RoundToInt(enemy.CurrentHealth);
                max = Mathf.Max(0, enemy.MaxHealth);
                return true;
            }

            var player = context.PlayerHealth;
            if (player)
            {
                current = Mathf.Max(0, player.currentHealth);
                max = Mathf.Max(0, player.maxHealth);
                return true;
            }

            var companion = context.Transform.GetComponentInParent<CompanionHealth>();
            if (companion)
            {
                current = Mathf.Max(0, companion.currentHealth);
                max = Mathf.Max(0, companion.maxHealth);
                return true;
            }

            // try to probe components in parent as a last resort
            var enemyProbe = context.Transform.GetComponentInParent<EnemyHealth2D>();
            if (enemyProbe)
            {
                current = Mathf.RoundToInt(enemyProbe.CurrentHealth);
                max = Mathf.Max(0, enemyProbe.MaxHealth);
                return true;
            }

            var playerProbe = context.Transform.GetComponentInParent<PlayerHealth>();
            if (playerProbe)
            {
                current = Mathf.Max(0, playerProbe.currentHealth);
                max = Mathf.Max(0, playerProbe.maxHealth);
                return true;
            }

            return false;
        }

        void SpawnFeedback(Transform target, int amount)
        {
            if (!target)
            {
                return;
            }

            Vector3 spawnPosition = target.position;
            if (healVfxPrefab)
            {
                var vfx = Object.Instantiate(healVfxPrefab, spawnPosition, Quaternion.identity, target);
                if (vfxCleanupDelay > 0f)
                {
                    Object.Destroy(vfx, vfxCleanupDelay);
                }
            }

            if (showCombatText && CombatTextManager.Instance)
            {
                CombatTextManager.Instance.SpawnHeal(amount, spawnPosition + Vector3.up * 0.2f);
            }
        }
    }
}




