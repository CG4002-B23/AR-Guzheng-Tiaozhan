using System;
using UnityEngine;

// Handles what state the game is currently in 
public class GameManager : MonoBehaviour
{
    // singleton setup - ensure that there is only 1 instance of GameManager by providing global access to it
    public static GameManager Instance { get; private set; }
    void Awake()
    {
        // delete any extra instances of this object
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    public enum GameState { Initialising, StartMenu, GuzhengPlacing, FieldScanning, GuzhengAlignment, Playing, Paused }

    // other scripts tune in to this event
    public static event Action<GameState> OnGameStateChanged;

    public GameState CurrentState { get; private set; }
    public GameState PreviousState { get; private set; } // store the previous state to return to when the game is paused
    
    public string DebugStatusText { get; private set; } = "";

    void Start()
    {
        CurrentState = GameState.Initialising;
        PreviousState = GameState.StartMenu;
        ChangeState(GameState.StartMenu);
    }

    // call this method from any script to change the game state (state sender)
    public void ChangeState(GameState newState)
    {
        Debug.Log($"ChangeState called: {CurrentState} -> {newState}");
        if (CurrentState == newState) return;

        // update the previous state so that if the pause button is pressed, we have the previous state to return to 
        if (CurrentState != GameState.Paused)
        {
            PreviousState = CurrentState;
        }

        CurrentState = newState;
        OnGameStateChanged?.Invoke(newState); // check if anyone is listening before firing the event

        DebugStatusText = "State: " + CurrentState;
    }

    // for the pause menu, to revert to the previous state that the game was in
    public void ResumeGame()
    {
        ChangeState(PreviousState);
    }

        // debug statements showing on phone screen
    void OnGUI()
    {
        GUIStyle style = new GUIStyle();
        style.fontSize = 40;
        style.normal.textColor = Color.green;
        style.fontStyle = FontStyle.Bold;

        // Draw the debug info at the top right
        GUI.Label(new Rect(1600, 50, 800, 1000), DebugStatusText, style);
    }
}