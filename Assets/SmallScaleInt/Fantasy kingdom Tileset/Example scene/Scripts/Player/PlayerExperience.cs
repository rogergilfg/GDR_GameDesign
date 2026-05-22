using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Tracks the player's experience and level progression. Provides simple hooks for UI and gameplay systems.
/// </summary>
[DisallowMultipleComponent]
[MovedFrom(true, null, null, "PlayerExperience")]
public class PlayerExperience : MonoBehaviour
{
    public static PlayerExperience Instance { get; private set; }

    [Header("XP Curve")]
    [SerializeField]
    [Tooltip("Level the player starts at when the scene loads.")]
    private int startingLevel = 0;

    [SerializeField]
    [Tooltip("Base experience required to advance from level 0 to level 1.")]
    private int baseExperienceRequired = 100;

    [SerializeField]
    [Tooltip("Exponential growth applied to the experience requirement each level (>= 1.01).")]
    [Range(1.01f, 5f)] private float experienceGrowthFactor = 1.25f;

    [SerializeField]
    [Tooltip("Flat bonus added to the required experience each level (optional).")]
    private int flatExperienceGrowthPerLevel = 0;

    [Header("Feedback")]
    [SerializeField]
    [Tooltip("Optional VFX spawned at the player's position on level up.")]
    private GameObject levelUpVfxPrefab;

    [SerializeField]
    [Tooltip("Optional secondary VFX spawned alongside the main effect on level up (optional).")]
    private GameObject additionalLevelUpVfxPrefab;

    [SerializeField]
    [Tooltip("Colour applied to the player briefly when a level up occurs.")]
    private Color levelUpFlashColor = new Color(1f, 0.9f, 0.2f, 1f);

    [SerializeField]
    [Tooltip("Duration of the level-up flash in seconds.")]
    [Min(0f)] private float levelUpFlashDuration = 0.45f;

    [SerializeField]
    [Tooltip("Sprite renderers that should flash when a level up occurs. Defaults to all children when left empty.")]
    private SpriteRenderer[] flashTargets = Array.Empty<SpriteRenderer>();

    [Header("Combat Text")]
    [SerializeField]
    [Tooltip("Show floating combat text when the player levels up.")]
    private bool showLevelUpCombatText = true;

    [SerializeField]
    [Tooltip("Show floating combat text for each chunk of experience gained.")]
    private bool showExperienceGainCombatText = false;

    [SerializeField]
    [Tooltip("Offset applied to combat text spawned as a result of experience events.")]
    private Vector3 combatTextOffset = new Vector3(0f, 1.2f, 0f);

    [Header("Attribute Points")]
    [SerializeField]
    [Tooltip("When enabled, each level up grants the player +1 attribute point.")]
    private bool grantAttributePointsOnLevelUp = true;

    public event Action<int, int, int> ExperienceChanged; // level, current XP, required XP
    public event Action<int> LevelChanged;

    public int CurrentLevel => currentLevel;
    public int CurrentExperience => currentExperience;
    public int ExperienceToNextLevel => experienceToNextLevel;
    public int TotalExperience => totalExperience;
    public float NormalizedProgress => experienceToNextLevel <= 0 ? 0f : Mathf.Clamp01((float)currentExperience / experienceToNextLevel);
    public float ExternalExperienceMultiplier => externalExperienceMultiplier;

    private int currentLevel;
    private int currentExperience;
    private int experienceToNextLevel;
    private int totalExperience;
    private float externalExperienceMultiplier = 1f;
    private Coroutine flashRoutine;
    private Color[] savedOriginalColors;

    void Reset()
    {
        flashTargets = GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple PlayerExperience instances detected. Destroying duplicate.", this);
            Destroy(this);
            return;
        }

        Instance = this;
        Initialise();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void Initialise()
    {
        currentLevel = Mathf.Max(0, startingLevel);
        currentExperience = 0;
        totalExperience = 0;
        experienceToNextLevel = CalculateExperienceRequirement(currentLevel);

        ExperienceChanged?.Invoke(currentLevel, currentExperience, experienceToNextLevel);
        LevelChanged?.Invoke(currentLevel);
    }

    public static void GrantStatic(int amount, Vector3 worldPosition, bool playFeedback = true)
    {
        if (Instance != null)
        {
            Instance.GrantExperience(amount, worldPosition, playFeedback);
        }
    }

    public void GrantExperience(int amount, bool playFeedback = true)
    {
        GrantExperience(amount, transform.position, playFeedback);
    }

