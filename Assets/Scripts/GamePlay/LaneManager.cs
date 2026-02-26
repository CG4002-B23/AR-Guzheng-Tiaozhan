using System.Collections.Generic;
using UnityEngine;

public class LaneManager : MonoBehaviour
{
    [Header("Spawners")]
    public ARStringSpawner guzhengSpawner;
    public ARStringSpawner enemySpawner;

    [Header("Alignment Settings")]
    [Tooltip("How many degrees of forgiveness for horizontal alignment")]
    public float alignmentToleranceDegrees = 5.0f;
    
    [Header("Lane Renderers (Translucent)")]
    [Tooltip("Assign 5 LineRenderers here for the connecting lanes")]
    public List<LineRenderer> connectionLanes;
    
    [Tooltip("Color of the connected lanes")]
    public Color translucentLaneColor = new Color(0.3f, 0.3f, 0.3f, 0.3f); // Gray with 30% opacity

    private bool areLanesConnected = false;

    void Start()
    {
        InitLanes();
    }

    void Update()
    {
        CheckAlignment();

        if (areLanesConnected)
        {
            UpdateConnectedLanes();
        }
    }

    private void InitLanes()
    {
        foreach (var lane in connectionLanes)
        {
            if (lane != null)
            {
                // fix widths
                lane.startWidth = guzhengSpawner.config.globalWidth;
                lane.endWidth = guzhengSpawner.config.globalWidth;

                // draw in global positions
                lane.useWorldSpace = true;

                // set colors
                lane.startColor = translucentLaneColor;
                lane.endColor = translucentLaneColor;

                // initially turned off
                lane.enabled = false;
            }
        }
    }

    void CheckAlignment()
    {
        // 1. Get the horizontal forward direction of the Guzheng
        Vector3 guzhengForward = Vector3.ProjectOnPlane(guzhengSpawner.transform.forward, Vector3.up).normalized;
        
        // 2. Get the horizontal direction from the Guzheng to the Enemy
        Vector3 dirToEnemy = Vector3.ProjectOnPlane(enemySpawner.transform.position - guzhengSpawner.transform.position, Vector3.up).normalized;

        // 3. Calculate the angle between them
        float angle = Vector3.Angle(guzhengForward, dirToEnemy);

        bool isAligned = angle <= alignmentToleranceDegrees;

        // 4. Handle State Changes
        if (isAligned && !areLanesConnected)
        {
            TransitionToConnected();
        }
        else if (!isAligned && areLanesConnected)
        {
            TransitionToDisconnected();
        }
    }

    void TransitionToConnected()
    {
        areLanesConnected = true;

        // Turn off original lines
        guzhengSpawner.SetLinesActive(false);
        enemySpawner.SetLinesActive(false);

        // Turn on connection lanes
        foreach (var lane in connectionLanes)
        {
            if (lane != null) lane.enabled = true;
        }
    }

    void TransitionToDisconnected()
    {
        areLanesConnected = false;

        // Turn original lines back on
        guzhengSpawner.SetLinesActive(true);
        enemySpawner.SetLinesActive(true);

        // Turn off connection lanes
        foreach (var lane in connectionLanes)
        {
            if (lane != null) lane.enabled = false;
        }
    }

    void UpdateConnectedLanes()
    {
        // Draw the lanes from Guzheng start points to Enemy start points
        for (int i = 0; i < connectionLanes.Count; i++)
        {
            if (i >= guzhengSpawner.StringStarts.Count || i >= enemySpawner.StringStarts.Count) break;

            LineRenderer lane = connectionLanes[i];
            if (lane != null)
            {
                lane.SetPosition(0, guzhengSpawner.StringStarts[i]);
                lane.SetPosition(1, enemySpawner.StringStarts[i]);
            }
        }
    }
}