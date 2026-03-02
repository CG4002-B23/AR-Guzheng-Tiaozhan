using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameplayUIStats : StateListener
{
    [Header("Player UI")]
    public GameObject playerHealthBarVisual;
    public Slider playerHealthBarSlider;
    public TextMeshProUGUI scoreText;

    [Header("Enemy UI")]
    public GameObject enemyHealthBarVisual;
    public Slider enemyHealthBarSlider;

    public void UpdatePlayerHealth(int currentHealth, int maxHealth)
    {
        if (playerHealthBarSlider != null)
            playerHealthBarSlider.value = (float)currentHealth / maxHealth;
    }

    public void UpdateEnemyHealth(int currentHealth, int maxHealth)
    {
        if (enemyHealthBarSlider != null)
            enemyHealthBarSlider.value = (float)currentHealth / maxHealth;
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
            enemyHealthBarVisual.SetActive(true);
            scoreText.enabled = true;
        }
        else
        {
            playerHealthBarVisual.SetActive(false);
            enemyHealthBarVisual.SetActive(false);
            scoreText.enabled = false;
        }
    }
}