using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using SmallScaleInc.CharacterCreatorFantasy;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    using Input = SmallScale.FantasyKingdomTileset.InputAdapter;
    [System.Serializable]
    [AbilityComponentDescription("Creates a persistent area effect that applies damage, movement speed changes, or damage amplification to targets within a radius over time.")]
    [MovedFrom("AbilitySystem")]
    public sealed class EffectAreaStep : AbilityStep
    {
        private enum AreaAnchor
        {
            Owner,
            Target,
            MousePosition
        }

        [System.Flags]
        private enum EffectType
        {
            None = 0,
            DamageOverTime = 1 << 0,
            SlowMovement = 1 << 1,
            WeakenDamage = 1 << 2
        }

        [Header("Area Settings")]
        [SerializeField]
        [Tooltip("Where the effect area is centered.")]
        private AreaAnchor anchor = AreaAnchor.Owner;

        [SerializeField]
        [Tooltip("Radius of the effect area.")]
        private float radius = 3f;

        [SerializeField]
        [Tooltip("How long the effect area persists.")]
        private float duration = 5f;

        [SerializeField]
        [Tooltip("Optional offset applied after choosing the anchor position.")]
        private Vector2 offset = Vector2.zero;

        [SerializeField]
        [Tooltip("When enabled, this step completes immediately and runs the effect area in the background. This allows other abilities to be used while the area is active.")]
        private bool nonBlocking = false;

        [Header("Target Filtering")]
        [SerializeField]
        [Tooltip("Layers that can be affected.")]
        private LayerMask targetMask = ~0;

        [SerializeField]
        [Tooltip("How often to check for and apply effects to targets (in seconds).")]
        [Min(0.1f)]
        private float tickInterval = 0.5f;

        [Header("Effects")]
        [SerializeField]
        [Tooltip("Types of effects to apply to targets in the area.")]
        private EffectType effects = EffectType.DamageOverTime;

        [Header("Damage Over Time")]
        [SerializeField]
        [Tooltip("Damage applied per tick when DamageOverTime is enabled.")]
        private int damagePerTick = 5;

        [SerializeField]
        [Tooltip("When enabled, adds a percentage of the owner's base damage to the tick damage.")]
        private bool scaleWithOwnerDamage = false;

        [SerializeField]
        [Range(0f, 2f)]
        [Tooltip("Percentage of owner's damage to add per tick.")]
        private float ownerDamageScale = 0.5f;

        [SerializeField]
        [Tooltip("Disable knockback and ability interrupts on targets while they are in this area. Prevents enemies from being continuously knocked back and having their abilities interrupted by DoT damage.")]
        private bool suppressKnockback = true;

        [Header("Movement Speed Modification")]
        [SerializeField]
        [Tooltip("Movement speed multiplier when SlowMovement is enabled (0.5 = 50% speed).")]
        [Range(0f, 2f)]
        private float movementSpeedMultiplier = 0.5f;

        [Header("Damage Modification")]
        [SerializeField]
        [Tooltip("Damage multiplier when WeakenDamage is enabled (0.5 = 50% damage dealt).")]
        [Range(0f, 2f)]
        private float damageMultiplier = 0.75f;

        [Header("Visual Feedback")]
        [SerializeField]
        [Tooltip("Optional prefab spawned at the effect area center (e.g., visual effect).")]
        private GameObject visualPrefab;

        [SerializeField]
        [Tooltip("Show damage combat text for each tick.")]
        private bool showDamageCombatText = true;

        readonly Collider2D[] _buffer = new Collider2D[32];
        readonly HashSet<Component> _affectedThisTick = new HashSet<Component>();
        readonly HashSet<EnemyHealth2D> _suppressedEnemies = new HashSet<EnemyHealth2D>();

        public override IEnumerator Execute(AbilityRuntimeContext context)
        {
            Vector2? centerPos = ResolveAnchor(context);
            if (!centerPos.HasValue)
            {
                yield break;
            }

            Vector2 center = centerPos.Value + offset;

            if (nonBlocking)
            {
                // Start the effect area in the background and return immediately
                context.Runner.StartCoroutine(RunEffectAreaCoroutine(context, center));
                yield break;
            }
            else
            {
                // Run the effect area blocking (old behavior)
                yield return RunEffectAreaCoroutine(context, center);
            }
        }

        IEnumerator RunEffectAreaCoroutine(AbilityRuntimeContext context, Vector2 center)
        {
            GameObject visual = null;

            // Spawn visual if provided
            if (visualPrefab)
            {
                Vector3 spawnPos = new Vector3(center.x, center.y, 0f);
                visual = Object.Instantiate(visualPrefab, spawnPos, Quaternion.identity);
            }

            float elapsed = 0f;
            float nextTickTime = 0f;

            while (elapsed < duration)
            {
                if (context.CancelRequested)
                {
                    break;
                }

                // Apply effects on tick interval
                if (elapsed >= nextTickTime)
                {
                    ApplyEffectsToTargetsInArea(context, center);
                    nextTickTime = elapsed + tickInterval;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // Cleanup visual
            if (visual)
            {
                Object.Destroy(visual);
            }

            // Restore knockback and hit interrupts on all suppressed enemies
            if (suppressKnockback)
            {
                foreach (var enemy in _suppressedEnemies)
                {
                    if (enemy)
                    {
                        enemy.SetKnockbackSuppressed(false);
                        enemy.SetHitInterruptSuppressed(false);
                    }
                }
                _suppressedEnemies.Clear();
            }

            yield break;
        }

        void ApplyEffectsToTargetsInArea(AbilityRuntimeContext context, Vector2 center)
        {
            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(targetMask);
            filter.SetDepth(float.NegativeInfinity, float.PositiveInfinity);
            int count = Physics2D.OverlapCircle(center, radius, filter, _buffer);
            _affectedThisTick.Clear();

            // Track which suppressed enemies are still in the area this tick
            HashSet<EnemyHealth2D> enemiesStillInArea = new HashSet<EnemyHealth2D>();

            for (int i = 0; i < count; i++)
            {
                var col = _buffer[i];
                if (!col) continue;

                // Avoid self-targeting unless explicitly in target mask
                if (col.transform == context.Transform) continue;

                // Track each unique component to avoid duplicate processing
                var component = col.GetComponentInParent<EnemyAI.IDamageable>();
                if (component != null)
                {
                    var key = component as Component;
                    if (_affectedThisTick.Contains(key)) continue;
                    _affectedThisTick.Add(key);

                    // Track enemy health if we're suppressing knockback
                    if (suppressKnockback)
                    {
                        var enemyHealth = col.GetComponentInParent<EnemyHealth2D>();
                        if (enemyHealth) enemiesStillInArea.Add(enemyHealth);
                    }

                    ApplyEffectsToTarget(context, component, col, center);
                }
            }

            // Restore knockback for enemies that left the area
            if (suppressKnockback)
            {
                var enemiesToRestore = new List<EnemyHealth2D>();
                foreach (var enemy in _suppressedEnemies)
                {
                    if (enemy && !enemiesStillInArea.Contains(enemy))
                    {
                        enemiesToRestore.Add(enemy);
                    }
                }

                foreach (var enemy in enemiesToRestore)
                {
                    enemy.SetKnockbackSuppressed(false);
                    enemy.SetHitInterruptSuppressed(false);
                    _suppressedEnemies.Remove(enemy);
                }
            }
        }

        void ApplyEffectsToTarget(AbilityRuntimeContext context, EnemyAI.IDamageable damageable, Collider2D col, Vector2 center)
        {
            // Suppress knockback and hit interrupts on this target if enabled
            EnemyHealth2D enemyHealth = null;
            if (suppressKnockback)
            {
                enemyHealth = col.GetComponentInParent<EnemyHealth2D>();
                if (enemyHealth && !_suppressedEnemies.Contains(enemyHealth))
                {
                    enemyHealth.SetKnockbackSuppressed(true);
                    enemyHealth.SetHitInterruptSuppressed(true);
                    _suppressedEnemies.Add(enemyHealth);
                }
            }

            // Apply damage over time
            if ((effects & EffectType.DamageOverTime) != 0 && damagePerTick > 0)
            {
                int damage = damagePerTick;

                // Scale with owner damage if enabled
                if (scaleWithOwnerDamage)
                {
                    int ownerDamage = context.GetOwnerBaseDamage();
                    int scaledDamage = Mathf.RoundToInt(ownerDamage * ownerDamageScale);
                    damage += scaledDamage;
                }

                Vector2 direction = ((Vector2)col.transform.position - center).normalized;

                // Apply damage
                damageable.TakeDamage(damage, direction);
                EnemyAI.NotifyDamageDealt(damageable, context.Transform, damage);

                // Show combat text
                if (showDamageCombatText && CombatTextManager.Instance)
                {
                    CombatTextManager.Instance.SpawnDamage(damage, col.bounds.center, false);
                }
            }

            // Apply movement speed modification
            if ((effects & EffectType.SlowMovement) != 0)
            {
                ApplyMovementSpeedEffect(col, movementSpeedMultiplier);
            }

            // Apply damage modification (weakening)
            if ((effects & EffectType.WeakenDamage) != 0)
            {
                ApplyDamageModificationEffect(col, damageMultiplier);
            }
        }

        void ApplyMovementSpeedEffect(Collider2D col, float speedMultiplier)
        {
            // Try to find EnemyAI component
            var enemyAI = col.GetComponentInParent<EnemyAI>();
            if (enemyAI != null)
            {
                // Apply temporary speed modification
                // Note: This requires EnemyAI to have a speed multiplier system
                // For now, we'll add a simple approach
                // You may need to extend EnemyAI to properly support temporary speed modifiers

                // TODO: Implement proper temporary speed modifier system in EnemyAI
                // For now, this is a placeholder
                Debug.Log($"[EffectAreaStep] Applying {speedMultiplier}x speed to {enemyAI.name}");
            }
        }

        void ApplyDamageModificationEffect(Collider2D col, float damageMultiplier)
        {
            // Try to find EnemyAI component
            var enemyAI = col.GetComponentInParent<EnemyAI>();
            if (enemyAI != null)
            {
                // Apply temporary damage modification
                // Note: This requires EnemyAI to have a damage multiplier system
                // For now, we'll add a simple approach

                // TODO: Implement proper temporary damage modifier system in EnemyAI
                // For now, this is a placeholder
                Debug.Log($"[EffectAreaStep] Applying {damageMultiplier}x damage to {enemyAI.name}");
            }
        }

        Vector2? ResolveAnchor(AbilityRuntimeContext context)
        {
            switch (anchor)
            {
                case AreaAnchor.Target:
                    {
                        Transform target = context.Target ? context.Target : context.Transform;
                        return target ? (Vector2)target.position : null;
                    }

                case AreaAnchor.MousePosition:
                    {
                        // Get mouse position in world space
                        Camera cam = Camera.main;
                        if (!cam) return context.Transform ? (Vector2)context.Transform.position : null;

                        Vector3 mouseScreenPos = Input.mousePosition;
                        // Set Z to match the owner's distance from camera for proper world conversion
                        if (context.Transform)
                        {
                            mouseScreenPos.z = cam.WorldToScreenPoint(context.Transform.position).z;
                        }

                        Vector3 mouseWorldPos = cam.ScreenToWorldPoint(mouseScreenPos);
                        Vector2 mousePos2D = new Vector2(mouseWorldPos.x, mouseWorldPos.y);

                        // Clamp to max range if WaitForTargetConfirmationStep set one
                        if (context.ConfirmedTargetMaxRange.HasValue && context.Transform)
                        {
                            Vector2 ownerPos = context.Transform.position;
                            Vector2 direction = (mousePos2D - ownerPos);
                            float distance = direction.magnitude;

                            if (distance > context.ConfirmedTargetMaxRange.Value)
                            {
                                mousePos2D = ownerPos + direction.normalized * context.ConfirmedTargetMaxRange.Value;
                            }
                        }

                        return mousePos2D;
                    }

                default: // Owner
                    return context.Transform ? (Vector2)context.Transform.position : null;
            }
        }
    }
}







