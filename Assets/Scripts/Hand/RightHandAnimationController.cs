using UnityEngine;

public class RightHandAnimationController : StateListener
{
    [Header("Component References")]
    public Animator handAnimator;
    public MockGestureProvider gestureProvider; // to be changed later to get information from fpga

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

    protected override void OnStateToggled(bool isNowActive)
    {
        base.OnStateToggled(isNowActive);
        Debug.Log($"OnStateToggled called, current state: {GameManager.Instance.CurrentState}");

        // we need to handle the animations in every state, so isNowActive is irrelevant
        ChangeHandGesture(GameManager.Instance.CurrentState);
    }

    private void ChangeHandGesture(GameManager.GameState newState)
    {
        if (handAnimator == null) return;

        switch (newState)
        {
            case GameManager.GameState.StartMenu:
            case GameManager.GameState.Paused:
                UnsubscribeFromGestures();
                SetHandTrigger("PointerTrigger");
                break;

            case GameManager.GameState.Playing:
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