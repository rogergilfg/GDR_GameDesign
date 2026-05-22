using UnityEngine;
using SmallScaleInc.CharacterCreatorFantasy;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    public static class AbilityEffectUtility
    {
        static bool s_loggedMissingTileManager;

        public static bool TryApplyDamage(AbilityRuntimeContext context, Transform target, int amount, Vector2 direction, bool showCombatText = true, Vector2? combatTextOffset = null, Vector3? hitWorldPosition = null, bool isCrit = false)
        {
            Transform attacker = context != null ? context.Transform : null;
            return TryApplyDamageInternal(target, amount, direction, attacker, showCombatText, combatTextOffset, hitWorldPosition, isCrit);
        }

        public static bool TryApplyDamage(AbilityRuntimeContext context, Collider2D collider, int amount, Vector2 sourcePosition, bool showCombatText = true, Vector2? combatTextOffset = null, bool isCrit = false)
        {
            if (!collider) return false;

            Vector2 dir = ((Vector2)collider.transform.position - sourcePosition).normalized;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
            Vector3? hitPoint = collider.bounds.center;

            Transform attacker = context != null ? context.Transform : null;
            return TryApplyDamageInternal(collider.transform, amount, dir, attacker, showCombatText, combatTextOffset, hitPoint, isCrit);
        }

        public static bool TryApplyDamage(Transform target, int amount, Vector2 direction)
        {
            return TryApplyDamageInternal(target, amount, direction, null, true, null, null, false);
        }

        public static bool TryApplyDamage(Collider2D collider, int amount, Vector2 sourcePosition)
        {
            if (!collider) return false;
            Vector2 dir = ((Vector2)collider.transform.position - sourcePosition).normalized;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
            Vector3? hitPoint = collider.bounds.center;
            return TryApplyDamageInternal(collider.transform, amount, dir, null, true, null, hitPoint, false);
        }

        public static bool TryHeal(Transform target, int amount)
        {
            if (!target || amount <= 0) return false;
            bool applied = false;

            var enemyHealth = target.GetComponentInParent<EnemyHealth2D>();
            if (enemyHealth != null && !enemyHealth.IsDead)
            {
                enemyHealth.Heal(Mathf.Max(1, amount));
                applied = true;
            }

            var playerHealth = target.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null && !PlayerHealth.IsPlayerDead)
            {
                int before = playerHealth.currentHealth;
                playerHealth.currentHealth = Mathf.Clamp(before + Mathf.Max(1, amount), 0, playerHealth.maxHealth);
                applied |= playerHealth.currentHealth != before;
            }

            return applied;
        }

        public static bool TryHeal(Collider2D collider, int amount)
        {
            return collider ? TryHeal(collider.transform, amount) : false;
        }

        public static bool TryGrantMana(Transform target, float amount)
        {
            if (!target || Mathf.Approximately(amount, 0f)) return false;
            var mana = target.GetComponentInParent<PlayerMana>();
            if (!mana) return false;

            if (amount > 0f)
            {
                mana.Grant(amount);
            }
            else
            {
                mana.TrySpend(-amount);
            }
            return true;
        }

        public static bool TryGrantMana(AbilityRuntimeContext context, float amount, bool applyToTarget)
        {
            if (Mathf.Approximately(amount, 0f)) return false;

            if (applyToTarget)
            {
                return context.Target && TryGrantMana(context.Target, amount);
            }

            if (context.PlayerMana)
            {
                if (amount > 0f)
                {
                    context.PlayerMana.Grant(amount);
                }
                else
                {
                    context.PlayerMana.TrySpend(-amount);
                }
                return true;
            }

            return TryGrantMana(context.Transform, amount);
        }

        public static bool TryDamageTilesCircle(Vector2 center, float radius, int damage, Object context = null, bool logWarning = true)
        {
            if (radius <= 0f || damage <= 0)
            {
                return false;
            }

            if (!TileDestructionManager.I)
            {
                if (logWarning && !s_loggedMissingTileManager)
                {
                    Debug.LogWarning("AbilityEffectUtility: tile damage was requested but no TileDestructionManager is present in the scene. Skipping tile damage.", context);
                    s_loggedMissingTileManager = true;
                }

                return false;
            }

            return TileDestructionManager.HitCircle(center, radius, Mathf.Max(1, damage)) > 0;
        }

        public static bool TryDamageTilesCone(Vector2 center, Vector2 forward, float radius, float coneDegrees, int damage, Object context = null, bool logWarning = true)
        {
            if (radius <= 0f || damage <= 0)
            {
                return false;
            }

            if (!TileDestructionManager.I)
            {
                if (logWarning && !s_loggedMissingTileManager)
                {
                    Debug.LogWarning("AbilityEffectUtility: tile damage was requested but no TileDestructionManager is present in the scene. Skipping tile damage.", context);
                    s_loggedMissingTileManager = true;
                }

                return false;
            }

            return TileDestructionManager.HitCone(center, forward, radius, coneDegrees, Mathf.Max(1, damage)) > 0;
        }

        static bool TryApplyDamageInternal(Transform target, int amount, Vector2 direction, Transform attacker, bool showCombatText, Vector2? combatTextOffset, Vector3? hitWorldPosition, bool isCrit)
        {
            if (!target) return false;

            int finalDamage = Mathf.Max(1, amount);
            int actualDamageDealt = 0;
            bool isLivingTarget = false;

            var damageable = target.GetComponentInParent<EnemyAI.IDamageable>();
            if (damageable != null)
            {
                // For enemies, the damage dealt is the finalDamage (enemies don't have defense reduction in IDamageable)
                actualDamageDealt = finalDamage;
                isLivingTarget = true;

                damageable.TakeDamage(finalDamage, direction);

                bool absorbed = false;
                var enemyHealth = damageable as EnemyHealth2D;
                if (enemyHealth != null && enemyHealth.LastDamageWasAbsorbed)
                {
                    absorbed = true;
                    actualDamageDealt = 0;
                    SpawnAbsorbedText(hitWorldPosition ?? target.position, combatTextOffset, showCombatText);
                }
                else
                {
                    MaybeSpawnDamageText(finalDamage, hitWorldPosition ?? target.position, combatTextOffset, showCombatText, isCrit);
                }

                if (!absorbed)
                {
                    if (attacker)
                    {
                        EnemyAI.NotifyDamageDealt(damageable, attacker, actualDamageDealt);
                    }

                    // Notify damage dealt event for ability system procs (if attacker is player)
                    NotifyPlayerDamageDealt(attacker, actualDamageDealt, isCrit);

                    // Apply lifesteal if attacker is player
                    ApplyLifesteal(attacker, actualDamageDealt, isLivingTarget, hitWorldPosition ?? target.position);
                }
                return true;
            }

            var playerHealth = target.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null && !PlayerHealth.IsPlayerDead)
            {
                // For player, we need to calculate actual damage after defense
                int damageBeforeDefense = finalDamage;
                if (PlayerStats.Instance != null)
                {
                    actualDamageDealt = PlayerStats.Instance.ApplyIncomingDamageReduction(damageBeforeDefense);
                }
                else
                {
                    actualDamageDealt = damageBeforeDefense;
                }
                isLivingTarget = true;

                playerHealth.TakeDamage(finalDamage, -direction, attacker);
                MaybeSpawnDamageText(finalDamage, hitWorldPosition ?? playerHealth.transform.position, combatTextOffset, showCombatText, isCrit);

                // Notify damage dealt event for ability system procs (if attacker is player)
                NotifyPlayerDamageDealt(attacker, actualDamageDealt, isCrit);

                // Apply lifesteal if attacker is player
                ApplyLifesteal(attacker, actualDamageDealt, isLivingTarget, hitWorldPosition ?? playerHealth.transform.position);
                return true;
            }

            return false;
        }

        static void NotifyPlayerDamageDealt(Transform attacker, int damage, bool wasCritical)
        {
            if (!attacker || damage <= 0) return;

            // Only notify if attacker is on the Player layer
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer == -1 || attacker.gameObject.layer != playerLayer) return;

            var playerStats = PlayerStats.Instance;
            if (playerStats != null)
            {
                playerStats.NotifyDamageDealt(damage, wasCritical);
            }
        }

        static void ApplyLifesteal(Transform attacker, int damageDealt, bool isLivingTarget, Vector3 hitPosition)
        {
            if (!attacker || damageDealt <= 0) return;

            // Only apply lifesteal if attacker is on the Player layer
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer == -1 || attacker.gameObject.layer != playerLayer) return;

            var playerStats = PlayerStats.Instance;
            if (!playerStats) return;

            float lifestealPercent = playerStats.CurrentLifestealPercent;
            int lifestealFlat = playerStats.CurrentLifestealFlat;
            int lifestealCap = playerStats.CurrentLifestealCap;
            bool lifestealLivingOnly = playerStats.CurrentLifestealLivingOnly;

            // Check if we should apply lifesteal
            if (lifestealPercent <= 0f && lifestealFlat <= 0) return;
            if (lifestealLivingOnly && !isLivingTarget) return;

            // Calculate lifesteal amount
            float percentHeal = damageDealt * lifestealPercent;
            int totalHeal = Mathf.RoundToInt(percentHeal) + lifestealFlat;

            // Apply cap if set
            if (lifestealCap > 0)
            {
                totalHeal = Mathf.Min(totalHeal, lifestealCap);
            }

            totalHeal = Mathf.Max(1, totalHeal);

            // Heal the attacker
            var playerHealth = PlayerHealth.Instance;
            if (playerHealth != null && !PlayerHealth.IsPlayerDead)
            {
                int before = playerHealth.currentHealth;
                int after = Mathf.Clamp(before + totalHeal, 0, playerHealth.maxHealth);
                int actualHealed = after - before;

                if (actualHealed > 0)
                {
                    playerHealth.currentHealth = after;

                    // Show lifesteal combat text
                    if (CombatTextManager.Instance)
                    {
                        CombatTextManager.Instance.SpawnLifesteal(actualHealed, hitPosition);
                    }
                }
            }
        }

        static void MaybeSpawnDamageText(int amount, Vector3 basePosition, Vector2? offset, bool show, bool isCrit)
        {
            if (!show || !CombatTextManager.Instance) return;
            Vector3 position = basePosition;
            if (offset.HasValue)
            {
                position += (Vector3)offset.Value;
            }

            CombatTextManager.Instance.SpawnDamage(Mathf.Max(1, amount), position, isCrit);
        }

        public static void SpawnAbsorbedText(Vector3 basePosition, Vector2? offset = null, bool show = true)
        {
            if (!show || !CombatTextManager.Instance) return;
            Vector3 position = basePosition;
            if (offset.HasValue)
            {
                position += (Vector3)offset.Value;
            }

            CombatTextManager.Instance.SpawnStatus("Absorbed", position);
        }
    }
}





