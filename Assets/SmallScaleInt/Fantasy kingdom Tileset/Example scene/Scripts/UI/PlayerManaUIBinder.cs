using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[MovedFrom(true, null, null, "PlayerManaUIBinder")]
public class PlayerManaUIBinder : MonoBehaviour
{
    public PlayerMana playerMana;
    public Slider slider;

    float _lastMax = -1f;
    float _lastCurrent = -1f;

    void Reset()
    {
        playerMana = FindFirstObjectByType<PlayerMana>();
        slider = GetComponentInChildren<Slider>(true);
    }

    void Start()
    {
        if (!playerMana || !slider) return;
        SyncImmediate();
    }

    void Update()
    {
        if (!playerMana || !slider) return;

        if (!Mathf.Approximately(playerMana.MaxMana, _lastMax))
        {
            _lastMax = playerMana.MaxMana;
            slider.maxValue = _lastMax;
        }

        if (!Mathf.Approximately(playerMana.CurrentMana, _lastCurrent))
        {
            _lastCurrent = playerMana.CurrentMana;
            slider.value = _lastCurrent;
        }
    }

    void SyncImmediate()
    {
        _lastMax = playerMana.MaxMana;
        _lastCurrent = Mathf.Clamp(playerMana.CurrentMana, 0f, _lastMax);
        slider.maxValue = _lastMax;
        slider.value = _lastCurrent;
    }
}



}




