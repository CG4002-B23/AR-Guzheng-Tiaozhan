using UnityEngine;

// Controls the right hand pointer animation based on game state, and whether it is near the pause button
// Also checks if the finger is hovering over a button
public class PointerController : StateListener
{
    [Header("Component References")]
    public Animator handAnimator;

    [Header("Fingertip Tracking")]
    [Tooltip("Transform representing the right hand index fingertip in world space")]
    public Transform indexFingertipTransform;

    [Header("UI References")]
    [Tooltip("RectTransform of the pause button in the top-left of the screen")]
    public RectTransform pauseButtonRectTransform;

    [Tooltip("The Canvas on which the pause button lives (needed for overlay/camera space)")]
    public Canvas pauseButtonCanvas;

    private bool _isTriggerActive = false;

    void Update()
    {
        if (!isActiveState) return;
        if (GameManager.Instance == null || handAnimator == null) return;

        bool shouldTrigger = ShouldActivatePointerTrigger(GameManager.Instance.CurrentState);

        // only fire the trigger on the rising edge
        if (shouldTrigger && !_isTriggerActive)
            handAnimator.SetTrigger("PointerTrigger");

        _isTriggerActive = shouldTrigger;
    }

    private bool ShouldActivatePointerTrigger(GameManager.GameState state)
    {
        if (state == GameManager.GameState.StartMenu ||
            state == GameManager.GameState.Paused)
        {
            return true;
        }
        
        // in-game states
        return IsFingertipOverPauseButton();
    }

    private bool IsFingertipOverPauseButton()
    {
        if (indexFingertipTransform == null || pauseButtonRectTransform == null ||
            Camera.main == null) return false;

        // convert fingertip's 3d world position to 2d screen coordinates
        Vector3 screenPoint = Camera.main.WorldToScreenPoint(indexFingertipTransform.position);

        // if the fingertip is behind the camera, ignore it
        if (screenPoint.z < 0f) return false;

        // // Determine the correct camera to pass based on the canvas render mode
        Camera uiCamera = null;
        // if (pauseButtonCanvas != null &&
        //     pauseButtonCanvas.renderMode == RenderMode.ScreenSpaceCamera)
        // {
        //     uiCamera = pauseButtonCanvas.worldCamera;
        // }
        // // For ScreenSpaceOverlay, uiCamera stays null (which is correct for the Unity API)

        return RectTransformUtility.RectangleContainsScreenPoint(
            pauseButtonRectTransform,
            screenPoint,
            uiCamera
        );
    }
}
