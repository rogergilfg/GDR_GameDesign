using System.Collections;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using SmallScaleInc.CharacterCreatorFantasy;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    using Input = SmallScale.FantasyKingdomTileset.InputAdapter;
    [System.Serializable]
    [AbilityComponentDescription("Launches a hook that latches onto enemies, deals damage, and optionally pulls them toward the owner.")]
    [MovedFrom("AbilitySystem")]
    public sealed class HookStep : AbilityStep
    {
        public enum AimMode
        {
            DesiredDirection,
            Mouse,
            Target,
            MeleeHitbox_SingleTarget,
            TransformForward,
            Custom
        }

        [Header("Aim")]
        [SerializeField]
        private AimMode aimMode = AimMode.DesiredDirection;

        [SerializeField]
        private Vector2 customDirection = Vector2.right;

        [SerializeField]
        private bool preferMouseForPlayers = true;

        [Header("Hook Motion")]
        [SerializeField] private float extendSpeed = 18f;
        [SerializeField] private float retractSpeed = 18f;
        [SerializeField] private float maxDistance = 6f;
        [SerializeField] private float pullSpeed = 9f;
        [SerializeField] private float stopPullDistance = 0.6f;
        [SerializeField] private bool pullTarget = true;

        [Header("Collision")]
        [SerializeField] private LayerMask enemyMask;
        [SerializeField] private float hitProbeRadius = 0.2f;

        [Header("Damage")]
        [SerializeField] private bool dealDamageOnHit = true;
        [SerializeField] private int baseDamage = 10;
        [SerializeField, Range(0f, 0.5f)] private float damageVariance = 0.1f;
        [SerializeField, Range(0f, 1f)] private float critChance = 0.15f;
        [SerializeField, Range(1f, 3f)] private float critMultiplier = 1.5f;
        [SerializeField] private bool showCombatText = true;
        [SerializeField] private Vector2 combatTextOffset = new Vector2(0f, 0.25f);

        [Header("Animation & Control")]
        [SerializeField] private string castTrigger = "Special1";
        [SerializeField] private bool lockControllerDuringHook = true;
        [SerializeField] private bool lockLatchedTargetController = true;

        [Header("VFX")]
        [SerializeField] private GameObject hookBodyPrefab;
        [SerializeField] private GameObject hookHeadPrefab;
        [SerializeField] private float bodyExtraRotationDeg = 0f;
        [SerializeField] private bool enableLine = true;
        [SerializeField] private Material lineMaterial;
        [SerializeField] private Color lineStartColor = Color.white;
        [SerializeField] private Color lineEndColor = Color.white;
        [SerializeField] private float lineStartWidth = 0.05f;
        [SerializeField] private float lineEndWidth = 0.05f;

        static readonly Collider2D[] s_HitBuffer = new Collider2D[16];

        public override IEnumerator Execute(AbilityRuntimeContext context)
        {
            Transform owner = context.Transform;
            if (!owner) yield break;

            Vector2 origin = owner.position;
            Vector2 direction = ResolveAimDirection(context, origin);
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector2.right;
            }
            direction.Normalize();

            Animator animator = context.Animator;
            if (animator && !string.IsNullOrEmpty(castTrigger))
            {
                animator.SetTrigger(castTrigger);
            }

            GenericTopDownController controller = context.TopDownController;
            bool controllerLocked = false;
            bool controllerWasEnabled = controller && controller.enabled;
            if (lockControllerDuringHook && controller)
            {
                controller.attackLockedExternally = true;
                controller.enabled = false;
                controllerLocked = true;
            }

            GameObject hookBody = null;
            GameObject hookHead = null;
            LineRenderer lineRenderer = null;
            SetupVisuals(owner, origin, direction, ref hookBody, ref hookHead, ref lineRenderer);

            bool latched = false;
            Component latchedComponent = null;
            Rigidbody2D latchedRigidbody = null;
            GenericTopDownController latchedController = null;
            bool latchedControllerWasEnabled = false;
            bool latchedControllerLocked = false;
            EnemyAI latchedEnemyAi = null;
            bool latchedEnemyPaused = false;
            Vector3 headPosition = origin;

            // Extend phase
            float traveled = 0f;
            while (traveled < maxDistance)
            {
                if (context.CancelRequested) break;

                float step = extendSpeed * Time.deltaTime;
                traveled = Mathf.Min(maxDistance, traveled + step);
                headPosition = origin + direction * traveled;

                UpdateVisuals(origin, headPosition, hookHead, lineRenderer);

                if (TryLatchTarget(headPosition, direction, context, out latchedComponent))
                {
                    latched = true;
                    latchedRigidbody = latchedComponent ? latchedComponent.GetComponent<Rigidbody2D>() : null;
                    break;
                }

                yield return null;
            }

            // Pull phase
            if (latched && pullTarget && latchedComponent)
            {
                Transform targetTransform = latchedComponent.transform;
                latchedController = latchedComponent.GetComponentInParent<GenericTopDownController>();
                if (latchedController)
                {
                    latchedController.attackLockedExternally = true;
                    latchedControllerLocked = true;

                    if (lockLatchedTargetController)
                    {
                        latchedControllerWasEnabled = latchedController.enabled;
                        latchedController.enabled = false;
                    }
                }

                latchedEnemyAi = latchedComponent.GetComponentInParent<EnemyAI>();
                if (latchedEnemyAi)
                {
                    latchedEnemyAi.SetExternalPause(true);
                    latchedEnemyPaused = true;
                }

                while (!context.CancelRequested && targetTransform)
                {
                    Vector2 current = targetTransform.position;
                    float distance = Vector2.Distance(current, origin);
                    if (distance <= stopPullDistance) break;

                    Vector2 next = Vector2.MoveTowards(current, origin, pullSpeed * Time.deltaTime);
                    if (latchedRigidbody)
                    {
#if UNITY_2022_2_OR_NEWER
                        latchedRigidbody.MovePosition(next);
#else
                        latchedRigidbody.position = next;
#endif
                        latchedRigidbody.linearVelocity = Vector2.zero;
                    }
                    else
                    {
                        targetTransform.position = next;
                    }

                    headPosition = targetTransform.position;
                    UpdateVisuals(origin, headPosition, hookHead, lineRenderer);
                    yield return null;
                }
            }

            // Retract phase
            float retractDistance = traveled;
            while (retractDistance > 0f && !context.CancelRequested)
            {
                float step = retractSpeed * Time.deltaTime;
                retractDistance = Mathf.Max(0f, retractDistance - step);
                headPosition = origin + direction * retractDistance;
                UpdateVisuals(origin, headPosition, hookHead, lineRenderer);
                yield return null;
            }

            CleanupVisuals(hookBody, hookHead, lineRenderer);

            CleanupVisuals(hookBody, hookHead, lineRenderer);

            if (latchedControllerLocked && latchedController)
            {
                latchedController.attackLockedExternally = false;
                if (lockLatchedTargetController)
                {
                    latchedController.enabled = latchedControllerWasEnabled;
                }
            }

            if (latchedEnemyAi && latchedEnemyPaused)
            {
                latchedEnemyAi.SetExternalPause(false);
            }

            if (controllerLocked && controller)
            {
                controller.attackLockedExternally = false;
                controller.enabled = controllerWasEnabled;
            }
        }

        Vector2 ResolveAimDirection(AbilityRuntimeContext context, Vector2 origin)
        {
            switch (aimMode)
            {
                case AimMode.Mouse:
                    return ComputeMouseDirection(context, origin);
                case AimMode.Target:
                    if (context.Target)
                    {
                        Vector2 toTarget = (Vector2)context.Target.position - origin;
                        if (toTarget.sqrMagnitude > 0.0001f) return toTarget.normalized;
                    }
                    break;
                case AimMode.MeleeHitbox_SingleTarget:
                    {
                        Transform meleeTarget = MeleeTargetUtility.GetMeleeTarget(context);
                        if (meleeTarget)
                        {
                            Vector2 toTarget = (Vector2)meleeTarget.position - origin;
                            if (toTarget.sqrMagnitude > 0.0001f) return toTarget.normalized;
                        }
                    }
                    break;
                case AimMode.TransformForward:
                    if (context.Transform)
                    {
                        Vector2 right = context.Transform.right;
                        if (right.sqrMagnitude > 0.0001f) return right.normalized;
                    }
                    break;
                case AimMode.Custom:
                    if (customDirection.sqrMagnitude > 0.0001f) return customDirection.normalized;
                    break;
                case AimMode.DesiredDirection:
                default:
                    if (context.DesiredDirection.sqrMagnitude > 0.0001f) return context.DesiredDirection.normalized;
                    break;
            }

            if (context.IsPlayerControlled && preferMouseForPlayers)
            {
                Vector2 mouse = ComputeMouseDirection(context, origin);
                if (mouse.sqrMagnitude > 0.0001f) return mouse.normalized;
            }

            if (context.Target)
            {
                Vector2 toTarget = (Vector2)context.Target.position - origin;
                if (toTarget.sqrMagnitude > 0.0001f) return toTarget.normalized;
            }

            if (context.Transform)
            {
                Vector2 right = context.Transform.right;
                if (right.sqrMagnitude > 0.0001f) return right.normalized;
            }

            return Vector2.right;
        }

        Vector2 ComputeMouseDirection(AbilityRuntimeContext context, Vector2 origin)
        {
            Camera cam = Camera.main;
            if (!context.IsPlayerControlled || !cam) return Vector2.zero;

            Vector3 owner = context.Transform ? context.Transform.position : new Vector3(origin.x, origin.y, 0f);
            Vector3 world = cam.ScreenToWorldPoint(Input.mousePosition);
            world.z = owner.z;
            return (Vector2)(world - owner);
        }

        bool TryLatchTarget(Vector3 headPosition, Vector2 impactDirection, AbilityRuntimeContext context, out Component latchedComponent)
        {
            latchedComponent = null;
            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(enemyMask);
            filter.SetDepth(float.NegativeInfinity, float.PositiveInfinity);
            int hits = Physics2D.OverlapCircle(headPosition, hitProbeRadius, filter, s_HitBuffer);
            for (int i = 0; i < hits; i++)
            {
                Collider2D col = s_HitBuffer[i];
                if (!col) continue;

                var damageable = col.GetComponentInParent<EnemyAI.IDamageable>();
                if (damageable == null) continue;

                latchedComponent = damageable as Component;

                if (dealDamageOnHit)
                {
                    ApplyDamage(context, damageable, impactDirection, col.bounds.center);
                }

                return true;
            }

            return false;
        }

        void ApplyDamage(AbilityRuntimeContext context, EnemyAI.IDamageable damageable, Vector2 direction, Vector3 impactPoint)
        {
            bool crit;
            int damage = RollDamage(baseDamage, out crit);
            damageable.TakeDamage(damage, direction);
            EnemyAI.NotifyDamageDealt(damageable, context.Transform ? context.Transform : context.Runner.transform, damage);

            if (showCombatText && CombatTextManager.Instance)
            {
                CombatTextManager.Instance.SpawnDamage(damage, impactPoint + (Vector3)combatTextOffset, crit);
            }
        }

        int RollDamage(int baseValue, out bool crit)
        {
            int modified = baseValue;
            if (PlayerStats.Instance != null)
            {
                modified = PlayerStats.Instance.GetModifiedBaseDamage(baseValue);
            }

            float variance = 1f + Random.Range(-damageVariance, damageVariance);
            int rolled = Mathf.Max(1, Mathf.RoundToInt(modified * variance));
            crit = Random.value < critChance;
            if (crit)
            {
                rolled = Mathf.Max(1, Mathf.RoundToInt(rolled * critMultiplier));
            }

            return rolled;
        }

        void SetupVisuals(Transform owner, Vector2 origin, Vector2 direction, ref GameObject hookBody, ref GameObject hookHead, ref LineRenderer lineRenderer)
        {
            Quaternion rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + bodyExtraRotationDeg);

            if (hookBodyPrefab)
            {
                hookBody = Object.Instantiate(hookBodyPrefab, owner.position, rotation, owner);
            }

            if (hookHeadPrefab)
            {
                hookHead = Object.Instantiate(hookHeadPrefab, origin, rotation);
            }

            if (enableLine)
            {
                var go = new GameObject("HookLine");
                lineRenderer = go.AddComponent<LineRenderer>();
                lineRenderer.useWorldSpace = true;
                lineRenderer.positionCount = 2;
                lineRenderer.startWidth = lineStartWidth;
                lineRenderer.endWidth = lineEndWidth;
                lineRenderer.startColor = lineStartColor;
                lineRenderer.endColor = lineEndColor;
                lineRenderer.material = lineMaterial ? lineMaterial : new Material(Shader.Find("Sprites/Default"));
                lineRenderer.enabled = true;
                lineRenderer.SetPosition(0, origin);
                lineRenderer.SetPosition(1, origin);
            }
        }

        void UpdateVisuals(Vector2 origin, Vector3 headPosition, GameObject hookHead, LineRenderer lineRenderer)
        {
            if (hookHead)
            {
                hookHead.transform.position = headPosition;
            }

            if (lineRenderer)
            {
                lineRenderer.SetPosition(0, origin);
                lineRenderer.SetPosition(1, headPosition);
            }
        }

        void CleanupVisuals(GameObject hookBody, GameObject hookHead, LineRenderer lineRenderer)
        {
            if (lineRenderer)
            {
                Object.Destroy(lineRenderer.gameObject);
            }

            if (hookHead)
            {
                Object.Destroy(hookHead);
            }

            if (hookBody)
            {
                Object.Destroy(hookBody);
            }
        }
    }
}






