using UnityEngine;
using TMPro;

public class FloatingText : MonoBehaviour
{
    [Header("Settings")]
    public float floatSpeed = 1.0f;
    public float lifetime = 1.0f;

    [Tooltip("Text field of world space canvas")]
    public TextMeshProUGUI textMesh; 

    void Start()
    {
        // automatically destroy after lifetime seconds
        Destroy(gameObject, lifetime);

        // billboarding
        if (Camera.main != null)
            transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
    }

    void Update()
    {
        transform.position += Vector3.up * floatSpeed * Time.deltaTime; // float up

        if (textMesh != null) 
        {
            Color c = textMesh.color;
            c.a -= (1f / lifetime) * Time.deltaTime; // fade out
            textMesh.color = c;
        }
    }

    public void Setup(string textToDisplay, Color textColor)
    {
        if (textMesh != null)
        {
            textMesh.text = textToDisplay;
            textMesh.color = textColor;
        }
    }
}