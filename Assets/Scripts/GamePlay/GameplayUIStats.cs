using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameplayUIStats : MonoBehaviour
{
    [Header("Player UI")]
    public Slider playerHealthBar;
    public TextMeshProUGUI scoreText;

    [Header("Enemy UI")]
    public Slider enemyHealthBar;

    public void UpdatePlayerHealth(int currentHealth, int maxHealth)
    {
        if (playerHealthBar != null)
            playerHealthBar.value = (float)currentHealth / maxHealth;
    }

    public void UpdateEnemyHealth(int currentHealth, int maxHealth)
    {
        if (enemyHealthBar != null)
            enemyHealthBar.value = (float)currentHealth / maxHealth;
    }

    public void UpdateScore(int newScore)
    {
        if (scoreText != null)
            scoreText.text = $"Score: {newScore}";
    }
}