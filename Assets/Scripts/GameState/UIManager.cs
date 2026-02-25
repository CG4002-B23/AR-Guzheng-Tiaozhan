using UnityEngine;
using UnityEngine.XR.ARFoundation;

// handles which button is clicked
public class UIManager : MonoBehaviour
{
    [Tooltip("GameStateManager object holding the GameManager script")]
    public GameManager gameManager;

    [Tooltip("ARSession object here")]
    public ARSession arSession;

    [Tooltip("Reference to the GuzhengAnchorManager")]
    public GuzhengAnchorManager guzhengAnchorManager;

    [Header("UI Canvases")]
    [Tooltip("Drag Canvas panel objects here")]
    public GameObject startMenu;
    public GameObject pauseMenu;
    public GameObject gameplayUI;

    [Tooltip("Drag the gameplayUI object here again to link its Canvas Group")]
    public CanvasGroup gameplayUICanvasGroup;

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
        startMenu.SetActive(newState == GameManager.GameState.StartMenu);
        pauseMenu.SetActive(newState == GameManager.GameState.Paused);
        
        bool shouldGameplayUIBeActive = 
            newState == GameManager.GameState.Playing || 
            newState == GameManager.GameState.GuzhengPlacing || 
            newState == GameManager.GameState.FieldScanning ||
            newState == GameManager.GameState.Paused; // in the background (determined by order in canvas Hierachy)

        // set active only if the state needs to change
        if (gameplayUI.activeSelf != shouldGameplayUIBeActive)
        {
            gameplayUI.SetActive(shouldGameplayUIBeActive);
        }

        // toggle interactivity for gameplayUI (when paused)
        if (gameplayUICanvasGroup != null)
        {
            // only interactable when not paused
            bool canInteract = (newState != GameManager.GameState.Paused);
            
            gameplayUICanvasGroup.interactable = canInteract;
            gameplayUICanvasGroup.blocksRaycasts = canInteract;
        }
    }

    // hook each of these functions to the OnClick() list of the buttons in the menus
    public void OnStartButtonClicked()
    {
        GameManager.Instance.ChangeState(GameManager.GameState.GuzhengPlacing);
    }

    public void OnPauseButtonClicked()
    {
        GameManager.Instance.ChangeState(GameManager.GameState.Paused);
    }

    public void OnResumeButtonClicked()
    {
        GameManager.Instance.ResumeGame(); // reverts to the state of the game just before pausing
    }

    public void OnRestartButtonClicked()
    {
        guzhengAnchorManager.DestroyGuzheng();
        GameManager.Instance.ChangeState(GameManager.GameState.GuzhengPlacing);
        arSession.Reset();
    }

    public void OnMainMenuButtonClicked()
    {
        guzhengAnchorManager.DestroyGuzheng();
        GameManager.Instance.ChangeState(GameManager.GameState.StartMenu);
        arSession.Reset();
    }
}