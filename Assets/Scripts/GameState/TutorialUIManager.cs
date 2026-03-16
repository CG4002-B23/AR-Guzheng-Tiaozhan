using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TutorialModalMapping
{
    public StateManager.GameState state;
    [Tooltip("Drag the modals here in the order they should appear.")]
    public List<GameObject> modalPanels;
}

public class TutorialUIManager : MonoBehaviour
{
    [Header("UI Mappings")]
    public List<TutorialModalMapping> modalMappings;

    [Header("Interaction Override")]
    public MenuInteractionController menuInteractionController;

    private TutorialModalMapping currentSequence = null;
    private int currentModalIndex = 0;
    private GameObject currentActiveModal = null;

    private void OnEnable()
    {
        StateManager.OnGameStateChanged += HandleGameStateChange;
    }

    private void OnDisable()
    {
        StateManager.OnGameStateChanged -= HandleGameStateChange;
    }

    private void Start()
    {
        foreach (var mapping in modalMappings)
        {
            foreach (var panel in mapping.modalPanels)
            {
                if (panel != null) panel.SetActive(false);
            }
        }
    }

    private void HandleGameStateChange(StateManager.GameState newState)
    {
        if (StateManager.Instance != null && !StateManager.Instance.isTutorialMode) 
            return;

        EndCurrentSequence();

        foreach (var mapping in modalMappings)
        {
            if (mapping.state == newState && mapping.modalPanels.Count > 0)
            {
                currentSequence = mapping;
                currentModalIndex = 0;
                ShowCurrentModal();
                break;
            }
        }
    }

    private void ShowCurrentModal()
    {
        StateManager.Instance.IsTutorialPaused = true;
        AudioManager.Instance.ToggleTutorialPause(true);

        currentActiveModal = currentSequence.modalPanels[currentModalIndex];
        currentActiveModal.SetActive(true);

        if (menuInteractionController != null)
            menuInteractionController.isTutorialUIOverrideActive = true; // turn on finger hover raycasting
    }

    // Onclick event
    public void AdvanceOrCloseModal()
    {
        if (currentActiveModal != null)
            currentActiveModal.SetActive(false);

        currentModalIndex++;

        if (currentSequence != null && currentModalIndex < currentSequence.modalPanels.Count)
            ShowCurrentModal(); // show the next modal in the sequence
        else // Sequence is finished
            EndCurrentSequence();
    }

    private void EndCurrentSequence()
    {
        StateManager.Instance.IsTutorialPaused = false;
        AudioManager.Instance.ToggleTutorialPause(false);

        if (currentActiveModal != null)
        {
            currentActiveModal.SetActive(false);
            currentActiveModal = null;
        }

        currentSequence = null;
        currentModalIndex = 0;

        if (menuInteractionController != null)
            menuInteractionController.isTutorialUIOverrideActive = false; // turn off finger hover raycasting
    }
}