using System;
using System.Collections.Generic;
using UnityEngine;

public enum GhostHandTarget
{
    Left,
    Right,
    Both
}

[Serializable]
public class GameplayTutorialEvent
{
    public float triggerTime;
    
    [Header("Hand Settings")]
    public GhostHandTarget handTarget;
    public string gestureTriggerName;

    [Header("Position Settings")]
    [Tooltip("If true, plays the gesture between the 2nd and 3rd string instead of on a specific lane.")]
    public bool playOffString;
    public Vector3 offStringOffset = new Vector3(0, 0, 0.1f); // Adjust Z offset here
    
    [Tooltip("Used only if playOffString is false.")]
    public int targetLaneIndex;
    
    [Header("UI")]
    public GameObject modalPanel;
}

public class TutorialGameplayUIManager : StateListener
{
    [Header("Tutorial Timeline")]
    public List<GameplayTutorialEvent> tutorialEvents;

    [Header("References")]
    public IncomingNoteManager noteManager;
    public LaneManager laneManager;
    public MenuInteractionController menuInteractionController;
    public GameObject gameUIPanel;
    
    [Header("Controllers")]
    public GhostHandController leftGhostHandController; 
    public GhostHandController rightGhostHandController; 

    [Header("Off-String Configuration")]
    [Tooltip("Lane indices for the 2nd and 3rd strings (assuming 0-indexed array)")]
    public int secondStringIndex = 1; 
    public int thirdStringIndex = 2;

    private int currentEventIndex = 0;
    private bool isHandlingEvent = false;

    protected override void OnEnable()
    {
        base.OnEnable();
        StateManager.OnGameStateChanged += HandleStateReset;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        StateManager.OnGameStateChanged -= HandleStateReset;
    }

    private void HandleStateReset(StateManager.GameState newState)
    {
        // hard reset if the player restarts game from pause menu
        if (newState == StateManager.GameState.StartMenu || newState == StateManager.GameState.GuzhengPlacing)
        {
            currentEventIndex = 0;
            isHandlingEvent = false;

            StopAllGhostHands();

            foreach (var ev in tutorialEvents) // hide all modals
                if (ev.modalPanel != null) ev.modalPanel.SetActive(false);

            if (StateManager.Instance != null) StateManager.Instance.IsTutorialPaused = false;
            if (gameUIPanel != null) gameUIPanel.SetActive(newState != StateManager.GameState.StartMenu);
        }
    }

    private void Start()
    {
        foreach (var ev in tutorialEvents)
            if (ev.modalPanel != null) ev.modalPanel.SetActive(false);
    }

    protected override void OnStateToggled(bool isNowActive)
    {
        base.OnStateToggled(isNowActive);
        
        if (!isNowActive && StateManager.Instance != null && StateManager.Instance.CurrentState != StateManager.GameState.Paused)
        {
            currentEventIndex = 0;
            isHandlingEvent = false;

            StopAllGhostHands();

            foreach (var ev in tutorialEvents) // hide panels
                if (ev.modalPanel != null) ev.modalPanel.SetActive(false);
                
            if (gameUIPanel != null) gameUIPanel.SetActive(StateManager.Instance.CurrentState != StateManager.GameState.StartMenu);
        }
    }

    void Update()
    {
        if (!isActiveState || StateManager.Instance == null || !StateManager.Instance.isTutorialMode) return;
        if (StateManager.Instance.IsTutorialPaused || isHandlingEvent) return;

        if (currentEventIndex < tutorialEvents.Count)
        {
            if (noteManager.CurrentSongTime >= tutorialEvents[currentEventIndex].triggerTime)
                TriggerTutorialEvent(tutorialEvents[currentEventIndex]);
        }
    }

    private void TriggerTutorialEvent(GameplayTutorialEvent tutorialEvent)
    {
        isHandlingEvent = true;

        StateManager.Instance.IsTutorialPaused = true;
        if (AudioManager.Instance != null) AudioManager.Instance.ToggleTutorialPause(true);
        if (gameUIPanel != null) gameUIPanel.SetActive(false);

        if (tutorialEvent.modalPanel != null) tutorialEvent.modalPanel.SetActive(true);
        if (menuInteractionController != null) menuInteractionController.isTutorialUIOverrideActive = true;

        // Calculate Hand Positions
        Vector3 startPos = Vector3.zero;
        Vector3 endPos = Vector3.zero;
        bool validPositionFound = false;

        if (tutorialEvent.playOffString)
        {
            if (laneManager.LaneStarts.ContainsKey(secondStringIndex) && laneManager.LaneStarts.ContainsKey(thirdStringIndex))
            {
                startPos = (laneManager.LaneStarts[secondStringIndex] + laneManager.LaneStarts[thirdStringIndex]) / 2f + tutorialEvent.offStringOffset;
                endPos = (laneManager.LaneEnds[secondStringIndex] + laneManager.LaneEnds[thirdStringIndex]) / 2f + tutorialEvent.offStringOffset;
                validPositionFound = true;
            }
            else
            {
                Debug.LogWarning("Tutorial UI: Could not find lanes for off-string positioning.");
            }
        }
        else
        {
            if (laneManager.LaneStarts.ContainsKey(tutorialEvent.targetLaneIndex) && laneManager.LaneEnds.ContainsKey(tutorialEvent.targetLaneIndex))
            {
                startPos = laneManager.LaneStarts[tutorialEvent.targetLaneIndex];
                endPos = laneManager.LaneEnds[tutorialEvent.targetLaneIndex];
                validPositionFound = true;
            }
        }

        // Trigger appropriate hand(s)
        if (validPositionFound)
        {
            if (tutorialEvent.handTarget == GhostHandTarget.Left || tutorialEvent.handTarget == GhostHandTarget.Both)
            {
                if (leftGhostHandController != null) 
                    leftGhostHandController.StartSequence(tutorialEvent.gestureTriggerName, startPos, endPos);
            }
            
            if (tutorialEvent.handTarget == GhostHandTarget.Right || tutorialEvent.handTarget == GhostHandTarget.Both)
            {
                if (rightGhostHandController != null) 
                    rightGhostHandController.StartSequence(tutorialEvent.gestureTriggerName, startPos, endPos);
            }
        }
    }

    public void ResumeGameplay()
    {
        StopAllGhostHands();

        if (currentEventIndex < tutorialEvents.Count)
        {
            GameObject activeModal = tutorialEvents[currentEventIndex].modalPanel;
            if (activeModal != null) activeModal.SetActive(false);
        }

        if (menuInteractionController != null) menuInteractionController.isTutorialUIOverrideActive = false;

        currentEventIndex++;
        isHandlingEvent = false;

        StateManager.Instance.IsTutorialPaused = false;
        if (AudioManager.Instance != null) AudioManager.Instance.ToggleTutorialPause(false);
        if (gameUIPanel != null) gameUIPanel.SetActive(true);
    }

    private void StopAllGhostHands()
    {
        if (leftGhostHandController != null) leftGhostHandController.StopSequence();
        if (rightGhostHandController != null) rightGhostHandController.StopSequence();
    }
}