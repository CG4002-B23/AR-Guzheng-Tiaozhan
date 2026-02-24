using System;
using UnityEngine;

// Handles what state the game is currently in 
public class GameManager : MonoBehaviour
{
    public enum GameState { Initialising, StartMenu, GuzhengPlacing, PlayingFieldScanning, Playing, Paused }

    // other scripts tune in to this event
    public static event Action<GameState> OnGameStateChanged;

    public GameState CurrentState { get; private set; }
    public GameState PreviousState { get; private set; } // store the previous state to return to when the game is paused

    void Start()
    {
        CurrentState = GameState.Initialising;
        PreviousState = GameState.StartMenu;
        ChangeState(GameState.StartMenu);
    }

    // call this method from any script to change the game state (state sender)
    public void ChangeState(GameState newState)
    {
        if (CurrentState == newState) return;

        // update the previous state so that if the pause button is pressed, we have the previous state to return to 
        if (CurrentState != GameState.Paused)
        {
            PreviousState = CurrentState;
        }

        CurrentState = newState;
        OnGameStateChanged?.Invoke(newState); // check if anyone is listening before firing the event
    }

    // for the pause menu, to revert to the previous state that the game was in
    public void ResumeGame()
    {
        ChangeState(PreviousState);
    }
}