using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SkillSystem
{
    /// <summary>
    /// Simple helper that displays a skill tree option and notifies when the user selects it.
    /// </summary>
    public sealed class SkillTreeSelectionButton : MonoBehaviour
    {
        [SerializeField]
        private Button button;

        [SerializeField]
        private Image iconImage;

        [SerializeField]
        private TextMeshProUGUI nameLabel;

        [SerializeField]
        private TextMeshProUGUI descriptionLabel;

        SkillTreeDefinition _tree;
        Action<SkillTreeDefinition> _onSelected;

        public void Initialize(SkillTreeDefinition tree, Action<SkillTreeDefinition> onSelected)
        {
            _tree = tree;
            _onSelected = onSelected;

            if (nameLabel)
            {
                nameLabel.text = tree ? tree.DisplayName : string.Empty;
            }

            if (descriptionLabel)
            {
                descriptionLabel.text = tree ? tree.Description : string.Empty;
            }

            if (iconImage)
            {
                if (tree != null && tree.Icon != null)
                {
                    iconImage.enabled = true;
                    iconImage.sprite = tree.Icon;
                }
                else
                {
                    iconImage.sprite = tree ? tree.Icon : null;
                    iconImage.enabled = tree != null && tree.Icon != null;
                }
            }

            if (button)
            {
                button.onClick.RemoveListener(HandleClicked);
                button.onClick.AddListener(HandleClicked);
            }
        }

        void OnDestroy()
        {
            if (button)
            {
                button.onClick.RemoveListener(HandleClicked);
            }
        }

        void HandleClicked()
        {
            if (_tree != null)
            {
                _onSelected?.Invoke(_tree);
            }
        }
    }
}




