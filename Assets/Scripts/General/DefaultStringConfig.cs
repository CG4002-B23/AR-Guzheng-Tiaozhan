using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// create a new option for adding string assets in the project section
[CreateAssetMenu(fileName = "NewStringConfig", menuName = "AR/Default String Config")]
public class DefaultStringConfig : ScriptableObject
{
    [Header("Global Settings")]
    public float globalWidth = 0.02f; // All strings share this thickness

    [System.Serializable]
    public class StringDefinition
    {
        public string name = "String";
        public Color color = Color.cyan;
        public float length = 5.0f;
        
        [Tooltip("Distance from the center marker")]
        public float centreOffset = 0.0f; 
    }

    [Header("Parallel Strings Setup")]
    // This list will hold your 5 strings
    public List<StringDefinition> parallelStrings = new List<StringDefinition>();
}