using System;
using M2MqttUnity.Examples;
using UnityEngine;

public class MockHardwareDataPoller : MonoBehaviour
{
    public static MockHardwareDataPoller Instance { get; private set; }

    private void Awake()
    {
        // singleton setup
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public void SendPluckSignal(bool isPlucked)
    {
        Debug.Log("HardwareDataPoller: Guzheng string plucked! Sending MQTT signal...");

        if (isPlucked)
        {
            M2MqttUnityTest.Instance.SetStreamState(true, "FB_001");
            Debug.Log("String plucked");

        }
        else
        {
            M2MqttUnityTest.Instance.SetStreamState(false, "FB_001");
            Debug.Log("String not plucked");
        }
    }
}