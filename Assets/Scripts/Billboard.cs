using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Billboard : MonoBehaviour
{
    [Tooltip("If true, the object rotates on Y-axis only")]
    public bool lockYAxis = true;

    void Update()
    {
        if (Camera.main != null)
        {
            Vector3 targetPosition = Camera.main.transform.position; // camera position
            if (lockYAxis)
            {
                targetPosition.y = transform.position.y; // so the object only changes it's yaw
            }

            transform.LookAt(targetPosition); // make object look at camera

            // FIX: Standard Unity Quads/Planes often face 'backwards' relative to LookAt.
            // If your photo looks invisible or backward, uncomment the line below:
            transform.Rotate(0, 180, 0);
        }
    }
}