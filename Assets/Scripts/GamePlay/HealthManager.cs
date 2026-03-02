using UnityEngine;

public class HealthManager : StateListener
{
    [Header("UI Reference")]
    public GameplayUIStats uiStatsManager;
    public GameManager gameManager;

    [Header("Player (Guzheng) Health")]
    public int playerMaxHealth = 100;
    public int playerCurrentHealth;

    void Awake()
    {
        ResetHealth();
    }

    protected override void OnStateToggled(bool isNowActive)
    {
        if (isNowActive && !targetStates.Contains(StateManager.Instance.PreviousState))
        {
            ResetHealth();
        }
    }

    public void ResetHealth()
    {
        playerCurrentHealth = playerMaxHealth;

        if (uiStatsManager != null)
            uiStatsManager.UpdatePlayerHealth(playerCurrentHealth, playerMaxHealth);
    }

    public void DamagePlayer(int amount)
    {
        playerCurrentHealth -= amount;

        if (uiStatsManager != null) uiStatsManager.UpdatePlayerHealth(playerCurrentHealth, playerMaxHealth);

        if (playerCurrentHealth <= 0)
        {
            playerCurrentHealth = 0;
            gameManager.HandleGameOver(false);
        }
    }
}