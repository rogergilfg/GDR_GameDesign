using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using SmallScaleInc.CharacterCreatorFantasy;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [System.Serializable]
    [AbilityComponentDescription("Searches for nearby allies (based on filters) and heals each one, optionally spawning VFX and respecting reservations.")]
    [MovedFrom("AbilitySystem")]
    public sealed class HealAlliesStep : AbilityStep
    {
        [Header("Search")]
        [SerializeField]
        [Tooltip("Layer mask defining which colliders are considered allies.")]
        private LayerMask allyMask;

        [SerializeField]
        [Tooltip("Radius used when searching for heal targets.")]
        private float radius = 4.5f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Only allies at or below this health percentage are considered.")]
        private float healthThreshold = 0.65f;

        [SerializeField]
        [Tooltip("Include the caster in the candidate list.")]
        private bool includeSelf = false;

        [SerializeField]
        [Tooltip("Minimum allies required (after filtering) for the heal to trigger.")]
        private int minAllies = 1;

        [SerializeField]
        [Tooltip("Maximum number of allies healed in a single cast. 0 = unlimited.")]
        private int maxAllies = 3;

        [Header("Heal Amount")]
        [SerializeField]
        [Tooltip("Flat heal amount applied to each ally.")]
        private int flatHeal = 10;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Additional heal amount as a fraction of max health.")]
        private float percentHeal = 0f;

        [SerializeField]
        [Tooltip("Clamp applied after flat + percent heal are combined.")]
        private Vector2Int clampAmount = new Vector2Int(1, 999);

        [Header("Visuals")]
        [SerializeField]
        [Tooltip("Optional VFX spawned on each healed ally.")]
        private GameObject healVfxPrefab;

        [SerializeField]
        [Tooltip("Lifetime of the heal VFX when spawned.")]
        private float vfxCleanupDelay = 2f;

        [SerializeField]
        [Tooltip("Show floating heal combat text for each target.")]
        private bool showCombatText = true;

        readonly Collider2D[] _buffer = new Collider2D[64];

        public override IEnumerator Execute(AbilityRuntimeContext context)
        {
            Transform owner = context.Transform;
            if (!owner) yield break;

            // --- MODIFIED: Handle player self-healing ---
            if (includeSelf && context.IsPlayerControlled && context.PlayerHealth)
            {
                var playerHp = context.PlayerHealth;
                if (!PlayerHealth.IsPlayerDead)
                {
                    float playerPct = playerHp.maxHealth > 0 ? playerHp.currentHealth / (float)playerHp.maxHealth : 0f;
                    if (playerPct <= healthThreshold)
                    {
                        int playerHealAmount = flatHeal + Mathf.RoundToInt(playerHp.maxHealth * percentHeal);
                        playerHealAmount = Mathf.Clamp(playerHealAmount, Mathf.Min(clampAmount.x, clampAmount.y), Mathf.Max(clampAmount.x, clampAmount.y));
                        if (playerHealAmount > 0)
                        {
                            playerHp.currentHealth = Mathf.Min(playerHp.maxHealth, playerHp.currentHealth + playerHealAmount);
                            SpawnFeedback(playerHp.transform, playerHealAmount);
                        }
                    }
                }
            }

            List<Component> candidates = GatherCandidates(owner, context);
            if (candidates.Count < Mathf.Max(1, minAllies))
            {
                yield break;
            }

            int healCount = maxAllies <= 0 ? candidates.Count : Mathf.Min(maxAllies, candidates.Count);
            for (int i = 0; i < healCount; i++)
            {
                var comp = candidates[i];
                if (!comp) continue;

                int amount = flatHeal;
                if (comp is EnemyHealth2D enemyHp)
                {
                    if (enemyHp.IsDead) continue;
                    amount += Mathf.RoundToInt(enemyHp.MaxHealth * percentHeal);
                    amount = MathfClamp(amount, clampAmount);
                    if (amount <= 0) continue;
                    enemyHp.Heal(amount);
                }
                else if (comp is CompanionHealth companionHp)
                {
                    if (companionHp.IsDead) continue;
                    amount += Mathf.RoundToInt(companionHp.maxHealth * percentHeal);
                    amount = MathfClamp(amount, clampAmount);
                    if (amount <= 0) continue;
                    companionHp.Heal(amount);
                }
                else
                {
                    continue;
                }

                SpawnFeedback(comp.transform, amount);
                yield return null;
            }
        }

        List<Component> GatherCandidates(Transform owner, AbilityRuntimeContext context)
        {
            var results = new List<Component>();
            Vector3 center = owner.position;
            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(allyMask);
            filter.SetDepth(float.NegativeInfinity, float.PositiveInfinity);
            int count = Physics2D.OverlapCircle(center, radius, filter, _buffer);
            for (int i = 0; i < count; i++)
            {
                var col = _buffer[i];
                if (!col) continue;

                var enemyHp = col.GetComponentInParent<EnemyHealth2D>();
                if (enemyHp)
                {
                    if (enemyHp.IsDead) continue;
                    if (enemyHp.transform == owner && !includeSelf) continue;
                    float pct = enemyHp.MaxHealth > 0 ? enemyHp.CurrentHealth / (float)enemyHp.MaxHealth : 0f;
                    if (pct > healthThreshold) continue;
                    if (!results.Contains(enemyHp))
                        results.Add(enemyHp);
                    continue;
                }

                var companionHp = col.GetComponentInParent<CompanionHealth>();
                if (companionHp)
                {
                    if (companionHp.IsDead) continue;
                    if (companionHp.transform == owner && !includeSelf) continue;
                    float pct = companionHp.maxHealth > 0 ? companionHp.currentHealth / (float)companionHp.maxHealth : 0f;
                    if (pct > healthThreshold) continue;
                    if (!results.Contains(companionHp))
                        results.Add(companionHp);
                }
            }

            results.Sort((a, b) =>
            {
                float pctA = GetHealthPercent(a);
                float pctB = GetHealthPercent(b);
                return pctA.CompareTo(pctB);
            });

            return results;
        }

        static float GetHealthPercent(Component comp)
        {
            if (comp is EnemyHealth2D enemy)
                return enemy.MaxHealth > 0 ? enemy.CurrentHealth / (float)enemy.MaxHealth : 0f;
            if (comp is CompanionHealth companion)
                return companion.maxHealth > 0 ? companion.currentHealth / (float)companion.maxHealth : 0f;
            return 1f;
        }

        void SpawnFeedback(Transform target, int amount)
        {
            Vector3 spawnPosition = target ? target.position : Vector3.zero;

            if (healVfxPrefab)
            {
                Transform parent = target ? target : null;
                var vfx = Object.Instantiate(healVfxPrefab, spawnPosition, Quaternion.identity, parent);
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

        static int MathfClamp(int value, Vector2Int clamp)
        {
            int min = Mathf.Min(clamp.x, clamp.y);
            int max = Mathf.Max(clamp.x, clamp.y);
            return Mathf.Clamp(value, min, max);
        }
    }
}




