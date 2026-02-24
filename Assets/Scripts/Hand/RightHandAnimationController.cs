using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RightHandAnimationController : MonoBehaviour
{
    [Header("Component References")]
    public Animator handAnimator;

    public MockGestureProvider gestureProvider; // to be changed later to get information from fpga

    void Start()
    {
        if (handAnimator == null)
        {
            handAnimator = GetComponent<Animator>();
        }
        if (gestureProvider != null)
        {
            gestureProvider.OnGestureReceived += HandleGestureDetected;
        }
    }

    void OnDestroy()
    {
        if (gestureProvider != null)
        {
            gestureProvider.OnGestureReceived -= HandleGestureDetected;
        }
    }

    private void HandleGestureDetected(string detectedGesture)
    {
        // e.g., "RightTuo" + "Trigger" = "RightTuoTrigger"
        handAnimator.SetTrigger(detectedGesture + "Trigger");
    }
}