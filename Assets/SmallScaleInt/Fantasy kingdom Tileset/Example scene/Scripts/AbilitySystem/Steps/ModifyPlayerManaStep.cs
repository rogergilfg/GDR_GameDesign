using System.Collections;
using UnityEngine;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    /// <summary>
    /// Restores mana to the player using flat or percentage based calculations, optionally spread over time.
    /// </summary>
    [System.Serializable]
    [AbilityComponentDescription("Restores mana to the caster (player). Supports flat or percentage based gains spread over multiple ticks.")]
    public sealed class ModifyPlayerManaStep : AbilityStep
    {
        private enum GainMode
        {
            Flat,
            PercentOfMax
        }

        [Header("Amount")]
        [SerializeField]
        [Tooltip("How the mana gain amount is determined.")]
        private GainMode mode = GainMode.Flat;

        [SerializeField]
        [Tooltip("Flat amount of mana restored when Mode is Flat.")]
        private float flatAmount = 20f;

        [SerializeField]
        [Tooltip("Percent (0..1) of max mana used when Mode is PercentOfMax.")]
        [Range(0f, 1f)]
        private float percentOfMax = 0.25f;

        [Header("Delivery")]
        [SerializeField]
        [Tooltip("0 = instant. Otherwise the total amount is spread evenly across the duration.")]
        private float duration = 0f;

        [SerializeField]
        [Tooltip("When duration > 0, determines how often to tick mana restoration.")]
        private float tickInterval = 0.25f;

        [SerializeField]
        [Tooltip("Show floating combat text when mana is restored.")]
        private bool showCombatText = true;

        [SerializeField]
        [Tooltip("Optional VFX spawned on the player when mana is restored.")]
        private GameObject manaVfxPrefab;

        [SerializeField]
        [Tooltip("Lifetime of the mana VFX (seconds). 0 = destroy next frame.")]
        private float vfxCleanupDelay = 1.5f;

        public override IEnumerator Execute(AbilityRuntimeContext context)
        {
            if (context == null || !context.Transform)
            {
                yield break;
            }

            var mana = context.PlayerMana ?? PlayerMana.Instance;
            if (mana == null)
            {
                yield break;
            }

            float totalAmount = ComputeAmount(mana.MaxMana);
            if (Mathf.Approximately(totalAmount, 0f))
            {
                yield break;
            }

            if (duration <= 0f)
            {
                ApplyMana(context, mana, totalAmount);
                yield break;
            }

            float tick = Mathf.Max(0.05f, tickInterval);
            float elapsed = 0f;
            float remaining = totalAmount;
            while (elapsed < duration && remaining > 0f)
            {
                if (context.CancelRequested || context.Owner == null)
                {
                    yield break;
                }

                float delta = Mathf.Min(remaining, (totalAmount / Mathf.Max(1f, duration / tick)) );
                ApplyMana(context, mana, delta);
                remaining -= delta;

                float wait = Mathf.Min(tick, duration - elapsed);
                if (wait > 0f)
                {
                    elapsed += wait;
                    yield return new WaitForSeconds(wait);
                }
                else
                {
                    break;
                }
            }

            if (remaining > 0f)
            {
                ApplyMana(context, mana, remaining);
            }
        }

        float ComputeAmount(float maxMana)
        {
            switch (mode)
            {
                case GainMode.Flat:
                    return Mathf.Max(0f, flatAmount);
                case GainMode.PercentOfMax:
                    return Mathf.Max(0f, Mathf.Clamp01(percentOfMax) * Mathf.Max(0f, maxMana));
                default:
                    return 0f;
            }
        }

        void ApplyMana(AbilityRuntimeContext context, PlayerMana mana, float amount)
        {
            if (context == null || mana == null || amount <= 0f)
            {
                return;
            }

            mana.Grant(amount);
            if (showCombatText && CombatTextManager.Instance != null)
            {
                CombatTextManager.Instance.SpawnMana(amount, mana.transform.position + Vector3.up * 0.8f);
            }

            if (manaVfxPrefab != null)
            {
                var vfx = Object.Instantiate(manaVfxPrefab, mana.transform.position, Quaternion.identity, mana.transform);
                if (vfxCleanupDelay > 0f)
                {
                    Object.Destroy(vfx, vfxCleanupDelay);
                }
            }
        }
    }
}



