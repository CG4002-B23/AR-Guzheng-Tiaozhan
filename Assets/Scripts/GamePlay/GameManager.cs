using UnityEngine;

public class GameManager : MonoBehaviour
{
    public void HandleGameOver(bool playerWon)
    {
        if (playerWon)
        {
            Debug.Log("Enemy defeated! Player Wins!");
            StateManager.Instance.ChangeState(StateManager.GameState.Victory);
        }
        else
        {
            Debug.Log("Player defeated! Game Over.");
            StateManager.Instance.ChangeState(StateManager.GameState.Defeat);
        }
    }
}
