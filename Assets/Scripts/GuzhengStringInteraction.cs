using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer), typeof(Collider))]
public class GuzhengStringInteraction : MonoBehaviour
{
    [Header("Visual Settings")]
    [Tooltip("The color and opacity of the string when touched.")]
    public Color highlightColor = new Color(0f, 1f, 1f, 0.5f); // 50% Cyan
    
    private Color invisibleColor;
    private Material stringMaterial;
    private string DebugStatusText = "";

    void Start()
    {
        stringMaterial = GetComponent<MeshRenderer>().material;
        
        invisibleColor = highlightColor;
        invisibleColor.a = 0.1f; // set to translucent opacity
        
        stringMaterial.color = invisibleColor;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Hand")) // check if the object touching the string is the player's hand
        {
            stringMaterial.color = highlightColor;
            DebugStatusText = "Hand touching string: " + gameObject.name;
            
            // trigger guzheng audio notes here
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Hand")) // turn the string invisible again when the hand leaves
        {
            stringMaterial.color = invisibleColor;
            DebugStatusText = "";
        }
    }

    // debug statements showing on phone screen
    void OnGUI()
    {
        GUIStyle style = new GUIStyle();
        style.fontSize = 40;
        style.normal.textColor = Color.green;
        style.fontStyle = FontStyle.Bold;

        // Draw the debug info at the top right
        GUI.Label(new Rect(1300, 50, 800, 1000), DebugStatusText, style);
    }
}