using UnityEngine;

public class UltimateKnife : MonoBehaviour
{
    [System.Serializable]
    public struct GestureIconMapping
    {
        public string gestureName;
        public Sprite icon;
    }

    [Header("Model & Animation")]
    public Transform knifeMesh;
    public Vector3 meshRotationOffset = new Vector3(0, -90, 0);
    public float floatSpeed = 3f;
    public float floatHeight = 0.03f;
    private Vector3 startPosition;

    [Header("Gesture Settings")]
    public SpriteRenderer iconRenderer;
    public GestureIconMapping[] possibleGestures;
    private string requiredGesture;

    [Header("Combat Settings")]
    public float flySpeed = 20f;
    public GameObject hitParticlePrefab;

    // State & Dependencies
    private bool isFired = false;
    private Transform targetEnemy;
    private UltimateMeterManager meterManager;
    private MockGestureProvider gestureProvider;

    public void Initialize(Transform enemy, UltimateMeterManager meter, MockGestureProvider provider)
    {
        targetEnemy = enemy;
        meterManager = meter;
        gestureProvider = provider;

        if (gestureProvider != null) gestureProvider.OnGestureReceived += CheckGesture;
    }

    void Start()
    {
        startPosition = transform.position;

        if (knifeMesh != null)
            knifeMesh.localEulerAngles = meshRotationOffset;

        // Select random gesture requirement
        if (possibleGestures != null && possibleGestures.Length > 0)
        {
            int randomIndex = Random.Range(0, possibleGestures.Length);
            requiredGesture = possibleGestures[randomIndex].gestureName;
            
            if (iconRenderer != null)
                iconRenderer.sprite = possibleGestures[randomIndex].icon;
        }
    }

    void Update()
    {
        if (!isFired)
        {
            // Float up and down while waiting
            float newY = startPosition.y + (Mathf.Sin(Time.time * floatSpeed) * floatHeight);
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
            
            // Keep looking at the enemy even if they move
            if (targetEnemy != null) transform.LookAt(targetEnemy);
        }
        else
        {
            // Fly towards the enemy
            if (targetEnemy != null)
                transform.position = Vector3.MoveTowards(transform.position, targetEnemy.position, flySpeed * Time.deltaTime);
        }
    }

    private void CheckGesture(HandType hand, string detectedGesture)
    {
        if (isFired) return; 
        
        Debug.Log($"detectedGesture: {detectedGesture}, requiredGesture: {requiredGesture}");

        if (detectedGesture == requiredGesture)
            FireKnife();
    }

    private void FireKnife()
    {
        isFired = true;
        
        if (iconRenderer != null) iconRenderer.enabled = false;
        if (meterManager != null) meterManager.ConsumeUltimate();
        if (gestureProvider != null) gestureProvider.OnGestureReceived -= CheckGesture;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only react if we are flying and hit the enemy
        if (isFired && other.CompareTag("Enemy"))
        {
            // Spawn particle effect
            if (hitParticlePrefab != null)
                Instantiate(hitParticlePrefab, transform.position, Quaternion.identity);

            // TODO: Add to the score here

            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (gestureProvider != null) gestureProvider.OnGestureReceived -= CheckGesture;
    }
}