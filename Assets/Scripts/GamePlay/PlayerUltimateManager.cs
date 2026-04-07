using UnityEngine;

public class PlayerUltimateManager : MonoBehaviour
{
    [Header("References")]
    public UltimateMeterManager ultimateMeter;
    public GameObject knifePrefab;
    public MockGestureProvider gestureProvider;
    // public GestureProvider gestureProvider;
    private Transform enemyCenter;

    private BoxCollider knifeSpawnArea;
    private GameObject spawnedKnife;

    void OnEnable()
    {
        if (ultimateMeter != null) 
            ultimateMeter.OnUltimateReady += SpawnUltimateKnife;
    }

    void OnDisable()
    {
        if (ultimateMeter != null) 
            ultimateMeter.OnUltimateReady -= SpawnUltimateKnife;
    }

    private void SpawnUltimateKnife()
    {
        if (spawnedKnife != null) return; // Prevent multiple spawns

        // Find the spawn area right before we need it
        if (knifeSpawnArea == null)
        {
            GuzhengController spawnedGuzheng = FindObjectOfType<GuzhengController>();
            if (spawnedGuzheng != null)
                knifeSpawnArea = spawnedGuzheng.knifeSpawnArea;
        }

        // dynamically get enemy's location
        if (enemyCenter == null)
        {
            LaneManager laneManager = FindObjectOfType<LaneManager>();
            if (laneManager != null && laneManager.enemySpawner != null)
                enemyCenter = laneManager.enemySpawner.transform;
        }

        if (knifeSpawnArea == null || knifePrefab == null)
        {
            Debug.LogWarning("Knife Prefab or Spawn Area is missing!");
            return;
        }

        // Calculate random position inside box collider
        Bounds bounds = knifeSpawnArea.bounds;
        Vector3 randomPosition = new Vector3(
            Random.Range(bounds.min.x, bounds.max.x),
            Random.Range(bounds.min.y, bounds.max.y),
            Random.Range(bounds.min.z, bounds.max.z)
        );

        // Spawn knife and make it face the enemy
        spawnedKnife = Instantiate(knifePrefab, randomPosition, Quaternion.identity);
        UltimateKnife knifeScript = spawnedKnife.GetComponent<UltimateKnife>();
        
        if (knifeScript != null)
            knifeScript.Initialize(enemyCenter, ultimateMeter, gestureProvider);
    }
}