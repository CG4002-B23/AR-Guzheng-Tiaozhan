using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class ARStateManager : MonoBehaviour
{
    [Header("AR Components")]
    public ARTrackedImageManager imageManager;
    public ARPlaneManager planeManager;
    public ARAnchorManager anchorManager;

    void OnEnable()
    {
        GameManager.OnGameStateChanged += HandleGameStateChange;
    }

    void OnDisable()
    {
        GameManager.OnGameStateChanged -= HandleGameStateChange;
    }

    private void HandleGameStateChange(GameManager.GameState newState)
    {
        switch (newState)
        {
            case GameManager.GameState.StartMenu:
            case GameManager.GameState.Paused:
                ToggleARFeatures(isGuzhengMarkerDetectionEnabled: false, isPlanesEnabled: false, isAnchorsEnabled: false);
                break;

            case GameManager.GameState.GuzhengPlacing:
                ToggleARFeatures(isGuzhengMarkerDetectionEnabled: true, isPlanesEnabled: false, isAnchorsEnabled: true);
                break;

            case GameManager.GameState.PlayingFieldScanning:
                ToggleARFeatures(isGuzhengMarkerDetectionEnabled: false, isPlanesEnabled: true, isAnchorsEnabled: true);
                break;

            case GameManager.GameState.Playing:
                ToggleARFeatures(isGuzhengMarkerDetectionEnabled: false, isPlanesEnabled: false, isAnchorsEnabled: true);
                break;
        }
    }

    private void ToggleARFeatures(bool isGuzhengMarkerDetectionEnabled, bool isPlanesEnabled, bool isAnchorsEnabled)
    {
        if (imageManager != null) imageManager.enabled = isGuzhengMarkerDetectionEnabled;
        if (planeManager != null) planeManager.enabled = isPlanesEnabled;
        if (anchorManager != null) anchorManager.enabled = isAnchorsEnabled;
    }
}