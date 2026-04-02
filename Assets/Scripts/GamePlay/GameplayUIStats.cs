using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameplayUIStats : StateListener
{
    [Header("Player UI")]
    public GameObject playerHealthBarVisual;
    public Slider playerHealthBarSlider;
    public TextMeshProUGUI scoreText;

    [Header("Ultimate Meter UI")]
    [Tooltip("The parent GameObject containing your Ultimate Meter UI elements")]
    public GameObject ultimateMeterVisual; 
    public UltimateMeterManager ultimateMeterManager;

    public void UpdatePlayerHealth(int currentHealth, int maxHealth)
    {
        if (playerHealthBarSlider != null)
            playerHealthBarSlider.value = (float)currentHealth / maxHealth;
    }

    public void UpdateScore(int newScore)
    {
        if (scoreText != null)
            scoreText.text = $"Score: {newScore}";
    }

    public void ShowStats(bool isPlaying)
    {
        if (isPlaying)
        {
            playerHealthBarVisual.SetActive(true);
            scoreText.enabled = true;
            if (ultimateMeterVisual != null) ultimateMeterVisual.SetActive(true);
            if (ultimateMeterManager != null) ultimateMeterManager.ConsumeUltimate();
        }
        else
        {
            playerHealthBarVisual.SetActive(false);
            scoreText.enabled = false;
            if (ultimateMeterVisual != null) ultimateMeterVisual.SetActive(false);
        }
    }
}