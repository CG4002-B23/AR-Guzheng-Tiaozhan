using System.Collections.Generic;
using UnityEngine;

public class LaneManager : StateListener
{
    [Header("Spawners")]
    public ARStringSpawner guzhengSpawner;
    public ARStringSpawner enemySpawner;

    [Header("Lane Renderers (Translucent)")]
    [Tooltip("Assign 5 LineRenderers here for the connecting lanes")]
    public List<LineRenderer> connectionLanes;
    
    [Tooltip("Color of the connected lanes")]
    public Color translucentLaneColor = new Color(0.7f, 0.7f, 0.7f, 0.3f); // Gray with 30% opacity

    // Key = Index of the lane (0 to 4), Value = World position
    // LaneStarts mirrors guzheng StringStarts, LaneEnds mirrors enemy StringStarts
    public Dictionary<int, Vector3> LaneStarts { get; private set; } = new Dictionary<int, Vector3>();
    public Dictionary<int, Vector3> LaneEnds   { get; private set; } = new Dictionary<int, Vector3>();

    void Awake()
    {
        InitLanes();  
    }

    protected override void OnStateToggled(bool isNowActive)
    {
        foreach (var lane in connectionLanes)
            if (lane != null) lane.enabled = isNowActive;

        if (!isNowActive)
        {
            LaneStarts.Clear();
            LaneEnds.Clear();
        }
    }

    void Update()
    {
        if (!isActiveState) return;

        UpdateLanes();
    }

    private void InitLanes()
    {
        foreach (var lane in connectionLanes)
        {
            if (lane != null)
            {
                lane.startWidth = guzhengSpawner.config.globalWidth;
                lane.endWidth = guzhengSpawner.config.globalWidth;
                lane.startColor = translucentLaneColor;
                lane.endColor = translucentLaneColor;
                lane.useWorldSpace = true;
                lane.positionCount = 2;
                lane.enabled = false;
            }
        }
    }

    void UpdateLanes()
    {
        for (int i = 0; i < connectionLanes.Count; i++)
        {
            if (i >= guzhengSpawner.StringStarts.Count || i >= enemySpawner.StringStarts.Count) break;

            Vector3 start = guzhengSpawner.StringStarts[i];
            Vector3 end   = enemySpawner.StringStarts[i];

            if (!LaneStarts.ContainsKey(i))
            {
                LaneStarts.Add(i, start);
                LaneEnds.Add(i, end);
            }
            else
            {
                LaneStarts[i] = start;
                LaneEnds[i]   = end;
            }

            LineRenderer lane = connectionLanes[i];
            if (lane != null)
            {
                lane.SetPosition(0, LaneStarts[i]);
                lane.SetPosition(1, LaneEnds[i]);
            }
        }
    }
}