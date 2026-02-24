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
        startMenu.SetActive(false);
        pauseMenu.SetActive(false);
        gameplayUI.SetActive(false);

        switch (newState)
        {
            case GameManager.GameState.StartMenu: startMenu.SetActive(true);
                break;
                
            case GameManager.GameState.Paused:
                pauseMenu.SetActive(true);
                gameplayUI.SetActive(true);  // set the gameplayUI to be visible behind the pause menu
                break;
                
            case GameManager.GameState.Playing:
            case GameManager.GameState.GuzhengPlacing:
            case GameManager.GameState.PlayingFieldScanning:
                // show the gameplayUI for all active game states
                gameplayUI.SetActive(true);
                break;
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