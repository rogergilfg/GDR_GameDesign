using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Input = SmallScale.FantasyKingdomTileset.InputAdapter;

namespace SmallScale.FantasyKingdomTileset
{
    /// <summary>
    /// Simple debug helper that lets designers click tiles to destroy them via TileDestructionManager.
    /// Use <see cref="SetEnabled"/> to toggle it on/off at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public class TileClickDestroyer : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField, Min(1), Tooltip("Tile damage dealt per click.")]
        private int tileDamage = 100;
        [SerializeField, Min(0f), Tooltip("Radius in tiles around the clicked position to destroy. 0 = single tile, 50 = 50-tile radius.")]
        private float tileRadius = 0f;
        [SerializeField, Tooltip("Optional modifier key that must be held while clicking. Leave as None to ignore.")]
        private KeyCode requiredModifier = KeyCode.None;
        [SerializeField, Tooltip("Optional modifier key that, if held, cancels destruction (e.g., Alt). Leave as None to ignore.")]
        private KeyCode cancelModifier = KeyCode.LeftAlt;
        [SerializeField, Tooltip("Camera used to convert mouse position to world position. Defaults to Camera.main.")]
        private Camera overrideCamera;
        [SerializeField, Tooltip("If true, forces the sampled world position's Z component to this plane value.")]
        private bool lockZPlane = true;
        [SerializeField, Tooltip("Z value assigned when lockZPlane is true.")]
        private float lockedZValue = 0f;

        const int PropBufferSize = 128;
        static readonly Collider2D[] s_propBuffer = new Collider2D[PropBufferSize];
        static readonly HashSet<Interactable> s_interactableScratch = new HashSet<Interactable>();
        static readonly HashSet<DestructibleProp2D> s_propScratch = new HashSet<DestructibleProp2D>();

        bool _enabled;

        void Awake()
        {
            if (!overrideCamera)
            {
                overrideCamera = Camera.main;
            }
        }

        void Update()
        {
            if (!_enabled || !TileDestructionManager.I)
                return;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            if (cancelModifier != KeyCode.None && Input.GetKey(cancelModifier))
                return;

            if (!Input.GetMouseButtonDown(0))
                return;

            if (requiredModifier != KeyCode.None && !Input.GetKey(requiredModifier))
                return;

            var cam = overrideCamera ? overrideCamera : Camera.main;
            if (!cam)
                return;

            Vector3 world = cam.ScreenToWorldPoint(Input.mousePosition);
            if (lockZPlane)
                world.z = lockedZValue;

            int damage = Mathf.Max(1, tileDamage);
            float radiusRaw = Mathf.Max(0f, tileRadius);

            const int maxPasses = 4;
            int totalHits = 0;

            if (radiusRaw <= 0.01f)
            {
                // Radius ~0: behave like single-tile hits, but run multiple passes so we
                // also walk down through stacked tilemaps (walls -> roofs) at that cell.
                for (int i = 0; i < maxPasses; i++)
                {
                    if (!TileDestructionManager.TryHitAtWorld(world, damage))
                        break;
                    totalHits++;
                }
            }
            else
            {
                // Radius > 0: circle hits, repeated a few times to walk down through stacked tilemaps.
                float radius = radiusRaw;
                for (int i = 0; i < maxPasses; i++)
                {
                    int tiles = TileDestructionManager.HitCircle(world, radius, damage);
                    if (tiles <= 0)
                        break;

                    totalHits += tiles;
                }
            }

            if (totalHits <= 0)
            {
                Debug.LogWarning("TileClickDestroyer: No tiles were destroyed at the clicked position.", this);
            }

            DestroyPropsAt(world, radiusRaw);
        }

        /// <summary>
        /// Enables or disables click handling.
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
        }

        /// <summary>
        /// Adjusts the tile destruction radius from code (e.g. debug slider).
        /// </summary>
        public void SetRadius(float radiusInTiles)
        {
            tileRadius = Mathf.Max(0f, radiusInTiles);
        }

        void DestroyPropsAt(Vector3 center, float radiusInTiles)
        {
            float effectiveRadius = radiusInTiles <= 0.01f ? 0.55f : Mathf.Max(0.55f, radiusInTiles);
            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(~0);
            filter.SetDepth(float.NegativeInfinity, float.PositiveInfinity);
            int hitCount = Physics2D.OverlapCircle(center, effectiveRadius, filter, s_propBuffer);
            if (hitCount <= 0)
            {
                return;
            }

            s_interactableScratch.Clear();
            s_propScratch.Clear();

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D col = s_propBuffer[i];
                if (!col)
                {
                    continue;
                }

                var interactable = col.GetComponentInParent<Interactable>();
                if (interactable && s_interactableScratch.Add(interactable))
                {
                    DestroyInteractable(interactable);
                }

                var prop = col.GetComponentInParent<DestructibleProp2D>();
                if (prop && s_propScratch.Add(prop))
                {
                    prop.ForceDestroy();
                }
            }
        }

        void DestroyInteractable(Interactable interactable)
        {
            if (!interactable)
            {
                return;
            }

            var prop = interactable.GetComponent<DestructibleProp2D>();
            if (prop)
            {
                prop.ForceDestroy();
                return;
            }

            Destroy(interactable.gameObject);
        }
    }
}




