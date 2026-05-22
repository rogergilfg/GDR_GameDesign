using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using UnityEngine;

[DisallowMultipleComponent]
[MovedFrom(true, null, null, "PlayerHealthUIBinder")]
public class PlayerHealthUIBinder : MonoBehaviour
{
    public PlayerHealth playerHealth;
    public HealthBarDualSliderUI ui;

    int _lastMax;
    int _lastCurrent;

    void Reset()
    {
        playerHealth = FindFirstObjectByType<PlayerHealth>();
        ui = GetComponentInChildren<HealthBarDualSliderUI>(true);
    }

    void Start()
    {
        if (!playerHealth || !ui) return;
        _lastMax     = playerHealth.maxHealth;
        _lastCurrent = playerHealth.currentHealth;
        ui.SetMax(_lastMax, _lastCurrent);
    }

    void Update()
    {
        if (!playerHealth || !ui) return;

        if (playerHealth.maxHealth != _lastMax)
        {
            _lastMax = playerHealth.maxHealth;
            ui.SetMax(_lastMax, Mathf.Clamp(playerHealth.currentHealth, 0, _lastMax));
        }

        if (playerHealth.currentHealth != _lastCurrent)
        {
            _lastCurrent = playerHealth.currentHealth;
            ui.AnimateTo(_lastCurrent);
        }
    }
}



}




