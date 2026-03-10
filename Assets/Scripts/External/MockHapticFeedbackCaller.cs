using System;
using UnityEngine;

public class MockHapticFeedbackCaller : MonoBehaviour
{
    public static MockHapticFeedbackCaller Instance { get; private set; }

    public event Action OnDamageReceived;

    private bool _triggerSignal;
    public bool TriggerSignal
    {
        get => _triggerSignal;
        set
        {
            if (value == true)
                OnDamageReceived?.Invoke();
            
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
        OnDamageReceived += HandleHapticEventTriggered; // subscribe
    }

    private void OnDisable()
    {
        OnDamageReceived -= HandleHapticEventTriggered; //  unsubscribe to prevent memory leaks
    }

    private void HandleHapticEventTriggered()
    {
        // replace with MQTT publishing logic for haptic feedback
        Debug.Log("HapticFeedbackCaller: damage taken!");
    }
}