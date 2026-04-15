using System.Collections.Generic;
using UnityEngine;

public class LaneManager : StateListener
{
    [Header("Spawners")]
    [HideInInspector]
    public ARStringSpawner guzhengSpawner;
    [HideInInspector]
    public ARStringSpawner enemySpawner;

    [HideInInspector]
    public List<Transform> guzhengHitboxes = new List<Transform>();

    [Header("Lane Renderers (Translucent)")]
    [Tooltip("Assign 5 LineRenderers here for the connecting lanes")]
    public List<LineRenderer> connectionLanes;

    [Tooltip("Color of the connected lanes")]
    public Color translucentLaneColor = new Color(0.7f, 0.7f, 0.7f, 0.4f); // Gray with 30% opacity

    [Tooltip("Estimated guzheng width (short side)")]
    [Range(0f, 1f)]
    public float estimatedGuzhengWidth = 0.6f;
    
    [Header("Pulse Effect")]
    [Tooltip("How fast the lanes pulse")]
    public float pulseSpeed = 3.0f;
    
    [Tooltip("Minimum opacity during the pulse")]
    [Range(0f, 1f)]
    public float minAlpha = 0.2f;
    
    [Tooltip("Maximum opacity during the pulse")]
    [Range(0f, 1f)]
    public float maxAlpha = 0.6f;

    private Vector3 guzhengOffset;

    // Key = Index of the lane (0 to 4), Value = World position
    // LaneStarts mirrors guzheng StringStarts, LaneEnds mirrors enemy StringStarts
    public Dictionary<int, Vector3> LaneStarts { get; private set; } = new Dictionary<int, Vector3>();
    public Dictionary<int, Vector3> LaneEnds   { get; private set; } = new Dictionary<int, Vector3>();

    private float laneThickness = 0.3f;
    private bool hitboxesFound = false;

    void Awake()
    {
        InitLanes();  
        guzhengOffset = new Vector3(0.0f, 0.0f, 0.5f * estimatedGuzhengWidth);
        
    }

    protected override void OnStateToggled(bool isNowActive)
    {
        if (!isNowActive)
        {
            foreach (var lane in connectionLanes)
                if (lane != null) lane.enabled = false;

            LaneStarts.Clear();
            LaneEnds.Clear();

            hitboxesFound = false; 
            guzhengHitboxes.Clear();
        }
    }

    void Update()
    {
        if (!isActiveState) return;
        if (StateManager.Instance.CurrentState == StateManager.GameState.Paused) return;

        if (!hitboxesFound)
            FindHitboxesDynamically();

        UpdateLanes();
        PulseLanes();
    }

    private void FindHitboxesDynamically()
    {
        GuzhengStringInteraction[] foundHitboxes = Object.FindObjectsByType<GuzhengStringInteraction>(FindObjectsSortMode.None);

        if (foundHitboxes != null && foundHitboxes.Length > 0)
        {
            guzhengHitboxes.Clear();
            foreach (var hitbox in foundHitboxes)
                guzhengHitboxes.Add(hitbox.transform);

            // ensure string 1 is string 1 and string 5 is string 5
            guzhengHitboxes.Sort((a, b) => a.GetSiblingIndex().CompareTo(b.GetSiblingIndex()));
            
            hitboxesFound = true;
            Debug.Log($"[LaneManager] Successfully auto-found and sorted {guzhengHitboxes.Count} hitboxes!");
        }
    }

    private void InitLanes()
    {
        foreach (var lane in connectionLanes)
        {
            if (lane != null)
            {
                lane.startWidth = laneThickness;
                lane.endWidth = laneThickness;
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
        if (guzhengHitboxes == null || guzhengHitboxes.Count == 0 || enemySpawner.StringStarts.Count == 0) return;

        for (int i = 0; i < connectionLanes.Count; i++)
        {
            if (i >= guzhengHitboxes.Count || i >= enemySpawner.StringStarts.Count) break;

            Vector3 start = guzhengHitboxes[i].position + guzhengOffset;
            Vector3 end   = enemySpawner.StringStarts[enemySpawner.StringStarts.Count - 1 - i];

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
                lane.enabled = true; // only enabled when we have valid positions
                lane.SetPosition(0, LaneStarts[i]);
                lane.SetPosition(1, LaneEnds[i]);
            }
        }
    }

    private void PulseLanes()
    {
        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f; // get a val between 0 and 1 each time
        
        // linear interpolation 
        float currentAlpha = Mathf.Lerp(minAlpha, maxAlpha, t);

        // change opacity of our color
        Color currentPulseColor = translucentLaneColor;
        currentPulseColor.a = currentAlpha;

        foreach (var lane in connectionLanes)
        {
            if (lane != null && lane.enabled)
            {
                lane.startColor = currentPulseColor;
                lane.endColor = currentPulseColor;
            }
        }
    }
}