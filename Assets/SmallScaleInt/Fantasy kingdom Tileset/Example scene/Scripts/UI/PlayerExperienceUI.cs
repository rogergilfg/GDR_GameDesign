using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Binds the player experience system to UI elements such as sliders and labels.
/// </summary>
[DisallowMultipleComponent]
[MovedFrom(true, null, null, "PlayerExperienceUI")]
public class PlayerExperienceUI : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Experience component to listen to. Defaults to the scene instance when omitted.")]
    private PlayerExperience experience;

    [Header("UI")]
    [SerializeField] private Slider experienceSlider;
    [SerializeField] private TMP_Text levelLabel;
    [SerializeField] private TMP_Text progressLabel;

    private void Awake()
    {
        if (experience == null)
        {
            experience = PlayerExperience.Instance;
        }
    }

    private void OnEnable()
    {
        TrySubscribe();
        RefreshUI();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (experience == null)
        {
            experience = PlayerExperience.Instance;
            if (experience != null)
            {
                TrySubscribe();
                RefreshUI();
            }
        }
    }

    private void TrySubscribe()
    {
        if (experience == null)
        {
            return;
        }

        experience.ExperienceChanged += HandleExperienceChanged;
        experience.LevelChanged += HandleLevelChanged;
    }

    private void Unsubscribe()
    {
        if (experience == null)
        {
            return;
        }

        experience.ExperienceChanged -= HandleExperienceChanged;
        experience.LevelChanged -= HandleLevelChanged;
    }

    private void HandleExperienceChanged(int level, int currentExperience, int requiredExperience)
    {
        UpdateSlider(currentExperience, requiredExperience);
        UpdateProgressLabel(currentExperience, requiredExperience);
        UpdateLevelLabel(level);
    }

    private void HandleLevelChanged(int level)
    {
        UpdateLevelLabel(level);
    }

    private void RefreshUI()
    {
        if (experience == null)
        {
            UpdateSlider(0, 1);
            UpdateLevelLabel(0);
            UpdateProgressLabel(0, 1);
            return;
        }

        UpdateSlider(experience.CurrentExperience, experience.ExperienceToNextLevel);
        UpdateProgressLabel(experience.CurrentExperience, experience.ExperienceToNextLevel);
        UpdateLevelLabel(experience.CurrentLevel);
    }

    private void UpdateSlider(int current, int required)
    {
        if (experienceSlider == null)
        {
            return;
        }

        int safeRequired = Mathf.Max(1, required);
        experienceSlider.minValue = 0;
        experienceSlider.maxValue = safeRequired;
        experienceSlider.value = Mathf.Clamp(current, 0, safeRequired);
    }

    private void UpdateLevelLabel(int level)
    {
        if (levelLabel != null)
        {
            levelLabel.text = $"Lv. {level}";
        }
    }

    private void UpdateProgressLabel(int current, int required)
    {
        if (progressLabel == null)
        {
            return;
        }

        progressLabel.text = $"{current} / {Mathf.Max(1, required)} XP";
    }
}



}




