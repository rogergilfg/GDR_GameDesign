using System.Collections;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [System.Serializable]
    [AbilityComponentDescription("Temporarily alters Rigidbody2D motion (locking, zeroing velocity) while the step executes, then restores the original state.")]
    [MovedFrom("AbilitySystem")]
    public sealed class RigidbodyLockStep : AbilityStep
    {
        [SerializeField]
        [Tooltip("Switch the Rigidbody2D to kinematic while this step is active.")]
        private bool makeKinematic = true;

        [SerializeField]
        [Tooltip("Clear velocity before applying the lock.")]
        private bool zeroVelocityOnEnter = true;

        [SerializeField]
        [Tooltip("Clear velocity after restoring the body state.")]
        private bool zeroVelocityOnExit = true;

        [SerializeField]
        [Tooltip("Reapply the original body type, gravity scale, and damping when the step completes.")]
        private bool restoreOnExit = true;

        private struct BodySnapshot
        {
            public RigidbodyType2D bodyType;
            public float gravity;
            public float drag;
        }

        public override IEnumerator Execute(AbilityRuntimeContext context)
        {
            Rigidbody2D rb = context.Rigidbody2D;
            if (!rb) yield break;

            BodySnapshot snapshot = default;
            if (restoreOnExit)
            {
                snapshot = new BodySnapshot
                {
                    bodyType = rb.bodyType,
                    gravity = rb.gravityScale,
                    drag = rb.linearDamping
                };
            }

            if (zeroVelocityOnEnter)
            {
#if UNITY_2022_2_OR_NEWER
                rb.linearVelocity = Vector2.zero;
#else
                rb.velocity = Vector2.zero;
#endif
            }

            if (makeKinematic)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.gravityScale = 0f;
                rb.linearDamping = 0f;
            }

            yield return null;

            if (restoreOnExit)
            {
                rb.bodyType = snapshot.bodyType;
                rb.gravityScale = snapshot.gravity;
                rb.linearDamping = snapshot.drag;
            }

            if (zeroVelocityOnExit)
            {
#if UNITY_2022_2_OR_NEWER
                rb.linearVelocity = Vector2.zero;
#else
                rb.velocity = Vector2.zero;
#endif
            }
        }
    }
}





