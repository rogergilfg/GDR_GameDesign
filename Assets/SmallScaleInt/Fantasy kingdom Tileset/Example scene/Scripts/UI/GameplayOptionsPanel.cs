using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using SkillSystem;
using SmallScaleInc.TopDownPixelCharactersPack1;
using TMPro;

namespace SmallScale.FantasyKingdomTileset
{
    /// <summary>
    /// Simple controller for gameplay options toggles.
    /// - Floating combat text (FCT) on/off
    /// - Show/hide a set of UI roots (drag into list)
    /// </summary>
    [DisallowMultipleComponent]
    public class GameplayOptionsPanel : MonoBehaviour
    {
        [Header("Toggles")]
        [SerializeField] private Toggle floatingCombatTextToggle;
        [SerializeField] private Toggle showUiToggle;
        [SerializeField, Tooltip("Toggle that shows/hides the options panel.")]
        private Toggle showOptionsPanelToggle;

        [Header("UI Roots To Toggle")] 
        [Tooltip("Assign top-level UI GameObjects you want to show/hide together when 'Show UI' is toggled.")]
        [SerializeField] private List<GameObject> uiRoots = new List<GameObject>();

        [Tooltip("Root object of this options panel to show/hide. Defaults to this GameObject if not set.")]
        [SerializeField] private GameObject optionsPanelRoot;

        [Header("Behavior")]
        [Tooltip("If enabled, the original active state of each UI root is remembered and restored when re-enabling UI.")]
        [SerializeField] private bool rememberOriginalStates = true;

        [Header("Photo Mode (Debug)")]
        [SerializeField, Tooltip("Toggle that enables/disables the free photo camera.")]
        private Toggle photoModeToggle;
        [SerializeField, Tooltip("Slider that drives the free camera move speed.")]
        private Slider photoModeSpeedSlider;
        [SerializeField, Tooltip("Optional label to print the current photo mode speed value.")]
        private TMP_Text photoModeSpeedLabel;
        [SerializeField, Tooltip("SmoothCameraFollow instance that actually performs the free fly movement. Defaults to Camera.main if left empty.")]
        private SmoothCameraFollow cameraFollow;

        [Header("Zoom (Debug)")]
        [SerializeField, Tooltip("When enabled, extends the camera zoom range to allow much further zooming out.")]
        private Toggle extendZoomToggle;
        [SerializeField, Tooltip("Maximum orthographic size used when 'Extend Zoom' is enabled.")]
        private float extendedMaxZoom = 15f;

        [Header("Tile Debug Tools")]
        [SerializeField, Tooltip("Toggle that enables click-to-destroy for tiles (debug helper).")]
        private Toggle clickToDestroyTileToggle;
        [SerializeField, Tooltip("Slider that controls the radius (in tiles) used when click-to-destroy is enabled.")]
        private Slider clickToDestroyRadiusSlider;
        [SerializeField, Tooltip("Optional label that displays the current click-to-destroy radius.")]
        private TMP_Text clickToDestroyRadiusLabel;
        [SerializeField, Tooltip("Component that performs the tile destruction when the toggle is active.")]
        private TileClickDestroyer clickToDestroyTileTool;

        private readonly Dictionary<GameObject, bool> _originalActive = new Dictionary<GameObject, bool>();
        private bool _initialized;
        private Vector2 _originalZoomLimits;
        private bool _zoomLimitsCaptured;

        private void Awake()
        {
            EnsureCameraReference();
            CacheOriginalStates();
            ApplyInitialToggleValues();
            _initialized = true;
        }

        private void OnEnable()
        {
            if (floatingCombatTextToggle)
                floatingCombatTextToggle.onValueChanged.AddListener(OnFctToggleChanged);
            if (showUiToggle)
                showUiToggle.onValueChanged.AddListener(OnShowUiToggleChanged);
            if (showOptionsPanelToggle)
                showOptionsPanelToggle.onValueChanged.AddListener(OnShowOptionsPanelToggleChanged);
            if (photoModeToggle)
                photoModeToggle.onValueChanged.AddListener(OnPhotoModeToggleChanged);
            if (photoModeSpeedSlider)
                photoModeSpeedSlider.onValueChanged.AddListener(OnPhotoModeSpeedChanged);
            if (extendZoomToggle)
                extendZoomToggle.onValueChanged.AddListener(OnExtendZoomToggleChanged);
            if (clickToDestroyTileToggle)
                clickToDestroyTileToggle.onValueChanged.AddListener(OnClickDestroyTileToggleChanged);
            if (clickToDestroyRadiusSlider)
                clickToDestroyRadiusSlider.onValueChanged.AddListener(OnClickDestroyRadiusChanged);
        }

