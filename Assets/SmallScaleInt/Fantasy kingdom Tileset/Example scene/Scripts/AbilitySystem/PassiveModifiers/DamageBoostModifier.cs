using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [System.Serializable]
    [PassiveModifierDescription("Increases damage dealt by a percentage for AI enemies.")]
    [MovedFrom("AbilitySystem")]
    public sealed class DamageBoostModifier : PassiveAbilityModifier
    {
        [SerializeField]
        [Tooltip("Percentage increase to damage dealt by EnemyAI owners (0.1 = +10% damage).")]
        [Range(0f, 2f)]
        private float enemyDamagePercent = 0.1f;

        private float _baseDamageMultiplier;
        private bool _baselineCaptured;

        public override void Apply(AbilityRunner runner)
        {
            if (!enabled) return;

            if (runner.OwnerKind == AbilityActorKind.Enemy && enemyDamagePercent > 0f)
            {
                var enemyAI = runner.CachedEnemyAI;
                if (enemyAI != null)
                {
                    if (!_baselineCaptured)
                    {
                        _baseDamageMultiplier = Mathf.Max(0.0001f, enemyAI.damageMultiplier);
                        _baselineCaptured = true;
                    }
                    enemyAI.damageMultiplier = _baseDamageMultiplier * (1f + enemyDamagePercent);
                }
            }
        }

        public override void Remove(AbilityRunner runner)
        {
            if (!enabled) return;

            if (_baselineCaptured)
            {
                var enemyAI = runner.CachedEnemyAI;
                if (enemyAI != null)
                {
                    enemyAI.damageMultiplier = _baseDamageMultiplier;
                }
                _baselineCaptured = false;
            }
        }
    }
}






