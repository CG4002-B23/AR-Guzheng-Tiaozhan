using UnityEngine;

public class ScoreManager : StateListener
{
    [Header("Score Tracking")]
    public int currentScore = 0;

    [Header("Hit Thresholds (Distance from Guzheng)")]
    [Tooltip("Distance closer than this is a Perfect hit")]
    public float perfectThreshold = 0.3f; 
    [Tooltip("Distance between Perfect and this value is a Good hit")]
    public float goodThreshold = 0.8f;    

    [Header("Points Awarded")]
    public int perfectPoints = 3;
    public int goodPoints = 1;

    void Awake()
    {
        ResetScore();
    }

    protected override void OnStateToggled(bool isNowActive)
    {
        if (isNowActive)
        {
            ResetScore();
        }
    }

    public void ResetScore()
    {
        currentScore = 0;
        Debug.Log("Score reset to 0.");
    }

    public void RegisterHit(float distanceAtImpact)
    {
        if (distanceAtImpact <= perfectThreshold)
        {
            currentScore += perfectPoints;
            Debug.Log($"<color=yellow>PERFECT!</color> Distance: {distanceAtImpact:F2} | Score: {currentScore}");
        }
        else if (distanceAtImpact <= goodThreshold)
        {
            currentScore += goodPoints;
            Debug.Log($"<color=green>GOOD!</color> Distance: {distanceAtImpact:F2} | Score: {currentScore}");
        }
        else
        {
            Debug.Log($"<color=gray>FAIR.</color> Distance: {distanceAtImpact:F2} | Score: {currentScore}");
        }
    }
}