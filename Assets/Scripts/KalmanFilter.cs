using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable] // so the class shows up in the inspector
public class KalmanFilter
{
    // state space is the position and velocity of the tag: [x, y, z, vx, vy, vz]
    // perform smoothing for all 3 axes
    private Vector3 _pos;
    private Vector3 _vel;
    
    // process noise (q): how inaccurate the dynamical model (how much the tag jitters) is (keep low)
    // measurement noise (r): how inaccurate the measurements (aprilTag detection) is (make high)
    private float _q = 0.01f; 
    private float _r = 0.1f;  
    private float _p = 0.1f; // estimation error covariance
    private float _k = 0.0f; // kalman gain

    public KalmanFilter() { }

    public void Reset(Vector3 startPos)
    {
        _pos = startPos;
        _vel = Vector3.zero;
        _p = 0.1f; // reset certainty
    }

    // project the state via the dynamical model 
    // runs on every frame
    public Vector3 KFPredict(float dt)
    {
        // const velocity model used to interpolate between detections, mitigating false negatives from detections
        _pos += _vel * dt;
        _p += _q; // no correction yet - uncertainty grows
        return _pos;
    }

    // run on frames where the tag is detected
    public void KFCorrect(Vector3 measuredPos, float dt)
    {
        if (dt <= 0) return;

        Vector3 measuredVelocity = (measuredPos - _pos) / dt; // compute estimated measured vel for velocity update
        _k = _p / (_p + _r); // kalman gain
        _pos += _k * (measuredPos - _pos); // update position by the kalman gain factor of the diff between measurement and current pos
        _vel += _k * (measuredVelocity - _vel); // update velocity in the same way
        _p = (1 - _k) * _p; // update error cov
    }
}

// keeping track of active tags
public class TagSession
{
    public KalmanFilter Filter = new KalmanFilter();
    public float LastSeenTime;
    public bool IsVisible;
}