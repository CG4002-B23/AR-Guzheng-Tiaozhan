using UnityEngine;

public class GuzhengAlignmentChecker : StateListener
{
    [Header("Spawners")]
    [HideInInspector]
    public ARStringSpawner guzhengSpawner;
    [HideInInspector]
    public ARStringSpawner enemySpawner;

    [Header("Alignment Settings")]
    [Tooltip("How many degrees of forgiveness for horizontal alignment")]
    public float alignmentToleranceDegrees = 10.0f;

    void Update()
    {
        if (!isActiveState) return;
        if (guzhengSpawner == null || enemySpawner == null) return;

        CheckStringsAlignment();
    }

    void CheckStringsAlignment()
    {
        Vector3 guzhengForward = Vector3.ProjectOnPlane(guzhengSpawner.transform.forward, Vector3.up).normalized;
        Vector3 dirToEnemy = Vector3.ProjectOnPlane(enemySpawner.transform.position - guzhengSpawner.transform.position, Vector3.up).normalized;
        float angle = Vector3.Angle(guzhengForward, dirToEnemy);

        if (angle <= alignmentToleranceDegrees) // transition to playing if aligned
        {
            Debug.Log("angle: " + angle);
            Debug.Log("tolerance deg: " + alignmentToleranceDegrees);
            GameManager.Instance.ChangeState(GameManager.GameState.Playing);
        }
    }
}