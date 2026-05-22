using UnityEngine;
using UnityEngine.EventSystems;

namespace SmallScale.FantasyKingdomTileset.UI
{
    using Input = SmallScale.FantasyKingdomTileset.InputAdapter;
    /// <summary>
    /// Attach to any UI element to display the shared general tooltip when the pointer hovers it.
    /// </summary>
    [DisallowMultipleComponent]
    public class GeneralTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        [Header("Content")]
        [SerializeField, Tooltip("Header text shown at the top of the tooltip.")]
        private string header = string.Empty;

        [SerializeField, TextArea, Tooltip("Description shown below the header.")]
        private string description = string.Empty;

        [Header("Behavior")]
        [SerializeField, Tooltip("Optional override for the tooltip controller. Leave empty to use the scene singleton.")]
        private GeneralTooltipController overrideController;

        private bool pointerInside;
        private GeneralTooltipController Controller => overrideController != null ? overrideController : GeneralTooltipController.Instance;

        private void OnDisable()
        {
            HideTooltip();
        }

        private void OnDestroy()
        {
            GeneralTooltipController.Instance?.NotifyTriggerDestroyed(this);
        }

        public void SetHeader(string value)
        {
            header = value ?? string.Empty;
            RefreshTooltip();
        }

        public void SetDescription(string value)
        {
            description = value ?? string.Empty;
            RefreshTooltip();
        }

        public void SetContent(string newHeader, string newDescription)
        {
            header = newHeader ?? string.Empty;
            description = newDescription ?? string.Empty;
            RefreshTooltip();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerInside = true;
            ShowTooltip(eventData.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerInside = false;
            HideTooltip();
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (!pointerInside)
            {
                return;
            }

            var controller = Controller;
            controller?.RefreshPositionFromTrigger(eventData.position);
        }

        private void ShowTooltip(Vector2 screenPosition)
        {
            var controller = Controller;
            if (controller == null)
            {
                Debug.LogWarning("No GeneralTooltipController available. Add one to the scene.", this);
                return;
            }

            controller.Show(this, header, description, screenPosition);
        }

        private void HideTooltip()
        {
            Controller?.Hide(this);
        }

        private void RefreshTooltip()
        {
            if (!pointerInside)
            {
                return;
            }

            Controller?.Show(this, header, description, Input.mousePosition);
        }
    }
}




