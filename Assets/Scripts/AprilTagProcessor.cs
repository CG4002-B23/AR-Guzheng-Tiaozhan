using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AprilTag;

public class AprilTagProcessor : MonoBehaviour
{
    [System.Serializable]
    public struct TagProfile { public int tagID; public GameObject linkedObject; }

    [Tooltip("Place camera reading script")]
    public AprilTagCameraReader cameraReader;

    [Tooltip("Perform detection smoothing using Kalman Filter")]
    public bool useSmoothing = true;
    
    [Tooltip("List of 3d objects to be rendered on each of the tags when they are detected in the frame")]
    public List<TagProfile> tagProfiles = new List<TagProfile>();
    private Dictionary<int, TagSession> _activeTagSessions = new Dictionary<int, TagSession>(); // holds 1 KF for every tag detection

    [Tooltip("How long (in seconds) to keep predicting position using KF before hiding the object")]
    public float lostTagTimeout = 0.3f;

    [Tooltip("The higher the decimation number, the more downscaling there is during the detection, therefore faster but less accurate detection")]
    [Range(1, 4)] public int decimation = 2;
    public float tagSizeMeters = 0.05f;

    // expose debug status text publicly, to be read by AprilTagDebugUI.cs
    public string DebugStatusText { get; private set; } = "Initialising...";
    
    private TagDetector _detector;
    private int _frameCount = 0;
    private int _currentWidth, _currentHeight;

    void Start()
    {
        InitialiseDetector(1280, 720); // default start

        // subscribe to the camera reader event (to get camera frames)
        if (cameraReader != null) {
            cameraReader.OnFrameAvailable += OnFrameAvailable;
        }

        if (!useSmoothing) return;

        foreach(var profile in tagProfiles) 
        {
            _activeTagSessions[profile.tagID] = new TagSession();
        }
    }

    void OnDestroy()
    {
        _detector?.Dispose();
        if (cameraReader != null) cameraReader.OnFrameAvailable -= OnFrameAvailable; // unsubscribe to prevent memory leaks
    }

    void Update()
    {
        if (!useSmoothing) return;

        float dt = Time.deltaTime;
        float currentTime = Time.time;

        foreach (var profile in tagProfiles)
        {
            if (!_activeTagSessions.ContainsKey(profile.tagID)) continue;
            
            TagSession session = _activeTagSessions[profile.tagID];
            GameObject trackedObject = profile.linkedObject;

            // hide trackedObject if not detected for a while
            if (currentTime - session.LastSeenTime > lostTagTimeout)
            {
                if (trackedObject.activeSelf) trackedObject.SetActive(false);
                continue; 
            }

            Vector3 smoothedPos = session.Filter.KFPredict(dt); // fill in gaps for detection

            // apply to object
            if (!trackedObject.activeSelf) trackedObject.SetActive(true);
            trackedObject.transform.localPosition = smoothedPos;
            
            // note: position is smoothed, but rotation is not smoothed
            // consider Quaternion.Slerp in the detection loop if rotation smoothing is required later on
        }
    }

    void InitialiseDetector(int width, int height)
    {
        if (_detector != null) _detector.Dispose();
        _detector = new TagDetector(width, height, decimation);
        _currentWidth = width;
        _currentHeight = height;
    }

    // called automatically by the camera reader
    void OnFrameAvailable(Color32[] pixels, int width, int height, float fov)
    {
        _frameCount++;

        // Resize detection if screen orientation changed (checked against cached width/height)
        if (_currentWidth != width || _currentHeight != height) {
            InitialiseDetector(width, height);
        }

        _detector.ProcessImage(pixels, fov, tagSizeMeters);
        
        // Build Debug String
        string status = $"Frame: {_frameCount}\n" +
                        $"Res: {width}x{height}\n" +
                        $"Decimation: {decimation}\n" + 
                        $"FOV: {fov}\n";
        float currentTime = Time.time;

        HashSet<int> foundTags = new HashSet<int>(); // which tags are found in this frame

        if (!useSmoothing)
        {
            // assume nothing is visible at the start of the frame
            // so when the apriltags go out of frame, the 3d objects are also hidden
            foreach (var profile in tagProfiles)
            {
                if (profile.linkedObject != null)
                {
                    profile.linkedObject.SetActive(false);
                }
            }

            // spawn 3d objects at the pose of the apriltags detected
            foreach (var tag in _detector.DetectedTags)
            {
                status += $"\n[ID {tag.ID}] \nPos: {tag.Position.ToString("F2")} \nOri: {tag.Rotation.ToString("F2")}";
                foundTags.Add(tag.ID);
                
                // Search for profile
                bool foundProfile = false;
                foreach (var profile in tagProfiles) {
                    if (profile.tagID == tag.ID && profile.linkedObject != null) {
                        profile.linkedObject.SetActive(true);
                        // need to realign axes because the coordinate frame from AprilTag detection and Unity are different
                        profile.linkedObject.transform.localPosition = new Vector3(-tag.Position.x, -tag.Position.y, tag.Position.z);
                        profile.linkedObject.transform.localRotation = new Quaternion(-tag.Rotation.x, -tag.Rotation.y, tag.Rotation.z, tag.Rotation.w);
                        foundProfile = true;
                    }
                }
                if (!foundProfile) status += " (No Profile)";
            }

            DebugStatusText = status;
            return;
        }

        // smoothing enabled
        foreach (var tag in _detector.DetectedTags)
        {
            foundTags.Add(tag.ID);
            Vector3 tagPosition = new Vector3(-tag.Position.x, -tag.Position.y, tag.Position.z);
            Quaternion tagOrientation = new Quaternion(-tag.Rotation.x, -tag.Rotation.y, tag.Rotation.z, tag.Rotation.w);

            // Find if we have a profile for this tag
            if (_activeTagSessions.ContainsKey(tag.ID))
            {
                TagSession session = _activeTagSessions[tag.ID];
                
                // avoid the object flying in from the last known position
                if (currentTime - session.LastSeenTime > lostTagTimeout)
                {
                    session.Filter.Reset(tagPosition);
                }
                
                // smooth trajectory with measurement update
                float dt = currentTime - session.LastSeenTime;
                session.Filter.KFCorrect(tagPosition, dt);

                session.LastSeenTime = currentTime;
                
                // directly set rotation (not smoothing yet)
                foreach(var p in tagProfiles) {
                    if(p.tagID == tag.ID) p.linkedObject.transform.localRotation = tagOrientation;

                    status += $"\n[ID {tag.ID}] (Smoothed)" +
                            $"\nPos: {p.linkedObject.transform.localPosition.ToString("F2")}" +
                            $"\nRot: {p.linkedObject.transform.localRotation.ToString("F2")}";
                }
            }
        }
        
        DebugStatusText = status;
    }
}