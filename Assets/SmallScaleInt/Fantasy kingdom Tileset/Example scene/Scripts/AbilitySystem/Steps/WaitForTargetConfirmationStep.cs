using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using SmallScale.FantasyKingdomTileset;
using Input = SmallScale.FantasyKingdomTileset.InputAdapter;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [System.Serializable]
    [AbilityComponentDescription("Requires player to confirm target position with left mouse click before continuing. Shows a preview cursor while waiting. Player-only step.")]
    [MovedFrom("AbilitySystem")]
    public sealed class WaitForTargetConfirmationStep : AbilityStep
    {
        [Header("Visual Feedback")]
        [SerializeField]
        [Tooltip("Optional cursor prefab shown at mouse position while waiting for confirmation.")]
        private GameObject cursorPrefab;

        [SerializeField]
        [Tooltip("Optional preview effect shown at the current mouse position.")]
        private GameObject previewPrefab;

        [SerializeField]
        [Tooltip("Radius of the preview indicator (if using circular preview).")]
        private float previewRadius = 2f;

        [SerializeField]
        [Tooltip("Color tint applied to the preview sprite renderer (if any).")]
        private Color previewColor = new Color(1f, 1f, 1f, 0.5f);

        [SerializeField]
        [Tooltip("Highlight enemies within the preview radius using the combat indicator color while aiming.")]
        private bool highlightEnemiesInPreview = false;

        [SerializeField]
        [Tooltip("Physics layers evaluated when searching for preview highlight targets.")]
        private LayerMask previewHighlightMask = ~0;

        [SerializeField]
        [Tooltip("Color applied to combat indicators while preview highlighting is active.")]
        private Color previewHighlightColor = new Color(0.35f, 1f, 0.35f, 0.9f);

        [SerializeField]
        [Min(0.1f)]
        [Tooltip("Scale multiplier applied to combat indicators while preview highlighting.")]
        private float previewHighlightScale = 1.1f;

        [SerializeField]
        [Min(1)]
        [Tooltip("Maximum number of colliders processed when searching for preview highlight targets each frame.")]
        private int previewHighlightBufferSize = 24;

        [Header("Targeting")]
        [SerializeField]
        [Tooltip("Maximum distance from caster that the target can be confirmed. 0 = unlimited.")]
        private float maxRange = 0f;

        [SerializeField]
        [Tooltip("Show range indicator at maximum distance.")]
        private bool showRangeIndicator = false;

        [SerializeField]
        [Tooltip("Optional range indicator prefab (circle or ring at max range).")]
        private GameObject rangeIndicatorPrefab;

        [Header("Cancellation")]
        [SerializeField]
        [Tooltip("Allow right mouse button or Escape key to cancel targeting.")]
        private bool allowCancel = true;

        [SerializeField]
        [Tooltip("Audio clip played when target is confirmed.")]
        private AudioClip confirmSound;

        [SerializeField]
        [Tooltip("Audio clip played when targeting is cancelled.")]
        private AudioClip cancelSound;

        [Header("Blocked Placement")]
        [SerializeField]
        [Tooltip("Physics layers that block confirmation (e.g., World). Leave empty to allow everything.")]
        private LayerMask blockedLayers = 0;

        [SerializeField]
        [Tooltip("Radius used when checking for blocked confirmation positions.")]
        private float blockedCheckRadius = 0.2f;

        [SerializeField]
        [Tooltip("Preview color used when the hovered position is blocked.")]
        private Color invalidPreviewColor = new Color(1f, 0.35f, 0.35f, 0.6f);

        [SerializeField]
        [Tooltip("Combat text displayed when the player confirms an invalid position.")]
        private string invalidPositionMessage = "Invalid Position";

        public override IEnumerator Execute(AbilityRuntimeContext context)
        {
            // Only works for player-controlled entities
            if (!context.IsPlayerControlled)
            {
                yield break;
            }

            // Set the max range in context so subsequent steps can use it
            if (maxRange > 0f)
            {
                context.ConfirmedTargetMaxRange = maxRange;
            }

            Camera cam = Camera.main;
            if (!cam)
            {
                Debug.LogWarning("[WaitForTargetConfirmationStep] No main camera found. Skipping confirmation step.");
                yield break;
            }

            GameObject cursorInstance = null;
            GameObject previewInstance = null;
            GameObject rangeIndicatorInstance = null;
            SpriteRenderer previewSpriteRenderer = null;
            Color previewOriginalColor = previewColor;

            // Spawn cursor preview
            if (cursorPrefab)
            {
                cursorInstance = Object.Instantiate(cursorPrefab);
                cursorInstance.SetActive(false); // Will activate when we position it
            }

            // Spawn preview effect
            if (previewPrefab)
            {
                previewInstance = Object.Instantiate(previewPrefab);

                // Apply preview color tint if there's a SpriteRenderer
                previewSpriteRenderer = previewInstance.GetComponent<SpriteRenderer>();
                if (previewSpriteRenderer)
                {
                    previewSpriteRenderer.color = previewColor;
                    previewOriginalColor = previewSpriteRenderer.color;
                }

                // Scale to preview radius if it's a circular indicator
                if (previewRadius > 0f)
                {
                    previewInstance.transform.localScale = Vector3.one * previewRadius * 2f;
                }

                previewInstance.SetActive(false);
            }

            // Spawn range indicator
            if (showRangeIndicator && maxRange > 0f && rangeIndicatorPrefab)
            {
                rangeIndicatorInstance = Object.Instantiate(rangeIndicatorPrefab, context.Transform.position, Quaternion.identity);
                rangeIndicatorInstance.transform.localScale = Vector3.one * maxRange * 2f;
            }

            bool canHighlightPreview = highlightEnemiesInPreview && previewRadius > 0f;
            List<EnemyHealth2D> previewHighlightedEnemies = null;
            HashSet<EnemyHealth2D> previewHighlightedLookup = null;
            HashSet<EnemyHealth2D> previewHighlightScratch = null;
            Collider2D[] previewHighlightBuffer = null;
            if (canHighlightPreview)
            {
                previewHighlightedEnemies = new List<EnemyHealth2D>();
                previewHighlightedLookup = new HashSet<EnemyHealth2D>();
                previewHighlightScratch = new HashSet<EnemyHealth2D>();
                previewHighlightBuffer = new Collider2D[Mathf.Max(1, previewHighlightBufferSize)];
            }

            bool confirmed = false;
            bool cancelled = false;
            Vector3 confirmedWorldPosition = Vector3.zero;

            // Wait for confirmation or cancellation
            while (!confirmed && !cancelled)
            {
                if (context.CancelRequested || !context.Owner)
                {
                    cancelled = true;
                    break;
                }

                // Get mouse world position
                Vector3 mouseScreenPos = Input.mousePosition;
                mouseScreenPos.z = cam.WorldToScreenPoint(context.Transform.position).z;
                Vector3 mouseWorldPos = cam.ScreenToWorldPoint(mouseScreenPos);
                Vector2 mouseWorldPos2D = new Vector2(mouseWorldPos.x, mouseWorldPos.y);
                bool blocked = IsBlocked(mouseWorldPos2D);

                // Clamp to max range if specified
                Vector2 ownerPos = context.Transform.position;
                if (maxRange > 0f)
                {
                    Vector2 direction = (mouseWorldPos2D - ownerPos);
                    float distance = direction.magnitude;

                    if (distance > maxRange)
                    {
                        mouseWorldPos2D = ownerPos + direction.normalized * maxRange;
                        mouseWorldPos = new Vector3(mouseWorldPos2D.x, mouseWorldPos2D.y, mouseWorldPos.z);
                    }
                }

                if (canHighlightPreview)
                {
                    UpdatePreviewHighlights(mouseWorldPos2D, previewHighlightedEnemies, previewHighlightedLookup, previewHighlightBuffer, previewHighlightScratch);
                }

                // Update cursor position
                if (cursorInstance)
                {
                    cursorInstance.transform.position = mouseWorldPos;
                    if (!cursorInstance.activeSelf)
                    {
                        cursorInstance.SetActive(true);
                    }
                }

                // Update preview position
                if (previewInstance)
                {
                    previewInstance.transform.position = mouseWorldPos;
                    if (!previewInstance.activeSelf)
                    {
                        previewInstance.SetActive(true);
                    }

                    if (previewSpriteRenderer)
                    {
                        previewSpriteRenderer.color = blocked ? invalidPreviewColor : previewOriginalColor;
                    }
                }

                // Update range indicator position (follows caster)
                if (rangeIndicatorInstance)
                {
                    rangeIndicatorInstance.transform.position = context.Transform.position;
                }

                // Check for confirmation (left mouse button)
                if (Input.GetMouseButtonDown(0))
                {
                    if (blocked)
                    {
                        cancelled = true;
                        if (!string.IsNullOrEmpty(invalidPositionMessage) && CombatTextManager.Instance)
                        {
                            CombatTextManager.Instance.SpawnStatus(invalidPositionMessage, mouseWorldPos);
                        }
                        if (cancelSound)
                        {
                            AudioSource.PlayClipAtPoint(cancelSound, mouseWorldPos);
                        }
                        break;
                    }

                    confirmed = true;
                    confirmedWorldPosition = mouseWorldPos;

                    // Play confirm sound
                    if (confirmSound)
                    {
                        AudioSource.PlayClipAtPoint(confirmSound, mouseWorldPos);
                    }
                }

                // Check for cancellation (right mouse button or escape)
                if (allowCancel && (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)))
                {
                    cancelled = true;

                    // Play cancel sound
                    if (cancelSound)
                    {
                        AudioSource.PlayClipAtPoint(cancelSound, context.Transform.position);
                    }
                }

                yield return null;
            }

            // Cleanup visual elements
            if (cursorInstance)
            {
                Object.Destroy(cursorInstance);
            }

            if (previewInstance)
            {
                Object.Destroy(previewInstance);
            }

            if (rangeIndicatorInstance)
            {
                Object.Destroy(rangeIndicatorInstance);
            }

            if (canHighlightPreview)
            {
                ClearPreviewHighlights(previewHighlightedEnemies, previewHighlightedLookup);
            }

            // If cancelled, request context cancellation to stop remaining steps
            if (cancelled)
            {
                context.RequestCancel();
                yield break;
            }

            if (confirmed)
            {
                context.ConfirmedTargetPosition = confirmedWorldPosition;
            }

            yield break;
        }

        void UpdatePreviewHighlights(Vector2 center, List<EnemyHealth2D> activeList, HashSet<EnemyHealth2D> activeLookup, Collider2D[] buffer, HashSet<EnemyHealth2D> scratch)
        {
            if (activeList == null || activeLookup == null || buffer == null || scratch == null)
            {
                return;
            }

            scratch.Clear();
            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(previewHighlightMask);
            filter.SetDepth(float.NegativeInfinity, float.PositiveInfinity);
            int hitCount = Physics2D.OverlapCircle(center, previewRadius, filter, buffer);
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D collider = buffer[i];
                if (!collider)
                {
                    continue;
                }

                EnemyHealth2D enemy = collider.GetComponentInParent<EnemyHealth2D>();
                if (!enemy)
                {
                    continue;
                }

                if (!scratch.Add(enemy))
                {
                    continue;
                }

                if (activeLookup.Add(enemy))
                {
                    activeList.Add(enemy);
                    enemy.SetPreviewHighlight(true, previewHighlightColor, previewHighlightScale);
                }
            }

            for (int i = activeList.Count - 1; i >= 0; i--)
            {
                EnemyHealth2D enemy = activeList[i];
                if (!enemy || !scratch.Contains(enemy))
                {
                    if (enemy)
                    {
                        enemy.SetPreviewHighlight(false, previewHighlightColor, previewHighlightScale);
                    }
                    activeLookup.Remove(enemy);
                    activeList.RemoveAt(i);
                }
            }

            scratch.Clear();
        }

        void ClearPreviewHighlights(List<EnemyHealth2D> activeList, HashSet<EnemyHealth2D> activeLookup)
        {
            if (activeList == null)
            {
                return;
            }

            for (int i = 0; i < activeList.Count; i++)
            {
                EnemyHealth2D enemy = activeList[i];
                if (enemy)
                {
                    enemy.SetPreviewHighlight(false, previewHighlightColor, previewHighlightScale);
                }
            }

            activeList.Clear();
            activeLookup?.Clear();
        }

        bool IsBlocked(Vector2 position)
        {
            if (blockedLayers == 0)
                return false;

            if (blockedCheckRadius <= 0f)
            {
                Collider2D hit = Physics2D.OverlapPoint(position, blockedLayers);
                return hit != null;
            }

            return Physics2D.OverlapCircle(position, blockedCheckRadius, blockedLayers) != null;
        }
    }
}







