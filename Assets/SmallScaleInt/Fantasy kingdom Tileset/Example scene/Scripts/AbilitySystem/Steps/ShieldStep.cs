using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using SmallScaleInc.CharacterCreatorFantasy;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [System.Serializable]
    [AbilityComponentDescription("Applies a temporary damage shield to the owner, current target, or nearby allies. The shield absorbs incoming damage until depleted or its lifetime expires.")]
    [MovedFrom("AbilitySystem")]
    public sealed class ShieldStep : AbilityStep
    {
        enum ShieldTargetMode
        {
            Owner,
            CurrentTarget,
            NearbyAllies
        }

        enum ShieldAmountMode
        {
            Flat,
            PercentOfOwnerMaxHealth,
            PercentOfTargetMaxHealth
        }

        [Header("Targets")]
        [SerializeField]
        [Tooltip("Determines who receives the shield.")]
        ShieldTargetMode targetMode = ShieldTargetMode.Owner;

        [SerializeField]
        [Tooltip("When shielding nearby allies, use this layer mask to find eligible colliders.")]
        LayerMask allyMask = ~0;

        [SerializeField]
        [Tooltip("Search radius used when shielding nearby allies.")]
        float allyRadius = 4f;

        [SerializeField]
        [Tooltip("Maximum number of allies to shield (0 = unlimited).")]
        int maxAllies = 0;

        [SerializeField]
        [Tooltip("Include the caster when shielding nearby allies.")]
        bool includeOwnerInAllySearch = true;

        [Header("Shield Amount")]
        [SerializeField]
        [Tooltip("How the shield amount should be calculated.")]
        ShieldAmountMode amountMode = ShieldAmountMode.Flat;

        [SerializeField]
        [Tooltip("Flat amount applied when using Flat mode.")]
        int flatAmount = 50;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Percent (0..1) of max health used when selecting a percentage-based mode.")]
        float percentAmount = 0.25f;

        [Header("Lifetime & Feedback")]
        [SerializeField]
        [Tooltip("Lifetime of the shield in seconds. <= 0 keeps it until depleted.")]
        float lifetime = 6f;

        [SerializeField]
        [Tooltip("Optional VFX spawned on each shielded actor.")]
        GameObject shieldVfxPrefab;

        [SerializeField]
        [Tooltip("Optional VFX spawned each time the shield blocks incoming damage.")]
        GameObject hitVfxPrefab;

        [SerializeField]
        [Tooltip("Lifetime of the hit VFX (seconds). 0 = let the prefab handle cleanup.")]
        [Min(0f)]
        float hitVfxLifetime = 1.5f;

        [SerializeField]
        [Tooltip("Minimum delay between individual hit VFX spawns for a single shield.")]
        [Min(0f)]
        float hitVfxCooldown = 0.08f;

        [SerializeField]
        [Tooltip("If enabled, the shield absorbs all damage for its lifetime regardless of the shield amount.")]
        bool invulnerable = false;

        [SerializeField]
        [Tooltip("If enabled, any existing shield instances on the target are removed before applying this one.")]
        bool replaceExistingShield = false;

        [Header("Post-Processing")]
        [SerializeField]
        [Tooltip("Clamp the final shield amount to a percentage of the recipient's max health.")]
        bool clampToRecipientMaxHealth = false;

        [SerializeField]
        [Tooltip("Multiplier applied to the target's max health when clamping the shield amount.")]
        [Range(0.1f, 3f)]
        float clampRecipientMultiplier = 1f;

        readonly List<Component> _recipients = new();
        readonly Collider2D[] _overlapBuffer = new Collider2D[64];

        public override IEnumerator Execute(AbilityRuntimeContext context)
        {
            if (!context.Transform)
            {
                yield break;
            }

            GatherRecipients(context);
            if (_recipients.Count == 0)
            {
                yield break;
            }

            for (int i = 0; i < _recipients.Count; i++)
            {
                var target = _recipients[i];
                if (!target) continue;

                int shieldAmount = ComputeShieldAmount(context, target);
                if (shieldAmount <= 0 && !invulnerable) continue;

                var handler = AbilityShieldHandler.GetOrCreate(target.transform);
                if (handler == null) continue;

                handler.AddShield(Mathf.Max(shieldAmount, 1), lifetime, shieldVfxPrefab, hitVfxPrefab, hitVfxLifetime, hitVfxCooldown, target.transform, invulnerable, replaceExistingShield);
            }

            yield break;
        }

        void GatherRecipients(AbilityRuntimeContext context)
        {
            _recipients.Clear();

            switch (targetMode)
            {
                case ShieldTargetMode.Owner:
                    AddRecipient(GetHealthComponent(context.Transform));
                    break;

                case ShieldTargetMode.CurrentTarget:
                    if (context.Target)
                    {
                        AddRecipient(GetHealthComponent(context.Target));
                    }
                    break;

                case ShieldTargetMode.NearbyAllies:
                    GatherNearbyAllies(context);
                    break;
            }
        }

        void GatherNearbyAllies(AbilityRuntimeContext context)
        {
            if (includeOwnerInAllySearch)
            {
                if (AddRecipient(GetHealthComponent(context.Transform)) && HasReachedLimit())
                {
                    return;
                }
            }

            Vector3 center = context.Transform.position;
            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(allyMask);
            filter.SetDepth(float.NegativeInfinity, float.PositiveInfinity);
            int hits = Physics2D.OverlapCircle(center, Mathf.Max(0.1f, allyRadius), filter, _overlapBuffer);

            for (int i = 0; i < hits; i++)
            {
                var col = _overlapBuffer[i];
                if (!col) continue;

                var component = GetHealthComponent(col.transform);
                if (component == null) continue;

                if (AddRecipient(component) && HasReachedLimit())
                {
                    break;
                }
            }
        }

        bool AddRecipient(Component comp)
        {
            if (!comp) return false;
            if (_recipients.Contains(comp)) return false;
            _recipients.Add(comp);
            return true;
        }

        bool HasReachedLimit()
        {
            return maxAllies > 0 && _recipients.Count >= maxAllies;
        }

        int ComputeShieldAmount(AbilityRuntimeContext context, Component target)
        {
            int amount = 0;
            switch (amountMode)
            {
                case ShieldAmountMode.Flat:
                    amount = Mathf.Max(0, flatAmount);
                    break;

                case ShieldAmountMode.PercentOfOwnerMaxHealth:
                    if (TryGetHealthStats(context.Transform, out _, out int ownerMax))
                    {
                        amount = Mathf.RoundToInt(ownerMax * Mathf.Clamp01(percentAmount));
                    }
                    break;

                case ShieldAmountMode.PercentOfTargetMaxHealth:
                    if (TryGetHealthStats(target, out _, out int targetMax))
                    {
                        amount = Mathf.RoundToInt(targetMax * Mathf.Clamp01(percentAmount));
                    }
                    break;
            }

            amount = Mathf.Max(0, amount);

            if (clampToRecipientMaxHealth && target != null && TryGetHealthStats(target, out _, out int recipientMax) && recipientMax > 0)
            {
                int cap = Mathf.RoundToInt(recipientMax * clampRecipientMultiplier);
                amount = Mathf.Min(amount, cap);
            }

            return amount;
        }

        static Component GetHealthComponent(Transform root)
        {
            if (!root) return null;

            var player = root.GetComponentInParent<PlayerHealth>();
            if (player) return player;

            var enemy = root.GetComponentInParent<EnemyHealth2D>();
            if (enemy) return enemy;

            var companion = root.GetComponentInParent<CompanionHealth>();
            if (companion) return companion;

            var neutral = root.GetComponentInParent<NeutralNpcAI>();
            if (neutral) return neutral;

            return null;
        }

        static bool TryGetHealthStats(Transform root, out int current, out int max)
        {
            return TryGetHealthStats(GetHealthComponent(root), out current, out max);
        }

        static bool TryGetHealthStats(Component component, out int current, out int max)
        {
            current = 0;
            max = 0;

            if (!component) return false;

            if (component is EnemyHealth2D enemy)
            {
                current = Mathf.RoundToInt(enemy.CurrentHealth);
                max = Mathf.Max(0, enemy.MaxHealth);
                return true;
            }

            if (component is PlayerHealth player)
            {
                current = Mathf.Max(0, player.currentHealth);
                max = Mathf.Max(0, player.maxHealth);
                return true;
            }

            if (component is CompanionHealth companion)
            {
                current = Mathf.Max(0, companion.currentHealth);
                max = Mathf.Max(0, companion.maxHealth);
                return true;
            }

            if (component is NeutralNpcAI neutral)
            {
                current = Mathf.Max(0, neutral.CurrentHealth);
                max = Mathf.Max(0, neutral.maxHealth);
                return true;
            }

            return false;
        }
    }
}




