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
    public GameObject gameUIPanel;

    [Header("Ghost Hand")]
    public GameObject ghostHandContainer;
    public Animator ghostHandAnimator;
    public Vector3 handOffset = new Vector3(0, 0.1f, 0); // Hover slightly above the string

    [Header("Ghost Hand Animation Settings")]
    [Tooltip("How high above the target position the hand starts")]
    public float hoverHeight = 0.15f; 
    [Tooltip("How many seconds it takes to hover down and fade in")]
    public float hoverDuration = 0.5f;
    [Tooltip("How long to wait at the string while the gesture animation plays")]
    public float gestureHoldTime = 1.0f;
    [Tooltip("How long to pause while invisible before repeating the loop")]
    public float loopPauseTime = 1.0f;
    [Tooltip("Max opacity (0-1)")]
    public float maxHandOpacity = 0.5f;

    private Coroutine activeHandLoop;
    private Renderer[] handRenderers;

    private int currentEventIndex = 0;
    private bool isHandlingEvent = false;

    private void Start()
    {
        if (ghostHandContainer != null) 
        {
            ghostHandContainer.SetActive(false);
            handRenderers = ghostHandContainer.GetComponentsInChildren<Renderer>();
        }
        
        foreach (var ev in tutorialEvents)
            if (ev.modalPanel != null) ev.modalPanel.SetActive(false);
    }

    protected override void OnStateToggled(bool isNowActive)
    {
        base.OnStateToggled(isNowActive);
        
        // reset the timeline when the game stops playing
        if (!isNowActive && StateManager.Instance != null && StateManager.Instance.CurrentState != StateManager.GameState.Paused)
        {
            currentEventIndex = 0;
            isHandlingEvent = false;
            if (ghostHandContainer != null) ghostHandContainer.SetActive(false);

            // ensure any open tutorial modal is closed if we quit the game
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

        // freeze gameplay and audio
        StateManager.Instance.IsTutorialPaused = true;
        if (AudioManager.Instance != null) AudioManager.Instance.ToggleTutorialPause(true);
        if (gameUIPanel != null) gameUIPanel.SetActive(false);

        if (tutorialEvent.modalPanel != null) tutorialEvent.modalPanel.SetActive(true);
        if (menuInteractionController != null) menuInteractionController.isTutorialUIOverrideActive = true;

        if (activeHandLoop != null) StopCoroutine(activeHandLoop);

        if (ghostHandContainer != null && laneManager.LaneStarts.ContainsKey(tutorialEvent.targetLaneIndex))
        {
            Vector3 stringPos = laneManager.LaneStarts[tutorialEvent.targetLaneIndex];
            ghostHandContainer.SetActive(true);
            
            activeHandLoop = StartCoroutine(GhostHandRoutine(tutorialEvent, stringPos));
        }
    }

    // on continue
    public void ResumeGameplay()
    {
        if (activeHandLoop != null) 
        {
            StopCoroutine(activeHandLoop);
            activeHandLoop = null;
        }

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
        if (gameUIPanel != null) gameUIPanel.SetActive(true);
    }

    private System.Collections.IEnumerator GhostHandRoutine(GameplayTutorialEvent tutorialEvent, Vector3 targetStringPos)
    {
        Vector3 bottomPos = targetStringPos + handOffset;
        Vector3 topPos = bottomPos + (Vector3.up * hoverHeight);

        while (isHandlingEvent) // as long as the modal is open
        {
            // --- STEP 1 & 2: Start high/faded, hover downwards, fade in ---
            float timer = 0f;
            ghostHandContainer.transform.position = topPos;
            SetHandAlpha(0f);
            
            if (ghostHandAnimator != null) ghostHandAnimator.SetTrigger("IdleTrigger");

            while (timer < hoverDuration)
            {
                timer += Time.deltaTime;
                float progress = timer / hoverDuration;
                
                ghostHandContainer.transform.position = Vector3.Lerp(topPos, bottomPos, progress);
                SetHandAlpha(progress * maxHandOpacity); // fade from 0 to max
                yield return null; // Wait for the next frame
            }

            // --- STEP 3: Do the gesture and hold ---
            if (ghostHandAnimator != null) ghostHandAnimator.SetTrigger(tutorialEvent.gestureTriggerName);
            yield return new WaitForSeconds(gestureHoldTime); // Wait for the animation to play out

            // --- STEP 4: Hover back upwards and fade out ---
            timer = 0f;
            while (timer < hoverDuration)
            {
                timer += Time.deltaTime;
                float progress = timer / hoverDuration;
                
                ghostHandContainer.transform.position = Vector3.Lerp(bottomPos, topPos, progress);
                SetHandAlpha((1f - progress) * maxHandOpacity); // Fade from max down to 0
                yield return null; 
            }

            // Pause invisibly before repeating the loop
            yield return new WaitForSeconds(loopPauseTime);
        }
    }

    private void SetHandAlpha(float alpha)
    {
        if (handRenderers == null) return;
        
        foreach (var rend in handRenderers)
        {
            if (rend.material.HasProperty("_Color"))
            {
                Color c = rend.material.color;
                c.a = alpha;
                rend.material.color = c;
            }
        }
    }
}