using System;
using UnityEngine;

public class MockHardwareDataPoller : MonoBehaviour
{
    public static MockHardwareDataPoller Instance { get; private set; }

    public event Action OnHardwareSignalTriggered;

    private bool _triggerSignal;
    public bool TriggerSignal
    {
        get => _triggerSignal;
        set
        {
            if (value == true)
                OnHardwareSignalTriggered?.Invoke();
            
            _triggerSignal = false;  // reset for the next trigger
        }
    }

    private void Awake()
    {
        // singleton setup
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void OnEnable()
    {
        OnHardwareSignalTriggered += HandleSignalTriggered; // subscribe
    }

    private void OnDisable()
    {
        OnHardwareSignalTriggered -= HandleSignalTriggered; //  unsubscribe to prevent memory leaks
    }

    private void HandleSignalTriggered()
    {
        // replace with MQTT publishing logic
        Debug.Log("HardwareDataPoller: Guzheng string plucked! Sending MQTT signal...");
    }
}