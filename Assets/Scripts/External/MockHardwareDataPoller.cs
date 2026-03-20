using System;
using System.Collections.Generic;
using M2MqttUnity.Examples;
using UnityEngine;

public class MockHardwareDataPoller : MonoBehaviour
{
    public static MockHardwareDataPoller Instance { get; private set; }

    private HashSet<GuzhengStringInteraction> touchedStrings = new HashSet<GuzhengStringInteraction>();
    private bool isCurrentlyStreaming = false;

    private void Awake()
    {
        // singleton setup
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public void UpdateStringState(GuzhengStringInteraction guzhengString, bool isTouched)
    {
        if (isTouched)
            touchedStrings.Add(guzhengString); // HashSet automatically ignores duplicates
        else
            touchedStrings.Remove(guzhengString);

        EvaluateStreamState();
    }

    private void EvaluateStreamState()
    {
        bool shouldStream = touchedStrings.Count > 0;

        if (shouldStream != isCurrentlyStreaming)
        {
            isCurrentlyStreaming = shouldStream;
            
            Debug.Log($"HardwareDataPoller: Guzheng strings touched: {touchedStrings.Count}. Stream state changing to {isCurrentlyStreaming}");

            if (M2MqttUnityTest.Instance != null) 
                M2MqttUnityTest.Instance.SetStreamState(isCurrentlyStreaming, "FB_001");
        }
    }
}