using System;
using System.Collections.Generic;
using UnityEngine;
using M2MqttUnity.Examples;

public class GestureProvider : MonoBehaviour
{
    [Header("Prediction Settings")]
    [Tooltip("Minimum confidence required to trigger a gesture (0.0 to 1.0)")]
    public float confidenceThreshold = 0.6f; 

    // list of all the gesture names 
    public readonly List<string> gestureNames = new List<string> { 
        "Idle", "RightTuo", "RightIndex", "RightMiddle", "RightRing", "RightPinky", "RightYaoZhi", "RightMute" 
    };

    [Header("Class Mapping")]
    [Tooltip("Map the incoming MQTT integer to the correct gesture string.")]
    public string[] classMapping = new string[] {
        "Idle",         // 0
        "RightTuo",     // 1
        "RightIndex",   // 2
        "RightMiddle",  // 3
        "RightRing",    // 4
        "RightPinky",   // 5
        "RightYaoZhi",  // 6
        "RightMute"     // 7
    };

    public event Action<string> OnGestureReceived;

    private void Start()
    {
        if (M2MqttUnityTest.Instance != null)
            M2MqttUnityTest.Instance.OnPredictionReceived += HandlePrediction;
        else
            Debug.LogError("GestureProvider: M2MqttUnityTest Instance not found! Is it active in the scene?");
    }

    private void OnDestroy()
    {
        if (M2MqttUnityTest.Instance != null)
            M2MqttUnityTest.Instance.OnPredictionReceived -= HandlePrediction;
    }

    private void HandlePrediction(M2MqttUnityTest.PredictionMessage predMsg)
    {
        if (predMsg.confidence < confidenceThreshold) return;

        int predClass = predMsg.prediction;
        
        if (predClass >= 0 && predClass < classMapping.Length)
        {
            string detectedGesture = classMapping[predClass];
            OnGestureReceived?.Invoke(detectedGesture);
        }
        else
        {
            Debug.LogWarning($"GestureProvider: Received unmapped prediction class index: {predClass}");
        }
    }
}