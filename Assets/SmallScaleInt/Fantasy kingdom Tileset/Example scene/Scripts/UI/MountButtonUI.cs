using UnityEngine;
using UnityEngine.UI;
using SmallScaleInc.CharacterCreatorFantasy;

namespace SmallScale.FantasyKingdomTileset
{
    /// <summary>
    /// Toggles visibility of the mount button based on the player's level and triggers mounting when clicked.
    /// </summary>
    [DisallowMultipleComponent]
    public class MountButtonUI : MonoBehaviour
    {
        [SerializeField] private GenericTopDownController controller;
        [SerializeField] private Button mountButton;

        private PlayerExperience experience;
        private bool subscribed;

        private void Awake()
        {
            if (controller == null)
            {
                controller = FindFirstObjectByType<GenericTopDownController>();
            }

            if (mountButton == null)
            {
                mountButton = GetComponent<Button>();
            }

            if (mountButton != null)
            {
                mountButton.onClick.AddListener(HandleMountButtonClicked);
            }

            CacheExperience();
        }

        private void OnEnable()
        {
            Subscribe();
            RefreshVisibility();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            if (mountButton != null)
            {
                mountButton.onClick.RemoveListener(HandleMountButtonClicked);
            }
            Unsubscribe();
        }

        private void Update()
        {
            if (controller == null)
            {
                controller = FindFirstObjectByType<GenericTopDownController>();
                RefreshVisibility();
            }

            if (experience == null)
            {
                CacheExperience();
                Subscribe();
                RefreshVisibility();
            }
        }

        private void CacheExperience()
        {
            if (experience == null)
            {
                experience = PlayerExperience.Instance;
            }
        }

        private void Subscribe()
        {
            if (subscribed || experience == null)
            {
                return;
            }

            experience.LevelChanged += HandleLevelChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            if (experience != null)
            {
                experience.LevelChanged -= HandleLevelChanged;
            }

            subscribed = false;
        }

        private void HandleLevelChanged(int level)
        {
            RefreshVisibility();
        }

        private void RefreshVisibility()
        {
            bool shouldShow = controller != null && controller.IsMountUnlocked;
            GameObject target = mountButton != null ? mountButton.gameObject : gameObject;
            if (target.activeSelf != shouldShow)
            {
                target.SetActive(shouldShow);
            }
        }

        private void HandleMountButtonClicked()
        {
            if (controller != null)
            {
                controller.TryToggleMount();
            }
        }
    }
}



