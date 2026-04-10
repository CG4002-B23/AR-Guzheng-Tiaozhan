using UnityEngine;

public class ScoreManager : StateListener
{
    [Header("UI Reference")]
    public GameplayUIStats uiStatsManager;

    [Header("UI Popups")]
    public GameObject floatingTextPrefab;

    [Header("Score Tracking")]
    public int currentScore = 0;

    [Header("Ultimate Meter Settings")]
    public UltimateMeterManager ultimateMeter;
    public int perfectChargeAmount = 10;
    public int goodChargeAmount = 5;

    [Header("Hit Thresholds (Distance from Guzheng)")]
    [Tooltip("Distance closer than this is a Perfect hit")]
    public float perfectThreshold = 0.3f; 
    [Tooltip("Distance between Perfect and this value is a Good hit")]
    public float goodThreshold = 0.8f;    

    [Header("Points Awarded")]
    public int perfectPoints = 3;
    public int goodPoints = 1;
    public int ultimatePoints = 5;

    void Start()
    {
        ResetScore();
    }

    protected override void OnStateToggled(bool isNowActive)
    {
        if (isNowActive && !targetStates.Contains(StateManager.Instance.PreviousState))
        {
            ResetScore();
        }
    }

    public void ResetScore()
    {
        currentScore = 0;
        if (uiStatsManager != null) uiStatsManager.UpdateScore(currentScore);
        if (ultimateMeter!= null) ultimateMeter.ConsumeUltimate();
    }

    public void RegisterHit(float distanceAtImpact, Vector3 hitPosition)
    {
        string popupText = "";
        Color popupColor = Color.white;

        if (distanceAtImpact <= perfectThreshold)
        {
            currentScore += perfectPoints;
            popupText = "PERFECT!";
            popupColor = Color.yellow;
            if (ultimateMeter != null) ultimateMeter.AddCharge(perfectChargeAmount);
            Debug.Log($"<color=yellow>PERFECT!</color> Distance: {distanceAtImpact:F2} | Score: {currentScore}");
        }
        else if (distanceAtImpact <= goodThreshold)
        {
            currentScore += goodPoints;
            popupText = "GOOD!";
            popupColor = Color.green;
            if (ultimateMeter != null) ultimateMeter.AddCharge(goodChargeAmount);
            Debug.Log($"<color=green>GOOD!</color> Distance: {distanceAtImpact:F2} | Score: {currentScore}");
        }
        else
        {
            popupText = "FAIR";
            popupColor = Color.gray;
            Debug.Log($"<color=gray>FAIR.</color> Distance: {distanceAtImpact:F2} | Score: {currentScore}");
        }

        if (floatingTextPrefab != null && popupText != "")
        {
            Vector3 spawnPos = hitPosition + (Vector3.up * 0.2f);
            GameObject popup = Instantiate(floatingTextPrefab, spawnPos, Quaternion.identity);
            
            popup.GetComponent<FloatingText>().Setup(popupText, popupColor);
        }

        if (uiStatsManager != null) uiStatsManager.UpdateScore(currentScore);
    }

    public void AddUltimateScore(Vector3 hitPosition)
    {
        currentScore += ultimatePoints;

        if (floatingTextPrefab != null)
        {
            Vector3 spawnPos = hitPosition + (Vector3.up * 0.2f);
            GameObject popup = Instantiate(floatingTextPrefab, spawnPos, Quaternion.identity);
            popup.GetComponent<FloatingText>().Setup("ULTIMATE!", Color.cyan);
        }

        if (uiStatsManager != null) uiStatsManager.UpdateScore(currentScore);
        Debug.Log($"<color=cyan>ULTIMATE!</color> Score: {currentScore}");
    }
}