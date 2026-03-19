using UnityEngine;

public class RightHandAnimationController : StateListener
{
    [Header("Component References")]
    public Animator handAnimator;
    public GestureProvider gestureProvider;

    private string _currentTrigger = "IdleTrigger";

    void Awake()
    {
        if (handAnimator == null)
            handAnimator = GetComponent<Animator>();
    }

    void OnDestroy()
    {
        if (gestureProvider != null)
            gestureProvider.OnGestureReceived -= HandleGestureDetected;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        StateManager.OnTutorialPauseToggled += HandleTutorialPause;
    }

    protected override void OnDisable()
    {
        base.OnDisable(); 
        StateManager.OnTutorialPauseToggled -= HandleTutorialPause;
    }

    private void HandleTutorialPause(bool isPaused)
    {
        if (isPaused)
        {
            UnsubscribeFromGestures();
            SetHandTrigger("PointerTrigger");
        }
        else
        {
            if (StateManager.Instance != null)
                ChangeHandGesture(StateManager.Instance.CurrentState);
        }
    }

    protected override void OnStateToggled(bool isNowActive)
    {
        base.OnStateToggled(isNowActive);
        Debug.Log($"OnStateToggled called, current state: {StateManager.Instance.CurrentState}");

        if (StateManager.Instance != null && StateManager.Instance.IsTutorialPaused) return;

        ChangeHandGesture(StateManager.Instance.CurrentState);
    }

    private void ChangeHandGesture(StateManager.GameState newState)
    {
        if (handAnimator == null) return;

        switch (newState)
        {
            case StateManager.GameState.StartMenu:
            case StateManager.GameState.Paused:
            case StateManager.GameState.Victory:
            case StateManager.GameState.Defeat:
                UnsubscribeFromGestures();
                SetHandTrigger("PointerTrigger");
                break;

            case StateManager.GameState.Playing:
                SetHandTrigger("IdleTrigger");
                SubscribeToGestures();
                break;

            default:
                UnsubscribeFromGestures();
                SetHandTrigger("IdleTrigger");
                break;
        }
    }

    private void SetHandTrigger(string triggerName)
    {
        _currentTrigger = triggerName; // always store it regardless of hand visibility

        if (handAnimator.gameObject.activeInHierarchy)
        {
            handAnimator.ResetTrigger("PointerTrigger");
            handAnimator.ResetTrigger("IdleTrigger");
            handAnimator.ResetTrigger("RightTuoTrigger");
            handAnimator.SetTrigger(triggerName);
        }
    }

    public void OnHandBecameVisible()
    {
        if (handAnimator == null) return;

        handAnimator.ResetTrigger("PointerTrigger");
        handAnimator.ResetTrigger("IdleTrigger");
        handAnimator.ResetTrigger("RightTuoTrigger");
        handAnimator.SetTrigger(_currentTrigger);
    }

    private void HandleGestureDetected(string detectedGesture)
    {
        // e.g., "RightTuo" + "Trigger" = "RightTuoTrigger"
        handAnimator.SetTrigger(detectedGesture + "Trigger");
    }

    private void SubscribeToGestures()
    {
        if (gestureProvider == null) return;
        gestureProvider.OnGestureReceived -= HandleGestureDetected; // avoid double-subscribing
        gestureProvider.OnGestureReceived += HandleGestureDetected;
    }

    private void UnsubscribeFromGestures()
    {
        if (gestureProvider == null) return;
        gestureProvider.OnGestureReceived -= HandleGestureDetected;
    }
}