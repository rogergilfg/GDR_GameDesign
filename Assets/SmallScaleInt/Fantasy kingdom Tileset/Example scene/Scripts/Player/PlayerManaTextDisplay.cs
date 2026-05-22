using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using TMPro;
using UnityEngine;

/// <summary>
/// Displays the player's mana as "current/max" on a TextMeshPro component.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
[MovedFrom(true, null, null, "PlayerManaTextDisplay")]
public class PlayerManaTextDisplay : MonoBehaviour
{
    [SerializeField]
    private TMP_Text manaText;

    [SerializeField]
    private PlayerMana playerMana;

    private void Awake()
    {
        if (manaText == null)
        {
            manaText = GetComponent<TMP_Text>();
        }
    }

    private void OnEnable()
    {
        TryAttachToPlayerMana();
        RefreshText();
    }

    private void OnDisable()
    {
        DetachFromPlayerMana();
    }

    private void Update()
    {
        if (playerMana == null)
        {
            TryAttachToPlayerMana();
        }
    }

    private void TryAttachToPlayerMana()
    {
        PlayerMana target = playerMana != null ? playerMana : PlayerMana.Instance ?? FindFirstObjectByType<PlayerMana>();
        if (target == null)
        {
            return;
        }

        if (playerMana != target)
        {
            DetachFromPlayerMana();
            playerMana = target;
        }

        playerMana.OnManaChanged -= HandleManaChanged;
        playerMana.OnManaChanged += HandleManaChanged;
    }

    private void DetachFromPlayerMana()
    {
        if (playerMana != null)
        {
            playerMana.OnManaChanged -= HandleManaChanged;
            playerMana = null;
        }
    }

    private void HandleManaChanged(float current, float max)
    {
        if (manaText == null)
        {
            return;
        }

        manaText.SetText("{0}/{1}", Mathf.RoundToInt(current), Mathf.RoundToInt(max));
    }

    private void RefreshText()
    {
        if (playerMana == null)
        {
            if (manaText != null)
            {
                manaText.text = "0/0";
            }
            return;
        }

        HandleManaChanged(playerMana.CurrentMana, playerMana.MaxMana);
    }
}



}




