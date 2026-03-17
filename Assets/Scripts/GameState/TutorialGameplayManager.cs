using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GameplayTutorialEvent
{
    [Tooltip("The exact second in the song to pause and show the tutorial")]
    public float triggerTime;
    
    [Tooltip("The lane index (0-4) where the hand should appear")]
    public int targetLaneIndex;
    
    [Tooltip("The exact name of the Animator Trigger for the hand gesture (e.g., 'RightIndex')")]
    public string gestureTriggerName;
    
    [Tooltip("The UI Panel to show")]
    public GameObject modalPanel;
}

public class TutorialGameplayManager : StateListener
{
    [Header("Tutorial Timeline")]
    public List<GameplayTutorialEvent> tutorialEvents;

    [Header("References")]
    public IncomingNoteManager noteManager;
    public LaneManager laneManager;
    public MenuInteractionController menuInteractionController;

    [Header("Ghost Hand")]
    public GameObject ghostHandContainer;
    public Animator ghostHandAnimator;
    public Vector3 handOffset = new Vector3(0, 0.1f, 0); // Hover slightly above the string

    private int currentEventIndex = 0;
    private bool isHandlingEvent = false;

    private void Start()
    {
        if (ghostHandContainer != null) ghostHandContainer.SetActive(false);
        
        foreach (var ev in tutorialEvents)
            if (ev.modalPanel != null) ev.modalPanel.SetActive(false);
    }

    protected override void OnStateToggled(bool isNowActive)
    {
        base.OnStateToggled(isNowActive);
        
        // reset the timeline when the game stops playing
        if (!isNowActive)
        {
            currentEventIndex = 0;
            isHandlingEvent = false;
            if (ghostHandContainer != null) ghostHandContainer.SetActive(false);
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

        // freeze gameplay and audio
        StateManager.Instance.IsTutorialPaused = true;
        if (AudioManager.Instance != null) AudioManager.Instance.ToggleTutorialPause(true);

        if (tutorialEvent.modalPanel != null) tutorialEvent.modalPanel.SetActive(true);
        if (menuInteractionController != null) menuInteractionController.isTutorialUIOverrideActive = true;

        if (ghostHandContainer != null && laneManager.LaneStarts.ContainsKey(tutorialEvent.targetLaneIndex))
        {
            // Move hand to the AR string position on the player's side
            Vector3 stringPos = laneManager.LaneStarts[tutorialEvent.targetLaneIndex];
            ghostHandContainer.transform.position = stringPos + handOffset;
            
            // Activate and animate
            ghostHandContainer.SetActive(true);
            if (ghostHandAnimator != null)
            {
                // Reset to idle first just in case, then trigger the new gesture
                ghostHandAnimator.SetTrigger("Idle"); 
                ghostHandAnimator.SetTrigger(tutorialEvent.gestureTriggerName);
            }
        }
    }

    // on continue
    public void ResumeGameplay()
    {
        if (currentEventIndex < tutorialEvents.Count)
        {
            // Hide the current UI
            GameObject activeModal = tutorialEvents[currentEventIndex].modalPanel;
            if (activeModal != null) activeModal.SetActive(false);
        }

        // Hide the hand and disable interaction override
        if (ghostHandContainer != null) ghostHandContainer.SetActive(false);
        if (menuInteractionController != null) menuInteractionController.isTutorialUIOverrideActive = false;

        // Advance to the next event
        currentEventIndex++;
        isHandlingEvent = false;

        // Unfreeze the Game & Audio
        StateManager.Instance.IsTutorialPaused = false;
        if (AudioManager.Instance != null) AudioManager.Instance.ToggleTutorialPause(false);
    }
}