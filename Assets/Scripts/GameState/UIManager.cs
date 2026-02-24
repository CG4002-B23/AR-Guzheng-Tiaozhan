using UnityEngine;

public class UIManager : MonoBehaviour
{
    [Tooltip("GameStateManager object holding the GameManager script")]
    public GameManager gameManager;

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