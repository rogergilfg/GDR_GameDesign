using UnityEngine;
using UnityEngine.UI;

namespace FantasyKingdoms.Minimap
{
    /// <summary>
    /// Lightweight UI helper that exposes cached references for minimap icon graphics.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class MinimapIconUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;

        private RectTransform rectTransform;
        private Sprite currentSprite;
        private Color currentColor = Color.white;
        private Vector2 currentSize;

        public RectTransform CachedRectTransform => rectTransform != null ? rectTransform : rectTransform = GetComponent<RectTransform>();

        public void SetAppearance(Sprite sprite, Color color, Vector2 size)
        {
            if (iconImage == null)
            {
                iconImage = GetComponentInChildren<Image>();
            }

            if (iconImage != null)
            {
                if (currentSprite != sprite)
                {
                    currentSprite = sprite;
                    iconImage.sprite = sprite;
                    iconImage.enabled = sprite != null;
                }

                if (currentColor != color)
                {
                    currentColor = color;
                    iconImage.color = color;
                }
            }

            if (currentSize != size)
            {
                currentSize = size;
                RectTransform rect = CachedRectTransform;
                rect.sizeDelta = size;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (iconImage == null)
            {
                iconImage = GetComponentInChildren<Image>();
            }

            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }

            currentSprite = iconImage != null ? iconImage.sprite : null;
            currentColor = iconImage != null ? iconImage.color : Color.white;
            currentSize = rectTransform != null ? rectTransform.sizeDelta : Vector2.zero;
        }
#endif
    }
}





