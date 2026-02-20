using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AprilTagDebugUI : MonoBehaviour
{
    // ensure access to debug text initialisation 
    public AprilTagProcessor processor;

    // debug statements showing on phone screen
    void OnGUI()
    {
        if (processor == null) return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 40;
        style.normal.textColor = Color.red;
        style.fontStyle = FontStyle.Bold;

        // Draw the debug info at the top left
        GUI.Label(new Rect(50, 50, 800, 1000), processor.DebugStatusText, style);
    }
}