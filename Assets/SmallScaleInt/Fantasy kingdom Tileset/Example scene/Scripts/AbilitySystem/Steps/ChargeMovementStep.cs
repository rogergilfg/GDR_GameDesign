using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SmallScaleInc.CharacterCreatorFantasy;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [System.Serializable]
    [AbilityComponentDescription("Propels the caster forward in a configurable charge, handling collisions, damage on contact, and optional stopping rules.")]
    public sealed class ChargeMovementStep : AbilityStep
    {
        [Header("Direction")]
        [SerializeField]
        [Tooltip("Lock the charge direction at the start instead of continually tracking the target.")]
        private bool lockDirectionAtStart = true;

        [Header("Speed & Limits")]
        [SerializeField]
        [Tooltip("Movement speed applied while charging.")]
        private float chargeSpeed = 8f;

        [SerializeField]
        [Tooltip("Maximum distance the charge can travel.")]
        private float maxDistance = 6f;

        [SerializeField]
        [Tooltip("Maximum time the charge can remain active.")]
        private float maxDuration = 0.8f;

        [Header("Physics")]
        [SerializeField]
        [Tooltip("Stop the charge when colliding with specified layers.")]
        private bool stopOnCollision = true;

        [SerializeField]
        [Tooltip("Layers that block the charge when Stop On Collision is enabled.")]
        private LayerMask collisionMask = default;

        [SerializeField]
        [Tooltip("Radius used when probing for obstacles during the charge.")]
        private float collisionRadius = 0.3f;

        [Header("Damage")]
        [SerializeField]
        [Tooltip("Apply damage to colliders encountered during the charge.")]
        private bool dealDamageOnHit = true;

        [SerializeField]
        [Tooltip("Damage dealt when colliding with a valid target.")]
        private int hitDamage = 10;

        [SerializeField]
        [Tooltip("Layers that receive damage while charging.")]
        private LayerMask damageMask = default;

        [SerializeField]
        [Tooltip("If true, the charge stops after dealing damage.")]
        private bool stopOnHit = true;

        [Header("Cleanup")]
        [SerializeField]
        [Tooltip("Reset velocity every frame to avoid physics accumulation.")]
        private bool zeroVelocityEachFrame = true;

        [SerializeField]
        [Tooltip("Reset velocity when the charge finishes.")]
        private bool zeroVelocityOnEnd = true;

        readonly Collider2D[] _hitBuffer = new Collider2D[12];
        readonly HashSet<Collider2D> _damagedColliders = new HashSet<Collider2D>();

        public override IEnumerator Execute(AbilityRuntimeContext context)
        {
            if (chargeSpeed <= 0f || maxDuration <= 0f)
                yield break;

            Vector2 direction = ResolveDirection(context);
            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector2.right;

            float travelled = 0f;
            float elapsed = 0f;

            while (elapsed < maxDuration && travelled < maxDistance)
            {
                if (context.CancelRequested || !context.Owner) break;

                float dt = Time.deltaTime;
                elapsed += dt;

                if (!lockDirectionAtStart)
                {
                    direction = ResolveDirection(context);
                }

                float stepDistance = chargeSpeed * dt;
                if (stepDistance <= 0f) break;

                bool blocked = stopOnCollision && collisionMask.value != 0 && CheckCollision(context, direction, stepDistance);
                if (blocked)
                {
                    break;
                }

                ApplyMovement(context, direction, stepDistance);
                travelled += stepDistance;

                if (zeroVelocityEachFrame && context.Rigidbody2D)
                {
#if UNITY_2022_2_OR_NEWER
                    context.Rigidbody2D.linearVelocity = Vector2.zero;
#else
                    context.Rigidbody2D.velocity = Vector2.zero;
#endif
                }

                if (dealDamageOnHit && damageMask.value != 0 && TryDealDamage(context))
                {
                    if (stopOnHit)
                        break;
                }

                yield return null;
            }

            if (zeroVelocityOnEnd && context.Rigidbody2D)
            {
#if UNITY_2022_2_OR_NEWER
                context.Rigidbody2D.linearVelocity = Vector2.zero;
#else
                context.Rigidbody2D.velocity = Vector2.zero;
#endif
            }

            _damagedColliders.Clear();
        }

        Vector2 ResolveDirection(AbilityRuntimeContext context)
        {
            if (context.Target)
            {
                Vector2 toTarget = (Vector2)(context.Target.position - context.Transform.position);
                if (toTarget.sqrMagnitude > 0.0001f)
                    return toTarget.normalized;
            }

            if (context.DesiredDirection.sqrMagnitude > 0.0001f)
            {
                return context.DesiredDirection.normalized;
            }

            if (context.Rigidbody2D)
            {
#if UNITY_2022_2_OR_NEWER
                var vel = context.Rigidbody2D.linearVelocity;
#else
                var vel = context.Rigidbody2D.velocity;
#endif
                if (vel.sqrMagnitude > 0.0001f)
                    return vel.normalized;
            }

            return context.Transform.right;
        }

        bool CheckCollision(AbilityRuntimeContext context, Vector2 direction, float distance)
        {
            Vector2 origin;
            if (context.Rigidbody2D)
            {
                origin = context.Rigidbody2D.position;
            }
            else
            {
                origin = context.Transform.position;
            }

            RaycastHit2D hit = Physics2D.CircleCast(origin, collisionRadius, direction, distance, collisionMask);
            return hit.collider != null;
        }

        void ApplyMovement(AbilityRuntimeContext context, Vector2 direction, float distance)
        {
            Vector2 delta = direction * distance;
            if (context.Rigidbody2D && context.Rigidbody2D.bodyType != RigidbodyType2D.Static)
            {
                if (Time.inFixedTimeStep)
                {
                    context.Rigidbody2D.MovePosition(context.Rigidbody2D.position + delta);
                }
                else
                {
                    context.Rigidbody2D.position += delta;
                }
            }
            else
            {
                Vector3 pos = context.Transform.position + (Vector3)delta;
                pos.z = context.Transform.position.z;
                context.Transform.position = pos;
            }
        }

        bool TryDealDamage(AbilityRuntimeContext context)
        {
            Vector2 center = context.Transform.position;
            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(damageMask);
            filter.SetDepth(float.NegativeInfinity, float.PositiveInfinity);
            int count = Physics2D.OverlapCircle(center, collisionRadius, filter, _hitBuffer);
            bool damagedAny = false;

            for (int i = 0; i < count; i++)
            {
                Collider2D col = _hitBuffer[i];
                if (!col || _damagedColliders.Contains(col)) continue;

                _damagedColliders.Add(col);

                var damageable = col.GetComponentInParent<EnemyAI.IDamageable>();
                if (damageable != null)
                {
                    Vector2 dir = ((Vector2)col.transform.position - center).normalized;
                    damageable.TakeDamage(hitDamage, dir);
                    damagedAny = true;
                    continue;
                }

                var playerHealth = col.GetComponentInParent<PlayerHealth>();
                if (playerHealth != null)
                {
                    Vector2 dir = ((Vector2)col.transform.position - center).normalized;
                    playerHealth.TakeDamage(hitDamage, dir);
                    damagedAny = true;
                }
            }

            return damagedAny;
        }
    }
}





