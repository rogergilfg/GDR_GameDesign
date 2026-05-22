using System;
using TMPro;
using SmallScale.FantasyKingdomTileset.Balance;
using UnityEngine;
using UnityEngine.UI;

namespace SmallScale.FantasyKingdomTileset.UI
{
    /// <summary>
    /// Binds game balance sliders to the GameBalanceManager configuration.
    /// </summary>
    [DisallowMultipleComponent]
    public class SettingsPanelController : MonoBehaviour
    {
        [Header("Player")]
        [SerializeField] Slider playerExperienceSlider;
        [SerializeField] TMP_Text playerExperienceLabel;
        [SerializeField] Slider tileExperienceSlider;
        [SerializeField] TMP_Text tileExperienceLabel;
        [SerializeField] Slider resourceExperienceSlider;
        [SerializeField] TMP_Text resourceExperienceLabel;
        [SerializeField] Slider resourceGainSlider;
        [SerializeField] TMP_Text resourceGainLabel;

        [Header("Player Damage")]
        [SerializeField] Slider abilityDamageSlider;
        [SerializeField] TMP_Text abilityDamageLabel;
        [SerializeField] Slider meleeDamageSlider;
        [SerializeField] TMP_Text meleeDamageLabel;
        [SerializeField] Slider tileDamageSlider;
        [SerializeField] TMP_Text tileDamageLabel;

        [Header("Enemy")]
        [SerializeField] Slider enemyHealthSlider;
        [SerializeField] TMP_Text enemyHealthLabel;
        [SerializeField] Slider enemyDamageSlider;
        [SerializeField] TMP_Text enemyDamageLabel;
        [SerializeField] Slider enemyExperienceSlider;
        [SerializeField] TMP_Text enemyExperienceLabel;

        [Header("Building")]
        [SerializeField] Slider buildCostSlider;
        [SerializeField] TMP_Text buildCostLabel;

        GameBalanceConfig _config;

        void Awake()
        {
            CacheConfig();
        }

        void CacheConfig()
        {
            if (GameBalanceManager.Instance != null)
                _config = GameBalanceManager.Instance.Config;
        }

        public void RefreshUI()
        {
            if (_config == null)
            {
                CacheConfig();
                if (_config == null)
                {
                    Debug.LogWarning("SettingsPanelController could not find GameBalanceManager in the scene.");
                    return;
                }
            }

            ConfigureBindings();
        }

        void ConfigureSlider(Slider slider, TMP_Text label, float defaultValue, Action<float> apply)
        {
            if (!slider || apply == null)
                return;

            slider.onValueChanged.RemoveAllListeners();
            slider.onValueChanged.AddListener(value =>
            {
                float snapped = Snap(value, 0.1f);
                apply(snapped);
                UpdateLabel(label, snapped);
            });

            float clamped = Snap(Mathf.Clamp(defaultValue, slider.minValue, slider.maxValue), 0.1f);
            slider.SetValueWithoutNotify(clamped);
            UpdateLabel(label, clamped);
        }

        float Snap(float value, float step)
        {
            if (step <= 0f)
                return value;
            return Mathf.Round(value / step) * step;
        }

        void UpdateLabel(TMP_Text label, float value)
        {
            if (label)
                label.text = $"x {value:0.0}";
        }

        void ConfigureBindings()
        {
            ConfigureSlider(playerExperienceSlider, playerExperienceLabel, _config.playerExperienceGainMultiplier, value =>
            {
                _config.playerExperienceGainMultiplier = value;
                GameBalanceManager.Instance?.RefreshExperienceMultiplier();
            });

            ConfigureSlider(tileExperienceSlider, tileExperienceLabel, _config.tileExperienceMultiplier, value =>
            {
                _config.tileExperienceMultiplier = value;
            });

            ConfigureSlider(resourceExperienceSlider, resourceExperienceLabel, _config.resourceExperienceMultiplier, value =>
            {
                _config.resourceExperienceMultiplier = value;
            });

            ConfigureSlider(resourceGainSlider, resourceGainLabel, _config.resourceGainMultiplier, value =>
            {
                _config.resourceGainMultiplier = value;
            });

            ConfigureSlider(abilityDamageSlider, abilityDamageLabel, _config.playerAbilityDamageMultiplier, value =>
            {
                _config.playerAbilityDamageMultiplier = value;
                PlayerStats.Instance?.RecalculateDamageFromSettings();
            });

            ConfigureSlider(meleeDamageSlider, meleeDamageLabel, _config.playerMeleeDamageMultiplier, value =>
            {
                _config.playerMeleeDamageMultiplier = value;
                PlayerStats.Instance?.RecalculateDamageFromSettings();
            });

            ConfigureSlider(tileDamageSlider, tileDamageLabel, _config.playerTileDamageMultiplier, value =>
            {
                _config.playerTileDamageMultiplier = value;
                PlayerStats.Instance?.RecalculateDamageFromSettings();
            });

            ConfigureSlider(enemyHealthSlider, enemyHealthLabel, _config.enemyHealthMultiplier, value =>
            {
                _config.enemyHealthMultiplier = value;
                GameBalanceManager.Instance?.RefreshEnemyStats();
            });

            ConfigureSlider(enemyDamageSlider, enemyDamageLabel, _config.enemyDamageMultiplier, value =>
            {
                _config.enemyDamageMultiplier = value;
                GameBalanceManager.Instance?.RefreshEnemyStats();
            });

            ConfigureSlider(enemyExperienceSlider, enemyExperienceLabel, _config.enemyExperienceRewardMultiplier, value =>
            {
                _config.enemyExperienceRewardMultiplier = value;
            });

            ConfigureSlider(buildCostSlider, buildCostLabel, _config.buildCostMultiplier, value =>
            {
                _config.buildCostMultiplier = value;
            });
        }

        void Update()
        {
            // If sliders were not configured yet (e.g., OnEnable before GameBalanceManager exists),
            // ensure bindings happen once the manager becomes available.
            if (_config == null && GameBalanceManager.Instance != null)
            {
                CacheConfig();
                if (_config != null)
                {
                    ConfigureBindings();
                }
            }
        }

        public void OnEnable()
        {
            if (_config != null)
                ConfigureBindings();
        }

        void OnValidate()
        {
            // Keep slider limits sensible in inspector (e.g., 0-10).
            ClampSlider(playerExperienceSlider);
            ClampSlider(tileExperienceSlider);
            ClampSlider(resourceExperienceSlider);
            ClampSlider(resourceGainSlider);
            ClampSlider(abilityDamageSlider);
            ClampSlider(meleeDamageSlider);
            ClampSlider(tileDamageSlider);
            ClampSlider(enemyHealthSlider);
            ClampSlider(enemyDamageSlider);
            ClampSlider(enemyExperienceSlider);
            ClampSlider(buildCostSlider);
        }

        void ClampSlider(Slider slider)
        {
            if (!slider)
                return;
            slider.minValue = Mathf.Min(slider.minValue, slider.maxValue);
            slider.maxValue = Mathf.Max(slider.maxValue, slider.minValue);
        }
    }
}