    public void GrantExperience(int amount, Vector3 worldPosition, bool playFeedback = true)
    {
        if (amount <= 0)
        {
            return;
        }

        int adjustedAmount = Mathf.Max(1, Mathf.RoundToInt(amount * Mathf.Max(0f, externalExperienceMultiplier)));

        totalExperience += adjustedAmount;
        currentExperience += adjustedAmount;

        int levelsGained = 0;
        while (currentExperience >= experienceToNextLevel)
        {
            currentExperience -= experienceToNextLevel;
            currentLevel++;

            if (grantAttributePointsOnLevelUp && PlayerStats.Instance != null)
            {
                PlayerStats.Instance.GrantAttributePoints(1);
            }

            experienceToNextLevel = CalculateExperienceRequirement(currentLevel);
            levelsGained++;
            LevelChanged?.Invoke(currentLevel);
        }

        if (levelsGained > 0 && playFeedback)
        {
            PlayLevelUpFeedback(worldPosition);
        }

        if (showExperienceGainCombatText || (showLevelUpCombatText && levelsGained > 0))
        {
            ShowCombatText(adjustedAmount, worldPosition, levelsGained);
        }

        ExperienceChanged?.Invoke(currentLevel, currentExperience, experienceToNextLevel);
    }

    public void SetExternalMultiplier(float multiplier)
    {
        externalExperienceMultiplier = Mathf.Max(0f, multiplier);
    }

    public int CalculateExperienceRequirement(int level)
    {
        if (level < 0)
        {
            level = 0;
        }

        double clampedBase = Math.Max(1, baseExperienceRequired);
        double exponential = clampedBase * Math.Pow(experienceGrowthFactor, level);
        double flat = flatExperienceGrowthPerLevel * level;
        int required = Mathf.Max(1, Mathf.RoundToInt((float)(exponential + flat)));
        return required;
    }

    void PlayLevelUpFeedback(Vector3 worldPosition)
    {
        Transform target = transform;
        Vector3 spawnPosition = target != null ? target.position : worldPosition;

        if (levelUpVfxPrefab != null)
        {
            Instantiate(levelUpVfxPrefab, spawnPosition, Quaternion.identity, target);
        }

        if (additionalLevelUpVfxPrefab != null)
        {
            Instantiate(additionalLevelUpVfxPrefab, spawnPosition, Quaternion.identity, target);
        }

        // Always refresh the flash targets to get current sprite renderers
        flashTargets = GetComponentsInChildren<SpriteRenderer>(includeInactive: true);

        if (flashTargets != null && flashTargets.Length > 0 && levelUpFlashDuration > 0f)
        {
            // Restore colors from any previous interrupted flash
            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
                RestoreSavedColors();
                flashRoutine = null;
            }

            flashRoutine = StartCoroutine(LevelUpFlashRoutine());
        }
    }

    IEnumerator LevelUpFlashRoutine()
    {
        // Save current colors (NOT flash colors if we're already flashing)
        savedOriginalColors = new Color[flashTargets.Length];
        for (int i = 0; i < flashTargets.Length; i++)
        {
            if (flashTargets[i] != null)
            {
                // Save the CURRENT color (which should be the normal color)
                savedOriginalColors[i] = flashTargets[i].color;
                flashTargets[i].color = levelUpFlashColor;
            }
        }

        yield return new WaitForSeconds(levelUpFlashDuration);

        // Restore original colors
        RestoreSavedColors();

        flashRoutine = null;
        savedOriginalColors = null;
    }

    void RestoreSavedColors()
    {
        if (savedOriginalColors == null || flashTargets == null) return;

        int count = Mathf.Min(savedOriginalColors.Length, flashTargets.Length);
        for (int i = 0; i < count; i++)
        {
            if (flashTargets[i] != null)
            {
                flashTargets[i].color = savedOriginalColors[i];
            }
        }
    }

    void ShowCombatText(int amount, Vector3 worldPosition, int levelsGained)
    {
        if (CombatTextManager.Instance == null)
        {
            return;
        }

        Vector3 position = worldPosition + combatTextOffset;

        if (showExperienceGainCombatText)
        {
            CombatTextManager.Instance.SpawnExperienceGain($"+{amount} XP", position);
        }

        if (showLevelUpCombatText && levelsGained > 0)
        {
            CombatTextManager.Instance.SpawnLevelUp(levelsGained > 1 ? $"Level +{levelsGained}" : "Level Up!", position);
        }
    }
}


}




