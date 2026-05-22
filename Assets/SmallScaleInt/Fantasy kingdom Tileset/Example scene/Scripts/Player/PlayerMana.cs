using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using UnityEngine;

[DisallowMultipleComponent]
[MovedFrom(true, null, null, "PlayerMana")]
public class PlayerMana : MonoBehaviour
{
    public static PlayerMana Instance { get; private set; }

    [Header("Mana")] 
    [Tooltip("Maximum mana the player can store.")]
    public float maxMana = 100f;

    [SerializeField]
    [Tooltip("Starting mana. Leave <=0 to start at max.")]
    private float currentMana = 100f;

    [Header("Regeneration")]
    [Tooltip("Mana regenerated per second.")]
    public float regenPerSecond = 5f;

    [Tooltip("Delay after spending mana before regeneration resumes.")]
    public float regenDelay = 1.5f;

    public event System.Action<float, float> OnManaChanged; // current, max

    float _regenResumeAt = 0f;

    public float CurrentMana => currentMana;
    public float MaxMana => maxMana;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple PlayerMana instances found. Destroying the newest one.", this);
            Destroy(this);
            return;
        }

        Instance = this;
        if (maxMana < 1f) maxMana = 1f;
        currentMana = Mathf.Clamp(currentMana <= 0f ? maxMana : currentMana, 0f, maxMana);
        RaiseChanged();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (PlayerHealth.IsPlayerDead) return;
        if (regenPerSecond <= 0f) return;
        if (currentMana >= maxMana - 0.0001f) return;
        if (Time.time < _regenResumeAt) return;

        float delta = regenPerSecond * Time.deltaTime;
        if (delta <= 0f) return;

        currentMana = Mathf.Min(maxMana, currentMana + delta);
        RaiseChanged();
    }

    public bool TrySpend(float amount)
    {
        if (amount <= 0f) return true;
        if (currentMana + 0.0001f < amount) return false;

        currentMana = Mathf.Max(0f, currentMana - amount);
        _regenResumeAt = Time.time + Mathf.Max(0f, regenDelay);
        RaiseChanged();
        return true;
    }

    public bool HasMana(float amount)
    {
        if (amount <= 0f) return true;
        return currentMana + 0.0001f >= amount;
    }

    public void Refill()
    {
        currentMana = maxMana;
        RaiseChanged();
    }

    public void Grant(float amount)
    {
        if (amount <= 0f) return;
        currentMana = Mathf.Clamp(currentMana + amount, 0f, maxMana);
        RaiseChanged();
    }

    public void SetMaxMana(float newMax, bool refill = true)
    {
        maxMana = Mathf.Max(1f, newMax);
        if (refill)
            currentMana = maxMana;
        else
            currentMana = Mathf.Clamp(currentMana, 0f, maxMana);
        RaiseChanged();
    }

    void RaiseChanged()
    {
        OnManaChanged?.Invoke(currentMana, maxMana);
    }
}



}




