using System;
using System.Collections.Generic;
using UnityEngine;

// replace this file entirely during integration with fpga
// this dummy script uses a timer to cycle between hand gestures
public class MockGestureProvider : MonoBehaviour
{
    [Header("Timer Settings")]
    [Tooltip("How many seconds before simulating a new gesture input")]
    public float switchInterval = 3.0f;
    private float timer = 0f;
    
    public readonly List<string> gestureNames = new List<string> { "Idle", "RightTuo", "RightIndex", "RightMiddle", "RightRing", "RightPinky", "RightYaoZhi", "RightMute" };
    private int currentState = 0;

    public event Action<string> OnGestureReceived; // broadcast the gesture string to anyone listening

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= switchInterval)
        {
            timer = 0f;
            CycleNextGesture();
        }
    }

    void CycleNextGesture()
    {
        currentState++;
        if (currentState >= gestureNames.Count) currentState = 0;

        string detectedGesture = gestureNames[currentState];
        
        OnGestureReceived?.Invoke(detectedGesture); // fire the event, making it available to anyone listening
    }
}