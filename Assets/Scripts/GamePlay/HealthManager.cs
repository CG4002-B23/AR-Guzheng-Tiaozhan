using UnityEngine;

public class HealthManager : StateListener
{
    [Header("UI Reference")]
    public GameplayUIStats uiStatsManager;

    [Header("Player (Guzheng) Health")]
    public int playerMaxHealth = 100;
    public int playerCurrentHealth;

    [Header("Enemy Health")]
    public int enemyMaxHealth = 100;
    public int enemyCurrentHealth;

    void Awake()
    {
        ResetHealth();
    }

    protected override void OnStateToggled(bool isNowActive)
    {
        if (isNowActive)
        {
            ResetHealth();
        }
    }

    public void ResetHealth()
    {
        playerCurrentHealth = playerMaxHealth;
        enemyCurrentHealth = enemyMaxHealth;

        if (uiStatsManager != null)
        {
            uiStatsManager.UpdatePlayerHealth(playerCurrentHealth, playerMaxHealth);
            uiStatsManager.UpdateEnemyHealth(enemyCurrentHealth, enemyMaxHealth);
        }
    }

    public void DamagePlayer(int amount)
    {
        playerCurrentHealth -= amount;

        if (uiStatsManager != null) uiStatsManager.UpdatePlayerHealth(playerCurrentHealth, playerMaxHealth);

        if (playerCurrentHealth <= 0)
        {
            playerCurrentHealth = 0;
            HandleGameOver(false);
        }
    }

    public void DamageEnemy(int amount)
    {
        enemyCurrentHealth -= amount;

        if (uiStatsManager != null) uiStatsManager.UpdateEnemyHealth(enemyCurrentHealth, enemyMaxHealth);

        if (enemyCurrentHealth <= 0)
        {
            enemyCurrentHealth = 0;
            HandleGameOver(true);
        }
    }

    private void HandleGameOver(bool playerWon)
    {
        if (playerWon)
        {
            Debug.Log("Enemy defeated! Player Wins!");
            // game manager switch to victory state
        }
        else
        {
            Debug.Log("Player defeated! Game Over.");
            // game manager switch to game over state
        }
    }
}