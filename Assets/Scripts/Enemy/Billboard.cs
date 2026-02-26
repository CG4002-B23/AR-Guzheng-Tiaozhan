using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Billboard : MonoBehaviour
{
    [Tooltip("If true, the object rotates on Y-axis only")]
    public bool lockYAxis = true;

    private GuzhengAnchorGetter anchorGetter;
    private Transform targetAnchor;

    void Start()
    {
        anchorGetter = FindFirstObjectByType<GuzhengAnchorGetter>();  
    }

    void Update()
    {
        if (targetAnchor == null && anchorGetter != null)
            targetAnchor = anchorGetter.FindGuzhengAnchor();

        // successfully found the guzheng anchor
        if (targetAnchor != null)
        {
            Vector3 targetPosition = Camera.main.transform.position; // camera position
            if (lockYAxis)
            {
                targetPosition.y = transform.position.y; // so the object only changes it's yaw
            }

            transform.LookAt(targetPosition); // make object look at camera

            // unity quad objects face in the opposite direction from LookAt(), causing them to be culled away
            // need to rotate them to face the camera
            // but we are rendering the enemy image only at the back and not the front, so no need for the rotation here
            // transform.Rotate(0, 180, 0);
        }
    }
}