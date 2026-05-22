using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using System.Collections;
using System.Collections.Generic;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    using Input = SmallScale.FantasyKingdomTileset.InputAdapter;
    [System.Serializable]
    [AbilityComponentDescription("Teleports the caster to a target location with various modes: mouse position, random nearby, towards/away from target, or to a specific transform.")]
    [MovedFrom("AbilitySystem")]
    public sealed class TeleportStep : AbilityStep
    {
        public enum TeleportMode
        {
            [Tooltip("Teleport to the mouse cursor position (player-only).")]
            ToMousePosition,

            [Tooltip("Teleport to a random position within a specified radius.")]
            RandomNearby,

            [Tooltip("Teleport towards the current target.")]
            TowardsTarget,

            [Tooltip("Teleport away from the current target.")]
            AwayFromTarget,

            [Tooltip("Teleport to a specific transform's position.")]
            ToTransform,

            [Tooltip("Teleport to the current target's position.")]
            ToTargetPosition,

            [Tooltip("Teleport behind the current target.")]
            BehindTarget
        }

        [Header("Teleport Settings")]
        [SerializeField]
        [Tooltip("Determines where the caster will teleport to.")]
        private TeleportMode mode = TeleportMode.ToMousePosition;

        [SerializeField]
        [Tooltip("If true, requires line of sight to the teleport destination. Won't teleport through walls.")]
        private bool requireLineOfSight = true;

        [SerializeField]
        [Tooltip("If true, checks if the destination is walkable/valid before teleporting.")]
        private bool validateDestination = true;

        [SerializeField]
        [Tooltip("Layers to check for obstacles when validating teleport destination.")]
        private LayerMask obstacleLayers = -1;

        [Header("Mouse Position Fallback")]
        [SerializeField]
        [Tooltip("When the mouse destination is blocked, attempt to snap to the closest valid teleport spot instead of failing.")]
        private bool snapMousePositionToNearestValid = true;

        [SerializeField]
        [Tooltip("Maximum radius (in world units) to search when looking for a fallback destination around the clicked point.")]
        private float mouseFallbackSearchRadius = 3f;

        [SerializeField]
        [Tooltip("Distance between concentric rings when scanning for fallback positions.")]
        private float mouseFallbackRingSpacing = 0.35f;

        [SerializeField]
        [Tooltip("Number of samples tested per ring during the fallback search.")]
        private int mouseFallbackSamplesPerRing = 16;

        [Header("Distance Settings")]
        [SerializeField]
        [Tooltip("Maximum teleport distance. Teleport will fail if destination exceeds this.")]
        private float maxDistance = 10f;

        [SerializeField]
        [Tooltip("Minimum teleport distance. Useful to prevent teleporting too close.")]
        private float minDistance = 0f;

        [Header("Random Mode Settings")]
        [SerializeField]
        [Tooltip("Radius to search for random teleport positions (used in RandomNearby mode).")]
        private float randomRadius = 5f;

        [SerializeField]
        [Tooltip("Maximum number of attempts to find a valid random position.")]
        private int maxRandomAttempts = 10;

        [SerializeField]
        [Tooltip("If true, random position stays on the same Y coordinate as the caster.")]
        private bool keepYPosition = true;

        [Header("Target-Relative Settings")]
        [SerializeField]
        [Tooltip("Distance from target for TowardsTarget/AwayFromTarget/BehindTarget modes.")]
        private float targetDistance = 3f;

        [SerializeField]
        [Tooltip("For BehindTarget mode: angle offset in degrees (0 = directly behind, 90 = to the side).")]
        [Range(-180f, 180f)]
        private float behindAngleOffset = 0f;

        [Header("Transform Mode Settings")]
        [SerializeField]
        [Tooltip("Target transform to teleport to (used in ToTransform mode).")]
        private Transform targetTransform;

        [SerializeField]
        [Tooltip("Offset from the target transform position.")]
        private Vector3 transformOffset = Vector3.zero;

        [Header("Visual Effects")]
        [SerializeField]
        [Tooltip("Particle effect to spawn at the departure position.")]
        private GameObject departureEffectPrefab;

        [SerializeField]
        [Tooltip("Particle effect to spawn at the arrival position.")]
        private GameObject arrivalEffectPrefab;

        [SerializeField]
        [Tooltip("If true, makes the caster face the direction they teleported.")]
        private bool faceMovementDirection = true;

        [Header("Safety Settings")]
        [SerializeField]
        [Tooltip("Radius to check for valid ground at destination.")]
#pragma warning disable 0414
        private float groundCheckRadius = 0.5f;
#pragma warning restore 0414

        [SerializeField]
        [Tooltip("If true, fails teleport if destination is occupied by a collider.")]
        private bool checkDestinationOccupied = true;

        [SerializeField]
        [Tooltip("Radius to check if destination is occupied.")]
        private float occupiedCheckRadius = 0.3f;

        public override IEnumerator Execute(AbilityRuntimeContext context)
        {
            if (context == null || context.Transform == null)
            {
                Debug.LogWarning("[TeleportStep] Invalid context or transform.");
                yield break;
            }

            Vector3 startPosition = context.Transform.position;
            Vector3? destination = CalculateDestination(context, startPosition);

            if (!destination.HasValue)
            {
                Debug.LogWarning($"[TeleportStep] Failed to calculate valid destination for mode: {mode}");
                yield break;
            }

            Vector3 targetPosition = destination.Value;

            // Validate distance constraints
            float distance = Vector3.Distance(startPosition, targetPosition);
            if (distance > maxDistance)
            {
                Debug.LogWarning($"[TeleportStep] Destination too far: {distance} > {maxDistance}");
                yield break;
            }

            if (distance < minDistance)
            {
                Debug.LogWarning($"[TeleportStep] Destination too close: {distance} < {minDistance}");
                yield break;
            }

            // Check line of sight
            if (requireLineOfSight && !HasLineOfSight(startPosition, targetPosition))
            {
                Debug.LogWarning("[TeleportStep] No line of sight to destination.");
                yield break;
            }

            // Validate destination
            if (validateDestination && !IsValidDestination(targetPosition))
            {
                if (!TryResolveMouseFallback(startPosition, ref targetPosition))
                {
                    Debug.LogWarning("[TeleportStep] Destination is not valid.");
                    yield break;
                }

                // Re-run distance/line-of-sight checks for the fallback point
                distance = Vector3.Distance(startPosition, targetPosition);
                if (distance > maxDistance)
                {
                    Debug.LogWarning($"[TeleportStep] Fallback destination too far: {distance} > {maxDistance}");
                    yield break;
                }

                if (distance < minDistance)
                {
                    Debug.LogWarning($"[TeleportStep] Fallback destination too close: {distance} < {minDistance}");
                    yield break;
                }

                if (requireLineOfSight && !HasLineOfSight(startPosition, targetPosition))
                {
                    Debug.LogWarning("[TeleportStep] No line of sight to fallback destination.");
                    yield break;
                }

                if (validateDestination && !IsValidDestination(targetPosition))
                {
                    Debug.LogWarning("[TeleportStep] Fallback destination is still invalid.");
                    yield break;
                }
            }

            // Perform the teleport
            PerformTeleport(context, startPosition, targetPosition);
            yield break;
        }

        Vector3? CalculateDestination(AbilityRuntimeContext context, Vector3 startPosition)
        {
            switch (mode)
            {
                case TeleportMode.ToMousePosition:
                    return GetMouseWorldPosition(context);

                case TeleportMode.RandomNearby:
                    return FindRandomNearbyPosition(startPosition);

                case TeleportMode.TowardsTarget:
                    return GetPositionTowardsTarget(context, startPosition);

                case TeleportMode.AwayFromTarget:
                    return GetPositionAwayFromTarget(context, startPosition);

                case TeleportMode.ToTransform:
                    return GetTransformPosition();

                case TeleportMode.ToTargetPosition:
                    return GetTargetPosition(context);

                case TeleportMode.BehindTarget:
                    return GetPositionBehindTarget(context);

                default:
                    return null;
            }
        }

        Vector3? GetMouseWorldPosition(AbilityRuntimeContext context)
        {
            if (!context.IsPlayerControlled)
            {
                Debug.LogWarning("[TeleportStep] ToMousePosition mode only works for player-controlled entities.");
                return null;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[TeleportStep] No main camera found.");
                return null;
            }

            Vector3 mousePos = Input.mousePosition;
            Vector3 ownerPos = context.Transform.position;

            // Set Z to the distance from camera to the owner's plane
            mousePos.z = cam.WorldToScreenPoint(ownerPos).z;

            Vector3 worldPos = cam.ScreenToWorldPoint(mousePos);
            worldPos.z = ownerPos.z;

            // Clamp to max distance - find closest point within radius
            Vector3 toMouse = worldPos - ownerPos;
            float distanceToMouse = toMouse.magnitude;

            if (distanceToMouse > maxDistance)
            {
                // Clamp to max distance in the direction of the mouse
                Vector3 direction = toMouse.normalized;
                worldPos = ownerPos + direction * maxDistance;
            }

            return worldPos;
        }

        Vector3? FindRandomNearbyPosition(Vector3 center)
        {
            for (int i = 0; i < maxRandomAttempts; i++)
            {
                // Generate random point in circle
                Vector2 randomCircle = Random.insideUnitCircle * randomRadius;
                Vector3 randomPos = center + new Vector3(randomCircle.x, keepYPosition ? 0 : randomCircle.y, 0);

                if (!keepYPosition)
                {
                    // If not keeping Y, we already used it above
                }

                // Check if this position is valid
                if (!validateDestination || IsValidDestination(randomPos))
                {
                    return randomPos;
                }
            }

            Debug.LogWarning($"[TeleportStep] Failed to find valid random position after {maxRandomAttempts} attempts.");
            return null;
        }

        Vector3? GetPositionTowardsTarget(AbilityRuntimeContext context, Vector3 startPosition)
        {
            if (context.Target == null)
            {
                Debug.LogWarning("[TeleportStep] No target for TowardsTarget mode.");
                return null;
            }

            Vector3 targetPos = context.Target.position;
            Vector3 direction = (targetPos - startPosition).normalized;
            return startPosition + direction * targetDistance;
        }

        Vector3? GetPositionAwayFromTarget(AbilityRuntimeContext context, Vector3 startPosition)
        {
            if (context.Target == null)
            {
                Debug.LogWarning("[TeleportStep] No target for AwayFromTarget mode.");
                return null;
            }

            Vector3 targetPos = context.Target.position;
            Vector3 direction = (startPosition - targetPos).normalized;
            return startPosition + direction * targetDistance;
        }

        Vector3? GetTransformPosition()
        {
            if (targetTransform == null)
            {
                Debug.LogWarning("[TeleportStep] No target transform assigned for ToTransform mode.");
                return null;
            }

            return targetTransform.position + transformOffset;
        }

        Vector3? GetTargetPosition(AbilityRuntimeContext context)
        {
            if (context.Target == null)
            {
                Debug.LogWarning("[TeleportStep] No target for ToTargetPosition mode.");
                return null;
            }

            return context.Target.position;
        }

        Vector3? GetPositionBehindTarget(AbilityRuntimeContext context)
        {
            if (context.Target == null)
            {
                Debug.LogWarning("[TeleportStep] No target for BehindTarget mode.");
                return null;
            }

            Vector3 targetPos = context.Target.position;
            Vector3 ownerPos = context.Transform.position;

            // Get direction target is facing (assume it's moving towards owner)
            Vector3 targetFacing = (ownerPos - targetPos).normalized;

            // Rotate by the angle offset
            float angleRad = behindAngleOffset * Mathf.Deg2Rad;
            float cos = Mathf.Cos(angleRad);
            float sin = Mathf.Sin(angleRad);
            Vector3 rotatedDirection = new Vector3(
                targetFacing.x * cos - targetFacing.y * sin,
                targetFacing.x * sin + targetFacing.y * cos,
                targetFacing.z
            );

            // Position behind target
            Vector3 behindPos = targetPos - rotatedDirection * targetDistance;
            return behindPos;
        }

        bool HasLineOfSight(Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            float distance = direction.magnitude;

            RaycastHit2D hit = Physics2D.Raycast(from, direction.normalized, distance, obstacleLayers);
            return hit.collider == null;
        }

        bool IsValidDestination(Vector3 position)
        {
            // Check if destination is occupied
            if (checkDestinationOccupied)
            {
                Collider2D overlap = Physics2D.OverlapCircle(position, occupiedCheckRadius, obstacleLayers);
                if (overlap != null)
                {
                    return false;
                }
            }

            // Additional ground check could go here
            // For now, we just check for obstacles

            return true;
        }

        void PerformTeleport(AbilityRuntimeContext context, Vector3 from, Vector3 to)
        {
            // Spawn departure effect
            if (departureEffectPrefab != null)
            {
                GameObject.Instantiate(departureEffectPrefab, from, Quaternion.identity);
            }

            // Teleport the transform
            context.Transform.position = to;

            // Face movement direction (for sprites, don't rotate the transform to avoid flipping)
            if (faceMovementDirection)
            {
                Vector3 direction = (to - from).normalized;
                if (direction.sqrMagnitude > 0.01f)
                {
                    // For 2D sprites with TopDownController or GenericTopDownController
                    // We don't need to do anything - the controller handles facing direction
                    // based on movement input, not transform rotation

                    // For enemies or entities without a player controller
                    if (context.EnemyAI != null || context.NeutralAI != null)
                    {
                        // Don't rotate - the AI's animation system handles facing
                        // Rotating the transform can cause sprite flipping issues
                    }
                }
            }

            // Spawn arrival effect
            if (arrivalEffectPrefab != null)
            {
                GameObject.Instantiate(arrivalEffectPrefab, to, Quaternion.identity);
            }

            Debug.Log($"[TeleportStep] Teleported from {from} to {to} (distance: {Vector3.Distance(from, to):F2})");
        }

        bool TryResolveMouseFallback(Vector3 startPosition, ref Vector3 targetPosition)
        {
            if (mode != TeleportMode.ToMousePosition || !snapMousePositionToNearestValid)
            {
                return false;
            }

            Vector3? fallback = FindNearestValidMouseDestination(targetPosition, startPosition);
            if (fallback.HasValue)
            {
                targetPosition = fallback.Value;
                return true;
            }

            return false;
        }

        Vector3? FindNearestValidMouseDestination(Vector3 desiredPosition, Vector3 startPosition)
        {
            if (!validateDestination)
            {
                return null;
            }

            float searchRadius = Mathf.Max(0f, mouseFallbackSearchRadius);
            if (searchRadius <= 0.01f)
            {
                return null;
            }

            float ringSpacing = Mathf.Max(0.05f, mouseFallbackRingSpacing);
            int samplesPerRing = Mathf.Max(6, mouseFallbackSamplesPerRing);
            int ringCount = Mathf.CeilToInt(searchRadius / ringSpacing);

            float bestDistanceToDesired = float.MaxValue;
            Vector3 bestPosition = desiredPosition;
            bool found = false;

            for (int ring = 1; ring <= ringCount; ring++)
            {
                float radius = ring * ringSpacing;
                for (int sample = 0; sample < samplesPerRing; sample++)
                {
                    float angle = (Mathf.PI * 2f * sample) / samplesPerRing;
                    Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
                    Vector3 candidate = desiredPosition + offset;

                    float distanceFromStart = Vector3.Distance(startPosition, candidate);
                    if (distanceFromStart > maxDistance || distanceFromStart < minDistance)
                    {
                        continue;
                    }

                    if (requireLineOfSight && !HasLineOfSight(startPosition, candidate))
                    {
                        continue;
                    }

                    if (!IsValidDestination(candidate))
                    {
                        continue;
                    }

                    float distanceToDesired = Vector3.Distance(candidate, desiredPosition);
                    if (distanceToDesired < bestDistanceToDesired)
                    {
                        bestDistanceToDesired = distanceToDesired;
                        bestPosition = candidate;
                        found = true;
                    }
                }
            }

            return found ? bestPosition : (Vector3?)null;
        }
    }
}







