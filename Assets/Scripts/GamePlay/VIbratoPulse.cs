using UnityEngine;

public class VibratoPulse : MonoBehaviour
{
    [Header("Pulse Settings")]
    [Tooltip("How fast the aura breathes in and out")]
    public float pulseSpeed = 5f;
    
    [Tooltip("Smallest scale multiplier")]
    public float minScale = 1.1f;
    
    [Tooltip("Largest scale multiplier")]
    public float maxScale = 1.4f;

    private Vector3 initialScale;

    void Start()
    {
        initialScale = transform.localScale;
    }

    void Update()
    {
        float wave = (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f; 
        float currentScaleMultiplier = Mathf.Lerp(minScale, maxScale, wave); // linear interpolation
        transform.localScale = initialScale * currentScaleMultiplier; // apply uniformly
    }
}