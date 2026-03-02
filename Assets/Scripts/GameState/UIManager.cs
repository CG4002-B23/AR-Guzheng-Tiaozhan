using UnityEngine;
using UnityEngine.XR.ARFoundation;

// handles which button is clicked
public class GameplayUIManager : MonoBehaviour
{
    [Tooltip("GameStateManager object holding the GameManager script")]
    public StateManager gameManager;

    [Tooltip("ARSession object here")]
    public ARSession arSession;

    [Tooltip("Reference to the GuzhengAnchorManager")]
    public GuzhengAnchorManager guzhengAnchorManager;

    [Tooltip("Reference to the EnemySpawner")]
    public AutoEnemySpawner enemySpawnerManager;

    [Header("UI Canvases")]
    [Tooltip("Drag Canvas panel objects here")]
    public GameObject startMenu;
    public GameObject pauseMenu;
    public GameObject gameplayUI;
    public GameObject victoryScreen;
    public GameObject defeatScreen;

    [Tooltip("Drag the gameplayUI object here again to link its Canvas Group")]
    public CanvasGroup gameplayUICanvasGroup;

    void OnEnable()
    {
        StateManager.OnGameStateChanged += HandleGameStateChange;
    }

    void OnDisable()
    {
        StateManager.OnGameStateChanged -= HandleGameStateChange;
    }

    private void HandleGameStateChange(StateManager.GameState newState)
    {
        startMenu.SetActive(newState == StateManager.GameState.StartMenu);
        pauseMenu.SetActive(newState == StateManager.GameState.Paused);
        victoryScreen.SetActive(newState == StateManager.GameState.Victory);
        defeatScreen.SetActive(newState == StateManager.GameState.Defeat);
        
        bool shouldGameplayUIBeActive = 
            newState == StateManager.GameState.Playing || 
            newState == StateManager.GameState.GuzhengPlacing || 
            newState == StateManager.GameState.FieldScanning ||
            newState == StateManager.GameState.GuzhengAlignment ||
            newState == StateManager.GameState.Paused; // in the background (determined by order in canvas Hierachy)

        // set active only if the state needs to change
        if (gameplayUI.activeSelf != shouldGameplayUIBeActive)
        {
            gameplayUI.SetActive(shouldGameplayUIBeActive);
        }

        // toggle interactivity for gameplayUI (when paused)
        if (gameplayUICanvasGroup != null)
        {
            // only interactable when not paused
            bool canInteract = (newState != StateManager.GameState.Paused);
            
            gameplayUICanvasGroup.interactable = canInteract;
            gameplayUICanvasGroup.blocksRaycasts = canInteract;
        }
    }

    // hook each of these functions to the OnClick() list of the buttons in the menus
    public void OnStartButtonClicked()
    {
        StateManager.Instance.ChangeState(StateManager.GameState.GuzhengPlacing);
    }

    public void OnPauseButtonClicked()
    {
        StateManager.Instance.ChangeState(StateManager.GameState.Paused);
    }

    public void OnResumeButtonClicked()
    {
        StateManager.Instance.ResumeGame(); // reverts to the state of the game just before pausing
    }

    public void OnRestartButtonClicked()
    {
        guzhengAnchorManager.DestroyGuzheng();
        enemySpawnerManager.DestroyEnemy();
        StateManager.Instance.ChangeState(StateManager.GameState.GuzhengPlacing);
        arSession.Reset();
    }

    public void OnMainMenuButtonClicked()
    {
        guzhengAnchorManager.DestroyGuzheng();
        enemySpawnerManager.DestroyEnemy();
        StateManager.Instance.ChangeState(StateManager.GameState.StartMenu);
        arSession.Reset();
    }
}