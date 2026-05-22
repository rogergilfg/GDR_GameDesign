using UnityEngine;
using UnityEngine.UI;

namespace SkillSystem
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class AbilityDragPreview : MonoBehaviour
    {
        [SerializeField]
        private Image iconImage;

        [SerializeField]
        private CanvasGroup canvasGroup;

        [SerializeField]
        private float followLerp = 20f;

        Vector3 _targetPosition;
        bool _visible;

        void Reset()
        {
            if (!iconImage)
            {
                iconImage = GetComponentInChildren<Image>();
            }

            if (!canvasGroup)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
        }

        void Awake()
        {
            if (!canvasGroup)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            AbilityDragDropService.DragStarted += HandleDragStarted;
            AbilityDragDropService.DragEnded += HandleDragEnded;
            AbilityDragDropService.DragMoved += HandleDragMoved;

            UpdateVisibility(false);
        }

        void OnDestroy()
        {
            AbilityDragDropService.DragStarted -= HandleDragStarted;
            AbilityDragDropService.DragEnded -= HandleDragEnded;
            AbilityDragDropService.DragMoved -= HandleDragMoved;
        }

        void Update()
        {
            if (!_visible) return;

            Vector3 current = transform.position;
            Vector3 next = Vector3.Lerp(current, _targetPosition, Time.deltaTime * followLerp);
            transform.position = next;
        }

        void HandleDragStarted(SmallScale.FantasyKingdomTileset.AbilitySystem.AbilityDefinition ability, Sprite icon)
        {
            if (iconImage)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
            }

            UpdateVisibility(true);
        }

        void HandleDragMoved(Vector2 position)
        {
            _targetPosition = position;
            if (!_visible)
            {
                transform.position = position;
                UpdateVisibility(true);
            }
        }

        void HandleDragEnded()
        {
            UpdateVisibility(false);
            if (iconImage)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }
        }

        void UpdateVisibility(bool visible)
        {
            _visible = visible;
            if (canvasGroup)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.blocksRaycasts = false;
            }

            if (!visible)
            {
                _targetPosition = transform.position;
            }
        }
    }
}





