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
    private List<Collider> touchingHands = new List<Collider>(); // hand colliders currently inside the trigger
    public bool IsTouched => touchingHands.Count > 0;

    void Start()
    {
        stringMaterial = GetComponent<MeshRenderer>().material;
        
        invisibleColor = highlightColor;
        invisibleColor.a = 0.1f; // set to translucent opacity
        
        stringMaterial.color = invisibleColor;
    }

    void Update()
    {
        if (touchingHands.Count > 0)
        {
            bool listChanged = false;

            // Iterate backwards through the list to safely remove items while looping
            for (int i = touchingHands.Count - 1; i >= 0; i--)
            {
                if (!touchingHands[i].gameObject.activeInHierarchy)  // disabled by tracking loss
                {
                    touchingHands.RemoveAt(i);
                    listChanged = true;
                }
            }

            if (listChanged && touchingHands.Count == 0) // removed a lost hand and there are no more hands touching the string
            {
                stringMaterial.color = invisibleColor;
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Hand"))
        {
            if (!touchingHands.Contains(other))
            {
                touchingHands.Add(other); // add the new finger that is touching the string
                
                if (touchingHands.Count == 1)
                {
                    stringMaterial.color = highlightColor;
                    
                    // play guzheng note here
                }
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Hand"))
        {
            if (touchingHands.Contains(other))
            {
                touchingHands.Remove(other); // remove hand from list when exited normally
                
                if (touchingHands.Count == 0)
                {
                    stringMaterial.color = invisibleColor;
                }
            }
        }
    }
}
