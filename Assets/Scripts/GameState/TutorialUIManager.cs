using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TutorialModalMapping
{
    public StateManager.GameState state;
    public GameObject modalPanel;
}

public class TutorialUIManager : MonoBehaviour
{
    [Header("UI Mappings")]
    [Tooltip("Map each GameState to its corresponding tutorial UI panel.")]
    public List<TutorialModalMapping> modalMappings;

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
        // hide all tutorial modals when the scene starts
        foreach (var mapping in modalMappings)
        {
            if (mapping.modalPanel != null)
                mapping.modalPanel.SetActive(false);
        }
    }

    private void HandleGameStateChange(StateManager.GameState newState)
    {
        if (StateManager.Instance != null && !StateManager.Instance.isTutorialMode) return;

        CloseCurrentModal();

        // show modal for this specific state
        foreach (var mapping in modalMappings)
        {
            if (mapping.state == newState && mapping.modalPanel != null)
            {
                currentActiveModal = mapping.modalPanel;
                currentActiveModal.SetActive(true);
                break;
            }
        }
    }

    // OnClick() call from the x buttons
    public void CloseCurrentModal()
    {
        if (currentActiveModal != null)
        {
            currentActiveModal.SetActive(false);
            currentActiveModal = null;
        }
    }
}