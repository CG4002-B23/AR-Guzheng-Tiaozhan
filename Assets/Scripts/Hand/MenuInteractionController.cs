using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class MenuInteractionController : StateListener
{
    [Header("Tracking References")]
    [Tooltip("The transform representing the tip of the index finger.")]
    public Transform indexFinger;
    public Camera mainCamera;

    [Header("Hover Settings")]
    [Tooltip("How long the finger must hover over the button to click it (in seconds).")]
    public float hoverDuration = 1.0f;

    private float hoverTimer = 0f;
    private GameObject currentHoveredButton = null;
    private PointerEventData pointerEventData;

    void Start()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        // Create reusable event data for our raycasts
        if (EventSystem.current != null)
        {
            pointerEventData = new PointerEventData(EventSystem.current);
        }
        else
        {
            Debug.LogWarning("MenuInteractionController: No EventSystem found in the scene!");
        }
    }

    void Update()
    {
        if (!isActiveState || indexFinger == null || EventSystem.current == null) return;

        // 3d finger position --> 2d screen coord
        Vector2 screenPos = mainCamera.WorldToScreenPoint(indexFinger.position);
        pointerEventData.position = screenPos;

        // raycasting against all UI elements in the screen
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerEventData, results);

        // check if we hit an button
        GameObject hitButton = null;
        foreach (RaycastResult result in results)
        {
            Button btn = result.gameObject.GetComponentInParent<Button>();  // handles cases where the text/image is hit instead of the root button object
            
            if (btn != null && btn.interactable)
            {
                hitButton = btn.gameObject;
                break; // stop looking once we find the first valid button in the hierarchy
            }
        }

        // hover timer logic
        if (hitButton != null)
        {
            if (hitButton == currentHoveredButton)
            {
                hoverTimer += Time.deltaTime;

                if (hoverTimer >= hoverDuration)
                {
                    ClickButton(hitButton);
                }
            }
            else // finger moved to a new button
            {
                currentHoveredButton = hitButton;
                hoverTimer = 0f;
                Debug.Log($"Started hovering over: {hitButton.name}");
            }
        }
        else // finger not on a button
        {
            if (currentHoveredButton != null)
            {
                Debug.Log("Hover cancelled.");
                currentHoveredButton = null;
                hoverTimer = 0f;
            }
        }
    }

    private void ClickButton(GameObject buttonToClick)
    {
        Debug.Log($"Button Clicked via Hover: {buttonToClick.name}");

        ExecuteEvents.Execute(buttonToClick, pointerEventData, ExecuteEvents.pointerClickHandler);

        // reset the state so the button is not spam clicked
        hoverTimer = 0f;
        currentHoveredButton = null; 
    }
}