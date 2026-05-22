using System.Collections;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    using Input = SmallScale.FantasyKingdomTileset.InputAdapter;
    [System.Serializable]
    [AbilityComponentDescription("Spawns one or more prefabs relative to the caster or a target transform, with optional parenting and position offsets.")]
    [MovedFrom("AbilitySystem")]
    public sealed class SpawnPrefabStep : AbilityStep
    {
        private enum SpawnAnchor
        {
            Owner,
            Target,
            MousePosition
        }

        [SerializeField]
        [Tooltip("Prefab instantiated when the step executes.")]
        private GameObject prefab;

        [SerializeField]
        [Tooltip("Transform used as the spawn reference.")]
        private SpawnAnchor anchor = SpawnAnchor.Owner;

        [SerializeField]
        [Tooltip("Offset applied to the spawn position relative to the anchor.")]
        private Vector3 positionOffset = Vector3.zero;

        [SerializeField]
        [Tooltip("Parent the spawned prefab to the anchor after instantiation.")]
        private bool parentToAnchor = false;

        [SerializeField]
        [Tooltip("Optional cleanup delay. <= 0 leaves the spawned prefab alive.")]
        private float autoDestroyDelay = 2f;

        public override IEnumerator Execute(AbilityRuntimeContext context)
        {
            if (!prefab) yield break;

            Vector3 spawnPosition;
            Quaternion rotation;
            Transform reference = null;

            if (anchor == SpawnAnchor.MousePosition)
            {
                // Get mouse position in world space
                Camera cam = Camera.main;
                if (!cam)
                {
                    // Fallback to owner if no camera
                    reference = context.Transform;
                    if (!reference) yield break;
                    spawnPosition = reference.position + positionOffset;
                    rotation = reference.rotation;
                }
                else
                {
                    Vector3 mouseScreenPos = Input.mousePosition;
                    // Set Z to match the owner's distance from camera for proper world conversion
                    if (context.Transform)
                    {
                        mouseScreenPos.z = cam.WorldToScreenPoint(context.Transform.position).z;
                    }

                    Vector3 mouseWorldPos = cam.ScreenToWorldPoint(mouseScreenPos);

                    // Clamp to max range if WaitForTargetConfirmationStep set one
                    if (context.ConfirmedTargetMaxRange.HasValue && context.Transform)
                    {
                        Vector2 ownerPos = context.Transform.position;
                        Vector2 mousePos2D = new Vector2(mouseWorldPos.x, mouseWorldPos.y);
                        Vector2 direction = (mousePos2D - ownerPos);
                        float distance = direction.magnitude;

                        if (distance > context.ConfirmedTargetMaxRange.Value)
                        {
                            mousePos2D = ownerPos + direction.normalized * context.ConfirmedTargetMaxRange.Value;
                            mouseWorldPos = new Vector3(mousePos2D.x, mousePos2D.y, mouseWorldPos.z);
                        }
                    }

                    spawnPosition = new Vector3(mouseWorldPos.x, mouseWorldPos.y, mouseWorldPos.z) + positionOffset;
                    rotation = Quaternion.identity; // Default rotation for mouse position
                }
            }
            else
            {
                reference = anchor == SpawnAnchor.Owner ? context.Transform : context.Target;
                if (!reference) reference = context.Transform;
                if (!reference) yield break;

                spawnPosition = reference.position + positionOffset;
                rotation = reference.rotation;
            }

            GameObject instance = Object.Instantiate(prefab, spawnPosition, rotation);

            if (parentToAnchor && reference)
            {
                instance.transform.SetParent(reference);
            }

            if (autoDestroyDelay > 0f)
            {
                Object.Destroy(instance, autoDestroyDelay);
            }

            yield break;
        }
    }
}






