using System.Collections;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using SmallScaleInc.CharacterCreatorFantasy;
using Input = SmallScale.FantasyKingdomTileset.InputAdapter;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [System.Serializable]
    [AbilityComponentDescription("Performs a short burst dash based on input or target direction, with optional invulnerability and collision tuning.")]
    [MovedFrom("AbilitySystem")]
    public sealed class DashMovementStep : AbilityStep
    {
        private enum PlayerDirectionMode
        {
            DesiredInput,
            AimDirection,
            TowardsTarget,
            AwayFromTarget
        }

        private enum PlayerDodgeMode
        {
            Sidestep,
            Directional
        }

        [System.Serializable]
        struct AiDirectionSettings
        {
            [Tooltip("Pick a random sideways direction (perpendicular to the target vector).")]
            public bool allowSideways;

            [Tooltip("Allow dodging directly away from the target.")]
            public bool allowBackward;

            [Tooltip("Allow dodging directly toward the target.")]
            public bool allowForward;

            [Tooltip("Bias applied to backward dodges when multiple options are available (0 = none, 1 = always backward).")]
            [Range(0f, 1f)] public float backwardBias;

            public static AiDirectionSettings Default => new AiDirectionSettings
            {
                allowSideways = true,
                allowBackward = true,
                allowForward = false,
                backwardBias = 0.25f
            };
        }

        private enum DirectionSource
        {
            ActivationDirection,
            TowardsTarget,
            AwayFromTarget,
            TransformRight,
            Custom
        }

        [Header("Owner-Specific Direction")]
        [SerializeField]
        [Tooltip("When enabled, dash direction is resolved per owner type (player / AI) before falling back to Direction Source.")]
        private bool enableOwnerSpecificDirection = true;

        [SerializeField]
        [Tooltip("Direction logic used when the owner is player-controlled.")]
        private PlayerDirectionMode playerDirectionMode = PlayerDirectionMode.AimDirection;

        [SerializeField]
        [Tooltip("Use legacy PlayerDodge2D-style movement logic for player owners.")]
        private bool useLegacyPlayerDirection = true;

        [SerializeField]
        [Tooltip("Legacy dodge mapping applied when Use Legacy Player Direction is enabled.")]
        private PlayerDodgeMode legacyPlayerDodgeMode = PlayerDodgeMode.Sidestep;

        [SerializeField]
        [Tooltip("When the player uses Aim Direction, sample the active camera + mouse position when ability input provides no direction.")]
        private bool useMouseAimWhenPlayer = true;

        [SerializeField]
        [Tooltip("Fallback direction used when player input provides no direction.")]
        private Vector2 playerFallbackDirection = Vector2.right;

        [SerializeField]
        [Tooltip("Direction behaviour when the owner is AI-controlled.")]
        private AiDirectionSettings aiDirection = AiDirectionSettings.Default;

        [Header("General Direction")]
        [SerializeField]
        [Tooltip("Determines which vector is used to orient the dash when owner-specific logic is disabled or returns zero.")]
        private DirectionSource directionSource = DirectionSource.ActivationDirection;

        [SerializeField]
        [Tooltip("Custom direction used when Direction Source is set to Custom.")]
        private Vector2 customDirection = Vector2.right;

        [SerializeField]
        [Tooltip("Units travelled along the resolved direction.")]
        private float distance = 3f;

        [SerializeField]
        [Tooltip("Duration of the dash.")]
        private float duration = 0.2f;

        [SerializeField]
        [Tooltip("Curve applied to step movement over the duration.")]
        private AnimationCurve speedCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        [Header("Movement Options")]
        [SerializeField]
        [Tooltip("Move the Rigidbody2D when available instead of directly altering the transform.")]
        private bool useRigidbodyMove = true;

        [SerializeField]
        [Tooltip("Reset velocity once the dash finishes.")]
        private bool zeroVelocityAtEnd = true;

        [SerializeField]
        [Tooltip("Temporarily disables GenericTopDownController during the dash to avoid input conflicts.")]
        private bool pauseControllerDuringDash = true;

        [SerializeField]
        [Tooltip("Force Z to remain unchanged so the dash stays in 2D space.")]
        private bool flattenZAxis = true;

        [Header("Trail (Optional)")]
        [SerializeField]
        [Tooltip("Enable or disable dash trails.")]
        private bool enableTrail = true;

        [SerializeField]
        [Tooltip("Trail prefab instantiated under the owner. If null, a basic TrailRenderer is created.")]
        private TrailRenderer trailPrefab;

        [SerializeField]
        [Tooltip("Trail lifetime when using a runtime-created TrailRenderer.")]
        private float trailTime = 0.15f;

        [SerializeField]
        [Tooltip("Trail width at the start (runtime TrailRenderer only).")]
        private float trailStartWidth = 0.25f;

        [SerializeField]
        [Tooltip("Trail width at the end (runtime TrailRenderer only).")]
        private float trailEndWidth = 0f;

        [Header("Directional VFX (Optional)")]
        [SerializeField]
        [Tooltip("Prefab spawned and aligned to the dash direction when the dash starts.")]
        private GameObject directionalVfxPrefab;

        [SerializeField]
        [Tooltip("Offset applied when spawning the directional VFX (in world space).")]
        private Vector3 directionalVfxOffset = Vector3.zero;

        [SerializeField]
        [Tooltip("Seconds before the directional VFX is automatically destroyed (<= 0 keeps it alive).")]
        private float directionalVfxLifetime = 2f;

        [SerializeField]
        [Tooltip("Spawn the directional VFX when the dash begins.")]
        private bool spawnDirectionalVfxAtStart = true;

        [SerializeField]
        [Tooltip("Spawn the directional VFX after the dash completes.")]
        private bool spawnDirectionalVfxAtEnd = false;

        [SerializeField]
        [Tooltip("Uniform scale multiplier applied to the directional VFX instance.")]
        private float directionalVfxScale = 1f;

        TrailRenderer _spawnedTrail;
        bool _runtimeTrailCreated;
        GameObject _spawnedDirectionalVfx;

        [Header("Collisions")]
        [SerializeField]
        [Tooltip("Stop the dash when colliding with the specified layers.")]
        private bool stopOnCollision = false;

        [SerializeField]
        [Tooltip("Layers that block the dash when Stop On Collision is enabled.")]
        private LayerMask collisionMask = default;

        [SerializeField]
        [Tooltip("Radius used when probing for blocking colliders.")]
        private float collisionRadius = 0.2f;

        public override IEnumerator Execute(AbilityRuntimeContext context)
        {
            if (distance <= 0f) yield break;

            Vector2 direction = ResolveDirection(context);
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector2.right;
            }
            direction.Normalize();

            var controller = context.TopDownController;
            bool controllerDisabled = false;
            if (pauseControllerDuringDash && controller && controller.enabled)
            {
                controller.enabled = false;
                controllerDisabled = true;
            }

            TrailRenderer activeTrail = null;
            if (enableTrail)
            {
                activeTrail = SpawnOrReuseTrail(context);
                if (activeTrail)
                {
                    activeTrail.emitting = true;
                }
            }

            if (spawnDirectionalVfxAtStart)
            {
                bool trackReference = directionalVfxLifetime <= 0f;
                SpawnDirectionalVfx(context, direction, trackReference);
            }

            float totalDuration = Mathf.Max(0.01f, duration);
            float elapsed = 0f;
            float travelled = 0f;

            while (elapsed < totalDuration && travelled < distance)
            {
                if (context.CancelRequested || !context.Owner) break;

                float dt = Time.deltaTime;
                elapsed += dt;
                float t = Mathf.Clamp01(elapsed / totalDuration);
                float curveValue = speedCurve != null && speedCurve.length > 0 ? Mathf.Max(0f, speedCurve.Evaluate(t)) : 1f;
                float stepDistance = (distance / totalDuration) * dt * curveValue;
                float remaining = distance - travelled;
                float move = Mathf.Min(remaining, stepDistance);

                if (move > 0f)
                {
                    if (ApplyMovement(context, direction, move))
                    {
                        travelled += move;
                    }
                    else
                    {
                        break;
                    }
                }

                yield return null;
            }

            if (zeroVelocityAtEnd && context.Rigidbody2D)
            {
#if UNITY_2022_2_OR_NEWER
                context.Rigidbody2D.linearVelocity = Vector2.zero;
#else
                context.Rigidbody2D.velocity = Vector2.zero;
#endif
            }

            if (activeTrail)
            {
                activeTrail.emitting = false;
            }

            if (directionalVfxLifetime <= 0f)
            {
                CleanupDirectionalVfx();
            }

            if (spawnDirectionalVfxAtEnd)
            {
                SpawnDirectionalVfx(context, direction, trackReference: false);
            }

            if (controllerDisabled && controller)
            {
                controller.enabled = true;
            }
        }

        bool ApplyMovement(AbilityRuntimeContext context, Vector2 direction, float distanceStep)
        {
            Vector2 delta = direction * distanceStep;
            Vector3 startPosition = context.Transform.position;
            Vector3 target = startPosition + (Vector3)delta;
            if (flattenZAxis)
            {
                target.z = startPosition.z;
            }

            if (stopOnCollision && collisionMask.value != 0)
            {
                Vector2 origin = context.Rigidbody2D ? context.Rigidbody2D.position : (Vector2)context.Transform.position;
                RaycastHit2D hit = Physics2D.CircleCast(origin, collisionRadius, direction, distanceStep, collisionMask);
                if (hit.collider != null)
                {
                    return false;
                }
            }

            if (useRigidbodyMove && context.Rigidbody2D && context.Rigidbody2D.bodyType != RigidbodyType2D.Static)
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
                context.Transform.position = target;
            }

            return true;
        }

        Vector2 ResolveDirection(AbilityRuntimeContext context)
        {
            if (enableOwnerSpecificDirection)
            {
                Vector2 ownerDir = ResolveOwnerSpecificDirection(context);
                if (ownerDir.sqrMagnitude > 0.0001f)
                {
                    return ownerDir.normalized;
                }
            }

            switch (directionSource)
            {
                case DirectionSource.TowardsTarget:
                    if (context.Target)
                        return ((Vector2)context.Target.position - (Vector2)context.Transform.position).normalized;
                    break;
                case DirectionSource.AwayFromTarget:
                    if (context.Target)
                        return ((Vector2)context.Transform.position - (Vector2)context.Target.position).normalized;
                    break;
                case DirectionSource.TransformRight:
                    return context.Transform.right;
                case DirectionSource.Custom:
                    return customDirection;
            }

            if (context.DesiredDirection.sqrMagnitude > 0.0001f)
            {
                return context.DesiredDirection.normalized;
            }

            Vector2 aim = ComputeMouseForward(context);
            if (aim.sqrMagnitude > 0.0001f)
            {
                return aim;
            }

            return context.Transform.right;
        }

        Vector2 ResolveOwnerSpecificDirection(AbilityRuntimeContext context)
        {
            if (context.IsPlayerControlled)
            {
                if (useLegacyPlayerDirection)
                {
                    return ResolveLegacyPlayerDirection(context);
                }

                switch (playerDirectionMode)
                {
                    case PlayerDirectionMode.AimDirection:
                        {
                            Vector2 aim = context.DesiredDirection;
                            if (aim.sqrMagnitude < 0.0001f && useMouseAimWhenPlayer)
                            {
                                aim = ComputeMouseForward(context);
                            }

                            if (aim.sqrMagnitude < 0.0001f && context.Target)
                            {
                                aim = (Vector2)context.Target.position - (Vector2)context.Transform.position;
                            }

                            if (aim.sqrMagnitude > 0.0001f)
                            {
                                return aim.normalized;
                            }

                            break;
                        }
                    case PlayerDirectionMode.TowardsTarget:
                        if (context.Target)
                        {
                            Vector2 toTarget = (Vector2)context.Target.position - (Vector2)context.Transform.position;
                            if (toTarget.sqrMagnitude > 0.0001f) return toTarget.normalized;
                        }
                        break;
                    case PlayerDirectionMode.AwayFromTarget:
                        if (context.Target)
                        {
                            Vector2 away = (Vector2)context.Transform.position - (Vector2)context.Target.position;
                            if (away.sqrMagnitude > 0.0001f) return away.normalized;
                        }
                        break;
                    case PlayerDirectionMode.DesiredInput:
                    default:
                        if (context.DesiredDirection.sqrMagnitude > 0.0001f)
                        {
                            return context.DesiredDirection.normalized;
                        }
                        break;
                }

                if (playerFallbackDirection.sqrMagnitude < 0.0001f)
                {
                    playerFallbackDirection = Vector2.right;
                }
                return playerFallbackDirection.normalized;
            }

            if (context.IsEnemyControlled || context.IsNeutralControlled)
            {
                return ResolveAiDirection(context);
            }

            return Vector2.zero;
        }

        Vector2 ResolveLegacyPlayerDirection(AbilityRuntimeContext context)
        {
            Vector2 desired = context.DesiredDirection;

            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");

            bool pressForward = vertical > 0.25f || Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) || desired.y > 0.25f;
            bool pressBack = vertical < -0.25f || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) || desired.y < -0.25f;
            bool pressRight = horizontal > 0.25f || Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) || desired.x > 0.25f;
            bool pressLeft = horizontal < -0.25f || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) || desired.x < -0.25f;

            Vector2 dir = ComputeLegacyDodgeDirection(context, pressForward, pressLeft, pressBack, pressRight);

            if (dir.sqrMagnitude < 0.0001f && desired.sqrMagnitude > 0.0001f)
            {
                dir = desired.normalized;
            }

            if (dir.sqrMagnitude < 0.0001f)
            {
                Vector2 mouse = ComputeMouseForward(context);
                if (mouse.sqrMagnitude > 0.0001f)
                {
                    dir = mouse;
                }
            }

            if (dir.sqrMagnitude < 0.0001f && context.Target)
            {
                dir = (Vector2)context.Target.position - (Vector2)context.Transform.position;
            }

            if (dir.sqrMagnitude < 0.0001f)
            {
                dir = playerFallbackDirection;
            }

            return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
        }

        Vector2 ComputeLegacyDodgeDirection(AbilityRuntimeContext context, bool pressForward, bool pressLeft, bool pressBack, bool pressRight)
        {
            Vector2 forward = Vector2.right;
            GenericTopDownController controller = context.TopDownController;
            if (controller && controller.movementMode == GenericTopDownController.MovementMode.RelativeToMouse)
            {
                Vector2 mouseForward = ComputeMouseForward(context);
                if (mouseForward.sqrMagnitude > 0.0001f)
                {
                    forward = mouseForward;
                }
            }
            else if (context.Transform)
            {
                Vector2 facing = context.Transform.right;
                if (facing.sqrMagnitude > 0.0001f)
                {
                    forward = facing;
                }
            }

            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector2.right;
            }

            forward.Normalize();
            Vector2 right = new Vector2(forward.y, -forward.x);

            if (legacyPlayerDodgeMode == PlayerDodgeMode.Directional)
            {
                if (pressForward) return forward;
                if (pressLeft) return -right;
                if (pressBack) return -forward;
                if (pressRight) return right;
            }
            else
            {
                if (pressForward) return -right;
                if (pressLeft) return -forward;
                if (pressBack) return right;
                if (pressRight) return forward;
            }

            return Vector2.zero;
        }

        Vector2 ComputeMouseForward(AbilityRuntimeContext context)
        {
            if (!useMouseAimWhenPlayer) return Vector2.zero;

            Camera cam = Camera.main;
            if (cam)
            {
                Vector3 owner = context.Transform.position;
                Vector3 mouse = Input.mousePosition;
                // Set Z to the distance from camera to the owner's plane
                mouse.z = cam.WorldToScreenPoint(owner).z;
                Vector3 world = cam.ScreenToWorldPoint(mouse);
                world.z = owner.z;
                Vector2 forward = (Vector2)(world - owner);
                if (forward.sqrMagnitude > 0.0001f)
                {
                    return forward.normalized;
                }
            }

            return Vector2.zero;
        }

        Vector2 ResolveAiDirection(AbilityRuntimeContext context)
        {
            Vector2 baseDir;
            if (context.Target)
            {
                baseDir = (Vector2)(context.Transform.position - context.Target.position);
            }
            else
            {
                baseDir = context.Transform.right;
            }

            if (baseDir.sqrMagnitude < 0.0001f)
            {
                baseDir = Vector2.right;
            }
            baseDir.Normalize();

            Vector2[] candidates = new Vector2[4];
            int count = 0;

            if (aiDirection.allowBackward)
            {
                candidates[count++] = baseDir;
            }

            if (aiDirection.allowSideways)
            {
                Vector2 left = new Vector2(-baseDir.y, baseDir.x);
                Vector2 right = -left;
                if (left.sqrMagnitude > 0.0001f) candidates[count++] = left.normalized;
                if (right.sqrMagnitude > 0.0001f) candidates[count++] = right.normalized;
            }

            if (aiDirection.allowForward)
            {
                candidates[count++] = -baseDir;
            }

            if (count == 0)
            {
                return baseDir;
            }

            if (aiDirection.backwardBias > 0f && aiDirection.allowBackward)
            {
                if (Random.value < aiDirection.backwardBias)
                {
                    return baseDir;
                }
            }

            return candidates[Random.Range(0, count)];
        }

        TrailRenderer SpawnOrReuseTrail(AbilityRuntimeContext context)
        {
            if (!context.Transform)
            {
                return null;
            }

            if (_spawnedTrail == null)
            {
                if (trailPrefab)
                {
                    _spawnedTrail = Object.Instantiate(trailPrefab, context.Transform);
                }
                else
                {
                    _spawnedTrail = context.Transform.gameObject.AddComponent<TrailRenderer>();
                    _runtimeTrailCreated = true;
                    _spawnedTrail.time = trailTime;
                    _spawnedTrail.startWidth = trailStartWidth;
                    _spawnedTrail.endWidth = trailEndWidth;
                    _spawnedTrail.minVertexDistance = 0.01f;
                    _spawnedTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    _spawnedTrail.receiveShadows = false;
                    _spawnedTrail.numCornerVertices = 2;
                    _spawnedTrail.numCapVertices = 2;
                }
            }

            if (_runtimeTrailCreated)
            {
                _spawnedTrail.time = trailTime;
                _spawnedTrail.startWidth = trailStartWidth;
                _spawnedTrail.endWidth = trailEndWidth;
            }

            _spawnedTrail.transform.SetParent(context.Transform, worldPositionStays: true);
            return _spawnedTrail;
        }

        GameObject SpawnDirectionalVfx(AbilityRuntimeContext context, Vector2 direction, bool trackReference)
        {
            if (!directionalVfxPrefab || !context.Transform) return null;

            if (trackReference)
            {
                CleanupDirectionalVfx();
            }

            Vector3 offset = context.Transform.rotation * directionalVfxOffset;
            Vector3 spawnPos = context.Transform.position + offset;
            Quaternion rotation = direction.sqrMagnitude > 0.0001f
                ? Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg)
                : context.Transform.rotation;

            GameObject instance = Object.Instantiate(directionalVfxPrefab, spawnPos, rotation);

            if (!Mathf.Approximately(directionalVfxScale, 1f))
            {
                instance.transform.localScale *= directionalVfxScale;
            }

            if (directionalVfxLifetime > 0f)
            {
                Object.Destroy(instance, directionalVfxLifetime);
            }
            else if (trackReference)
            {
                _spawnedDirectionalVfx = instance;
            }

            return instance;
        }

        void CleanupDirectionalVfx()
        {
            if (_spawnedDirectionalVfx)
            {
                Object.Destroy(_spawnedDirectionalVfx);
                _spawnedDirectionalVfx = null;
            }
        }
    }
}






