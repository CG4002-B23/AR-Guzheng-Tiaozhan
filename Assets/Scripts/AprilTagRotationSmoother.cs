using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class AprilTagRotationSmoother
{
    private float _rotationSmoothSpeed = 5.0f; // lower = smoother but laggier, higher = faster but more jittery
    public Quaternion TargetRotation = Quaternion.identity;

    public Quaternion SmoothRotation(Quaternion currentRotation, float dt)
    {
        // 0 - lean to previous frame's rotation. 1 - lean to current frame's rotation
        float interpolationRatio = 1f - Mathf.Exp(-_rotationSmoothSpeed * dt);
        
        return Quaternion.Slerp(
            currentRotation, 
            TargetRotation, 
            interpolationRatio
        );
    }
}