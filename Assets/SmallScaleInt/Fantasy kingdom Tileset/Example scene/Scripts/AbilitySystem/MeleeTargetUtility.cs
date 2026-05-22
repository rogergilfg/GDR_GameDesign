using UnityEngine;
using SmallScaleInc.CharacterCreatorFantasy;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    using Input = SmallScale.FantasyKingdomTileset.InputAdapter;
    /// <summary>
    /// Utility for getting the prioritized melee target for player or enemy.
    /// Matches the targeting logic used by PlayerMeleeHitbox and EnemyAI.
    /// </summary>
    public static class MeleeTargetUtility
    {
        /// <summary>
        /// Gets the prioritized melee target for the given context.
        /// For players, checks the melee hitbox area for enemies.
        /// For enemies, returns their current target (player).
        /// </summary>
        public static Transform GetMeleeTarget(AbilityRuntimeContext context)
        {
            // Try player melee hitbox
            var playerMeleeHitbox = context.Transform.GetComponent<PlayerMeleeHitbox>();
            if (playerMeleeHitbox != null)
            {
                return GetPlayerMeleeTarget(context, playerMeleeHitbox);
            }

            // Try enemy AI
            var enemyAI = context.EnemyAI;
            if (enemyAI != null)
            {
                return GetEnemyMeleeTarget(enemyAI);
            }

            // Fallback to context target
            return context.Target;
        }

        private static Transform GetPlayerMeleeTarget(AbilityRuntimeContext context, PlayerMeleeHitbox meleeHitbox)
        {
            // Calculate hit center based on mouse direction
            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 forward = ((Vector2)mouseWorld - (Vector2)context.Transform.position).normalized;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector2.right;

            // Rotate offset based on direction
            Vector2 rotatedOffset = RotateVector2(meleeHitbox.hitOffset, forward);
            Vector2 center = (Vector2)context.Transform.position + rotatedOffset;

            // Find closest enemy in melee range
            var hits = Physics2D.OverlapCircleAll(center, meleeHitbox.hitRadius, meleeHitbox.enemyMask);
            Transform closest = null;
            float closestDist = float.MaxValue;

            foreach (var hit in hits)
            {
                if (!hit) continue;

                float dist = Vector2.Distance(center, hit.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = hit.transform;
                }
            }

            return closest;
        }

        private static Transform GetEnemyMeleeTarget(EnemyAI enemyAI)
        {
            // Enemies use their current target (usually the player)
            // This is stored in EnemyAI's internal state
            // We can access it through the player field if available
            if (enemyAI.player != null)
            {
                return enemyAI.player;
            }

            return null;
        }

        private static Vector2 RotateVector2(Vector2 v, Vector2 forward)
        {
            // Calculate angle from right (1, 0) to forward direction
            float angle = Mathf.Atan2(forward.y, forward.x);
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            // Rotate the vector
            return new Vector2(
                v.x * cos - v.y * sin,
                v.x * sin + v.y * cos
            );
        }
    }
}







