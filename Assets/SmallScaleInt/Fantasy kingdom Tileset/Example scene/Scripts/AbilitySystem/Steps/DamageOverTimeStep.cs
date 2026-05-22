using System.Collections;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [System.Serializable]
    [AbilityComponentDescription("Schedules repeated damage ticks on a target over time, with optional stacking and cancel rules.")]
    [MovedFrom("AbilitySystem")]
    public sealed class DamageOverTimeStep : AbilityStep
    {
        private enum DotMode
        {
            Target,
            Area
        }

        [SerializeField]
        [Tooltip("Determines whether the DoT tracks the current target or an area.")]
        private DotMode mode = DotMode.Target;

        [SerializeField]
        [Tooltip("Damage applied each tick.")]
        private int damagePerTick = 5;

        [SerializeField]
        [Tooltip("Number of ticks applied over the lifetime of the DoT.")]
        private int totalTicks = 5;

        [SerializeField]
        [Tooltip("Seconds between each tick.")]
        private float tickInterval = 0.5f;

        [SerializeField]
        [Tooltip("When enabled, this step completes immediately and runs the DoT in the background. This allows other abilities to be used while the DoT is active.")]
        private bool nonBlocking = false;

        [Header("Area Settings")]
        [SerializeField]
        [Tooltip("Radius used when mode is Area.")]
        private float areaRadius = 2f;

        [SerializeField]
        [Tooltip("Layers affected when mode is Area.")]
        private LayerMask areaMask = ~0;

        [SerializeField]
        [Tooltip("Offset added to the area centre when mode is Area.")]
        private Vector2 areaOffset = Vector2.zero;

        readonly Collider2D[] _buffer = new Collider2D[32];

        public override IEnumerator Execute(AbilityRuntimeContext context)
        {
            if (totalTicks <= 0)
            {
                yield break;
            }

            if (nonBlocking)
            {
                // Start the DoT in the background and return immediately
                context.Runner.StartCoroutine(RunDotCoroutine(context));
                yield break;
            }
            else
            {
                // Run the DoT blocking (old behavior)
                yield return RunDotCoroutine(context);
            }
        }

        IEnumerator RunDotCoroutine(AbilityRuntimeContext context)
        {
            IEnumerator WaitForNextTick()
            {
                if (tickInterval <= 0f)
                {
                    yield break;
                }

                float end = Time.time + tickInterval;
                while (Time.time < end)
                {
                    if (context.CancelRequested) yield break;
                    yield return null;
                }
            }

            for (int tick = 0; tick < totalTicks; tick++)
            {
                if (context.CancelRequested) yield break;

                int amount = damagePerTick;
                if (amount <= 0)
                {
                    yield return WaitForNextTick();
                    continue;
                }

                if (mode == DotMode.Target)
                {
                    Transform target = context.Target;
                    if (!target)
                    {
                        yield break;
                    }

                    Vector2 direction = ((Vector2)target.position - (Vector2)context.Transform.position).normalized;
                    if (direction.sqrMagnitude < 0.0001f) direction = context.Transform.right;
                    AbilityEffectUtility.TryApplyDamage(target, amount, direction);
                }
                else
                {
                Transform anchor = context.Target ? context.Target : context.Transform;
                Vector2 centre = (Vector2)anchor.position + areaOffset;
                ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
                filter.SetLayerMask(areaMask);
                filter.SetDepth(float.NegativeInfinity, float.PositiveInfinity);
                int count = Physics2D.OverlapCircle(centre, areaRadius, filter, _buffer);
                    for (int i = 0; i < count; i++)
                    {
                        var col = _buffer[i];
                        if (!col) continue;
                        AbilityEffectUtility.TryApplyDamage(col, amount, centre);
                    }
                }

                yield return WaitForNextTick();
            }
        }
    }
}





