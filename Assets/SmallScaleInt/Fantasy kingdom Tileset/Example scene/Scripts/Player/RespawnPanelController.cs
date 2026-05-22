using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using SmallScale.FantasyKingdomTileset;

namespace SmallScale.FantasyKingdomTileset
{
public class RespawnPanelController : MonoBehaviour
{
    [Header("Refs")]
    public GameObject panelRoot;     // assign your RespawnPanel GO
    public Button respawnButton;
    public Button restartButton;
    public TMP_Text message;         // optional â€œYou Diedâ€ text

    void Awake()
    {
        if (panelRoot) panelRoot.SetActive(false);

        if (respawnButton) respawnButton.onClick.AddListener(OnRespawnClicked);
        if (restartButton) restartButton.onClick.AddListener(OnRestartClicked);
    }

    void OnEnable()
    {
        PlayerHealth.OnPlayerDied += ShowPanel;
        PlayerHealth.OnPlayerRespawned += HidePanel;
    }

    void OnDisable()
    {
        PlayerHealth.OnPlayerDied -= ShowPanel;
        PlayerHealth.OnPlayerRespawned -= HidePanel;
    }

    void ShowPanel()
    {
        if (message) message.text = "You died.";
        if (panelRoot) panelRoot.SetActive(true);

        // Optional: pause game time
        // Time.timeScale = 0f;
    }

    void HidePanel()
    {
        if (panelRoot) panelRoot.SetActive(false);

        // Optional: unpause
        // Time.timeScale = 1f;
    }

    void OnRespawnClicked()
    {
        // Optional: unpause first if you paused
        // Time.timeScale = 1f;

        if (PlayerHealth.Instance != null)
            PlayerHealth.Instance.RespawnAtSpawn(); // full heal by default
        else
            HidePanel();
    }

    void OnRestartClicked()
    {
        // Optional: unpause
        // Time.timeScale = 1f;

        var s = SceneManager.GetActiveScene();
        SceneManager.LoadScene(s.buildIndex);
    }
}
}





