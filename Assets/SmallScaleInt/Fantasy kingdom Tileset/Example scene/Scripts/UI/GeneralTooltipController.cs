using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SmallScale.FantasyKingdomTileset.UI
{
    using Input = SmallScale.FantasyKingdomTileset.InputAdapter;
    /// <summary>
    /// Displays a lightweight tooltip panel that follows the mouse cursor.
    /// </summary>
    [DisallowMultipleComponent]
    public class GeneralTooltipController : MonoBehaviour
    {
        public static GeneralTooltipController Instance { get; private set; }

        [Header("References")]
        [SerializeField] private RectTransform tooltipRoot;
        [SerializeField] private TMP_Text headerText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Canvas canvas;

        [Header("Behavior")]
        [SerializeField] private Vector2 mouseOffset = new Vector2(24f, -24f);
        [SerializeField] private bool followMouseWhileHovered = true;
        [SerializeField] private bool clampToCanvas = true;
        [SerializeField] private Vector2 canvasPadding = new Vector2(16f, 16f);

        private RectTransform canvasRectTransform;
        private bool isVisible;
        private GeneralTooltipTrigger currentOwner;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("Multiple GeneralTooltipController instances detected. Destroying the newest one.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;
            InitializeReferences();
            HideImmediate();
        }

        private void LateUpdate()
        {
            if (isVisible && followMouseWhileHovered && currentOwner != null)
            {
                UpdateTooltipPosition(Input.mousePosition);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void InitializeReferences()
        {
            if (tooltipRoot == null)
            {
                tooltipRoot = GetComponent<RectTransform>();
            }

            if (canvas == null && tooltipRoot != null)
            {
                canvas = tooltipRoot.GetComponentInParent<Canvas>();
            }

            if (canvas != null)
            {
                canvasRectTransform = canvas.GetComponent<RectTransform>();
            }
        }

        public void Show(GeneralTooltipTrigger owner, string header, string description, Vector2 screenPosition)
        {
            if (tooltipRoot == null)
            {
                Debug.LogWarning("GeneralTooltipController has no tooltip root assigned.", this);
                return;
            }

            currentOwner = owner;
            isVisible = true;

            if (headerText != null)
            {
                headerText.text = header;
                headerText.gameObject.SetActive(!string.IsNullOrWhiteSpace(header));
            }

            if (descriptionText != null)
            {
                descriptionText.text = description;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRoot);

            tooltipRoot.gameObject.SetActive(true);
            UpdateTooltipPosition(screenPosition);
        }

        public void Hide(GeneralTooltipTrigger owner)
        {
            if (!isVisible)
            {
                return;
            }

            if (owner != null && owner != currentOwner)
            {
                return;
            }

            HideImmediate();
        }

        private void HideImmediate()
        {
            isVisible = false;
            currentOwner = null;

            if (tooltipRoot != null)
            {
                tooltipRoot.gameObject.SetActive(false);
            }
        }

        internal void NotifyTriggerDestroyed(GeneralTooltipTrigger trigger)
        {
            if (trigger == currentOwner)
            {
                HideImmediate();
            }
        }

        internal void RefreshPositionFromTrigger(Vector2 screenPosition)
        {
            if (isVisible && currentOwner != null)
            {
                UpdateTooltipPosition(screenPosition);
            }
        }

        private void UpdateTooltipPosition(Vector2 screenPosition)
        {
            if (tooltipRoot == null || canvasRectTransform == null)
            {
                return;
            }

            float scale = canvas != null ? canvas.scaleFactor : 1f;
            Vector2 targetScreen = screenPosition + (mouseOffset * scale);
            Camera canvasCamera = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                canvasCamera = canvas.worldCamera;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRectTransform, targetScreen, canvasCamera, out Vector2 anchoredPosition))
            {
                return;
            }

            if (clampToCanvas)
            {
                Vector2 halfCanvas = canvasRectTransform.rect.size * 0.5f;
                Vector2 halfTooltip = tooltipRoot.rect.size * 0.5f;

                Vector2 min = new Vector2(-halfCanvas.x + halfTooltip.x + canvasPadding.x, -halfCanvas.y + halfTooltip.y + canvasPadding.y);
                Vector2 max = new Vector2(halfCanvas.x - halfTooltip.x - canvasPadding.x, halfCanvas.y - halfTooltip.y - canvasPadding.y);

                anchoredPosition.x = Mathf.Clamp(anchoredPosition.x, min.x, max.x);
                anchoredPosition.y = Mathf.Clamp(anchoredPosition.y, min.y, max.y);
            }

            tooltipRoot.anchoredPosition = anchoredPosition;
        }
    }
}




