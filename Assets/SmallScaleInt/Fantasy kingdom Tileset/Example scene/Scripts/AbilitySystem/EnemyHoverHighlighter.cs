using UnityEngine;
using UnityEngine.EventSystems;
using SmallScale.FantasyKingdomTileset;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    using Input = SmallScale.FantasyKingdomTileset.InputAdapter;
    /// <summary>
    /// Highlights enemies by tinting their combat indicator while the cursor hovers them.
    /// </summary>
    public sealed class EnemyHoverHighlighter : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Camera used to convert mouse position into world space. Defaults to the main camera.")]
        private Camera targetCamera;

        [SerializeField]
        [Tooltip("Color applied to the combat indicator while hovering an enemy.")]
        private Color hoverColor = new Color(0.35f, 1f, 0.35f, 0.9f);

        [SerializeField]
        [Min(0.1f)]
        [Tooltip("Scale multiplier applied to the combat indicator while hovering.")]
        private float hoverScaleMultiplier = 1.1f;

        [SerializeField]
        [Tooltip("Physics layers evaluated when searching for hovered enemies.")]
        private LayerMask hoverMask = ~0;

        [SerializeField]
        [Min(0.01f)]
        [Tooltip("Radius used when checking for hovered enemies around the cursor.")]
        private float hoverDetectionRadius = 0.3f;

        [SerializeField]
        [Min(1)]
        [Tooltip("Maximum number of colliders processed per frame while searching for hover targets.")]
        private int overlapBufferSize = 8;

        [SerializeField]
        [Tooltip("When enabled, hovering UI widgets does not highlight enemies.")]
        private bool ignorePointerOverUI = true;

        EnemyHealth2D _currentEnemy;
        Collider2D[] _overlapBuffer;

        void Awake()
        {
            EnsureBuffer();
            if (!targetCamera)
            {
                targetCamera = Camera.main;
            }
        }

        void OnValidate()
        {
            overlapBufferSize = Mathf.Max(1, overlapBufferSize);
            hoverScaleMultiplier = Mathf.Max(0.1f, hoverScaleMultiplier);
            hoverDetectionRadius = Mathf.Max(0.01f, hoverDetectionRadius);
            EnsureBuffer();
        }

        void OnDisable()
        {
            SetCurrentEnemy(null);
        }

        void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (!targetCamera)
            {
                targetCamera = Camera.main;
            }

            if (!targetCamera)
            {
                return;
            }

            if (ignorePointerOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                SetCurrentEnemy(null);
                return;
            }

            Vector3 mousePosition = Input.mousePosition;
            if (!targetCamera.orthographic)
            {
                mousePosition.z = Mathf.Abs(targetCamera.transform.position.z);
            }

            Vector3 world = targetCamera.ScreenToWorldPoint(mousePosition);
            Vector2 world2D = new Vector2(world.x, world.y);

            float radius = Mathf.Max(0.01f, hoverDetectionRadius);
            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(hoverMask);
            filter.SetDepth(float.NegativeInfinity, float.PositiveInfinity);
            int hitCount = Physics2D.OverlapCircle(world2D, radius, filter, _overlapBuffer);
            EnemyHealth2D bestEnemy = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D collider = _overlapBuffer[i];
                if (!collider)
                {
                    continue;
                }

                EnemyHealth2D enemy = collider.GetComponentInParent<EnemyHealth2D>();
                if (!enemy)
                {
                    continue;
                }

                float distance = ((Vector2)enemy.transform.position - world2D).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestEnemy = enemy;
                }
            }

            SetCurrentEnemy(bestEnemy);
        }

        void EnsureBuffer()
        {
            if (_overlapBuffer == null || _overlapBuffer.Length != overlapBufferSize)
            {
                _overlapBuffer = new Collider2D[overlapBufferSize];
            }
        }

        void SetCurrentEnemy(EnemyHealth2D enemy)
        {
            if (_currentEnemy == enemy)
            {
                return;
            }

            if (_currentEnemy != null)
            {
                _currentEnemy.SetHoverHighlight(false, hoverColor, hoverScaleMultiplier);
            }

            _currentEnemy = enemy;

            if (_currentEnemy != null)
            {
                _currentEnemy.SetHoverHighlight(true, hoverColor, hoverScaleMultiplier);
            }
        }
    }
}