        private void OnDisable()
        {
            if (floatingCombatTextToggle)
                floatingCombatTextToggle.onValueChanged.RemoveListener(OnFctToggleChanged);
            if (showUiToggle)
                showUiToggle.onValueChanged.RemoveListener(OnShowUiToggleChanged);
            if (showOptionsPanelToggle)
                showOptionsPanelToggle.onValueChanged.RemoveListener(OnShowOptionsPanelToggleChanged);
            if (photoModeToggle)
                photoModeToggle.onValueChanged.RemoveListener(OnPhotoModeToggleChanged);
            if (photoModeSpeedSlider)
                photoModeSpeedSlider.onValueChanged.RemoveListener(OnPhotoModeSpeedChanged);
            if (extendZoomToggle)
                extendZoomToggle.onValueChanged.RemoveListener(OnExtendZoomToggleChanged);
            if (clickToDestroyTileToggle)
                clickToDestroyTileToggle.onValueChanged.RemoveListener(OnClickDestroyTileToggleChanged);
            if (clickToDestroyRadiusSlider)
                clickToDestroyRadiusSlider.onValueChanged.RemoveListener(OnClickDestroyRadiusChanged);
        }

        private void CacheOriginalStates()
        {
            _originalActive.Clear();
            if (!rememberOriginalStates || uiRoots == null) return;
            for (int i = 0; i < uiRoots.Count; i++)
            {
                var go = uiRoots[i];
                if (!go) continue;
                if (!_originalActive.ContainsKey(go))
                    _originalActive.Add(go, go.activeSelf);
            }
        }

        private void ApplyInitialToggleValues()
        {
            // Apply current toggle states at startup without double-notifying listeners
            if (floatingCombatTextToggle)
            {
                CombatTextManager.Enabled = floatingCombatTextToggle.isOn;
            }

            if (showUiToggle)
            {
                SetUiVisible(showUiToggle.isOn);
            }

            if (showOptionsPanelToggle)
            {
                SetOptionsPanelVisible(showOptionsPanelToggle.isOn);
            }

            SyncPhotoModeControls();
            SyncZoomToggle();
            SyncClickDestroyTileToggle();
        }

        private void OnFctToggleChanged(bool isOn)
        {
            CombatTextManager.Enabled = isOn;
        }

        private void OnShowUiToggleChanged(bool isOn)
        {
            SetUiVisible(isOn);
        }

        private void OnShowOptionsPanelToggleChanged(bool isOn)
        {
            SetOptionsPanelVisible(isOn);
        }

        private void SetUiVisible(bool visible)
        {
            if (uiRoots == null) return;

            if (visible && rememberOriginalStates && _initialized)
            {
                // Restore remembered states
                for (int i = 0; i < uiRoots.Count; i++)
                {
                    var go = uiRoots[i];
                    if (!go) continue;
                    bool state = true;
                    if (_originalActive.TryGetValue(go, out var orig)) state = orig;
                    if (go.activeSelf != state) go.SetActive(state);
                }
                return;
            }

            // Hide all or show all (if not remembering per-item states)
            for (int i = 0; i < uiRoots.Count; i++)
            {
                var go = uiRoots[i];
                if (!go) continue;
                bool target = visible;
                if (visible && rememberOriginalStates && _originalActive.TryGetValue(go, out var orig))
                {
                    target = orig;
                }
                if (go.activeSelf != target) go.SetActive(target);
            }
        }

        [Header("Level Up (Debug/Dev)")]
        [SerializeField, Tooltip("Button that triggers a single level-up for the player.")]
        private Button levelUpButton;

        [Header("Death/Restart Actions")]
        [SerializeField, Tooltip("Button that reloads the active scene (same behavior as death 'Restart').")] 
        private Button restartSceneButton;
        [SerializeField, Tooltip("Button that respawns the player at the recorded spawn point (same as death 'Respawn').")] 
        private Button respawnButton;

        [Header("Resources (Cheat/Debug)")]
        [SerializeField, Tooltip("Button that grants a fixed amount of every defined resource to the player.")]
        private Button grantAllResourcesButton;
        [SerializeField, Min(1), Tooltip("Amount of each resource to grant when pressing the button.")]
        private int grantAmountPerResource = 10;

        [Header("Skills (Reset)")]
        [SerializeField, Tooltip("Button that resets all skill nodes and refunds spent points.")]
        private Button resetSkillPointsButton;

