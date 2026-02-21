using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RightHandAnimationController : MonoBehaviour
{
    [Header("Component References")]
    public Animator handAnimator;

    [Header("Timer Settings")]
    [Tooltip("How many seconds before switching animations")]
    public float switchInterval = 3.0f;
    private float timer = 0f;
    
    // 0 = Idle, 1 = Pluck, 2 = Pointer
    private int currentState = 0; 

    void Start()
    {
        if (handAnimator == null)
        {
            handAnimator = GetComponent<Animator>();
        }
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= switchInterval)
        {
            timer = 0f;
            CycleNextAnimation();
        }
    }

    void CycleNextAnimation()
    {
        currentState++;
        if (currentState > 2) currentState = 0;

        switch (currentState)
        {
            case 0:
                handAnimator.SetTrigger("IdleTrigger");
                break;
            case 1:
                handAnimator.SetTrigger("RightTuoTrigger"); // right tuo has an automatic exit time
                break;
            case 2:
                handAnimator.SetTrigger("PointerTrigger");
                break;
        }
    }
}