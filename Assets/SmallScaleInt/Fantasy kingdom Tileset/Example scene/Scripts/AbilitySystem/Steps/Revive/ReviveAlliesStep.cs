using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using SmallScaleInc.CharacterCreatorFantasy;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [System.Serializable]
    [AbilityComponentDescription("Searches for fallen allies and revives them after an optional windup, pausing AI and spawning VFX as needed.")]
    [MovedFrom("AbilitySystem")]
    public sealed class ReviveAlliesStep : AbilityStep
    {
        [Header("Search")]
        [SerializeField]
        private ReviveSearchSettings searchSettings = new ReviveSearchSettings();

        [SerializeField]
        [Tooltip("Avoid corpses that another reviver has already reserved.")]
        private bool avoidReservedTargets = true;

        [Header("Gates")]
        [SerializeField]
        [Tooltip("Require the AI to be engaged/leashed before reviving.")]
        private bool requireEngaged = false;

        [Header("Windup")]
        [SerializeField]
        [Tooltip("Windup duration before the revive effect fires.")]
        private float windupTime = 0.6f;

        [SerializeField]
        [Tooltip("Pause EnemyAI while performing the revive.")]
        private bool pauseAiDuringWindup = true;

        [SerializeField]
        [Tooltip("Trigger sent to the caster's animator when windup begins.")]
        private string windupTrigger = "Cast";

        [SerializeField]
        [Tooltip("Optional VFX spawned at the caster during windup.")]
        private GameObject windupVfx;

        [SerializeField]
        [Tooltip("Lifetime of the windup VFX.")]
        private float windupVfxLifetime = 2f;

        [Header("Revive Effect")]
        [SerializeField]
        [Tooltip("Health restored to the revived ally.")]
        private int reviveHealth = 20;

        [SerializeField]
        [Tooltip("Use EnemyHealth2D.Revive(..., fullReactivate) for physics/collider reactivation.")]
        private bool fullReactivation = true;

        [SerializeField]
        [Tooltip("Animator trigger sent to the revived ally after they stand up.")]
        private string targetReviveTrigger = "Revive";

        [SerializeField]
        [Tooltip("Optional VFX spawned on the revived ally.")]
        private GameObject reviveVfx;

        [SerializeField]
        [Tooltip("Lifetime of the revive VFX.")]
        private float reviveVfxLifetime = 3f;

        [Header("Recovery")]
        [SerializeField]
        [Tooltip("Recovery duration after the revive completes.")]
        private float recoveryTime = 0.25f;

        [Header("Physics Lock")]
        [SerializeField]
        [Tooltip("Set the caster's Rigidbody2D to kinematic during the ability.")]
        private bool makeCasterKinematic = true;

        [SerializeField]
        [Tooltip("Zero out caster velocity each frame while the ability runs.")]
        private bool zeroVelocityEachFrame = true;

        class ReviveReservation : MonoBehaviour
        {
            public float reservedUntil;
        }

        public override IEnumerator Execute(AbilityRuntimeContext context)
        {
            var caster = context.Transform;
            if (!caster)
            {
                yield break;
            }

            EnemyHealth2D target = FindTarget(context);
            if (!target)
            {
                yield break;
            }

            bool reserved = !avoidReservedTargets || Reserve(target);
            if (!reserved)
            {
                yield break;
            }

            EnemyAI ai = context.EnemyAI;
            Rigidbody2D rb = context.Rigidbody2D;
            RigidbodyType2D savedBodyType = default;
            float savedGravity = 0f;
            float savedDrag = 0f;

            if (makeCasterKinematic && rb)
            {
                savedBodyType = rb.bodyType;
                savedGravity = rb.gravityScale;
                savedDrag = rb.linearDamping;
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.gravityScale = 0f;
                rb.linearDamping = 0f;
            }

            if (pauseAiDuringWindup && ai)
            {
                ai.SetExternalPause(true);
            }

            if (!string.IsNullOrEmpty(windupTrigger) && context.Animator)
            {
                context.Animator.SetTrigger(windupTrigger);
            }

            if (windupVfx)
            {
                var vfx = Object.Instantiate(windupVfx, caster.position, Quaternion.identity);
                if (windupVfxLifetime > 0f)
                {
                    Object.Destroy(vfx, windupVfxLifetime);
                }
            }

            float elapsed = 0f;
            while (elapsed < windupTime)
            {
                if (context.CancelRequested)
                {
                    Cleanup(rb, savedBodyType, savedGravity, savedDrag, ai);
                    Release(target);
                    yield break;
                }

                elapsed += Time.deltaTime;

                if (zeroVelocityEachFrame && rb)
                {
#if UNITY_2022_2_OR_NEWER
                    rb.linearVelocity = Vector2.zero;
#else
                    rb.velocity = Vector2.zero;
#endif
                    rb.angularVelocity = 0f;
                }

                yield return null;
            }

            bool success = target && target.Revive(Mathf.Max(1, reviveHealth), fullReactivation);
            if (success)
            {
                if (!string.IsNullOrEmpty(targetReviveTrigger) && target.animator)
                {
                    target.animator.SetTrigger(targetReviveTrigger);
                }

                if (reviveVfx)
                {
                    var vfx = Object.Instantiate(reviveVfx, target.transform.position, Quaternion.identity);
                    if (reviveVfxLifetime > 0f)
                    {
                        Object.Destroy(vfx, reviveVfxLifetime);
                    }
                }
            }

            if (recoveryTime > 0f)
            {
                float end = Time.time + recoveryTime;
                while (Time.time < end)
                {
                    if (context.CancelRequested)
                    {
                        break;
                    }

                    if (zeroVelocityEachFrame && rb)
                    {
#if UNITY_2022_2_OR_NEWER
                        rb.linearVelocity = Vector2.zero;
#else
                        rb.velocity = Vector2.zero;
#endif
                        rb.angularVelocity = 0f;
                    }

                    yield return null;
                }
            }

            Cleanup(rb, savedBodyType, savedGravity, savedDrag, ai);
            Release(target);
        }

        EnemyHealth2D FindTarget(AbilityRuntimeContext context)
        {
            Transform owner = context.Transform;
            if (!owner)
            {
                return null;
            }

            var all = EnemyHealth2D.All;
            EnemyHealth2D best = null;
            float bestDist = float.PositiveInfinity;
            float bestTimestamp = searchSettings.preference == ReviveSearchSettings.TargetPreference.NewestDeadFirst
                ? float.NegativeInfinity
                : float.PositiveInfinity;

            for (int i = 0; i < all.Count; i++)
            {
                var candidate = all[i];
                if (!candidate || !candidate.IsDead) continue;
                if (candidate == context.EnemyHealth) continue;

                if ((searchSettings.allyMask.value & (1 << candidate.gameObject.layer)) == 0)
                {
                    continue;
                }

                float dist = Vector2.Distance(owner.position, candidate.transform.position);
                if (dist > searchSettings.searchRadius) continue;
                if (dist < searchSettings.minDistance) continue;
                if (searchSettings.maxDistance > 0f && dist > searchSettings.maxDistance) continue;

                if (avoidReservedTargets && IsReserved(candidate)) continue;

                if (searchSettings.requireLineOfSight && searchSettings.lineOfSightBlockers.value != 0)
                {
                    Vector2 a = owner.position;
                    Vector2 b = candidate.transform.position;
                    if (Physics2D.Linecast(a, b, searchSettings.lineOfSightBlockers))
                    {
                        continue;
                    }
                }

                switch (searchSettings.preference)
                {
                    case ReviveSearchSettings.TargetPreference.Nearest:
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            best = candidate;
                        }
                        break;
                    case ReviveSearchSettings.TargetPreference.OldestDeadFirst:
                        if (candidate.DiedAt < bestTimestamp || (Mathf.Approximately(candidate.DiedAt, bestTimestamp) && dist < bestDist))
                        {
                            bestTimestamp = candidate.DiedAt;
                            bestDist = dist;
                            best = candidate;
                        }
                        break;
                    case ReviveSearchSettings.TargetPreference.NewestDeadFirst:
                        if (candidate.DiedAt > bestTimestamp || (Mathf.Approximately(candidate.DiedAt, bestTimestamp) && dist < bestDist))
                        {
                            bestTimestamp = candidate.DiedAt;
                            bestDist = dist;
                            best = candidate;
                        }
                        break;
                }
            }

            if (requireEngaged && context.EnemyAI && context.EnemyAI.player)
            {
                float leash = Mathf.Max(0f, context.EnemyAI.leashRadius);
                if (leash > 0f)
                {
                    float dist = Vector2.Distance(owner.position, context.EnemyAI.player.position);
                    if (dist > leash)
                    {
                        return null;
                    }
                }
            }

            return best;
        }

        void Cleanup(Rigidbody2D rb, RigidbodyType2D savedType, float savedGravity, float savedDrag, EnemyAI ai)
        {
            if (pauseAiDuringWindup && ai)
            {
                ai.SetExternalPause(false);
            }

            if (makeCasterKinematic && rb)
            {
                rb.bodyType = savedType;
                rb.gravityScale = savedGravity;
                rb.linearDamping = savedDrag;
            }
        }

        bool Reserve(EnemyHealth2D target)
        {
            var res = target.GetComponent<ReviveReservation>() ?? target.gameObject.AddComponent<ReviveReservation>();
            if (Time.time < res.reservedUntil) return false;
            res.reservedUntil = Time.time + Mathf.Max(0.25f, windupTime + recoveryTime + 0.2f);
            return true;
        }

        void Release(EnemyHealth2D target)
        {
            if (!target) return;
            var res = target.GetComponent<ReviveReservation>();
            if (res) res.reservedUntil = 0f;
        }

        bool IsReserved(EnemyHealth2D target)
        {
            var res = target ? target.GetComponent<ReviveReservation>() : null;
            return res && Time.time < res.reservedUntil;
        }
    }
}