        private void Start()
        {
            // Hook late to ensure PlayerExperience singleton exists
            if (levelUpButton) levelUpButton.onClick.AddListener(LevelUpOnce);
            if (restartSceneButton) restartSceneButton.onClick.AddListener(RestartScene);
            if (respawnButton) respawnButton.onClick.AddListener(RespawnPlayer);
            if (grantAllResourcesButton) grantAllResourcesButton.onClick.AddListener(GrantAllResources);
            if (resetSkillPointsButton) resetSkillPointsButton.onClick.AddListener(ResetSkillsAndRefund);
        }

        private void OnDestroy()
        {
            if (levelUpButton) levelUpButton.onClick.RemoveListener(LevelUpOnce);
            if (restartSceneButton) restartSceneButton.onClick.RemoveListener(RestartScene);
            if (respawnButton) respawnButton.onClick.RemoveListener(RespawnPlayer);
            if (grantAllResourcesButton) grantAllResourcesButton.onClick.RemoveListener(GrantAllResources);
            if (resetSkillPointsButton) resetSkillPointsButton.onClick.RemoveListener(ResetSkillsAndRefund);
        }

        private void LevelUpOnce()
        {
            var pxp = PlayerExperience.Instance ?? FindFirstObjectByType<PlayerExperience>();
            if (!pxp)
            {
                Debug.LogWarning("PlayerExperience not found; cannot level up.", this);
                return;
            }

            int needed = Mathf.Max(1, pxp.ExperienceToNextLevel - pxp.CurrentExperience);
            pxp.GrantExperience(needed, playFeedback: true);
        }

        private void RestartScene()
        {
            // Optional: ensure time is unpaused
            if (Time.timeScale <= 0f) Time.timeScale = 1f;
            var s = SceneManager.GetActiveScene();
            SceneManager.LoadScene(s.buildIndex);
        }

        private void RespawnPlayer()
        {
            // Optional: ensure time is unpaused
            if (Time.timeScale <= 0f) Time.timeScale = 1f;

            var ph = PlayerHealth.Instance ?? FindFirstObjectByType<PlayerHealth>();
            if (ph)
            {
                ph.TeleportToSpawn(fullHeal: true);
            }
            else
            {
                Debug.LogWarning("PlayerHealth not found; cannot respawn.", this);
            }
        }

        private void GrantAllResources()
        {
            var drm = DynamicResourceManager.Instance ?? FindFirstObjectByType<DynamicResourceManager>();
            if (!drm)
            {
                Debug.LogWarning("DynamicResourceManager not found; cannot grant resources.", this);
                return;
            }

            var db = drm.Database;
            if (db == null || db.Resources == null || db.Resources.Count == 0)
            {
                Debug.LogWarning("ResourceDatabase is missing or empty; nothing to grant.", this);
                return;
            }

            var grant = new ResourceSet();
            for (int i = 0; i < db.Resources.Count; i++)
            {
                var def = db.Resources[i];
                if (!def) continue;
                grant.Add(def, Mathf.Max(1, grantAmountPerResource));
            }

            Vector3 pos = (PlayerHealth.Instance ? PlayerHealth.Instance.transform.position : transform.position);
            drm.GrantResources(grant, pos, showFeedback: true, awardExperience: true);
        }

        private void ResetSkillsAndRefund()
        {
            var mgr = SkillManager.Instance ?? FindFirstObjectByType<SkillManager>();
            if (!mgr)
            {
                // Fallback: include inactive or objects not found by default search
                var all = Resources.FindObjectsOfTypeAll<SkillManager>();
                for (int i = 0; i < all.Length; i++)
                {
                    var m = all[i];
                    if (m && m.gameObject.scene.IsValid()) { mgr = m; break; }
                }
            }
            if (!mgr)
            {
                Debug.LogWarning("SkillManager not found; cannot reset skills.", this);
                return;
            }
            mgr.ResetAllSkillsAndRefund();
        }

        private void SetOptionsPanelVisible(bool visible)
        {
            var root = optionsPanelRoot ? optionsPanelRoot : gameObject;
            if (root.activeSelf != visible)
                root.SetActive(visible);
        }

        private void EnsureCameraReference()
        {
            if (cameraFollow)
                return;

            var mainCam = Camera.main;
            if (mainCam)
                cameraFollow = mainCam.GetComponent<SmoothCameraFollow>();
        }

        private void EnsureZoomLimitsCached()
        {
            EnsureCameraReference();
            if (_zoomLimitsCaptured || !cameraFollow)
                return;

            _originalZoomLimits = cameraFollow.zoomLimits;
            _zoomLimitsCaptured = true;
        }

