using UnityEngine;

public class HandAnimationController : StateListener
{
    [Header("Component References")]
    public Animator handAnimator;
    public MockGestureProvider gestureProvider; // for testing standalone
    // public GestureProvider gestureProvider;

    [Header("Hand Identity")]
    public HandType myHandType;

    private string _currentTrigger = "IdleTrigger";
    public string[] gestureTriggersToReset =
    {
        "IdleTrigger", "PointerTrigger", "TuoTrigger", "IndexTrigger", "MiddleTrigger", "RingTrigger", "PinkyTrigger", "YaoZhiTrigger", "MuteTrigger", "DragonClawTrigger", 
        "CraneWingTrigger", "BuddhaChopTrigger", "PunchTrigger", "SnakeStrikeTrigger"
    };

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
            ResetTriggers();
            handAnimator.SetTrigger(triggerName);
        }
    }

    public void OnHandBecameVisible()
    {
        if (handAnimator == null) return;

        ResetTriggers();
        handAnimator.SetTrigger(_currentTrigger);
    }

    private void ResetTriggers()
    {
        foreach (string trigger in gestureTriggersToReset)
            handAnimator.ResetTrigger(trigger);
    }

    private void HandleGestureDetected(HandType targetHand, string detectedGesture)
    {
        if (targetHand != myHandType) return;
        ResetTriggers();

        // e.g., "Tuo" + "Trigger" = "TuoTrigger"
        _currentTrigger = detectedGesture + "Trigger";
        handAnimator.SetTrigger(_currentTrigger);
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