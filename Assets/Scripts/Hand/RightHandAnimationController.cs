using UnityEngine;

public class RightHandAnimationController : StateListener
{
    [Header("Component References")]
    public Animator handAnimator;
    public MockGestureProvider gestureProvider; // to be changed later to get information from fpga

    [Header("Fingertip Tracking")]
    [Tooltip("Transform representing the right hand index fingertip in world space")]
    public Transform indexFingertipTransform;

    [Header("UI References")]
    [Tooltip("RectTransform of the pause button in the top-left of the screen")]
    public RectTransform pauseButtonRectTransform;

    [Tooltip("The Canvas on which the pause button lives (needed for overlay/camera space)")]
    public Canvas pauseButtonCanvas;

    private bool _isPointerTriggerActive = false;
    private GameManager.GameState currentGameState = GameManager.GameState.Initialising;

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

    void Update()
    {
        if (!isActiveState || GameManager.Instance == null || handAnimator == null) return;

        currentGameState = GameManager.Instance.CurrentState;
        bool shouldTriggerPointer = ShouldActivatePointerTrigger(currentGameState);

        // only fire the trigger on the rising edge
        if (shouldTriggerPointer && !_isPointerTriggerActive)
            handAnimator.SetTrigger("PointerTrigger");

        _isPointerTriggerActive = shouldTriggerPointer;
    }

    protected override void OnStateToggled(bool isNowActive)
    {
        base.OnStateToggled(isNowActive);

        if (gestureProvider == null) return;
        if (isNowActive && currentGameState == GameManager.GameState.Playing) // only subscribe to gestureProvider when in Playing state
            gestureProvider.OnGestureReceived += HandleGestureDetected;
        else
            gestureProvider.OnGestureReceived -= HandleGestureDetected;
    }

    private void HandleGestureDetected(string detectedGesture)
    {
        // e.g., "RightTuo" + "Trigger" = "RightTuoTrigger"
        handAnimator.SetTrigger(detectedGesture + "Trigger");
    }

    private bool ShouldActivatePointerTrigger(GameManager.GameState state)
    {
        // always point in menu/paused states
        if (state == GameManager.GameState.StartMenu ||
            state == GameManager.GameState.Paused)
            return true;

        // in-game states: only point when hovering over the pause button
        return IsFingertipOverPauseButton();
    }

    private bool IsFingertipOverPauseButton()
    {
        if (indexFingertipTransform == null || pauseButtonRectTransform == null ||
            Camera.main == null) return false;

        // convert fingertip's 3D world position to 2D screen coordinates
        Vector3 screenPoint = Camera.main.WorldToScreenPoint(indexFingertipTransform.position);

        // if the fingertip is behind the camera, ignore it
        if (screenPoint.z < 0f) return false;

        // uiCamera stays null for ScreenSpaceOverlay (correct for the Unity API)
        Camera uiCamera = null;

        return RectTransformUtility.RectangleContainsScreenPoint(
            pauseButtonRectTransform,
            screenPoint,
            uiCamera
        );
    }
}