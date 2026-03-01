using UnityEngine;
using TMPro;

public class GameOverUI : StateListener
{
    [Header("References")]
    public ScoreManager scoreManager;
    
    [Header("UI Text Elements")]
    public TextMeshProUGUI victoryScoreText;
    public TextMeshProUGUI defeatScoreText;

    protected override void OnStateToggled(bool isNowActive)
    {
        if (isNowActive && scoreManager != null)
            UpdateFinalScoreText();
    }

    private void UpdateFinalScoreText()
    {
        int finalScore = scoreManager.currentScore;

        if (victoryScoreText != null)
            victoryScoreText.text = $"Final Score: {finalScore}";

        if (defeatScoreText != null)
            defeatScoreText.text = $"Final Score: {finalScore}";
    }
}