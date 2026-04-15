using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class AprilTagSession
{
    public AprilTagPositionSmootherKF PositionSmoother = new AprilTagPositionSmootherKF();
    public AprilTagRotationSmoother RotationSmoother = new AprilTagRotationSmoother();
    
    public float LastSeenTime;
}