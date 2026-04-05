using System;
using System.Collections.Generic;
using UnityEngine;

// replace this file entirely during integration with fpga
// this dummy script uses a timer to cycle between hand gestures
public class MockGestureProvider : MonoBehaviour
{
    [Header("Timer Settings")]
    [Tooltip("How many seconds before simulating a new gesture input")]
    public float switchInterval = 1.0f;
    private float timer = 0f;
    
    [Header("Class Mapping")]
    [Tooltip("Map the incoming MQTT integer to the correct gesture string.")]
    public List<string> gestureNames = new List<string> {
        "Idle",    // 0
        "Tuo",     // 1
        "Index",   // 2
        "Middle",  // 3
        "Ring",    // 4
        "Pinky",   // 5
        "YaoZhi",  // 6
        "Mute",    // 7
        "DragonClaw",  // 8
        "CraneWing",   // 9
        "BuddhaChop",  // 10
        "Punch",       // 11
        "SnakeStrike", // 12
    };
    private int currentState = 0;

    public event Action<HandType, string> OnGestureReceived;// broadcast the gesture string to anyone listening

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
        
        HandType randomHand = (currentState % 2 == 0) ? HandType.Left : HandType.Right;
        OnGestureReceived?.Invoke(randomHand, detectedGesture); // fire the event, making it available to anyone listening
    }
}