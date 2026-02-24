using UnityEngine;

// handles which button is clicked
public class UIManager : MonoBehaviour
{
    [Tooltip("GameStateManager object holding the GameManager script")]
    public GameManager gameManager;

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
        gameManager.ChangeState(GameManager.GameState.GuzhengPlacing);
    }

    public void OnPauseButtonClicked()
    {
        gameManager.ChangeState(GameManager.GameState.Paused);
    }

    public void OnResumeButtonClicked()
    {
        gameManager.ChangeState(GameManager.GameState.Playing); 
    }

    public void OnMainMenuButtonClicked()
    {
        gameManager.ChangeState(GameManager.GameState.StartMenu);
    }
}