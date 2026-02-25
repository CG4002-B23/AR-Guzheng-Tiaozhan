using UnityEngine;

public class PauseController : StateListener
{
    [Header("Tracking References")]
    public Transform hand; // spawned hand rig
    public Camera mainCamera; // AR camera

    [Header("Button References")]
    public RectTransform pauseButtonRect;
    
    void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    void Update()
    {
        if (!isActiveState || hand == null) return;

        Vector2 screenPos = mainCamera.WorldToScreenPoint(hand.position); // project hand position to screen coordinates
        // Debug.Log("Hand at position: " + screenPos.ToString());

        bool isTouchingPauseButton = RectTransformUtility.RectangleContainsScreenPoint(pauseButtonRect, screenPos, null);
        if (isTouchingPauseButton)
        {
            GameManager.Instance.ChangeState(GameManager.GameState.Paused);
        }
    }
}
