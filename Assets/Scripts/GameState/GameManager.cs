using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public enum GameState { StartMenu, GuzhengPlacing, PlayingFieldScanning, Playing, Paused }

    // other scripts tune in to this event
    public static event Action<GameState> OnGameStateChanged;

    public GameState CurrentState { get; private set; }

    void Start()
    {
        ChangeState(GameState.StartMenu);
    }

    // call this method from any script to change the game state
    public void ChangeState(GameState newState)
    {
        CurrentState = newState;
        OnGameStateChanged?.Invoke(newState); // check if anyone is listening before firing the event
    }
}