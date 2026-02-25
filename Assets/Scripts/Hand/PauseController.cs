using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PauseController : StateListener
{
    [Header("Tracking References")]
    public List<Transform> handPoints = new List<Transform>(); // add multiple hand parts
    public Camera mainCamera; // AR camera

    [Header("Button References")]
    public RectTransform pauseButtonRect;

    [Header("Settings")]
    [Range(1f, 30f)]
    public float checkFrequency = 10f; // Hz
    
    void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    protected override void OnEnable() // when the object is disabled, Unity kills the coroutine. so we need to reenable it
    {
        base.OnEnable();
        StartCoroutine(HandPositionCheckingRoutine());
    }

    private IEnumerator HandPositionCheckingRoutine()
    {
        var wait = new WaitForSeconds(1f / checkFrequency);
        Debug.Log("PauseController: Started Coroutine");

        while (true)
        {
            if (isActiveState)
            {
                Debug.Log("PauseController: checking hand points");
                CheckHandPoints();
            }
            
            Debug.Log("PauseController: in the loop");

            yield return wait;
        }
    }

    private void CheckHandPoints()
    {
        foreach (Transform point in handPoints)
        {
            if (point == null) continue;

            Vector2 screenPos = mainCamera.WorldToScreenPoint(point.position);

            if (RectTransformUtility.RectangleContainsScreenPoint(pauseButtonRect, screenPos, null))
            {
                GameManager.Instance.ChangeState(GameManager.GameState.Paused);
                return; // no need to check remaining points
            }
        }
    }
}
