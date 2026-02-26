using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class ARStringSpawner : MonoBehaviour
{
    [Tooltip("From string configuration file")]
    public DefaultStringConfig config;
    public List<LineRenderer> lineRenderers;

    [Tooltip("How much to lower the strings by")]
    public float horizontalOffset = -0.15f;
    Vector3 horizontalOffsetCoords;

    // access start/end points of any string
    // Key = Index of the string (0 to 4), Value = The positions
    // {get; private set;} allows for reading, modifying entries, but forbids overwriting files
    // example usage in other scripts
    // Vector3 start = spawner.StringStarts[2]; // Get start of the Middle String
    // Vector3 end = spawner.StringEnds[2];     // Get end of the Middle String
    public Dictionary<int, Vector3> StringStarts { get; private set; } = new Dictionary<int, Vector3>();
    public Dictionary<int, Vector3> StringEnds { get; private set; } = new Dictionary<int, Vector3>();

    void Start()
    {
        horizontalOffsetCoords = new Vector3(0.0f, horizontalOffset, 0.0f);
    }

    void OnEnable() // executed everytime the object is activated in the scene
    {
        InitializeLines();
    }

    void Update()
    {
        DrawParallelLines();
    }

    void InitializeLines()
    {
        if (config == null) return;

        // Loop through our Line Renderers and apply settings
        for (int i = 0; i < lineRenderers.Count; i++)
        {
            if (i >= config.parallelStrings.Count) break; // Safety check

            LineRenderer lr = lineRenderers[i];
            var settings = config.parallelStrings[i];

            if (lr != null)
            {
                lr.startWidth = config.globalWidth;
                lr.endWidth = config.globalWidth;
                lr.startColor = settings.color;
                lr.endColor = settings.color;
                lr.useWorldSpace = true;
                lr.positionCount = 2;
            }
        }

        DrawParallelLines();
    }

    void DrawParallelLines()
    {
        if (config == null) return;

        Vector3 center = transform.position + horizontalOffsetCoords;
        Vector3 rightDir = transform.right;   // Direction of the lines
        Vector3 forwardDir = transform.forward; // Direction of the parallel spacing

        for (int i = 0; i < lineRenderers.Count; i++)
        {
            // Safety: Ensure we have both a renderer and a config for this index
            if (i >= config.parallelStrings.Count || lineRenderers[i] == null) continue;

            var settings = config.parallelStrings[i];
            LineRenderer lr = lineRenderers[i];

            // start position is touching the guzheng
            Vector3 startPos = center + (rightDir * settings.centreOffset);
            Vector3 endPos = startPos + (forwardDir * settings.length);
            lr.SetPosition(0, startPos);
            lr.SetPosition(1, endPos);

            // store string start and end positions in dictionary to be referenced by other 
            if (!StringStarts.ContainsKey(i))
            {
                StringStarts.Add(i, startPos);
                StringEnds.Add(i, endPos);
            }
            else
            {
                StringStarts[i] = startPos;
                StringEnds[i] = endPos;
            }
        }
    }

    public void SetLinesActive(bool active)
    {
        // Toggle the visibility of the line renderers
        foreach (var lr in lineRenderers)
            if (lr != null) lr.enabled = active;
        
        this.enabled = active;  // enable or disable the script so it stops updating the parallel lines
    }
}