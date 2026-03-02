using UnityEngine;

public class GameManager : MonoBehaviour
{
    public void HandleGameOver(bool playerWon)
    {
        if (playerWon)
        {
            Debug.Log("Player finished song without dying!");
            StateManager.Instance.ChangeState(StateManager.GameState.Victory);
        }
        else
        {
            Debug.Log("Player defeated! Game Over.");
            StateManager.Instance.ChangeState(StateManager.GameState.Defeat);
        }
    }
}
