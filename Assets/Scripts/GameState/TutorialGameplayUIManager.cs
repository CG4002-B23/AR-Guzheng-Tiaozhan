using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GameplayTutorialEvent
{
    public float triggerTime;
    public int targetLaneIndex;
    public string gestureTriggerName;
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
    public GhostHandController ghostHandController; 

    private int currentEventIndex = 0;
    private bool isHandlingEvent = false;

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

            if (ghostHandController != null) ghostHandController.StopSequence();

            if (currentEventIndex < tutorialEvents.Count && tutorialEvents[currentEventIndex].modalPanel != null)
                tutorialEvents[currentEventIndex].modalPanel.SetActive(false);
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

        if (ghostHandController != null && 
            laneManager.LaneStarts.ContainsKey(tutorialEvent.targetLaneIndex) && 
            laneManager.LaneEnds.ContainsKey(tutorialEvent.targetLaneIndex))
        {
            Vector3 startPos = laneManager.LaneStarts[tutorialEvent.targetLaneIndex];
            Vector3 endPos = laneManager.LaneEnds[tutorialEvent.targetLaneIndex];
            ghostHandController.StartSequence(tutorialEvent.gestureTriggerName, startPos, endPos);
        }
    }

    public void ResumeGameplay()
    {
        if (ghostHandController != null) ghostHandController.StopSequence();

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
}