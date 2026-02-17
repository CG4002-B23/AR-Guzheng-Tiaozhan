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
    
    // process noise (q): how inaccurate the dynamical model (const vel model + vel_friction) is
    // measurement noise (r): how inaccurate the measurements (aprilTag detection) is
    private float _q = 0.03f; 
    private float _r = 0.005f;  
    private float _friction = 0.85f; // friction. 1.0f = no friction, 0.8f = high friction
    private float _estimated_dt = 0.016f; // 60 fps on modern devices

    // estimation error covariance (level of confidence of position estimate)
    // set high --> high initial _k --> instant snapping to detected position --> converge to stable value over time
    private float _p = 100.0f; 
    private float _k = 0.0f; // kalman gain

    public KalmanFilter() { }

    public void Reset(Vector3 startPos)
    {
        _pos = startPos;
        _vel = Vector3.zero;
        _p = 100.0f; // reset uncertainty
    }

    // project the state via the dynamical model 
    // runs on every frame
    public Vector3 KFPredict(float dt)
    {
        _vel *= Mathf.Pow(_friction, dt / _estimated_dt); // apply friction to vel prevents overshooting when we stop moving (variation from const vel model)
        _pos += _vel * dt;
        _p += _q * dt; // no correction yet - uncertainty grows
        return _pos;
    }

    // run on frames where the tag is detected
    public void KFCorrect(Vector3 measuredPos, float dt)
    {
        if (dt <= 0) return;

        Vector3 positionError = measuredPos - _pos; 
        _k = _p / (_p + _r); // kalman gain
        _pos += _k * positionError; // update position by the kalman gain factor of the diff between measurement and current pos

        // use positional error to nudge the velocity (alpha-beta filter approach)
        // rather than typically using measured velocity, which could cause massive noise injections to the vel update
        float velocityGain = _k / dt; 
        _vel += velocityGain * positionError;
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