using UnityEngine;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    /// <summary>
    /// Debuff that reduces the target's movement speed by a percentage.
    /// Works on both player and enemy targets.
    /// Has built-in duration support with optional refresh on reapply.
    /// </summary>
    [System.Serializable]
    [PassiveModifierDescription("Reduces target's movement speed by a percentage (debuff).")]
    public sealed class DebuffMovementSlowModifier : PassiveAbilityModifier
    {
        [SerializeField]
        [Tooltip("Percentage reduction to movement speed (0.5 = 50% slower, 1.0 = cannot move).")]
        [Range(0f, 1f)]
        private float movementSlowPercent = 0.3f;

        [SerializeField]
        [Tooltip("Duration in seconds. If > 0, debuff will auto-remove after duration. If 0, debuff is permanent.")]
        [Min(0f)]
        private float duration = 5f;

        [SerializeField]
        [Tooltip("If true, reapplying this debuff will reset the duration timer.")]
        private bool refreshDuration = true;

        // Player controller baselines
        private float _originalPlayerRunSpeed;
        private bool _isAppliedToPlayer;

        // Enemy AI baselines (need to track all three speeds)
        private float _originalEnemyRoamSpeed;
        private float _originalEnemyWalkSpeed;
        private float _originalEnemyRunSpeed;
        private bool _isAppliedToEnemy;

        private float _expirationTime;
        private AbilityRunner _runner;
        private AbilityDefinition _abilityDefinition;

        /// <summary>
        /// Called by AbilityRunner to set the ability definition for UI display.
        /// </summary>
        public void SetAbilityDefinition(AbilityDefinition definition)
        {
            _abilityDefinition = definition;
        }

        public override void Apply(AbilityRunner runner)
        {
            if (!enabled || movementSlowPercent <= 0f) return;

            _runner = runner;

            // Try to apply to player
            var controller = runner.CachedTopDownController;
            if (controller != null)
            {
                if (!_isAppliedToPlayer)
                {
                    // Capture baseline on first apply
                    _originalPlayerRunSpeed = controller.runSpeed;
                    _isAppliedToPlayer = true;

                    // Apply the slow
                    float newSpeed = _originalPlayerRunSpeed * (1f - movementSlowPercent);
                    controller.runSpeed = newSpeed;
                    Debug.Log($"[DebuffMovementSlow] Applied to player. Baseline: {_originalPlayerRunSpeed}, New speed: {newSpeed} (reduced by {movementSlowPercent * 100}%)");
                }
                else if (refreshDuration && duration > 0f)
                {
                    // Already applied, just refresh duration
                    _expirationTime = Time.time + duration;
                    Debug.Log($"[DebuffMovementSlow] Refreshed duration on player. Expiration: {_expirationTime - Time.time}s from now");
                    return;
                }
            }

            // Try to apply to enemy
            var enemyAI = runner.CachedEnemyAI;
            if (enemyAI != null)
            {
                if (!_isAppliedToEnemy)
                {
                    // Capture baselines on first apply (all three speed fields)
                    _originalEnemyRoamSpeed = enemyAI.roamSpeed;
                    _originalEnemyWalkSpeed = enemyAI.walkSpeed;
                    _originalEnemyRunSpeed = enemyAI.runSpeed;
                    _isAppliedToEnemy = true;

                    // Apply the slow to all three speed fields
                    float multiplier = 1f - movementSlowPercent;
                    enemyAI.roamSpeed = _originalEnemyRoamSpeed * multiplier;
                    enemyAI.walkSpeed = _originalEnemyWalkSpeed * multiplier;
                    enemyAI.runSpeed = _originalEnemyRunSpeed * multiplier;
                }
                else if (refreshDuration && duration > 0f)
                {
                    // Already applied, just refresh duration
                    _expirationTime = Time.time + duration;
                    return;
                }
            }

            // Set expiration time if duration is set
            if (duration > 0f)
            {
                _expirationTime = Time.time + duration;
            }

            // Register with UI
            if (_abilityDefinition != null && PassiveBuffPanelController.Instance != null && duration > 0f)
            {
                PassiveBuffPanelController.Instance.RegisterDebuff(_abilityDefinition, duration, _expirationTime);
            }
        }

        public override void Remove(AbilityRunner runner)
        {
            if (!enabled) return;

            // Restore player speed
            if (_isAppliedToPlayer)
            {
                var controller = runner.CachedTopDownController;
                if (controller != null)
                {
                    controller.runSpeed = _originalPlayerRunSpeed;
                    Debug.Log($"[DebuffMovementSlow] Restored player speed to {_originalPlayerRunSpeed}, current speed is now {controller.runSpeed}");
                }
                _isAppliedToPlayer = false;
            }

            // Restore enemy speeds
            if (_isAppliedToEnemy)
            {
                var enemyAI = runner.CachedEnemyAI;
                if (enemyAI != null)
                {
                    enemyAI.roamSpeed = _originalEnemyRoamSpeed;
                    enemyAI.walkSpeed = _originalEnemyWalkSpeed;
                    enemyAI.runSpeed = _originalEnemyRunSpeed;
                    Debug.Log($"[DebuffMovementSlow] Restored enemy speeds to roam:{_originalEnemyRoamSpeed}, walk:{_originalEnemyWalkSpeed}, run:{_originalEnemyRunSpeed}");
                }
                _isAppliedToEnemy = false;
            }

            _runner = null;

            // Unregister from UI
            if (_abilityDefinition != null && PassiveBuffPanelController.Instance != null)
            {
                PassiveBuffPanelController.Instance.UnregisterPassive(_abilityDefinition);
            }
        }

        /// <summary>
        /// Called every frame to check duration expiration.
        /// </summary>
        public void Update()
        {
            if ((!_isAppliedToPlayer && !_isAppliedToEnemy) || duration <= 0f || _runner == null) return;

            if (Time.time >= _expirationTime)
            {
                Remove(_runner);
            }
        }
    }
}






