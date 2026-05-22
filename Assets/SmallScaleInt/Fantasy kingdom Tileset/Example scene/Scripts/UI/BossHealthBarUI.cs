using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SmallScaleInc.CharacterCreatorFantasy
{
    [DisallowMultipleComponent]
    public class BossHealthBarUI : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] Slider healthSlider;
        [SerializeField] TMP_Text bossNameText;
        [SerializeField] TMP_Text valueText;

        int _maxHealth = 1;

        void Awake()
        {
            CacheReferences();
            ConfigureSlider();
        }

        void CacheReferences()
        {
            if (!healthSlider)
                healthSlider = GetComponentInChildren<Slider>(true);

            if (!bossNameText)
            {
                var texts = GetComponentsInChildren<TMP_Text>(true);
                if (texts.Length > 0)
                    bossNameText = texts[0];
                if (texts.Length > 1 && !valueText)
                    valueText = texts[1];
            }
            else if (!valueText)
            {
                var texts = GetComponentsInChildren<TMP_Text>(true);
                foreach (var t in texts)
                {
                    if (t != bossNameText)
                    {
                        valueText = t;
                        break;
                    }
                }
            }
        }

        void ConfigureSlider()
        {
            if (!healthSlider)
            {
                Debug.LogWarning("BossHealthBarUI requires a Slider reference to function.", this);
                return;
            }

            healthSlider.minValue = 0f;
            healthSlider.maxValue = _maxHealth;
            healthSlider.wholeNumbers = true;
            healthSlider.interactable = false;
        }

        public void Configure(string displayName, int maxHealth, int currentHealth)
        {
            CacheReferences();
            _maxHealth = Mathf.Max(1, maxHealth);
            if (healthSlider)
            {
                healthSlider.minValue = 0f;
                healthSlider.maxValue = _maxHealth;
            }

            SetBossName(displayName);
            UpdateValues(maxHealth, currentHealth);
            SetVisible(true);
        }

        public void UpdateValues(int maxHealth, int currentHealth)
        {
            CacheReferences();
            _maxHealth = Mathf.Max(1, maxHealth);
            if (healthSlider)
            {
                if (!Mathf.Approximately(healthSlider.maxValue, _maxHealth))
                    healthSlider.maxValue = _maxHealth;
                healthSlider.value = Mathf.Clamp(currentHealth, 0, _maxHealth);
            }

            if (valueText)
                valueText.text = $"{Mathf.Clamp(currentHealth, 0, _maxHealth)} / {_maxHealth}";
        }

        public void SetBossName(string displayName)
        {
            CacheReferences();
            if (bossNameText)
                bossNameText.text = displayName;
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
                gameObject.SetActive(visible);
        }
    }
}






