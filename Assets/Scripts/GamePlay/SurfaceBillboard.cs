using UnityEngine;

public class SurfaceBillboard : MonoBehaviour
{
    [Tooltip("Distance from the sphere's center. Set this slightly higher than the sphere's actual radius so the text doesn't clip inside the mesh.")]
    public float offsetRadius = 0.11f; 

    private Transform parentSphere;
    private Camera mainCamera;

    void Start()
    {
        parentSphere = transform.parent;
        mainCamera = Camera.main;
    }

    void LateUpdate()
    {
        if (mainCamera == null || parentSphere == null) return;

        Vector3 targetPosition = mainCamera.transform.position;

        Vector3 directionToCamera = (targetPosition - parentSphere.position).normalized;
        transform.position = parentSphere.position + (directionToCamera * offsetRadius);
        transform.LookAt(targetPosition);
        transform.Rotate(0, 180, 0); 
    }
}