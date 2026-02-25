using UnityEngine;

public class RightHandAnimationController : StateListener
{
    [Header("Component References")]
    public Animator handAnimator;
    public MockGestureProvider gestureProvider; // to be changed later to get information from fpga

    void Start()
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

        // we need to handle the animations in every state, so isNowActive is irrelevant
        if (GameManager.Instance != null)
            HandleGameStateChanged(GameManager.Instance.CurrentState);
    }

    private void HandleGameStateChanged(GameManager.GameState newState)
    {
        switch (newState)
        {
            case GameManager.GameState.StartMenu:
            case GameManager.GameState.Paused:
                UnsubscribeFromGestures();
                handAnimator.SetTrigger("PointerTrigger");
                break;

            case GameManager.GameState.Playing:
                SubscribeToGestures();
                break;

            default: // all other states
                UnsubscribeFromGestures();
                handAnimator.SetTrigger("IdleTrigger");
                break;
        }
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