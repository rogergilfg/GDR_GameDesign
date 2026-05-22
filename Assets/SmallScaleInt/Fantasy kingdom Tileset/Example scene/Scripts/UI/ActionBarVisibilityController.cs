using UnityEngine;
using UnityEngine.UI;
using SmallScale.FantasyKingdomTileset.AbilitySystem;
using SkillSystem;

namespace SmallScale.FantasyKingdomTileset.UI
{
    /// <summary>
    /// Controls the visibility of action bar buttons based on player progression.
    /// Skill button becomes visible after the player earns a skill point.
    /// Spellbook button becomes visible after the player acquires any ability.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActionBarVisibilityController : MonoBehaviour
    {
        [Header("Button References")]
        [SerializeField]
        [Tooltip("Button that opens the skill tree/skill window.")]
        private Button skillButton;

        [SerializeField]
        [Tooltip("Button that opens the spell book.")]
        private Button spellBookButton;

        [SerializeField]
        [Tooltip("Optional image component on the skill button. When provided, it can swap sprites while points are unspent.")]
        private Image skillButtonImage;

        [SerializeField]
        [Tooltip("Optional sprite used while no points are pending (skill button idle).")]
        private Sprite skillButtonDefaultSprite;

        [SerializeField]
        [Tooltip("Sprite used while the player has unspent skill points.")]
        private Sprite skillButtonAlertSprite;

        [SerializeField]
        [Tooltip("Enable pulsing animation while the player has unspent skill points.")]
        private bool pulseSkillButton = true;

        [SerializeField]
        [Range(1f, 2f)]
        [Tooltip("Maximum scale multiplier reached while pulsing.")]
        private float pulseScaleMultiplier = 1.1f;

        [SerializeField]
        [Tooltip("Speed of the pulse animation.")]
        private float pulseSpeed = 4f;

        [Header("Dependencies")]
        [SerializeField]
        private SkillManager skillManager;

        [SerializeField]
        private AbilityRunner abilityRunner;

        [SerializeField]
        [Tooltip("Optional ability slot manager (used for fallback ability detection).")]
        private AbilitySlotManager abilitySlotManager;

        private bool skillButtonUnlocked;
        private bool spellbookButtonUnlocked;
        private bool pendingSkillPointNotification;
        private int lastKnownSkillPoints;
        private Coroutine skillPulseRoutine;
        private Vector3 skillButtonDefaultScale = Vector3.one;

        private void Awake()
        {
            if (skillManager == null)
            {
                skillManager = SkillManager.Instance ?? FindFirstObjectByType<SkillManager>();
            }

            if (abilityRunner == null)
            {
                abilityRunner = FindFirstObjectByType<AbilityRunner>();
            }

            if (abilitySlotManager == null)
            {
                abilitySlotManager = FindFirstObjectByType<AbilitySlotManager>();
            }

            if (skillButtonImage == null && skillButton != null)
            {
                skillButtonImage = skillButton.GetComponent<Image>();
            }

            if (skillButton != null)
            {
                skillButtonDefaultScale = skillButton.transform.localScale;
                if (skillButtonImage != null && skillButtonDefaultSprite == null)
                {
                    skillButtonDefaultSprite = skillButtonImage.sprite;
                }
            }

            lastKnownSkillPoints = CurrentSkillPoints;
            pendingSkillPointNotification = false;

            UpdateInitialVisibility();
        }

        private void OnEnable()
        {
            Subscribe();
            UpdateInitialVisibility();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (skillManager != null)
            {
                skillManager.SkillPointsChanged += HandleSkillPointsChanged;
            }

            if (abilityRunner != null)
            {
                abilityRunner.AbilityGranted += HandleAbilityGranted;
            }

            if (skillButton != null)
            {
                skillButton.onClick.AddListener(HandleSkillButtonClicked);
            }
        }

        private void Unsubscribe()
        {
            if (skillManager != null)
            {
                skillManager.SkillPointsChanged -= HandleSkillPointsChanged;
            }

            if (abilityRunner != null)
            {
                abilityRunner.AbilityGranted -= HandleAbilityGranted;
            }

            if (skillButton != null)
            {
                skillButton.onClick.RemoveListener(HandleSkillButtonClicked);
            }
        }

        private void UpdateInitialVisibility()
        {
            if (skillManager != null)
            {
                skillButtonUnlocked = skillManager.AvailableSkillPoints > 0;
                lastKnownSkillPoints = skillManager.AvailableSkillPoints;
            }

            if (!skillButtonUnlocked && abilityRunner != null)
            {
                // If player already spent points but has skill unlocks, button should be visible.
                skillButtonUnlocked = abilityRunner.HasAnyUnlockedAbility();
            }

            if (abilityRunner != null)
            {
                spellbookButtonUnlocked = HasAnyUnlockedAbility();
            }
            else if (abilitySlotManager != null)
            {
                spellbookButtonUnlocked = abilitySlotManager.HasAnyAssignedAbility();
            }

            ApplySkillButtonState();
            UpdateSkillPointIndicator();
            ApplySpellbookButtonState();
        }

        private void HandleSkillPointsChanged(int points)
        {
            if (points > lastKnownSkillPoints)
            {
                pendingSkillPointNotification = true;
            }

            lastKnownSkillPoints = points;

            if (!skillButtonUnlocked && points > 0)
            {
                skillButtonUnlocked = true;
                ApplySkillButtonState();
            }

            UpdateSkillPointIndicator();
        }

        private void HandleAbilityGranted(AbilityDefinition ability)
        {
            if (ability == null || spellbookButtonUnlocked)
            {
                return;
            }

            spellbookButtonUnlocked = true;
            ApplySpellbookButtonState();

            // Also treat ability unlock as skill availability (if player spent all points already)
            if (!skillButtonUnlocked)
            {
                skillButtonUnlocked = true;
                ApplySkillButtonState();
                UpdateSkillPointIndicator();
            }
        }

        private bool HasAnyUnlockedAbility()
        {
            if (abilityRunner != null && abilityRunner.HasAnyUnlockedAbility())
            {
                return true;
            }

            if (abilitySlotManager != null)
            {
                return abilitySlotManager.HasAnyAssignedAbility();
            }

            return false;
        }

        private void ApplySkillButtonState()
        {
            if (skillButton != null)
            {
                skillButton.gameObject.SetActive(skillButtonUnlocked);
            }
        }

        private void ApplySpellbookButtonState()
        {
            if (spellBookButton != null)
            {
                spellBookButton.gameObject.SetActive(spellbookButtonUnlocked);
            }
        }

        private void UpdateSkillPointIndicator()
        {
            bool shouldPulse = skillButtonUnlocked && pendingSkillPointNotification;
            if (shouldPulse)
            {
                StartSkillPulse();
            }
            else
            {
                StopSkillPulse();
            }
        }

        private void StartSkillPulse()
        {
            if (!pulseSkillButton || skillButton == null)
            {
                ApplySkillButtonSprite(true);
                return;
            }

            if (skillPulseRoutine == null)
            {
                skillPulseRoutine = StartCoroutine(SkillPulseRoutine());
            }

            ApplySkillButtonSprite(false);
        }

        private void StopSkillPulse()
        {
            if (skillPulseRoutine != null)
            {
                StopCoroutine(skillPulseRoutine);
                skillPulseRoutine = null;
            }

            if (skillButton != null)
            {
                skillButton.transform.localScale = skillButtonDefaultScale;
            }

            ApplySkillButtonSprite(true);
        }

        private void ApplySkillButtonSprite(bool useDefault)
        {
            if (skillButtonImage == null)
            {
                return;
            }

            if (useDefault && skillButtonDefaultSprite != null)
            {
                skillButtonImage.sprite = skillButtonDefaultSprite;
            }
            else if (!useDefault && skillButtonAlertSprite != null)
            {
                skillButtonImage.sprite = skillButtonAlertSprite;
            }
        }

        private System.Collections.IEnumerator SkillPulseRoutine()
        {
            if (skillButton == null)
            {
                yield break;
            }

            RectTransform rect = skillButton.transform as RectTransform;
            float elapsed = 0f;
            Vector3 baseScale = skillButtonDefaultScale == Vector3.zero ? Vector3.one : skillButtonDefaultScale;

            while (true)
            {
                elapsed += Time.unscaledDeltaTime * pulseSpeed;
                float pulse = (Mathf.Sin(elapsed) + 1f) * 0.5f;
                float scale = Mathf.Lerp(1f, pulseScaleMultiplier, pulse);
                rect.localScale = baseScale * scale;
                yield return null;
            }
        }

        private int CurrentSkillPoints => skillManager != null ? skillManager.AvailableSkillPoints : 0;

        private void HandleSkillButtonClicked()
        {
            if (!pendingSkillPointNotification)
            {
                return;
            }

            pendingSkillPointNotification = false;
            UpdateSkillPointIndicator();
        }
    }
}




