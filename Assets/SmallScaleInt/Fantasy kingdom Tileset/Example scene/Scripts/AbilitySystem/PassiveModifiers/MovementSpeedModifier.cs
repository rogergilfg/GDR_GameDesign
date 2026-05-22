using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [System.Serializable]
    [PassiveModifierDescription("Increases movement speed by a percentage for player or AI.")]
    [MovedFrom("AbilitySystem")]
    public sealed class MovementSpeedModifier : PassiveAbilityModifier
    {
        [SerializeField]
        [Tooltip("Percentage increase to player run speed (0.1 = +10% speed).")]
        [Range(0f, 2f)]
        private float playerRunSpeedPercent = 0.1f;

        [SerializeField]
        [Tooltip("Percentage increase to AI (Enemy/Neutral) run speed (0.1 = +10% speed).")]
        [Range(0f, 2f)]
        private float enemyRunSpeedPercent = 0.1f;

        private float _basePlayerRunSpeed;
        private float _baseEnemyWalkSpeed;
        private float _baseEnemyRunSpeed;
        private float _baseNeutralRunSpeed;
        private bool _playerBaselineCaptured;
        private bool _enemyBaselineCaptured;
        private bool _neutralBaselineCaptured;

        public override void Apply(AbilityRunner runner)
        {
            if (!enabled) return;

            if (runner.OwnerKind == AbilityActorKind.Player && playerRunSpeedPercent > 0f)
            {
                var controller = runner.CachedTopDownController;
                if (controller != null)
                {
                    if (!_playerBaselineCaptured)
                    {
                        _basePlayerRunSpeed = controller.runSpeed;
                        _playerBaselineCaptured = true;
                    }
                    controller.runSpeed = _basePlayerRunSpeed * (1f + playerRunSpeedPercent);
                }
            }
            else if (runner.OwnerKind == AbilityActorKind.Enemy && enemyRunSpeedPercent > 0f)
            {
                var enemyAI = runner.CachedEnemyAI;
                if (enemyAI != null)
                {
                    if (!_enemyBaselineCaptured)
                    {
                        _baseEnemyWalkSpeed = enemyAI.walkSpeed;
                        _baseEnemyRunSpeed = enemyAI.runSpeed;
                        _enemyBaselineCaptured = true;
                    }
                    float multiplier = 1f + enemyRunSpeedPercent;
                    enemyAI.walkSpeed = _baseEnemyWalkSpeed * multiplier;
                    enemyAI.runSpeed = _baseEnemyRunSpeed * multiplier;
                }
            }
            else if (runner.OwnerKind == AbilityActorKind.Neutral && enemyRunSpeedPercent > 0f)
            {
                var neutralAI = runner.CachedNeutralNpcAI;
                if (neutralAI != null)
                {
                    if (!_neutralBaselineCaptured)
                    {
                        _baseNeutralRunSpeed = neutralAI.runSpeed;
                        _neutralBaselineCaptured = true;
                    }
                    neutralAI.runSpeed = _baseNeutralRunSpeed * (1f + enemyRunSpeedPercent);
                }
            }
        }

        public override void Remove(AbilityRunner runner)
        {
            if (!enabled) return;

            if (runner.OwnerKind == AbilityActorKind.Player && _playerBaselineCaptured)
            {
                var controller = runner.CachedTopDownController;
                if (controller != null)
                {
                    controller.runSpeed = _basePlayerRunSpeed;
                }
                _playerBaselineCaptured = false;
            }
            else if (runner.OwnerKind == AbilityActorKind.Enemy && _enemyBaselineCaptured)
            {
                var enemyAI = runner.CachedEnemyAI;
                if (enemyAI != null)
                {
                    enemyAI.walkSpeed = _baseEnemyWalkSpeed;
                    enemyAI.runSpeed = _baseEnemyRunSpeed;
                }
                _enemyBaselineCaptured = false;
            }
            else if (runner.OwnerKind == AbilityActorKind.Neutral && _neutralBaselineCaptured)
            {
                var neutralAI = runner.CachedNeutralNpcAI;
                if (neutralAI != null)
                {
                    neutralAI.runSpeed = _baseNeutralRunSpeed;
                }
                _neutralBaselineCaptured = false;
            }
        }
    }
}






