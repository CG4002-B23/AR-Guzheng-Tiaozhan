using System;
using System.Collections.Generic;
using UnityEngine;
using M2MqttUnity.Examples;

public class GestureProvider : MonoBehaviour
{
    [Header("Prediction Settings")]
    [Tooltip("Minimum confidence required to trigger a gesture (0.0 to 1.0)")]
    public float confidenceThreshold = 0.6f; 

    [Header("Class Mapping")]
    [Tooltip("Map the incoming MQTT integer to the correct gesture string.")]
    public List<string> gestureNames = new List<string> {
        "Idle",        // 0
        "Tuo",         // 1
        "Index",       // 2
        "Middle",      // 3
        "DragonClaw",  // 4
        "CraneWing",   // 5
        "BuddhaChop",  // 6
        "Punch",       // 7
        "SnakeStrike", // 8
    };

    public event Action<HandType, string> OnGestureReceived;

    private void Start()
    {
        if (M2MqttUnityTest.Instance != null)
            M2MqttUnityTest.Instance.OnPredictionReceived += HandlePrediction;
        else
            Debug.LogError("GestureProvider: M2MqttUnityTest Instance not found! Is it active in the scene?");

        Debug.Log("GestureProvider started");
    }

    private void OnDestroy()
    {
        if (M2MqttUnityTest.Instance != null)
            M2MqttUnityTest.Instance.OnPredictionReceived -= HandlePrediction;
    }

    private void HandlePrediction(M2MqttUnityTest.PredictionMessage predMsg)
    {
        if (predMsg.confidence < confidenceThreshold) return;

        HandType hand = (predMsg.device_id == "FB_001") ? HandType.Left : HandType.Right;
        int predClass = predMsg.prediction;

        if (predClass == 0) return;
        if (predClass >= 0 && predClass < gestureNames.Count)
        {
            string detectedGesture = gestureNames[predClass];
            Debug.Log($"GestureProvider: received prediction: {detectedGesture} on hand: {hand}");
            OnGestureReceived?.Invoke(hand, detectedGesture);
        }
        else
        {
            Debug.LogWarning($"GestureProvider: Received unmapped prediction class index: {predClass}");
        }
    }
}