        private bool EnsureClickDestroyerReference()
        {
            if (clickToDestroyTileTool)
                return true;

            clickToDestroyTileTool = FindFirstObjectByType<TileClickDestroyer>();
            return clickToDestroyTileTool;
        }

        private void ApplyExtendedZoom(bool enabled)
        {
            EnsureZoomLimitsCached();
            if (!cameraFollow)
                return;

            if (enabled)
            {
                Vector2 limits = cameraFollow.zoomLimits;
                float max = Mathf.Max(extendedMaxZoom, limits.y);
                cameraFollow.zoomLimits = new Vector2(limits.x, max);
            }
            else if (_zoomLimitsCaptured)
            {
                cameraFollow.zoomLimits = _originalZoomLimits;
            }

            // Clamp current zoom into the configured range.
            float minZ = cameraFollow.zoomLimits.x;
            float maxZ = cameraFollow.zoomLimits.y;
            float current = Mathf.Clamp(cameraFollow.zoom, minZ, maxZ);
            cameraFollow.SetZoom(current);
        }

        private void OnPhotoModeToggleChanged(bool enabled)
        {
            EnsureCameraReference();
            if (!cameraFollow)
            {
                Debug.LogWarning("Cannot toggle photo mode because SmoothCameraFollow was not found.", this);
                return;
            }

            cameraFollow.SetPhotoMode(enabled);
        }

        private void OnPhotoModeSpeedChanged(float value)
        {
            UpdatePhotoModeSpeedLabel(value);
            EnsureCameraReference();
            if (!cameraFollow)
                return;

            cameraFollow.SetPhotoModeSpeed(value);
        }

        private void OnExtendZoomToggleChanged(bool enabled)
        {
            ApplyExtendedZoom(enabled);
        }

        private void SyncPhotoModeControls()
        {
            EnsureCameraReference();
            if (!cameraFollow)
                return;

            if (photoModeToggle)
                photoModeToggle.SetIsOnWithoutNotify(cameraFollow.IsPhotoModeActive);

            if (photoModeSpeedSlider)
            {
                photoModeSpeedSlider.SetValueWithoutNotify(cameraFollow.PhotoModeSpeed);
                UpdatePhotoModeSpeedLabel(cameraFollow.PhotoModeSpeed);
            }
        }

        private void UpdatePhotoModeSpeedLabel(float value)
        {
            if (photoModeSpeedLabel)
                photoModeSpeedLabel.text = $"{value:0.0}";
        }

        private void SyncZoomToggle()
        {
            if (!extendZoomToggle)
                return;

            EnsureZoomLimitsCached();
            if (!cameraFollow)
            {
                extendZoomToggle.SetIsOnWithoutNotify(false);
                return;
            }

            // Do not try to infer state from current limits; just apply the current toggle value.
            ApplyExtendedZoom(extendZoomToggle.isOn);
        }

        private void SyncClickDestroyTileToggle()
        {
            if (!clickToDestroyTileToggle)
                return;

            if (!EnsureClickDestroyerReference())
            {
                clickToDestroyTileToggle.SetIsOnWithoutNotify(false);
                if (clickToDestroyRadiusSlider)
                    clickToDestroyRadiusSlider.SetValueWithoutNotify(1f);
                UpdateClickDestroyRadiusLabel(1f);
            }
            else
            {
                if (clickToDestroyRadiusSlider)
                {
                    float value = clickToDestroyRadiusSlider.value;
                    clickToDestroyTileTool.SetRadius(value);
                    UpdateClickDestroyRadiusLabel(value);
                }
            }
        }

        private void OnClickDestroyTileToggleChanged(bool enabled)
        {
            if (!EnsureClickDestroyerReference())
            {
                if (enabled)
                {
                    Debug.LogWarning("Click-to-destroy tile toggle requires a TileClickDestroyer in the scene, but none was found.", this);
                    clickToDestroyTileToggle.SetIsOnWithoutNotify(false);
                }
                return;
            }

            clickToDestroyTileTool.SetEnabled(enabled);
        }

        private void OnClickDestroyRadiusChanged(float value)
        {
            UpdateClickDestroyRadiusLabel(value);
            if (!EnsureClickDestroyerReference())
                return;

            clickToDestroyTileTool.SetRadius(value);
        }

        private void UpdateClickDestroyRadiusLabel(float value)
        {
            if (clickToDestroyRadiusLabel)
            {
                clickToDestroyRadiusLabel.text = $"{value:0}";
            }
        }
    }
}




