using UnityEngine;

public class UltimateKnife : MonoBehaviour
{
    [Header("Model Correction")]
    [Tooltip("Drag the child object containing the 3D mesh here")]
    public Transform knifeMesh;
    [Tooltip("Adjust this if the knife faces sideways. Usually (0, -90, 0) or (0, 90, 0)")]
    public Vector3 meshRotationOffset = new Vector3(0, -90, 0);

    [Header("Floating Animation")]
    public float floatSpeed = 3f;
    public float floatHeight = 0.1f;
    private Vector3 startPosition;

    [Header("Hovering Icon")]
    public SpriteRenderer iconRenderer;
    public Sprite[] possibleIcons;

    void Start()
    {
        startPosition = transform.position;

        // make mesh rotation point forward
        if (knifeMesh != null)
            knifeMesh.localEulerAngles = meshRotationOffset;

        // select random icon to spawn
        if (iconRenderer != null && possibleIcons != null && possibleIcons.Length > 0)
        {
            int randomIndex = Random.Range(0, possibleIcons.Length);
            iconRenderer.sprite = possibleIcons[randomIndex];
        }
    }

    void Update()
    {
        // float up and down
        float newY = startPosition.y + (Mathf.Sin(Time.time * floatSpeed) * floatHeight);
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }
}