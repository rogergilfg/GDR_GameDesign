using UnityEngine;

namespace SmallScale.FantasyKingdomTileset.UI
{
    /// <summary>
    /// Simple start screen gate that hides gameplay UI and disables player controls until the user presses Start.
    /// </summary>
    [DisallowMultipleComponent]
    public class StartScreenController : MonoBehaviour
    {
        [Header("Start Screen")]
        [SerializeField]
        private CanvasGroup startOverlay;

        [SerializeField]
        [Tooltip("Optional elements (HUD, menus) that should stay hidden until the game starts.")]
        private GameObject[] uiRootsToHide;

        [Header("Gameplay Controls")]
        [SerializeField]
        [Tooltip("Any behaviours that should be disabled while the start overlay is visible (e.g., GenericTopDownController, AbilityHotkeyManager).")]
        private Behaviour[] behavioursToDisable;

        [Header("Settings Panel")]
        [SerializeField]
        private GameObject settingsPanel;

        [SerializeField]
        private SettingsPanelController settingsPanelController;

        [Header("Description Panel")]
        [SerializeField]
        private GameObject descriptionPanel;

        [Header("Quit Panel")]
        [SerializeField]
        private GameObject quitPanel;

        bool hasStarted;

        void Awake()
        {
            if (!startOverlay)
            {
                Debug.LogWarning("StartScreenController requires a CanvasGroup assigned to 'startOverlay'. Disabling controller.");
                enabled = false;
                return;
            }

            ApplyStartState(paused: true);
        }

        /// <summary>
        /// Called by the Start Game button.
        /// </summary>
        public void StartGame()
        {
            if (hasStarted)
                return;

            hasStarted = true;
            ApplyStartState(paused: false);
        }

        void ApplyStartState(bool paused)
        {
            SetOverlayVisible(paused);
            ToggleUIRoots(!paused);
            ToggleBehaviours(!paused);
            if (paused)
                ToggleSettingsPanel(false);
        }

        void SetOverlayVisible(bool visible)
        {
            startOverlay.alpha = visible ? 1f : 0f;
            startOverlay.interactable = visible;
            startOverlay.blocksRaycasts = visible;
            if (startOverlay.gameObject.activeSelf != visible)
                startOverlay.gameObject.SetActive(visible);
        }

        void ToggleUIRoots(bool visible)
        {
            if (uiRootsToHide == null)
                return;
            for (int i = 0; i < uiRootsToHide.Length; i++)
            {
                GameObject root = uiRootsToHide[i];
                if (!root) continue;
                if (root.activeSelf != visible)
                    root.SetActive(visible);
            }
        }

        void ToggleBehaviours(bool enable)
        {
            if (behavioursToDisable == null)
                return;
            for (int i = 0; i < behavioursToDisable.Length; i++)
            {
                Behaviour behaviour = behavioursToDisable[i];
                if (!behaviour) continue;
                behaviour.enabled = enable;
            }
        }

        public void ShowSettingsPanel()
        {
            ToggleSettingsPanel(true);
            settingsPanelController?.RefreshUI();
        }

        public void HideSettingsPanel()
        {
            ToggleSettingsPanel(false);
        }

        void ToggleSettingsPanel(bool visible)
        {
            if (!settingsPanel)
                return;
            if (settingsPanel.activeSelf != visible)
                settingsPanel.SetActive(visible);
            if (visible)
            {
                ToggleDescriptionPanel(false);
                ToggleQuitPanel(false);
            }
        }

        public void ShowDescriptionPanel()
        {
            ToggleDescriptionPanel(true);
        }

        public void HideDescriptionPanel()
        {
            ToggleDescriptionPanel(false);
        }

        void ToggleDescriptionPanel(bool visible)
        {
            if (!descriptionPanel)
                return;
            if (descriptionPanel.activeSelf != visible)
                descriptionPanel.SetActive(visible);
            if (visible)
            {
                ToggleSettingsPanel(false);
                ToggleQuitPanel(false);
            }
        }

        public void ShowQuitPanel()
        {
            ToggleQuitPanel(true);
        }

        public void HideQuitPanel()
        {
            ToggleQuitPanel(false);
        }

        void ToggleQuitPanel(bool visible)
        {
            if (!quitPanel)
                return;
            if (quitPanel.activeSelf != visible)
                quitPanel.SetActive(visible);
            if (visible)
            {
                ToggleSettingsPanel(false);
                ToggleDescriptionPanel(false);
            }
        }
    }
}



