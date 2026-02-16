using System.Collections.Generic;
using UnityEngine;
using AprilTag;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.Collections;
using System;

public class AprilTagTracker : MonoBehaviour
{
    [System.Serializable]
    public struct TagProfile { public int tagID; public GameObject linkedObject; }
    
    public List<TagProfile> tagProfiles = new List<TagProfile>();

    [Tooltip("Put AR camera here")]
    public ARCameraManager arCameraManager;

    [Tooltip("The higher the decimation number, the more downscaling there is, therefore faster but less accurate detection")]
    [Range(1, 4)] public int decimation = 2;
    public float tagSizeMeters = 0.05f;

    [Tooltip("Input cameraFOV from phone specification. The value will also be computed in code")]
    public float cameraFOV = 77.0f; 

    private TagDetector _detector;
    private NativeArray<byte> _rawPixelBuffer;
    private Color32[] _colorBuffer;
    private int _currentWidth, _currentHeight;
    
    // debug variables
    private string _debugText = "Initializing...";
    private int _frameCount = 0;
    private float _lastDetectionTime = 0;

    void Start()
    {
        // 1. Force Auto Focus (Crucial for detecting small tags)
        if (arCameraManager != null) {
            arCameraManager.autoFocusRequested = true;
        }

        // 2. Initialize Code
        InitializeDetector(1280, 720);
        if (arCameraManager != null) arCameraManager.frameReceived += OnCameraFrameReceived;
    }

    void OnDestroy()
    {
        _detector?.Dispose();
        if (_rawPixelBuffer.IsCreated) _rawPixelBuffer.Dispose();
        if (arCameraManager != null) arCameraManager.frameReceived -= OnCameraFrameReceived;
    }

    // debug statements showing on phone screen
    void OnGUI()
    {
        GUIStyle style = new GUIStyle();
        style.fontSize = 40;
        style.normal.textColor = Color.red;
        style.fontStyle = FontStyle.Bold;

        // Draw the debug info at the top left
        GUI.Label(new Rect(50, 50, 800, 1000), _debugText, style);
    }

    void InitializeDetector(int width, int height)
    {
        if (_detector != null) _detector.Dispose();
        
        _detector = new TagDetector(width, height, decimation);

        _currentWidth = width;
        _currentHeight = height;
        _colorBuffer = new Color32[width * height];
        
        if (_rawPixelBuffer.IsCreated) _rawPixelBuffer.Dispose();
        _rawPixelBuffer = new NativeArray<byte>(width * height * 4, Allocator.Persistent); 
    }

    void OnCameraFrameReceived(ARCameraFrameEventArgs args)
    {
        _frameCount++;

        if (!arCameraManager.TryAcquireLatestCpuImage(out XRCpuImage image)) {
            _debugText = "Status: Fail to acquire CPU Image";
            return;
        }

        // Downscale logic
        float ratio = (float)image.width / 640f;
        int targetWidth = image.width;
        int targetHeight = image.height;
        if (ratio > 1.0f) {
            targetWidth = Mathf.RoundToInt(image.width / ratio);
            targetHeight = Mathf.RoundToInt(image.height / ratio);
        }

        // Resize detection if screen orientation changed
        if (_currentWidth != targetWidth || _currentHeight != targetHeight) {
            InitializeDetector(targetWidth, targetHeight);
        }

        // Convert Image
        var conversionParams = new XRCpuImage.ConversionParams {
            inputRect = new RectInt(0, 0, image.width, image.height),
            outputDimensions = new Vector2Int(targetWidth, targetHeight),
            outputFormat = TextureFormat.RGBA32, // AprilTag unity repo method takes in RGB image, not grayscale, though tag detection is usually done in grayscale
            transformation = XRCpuImage.Transformation.MirrorY
        };

        // allocate sufficient bytes in gray buffer
        int size = image.GetConvertedDataSize(conversionParams);
        if (_rawPixelBuffer.Length < size) {
            _rawPixelBuffer.Dispose();
            _rawPixelBuffer = new NativeArray<byte>(size, Allocator.Persistent);
        }

        // XRCpuImage takes the raw camera data and pushes converted pixels directly into _rawPixelBuffer
        // _rawPixelBuffer is a NativeArray allocated with Allocator.Persistent, so
        // it sits in a special memory area that doesn't get wiped every frame, saving performance.
        image.Convert(conversionParams, _rawPixelBuffer);
        image.Dispose();

        // copying bytes into a color array is very slow (4 channels, RGBA)
        // instead of moving data, treat every chunk of 4 bytes as 1 color
        NativeArray<Color32> tmp = _rawPixelBuffer.Reinterpret<Color32>(1);
        tmp.CopyTo(_colorBuffer);

        // Calculate simplified FOV from android camera intrinsics.
        // we still have the back up variable for cameraFOV if this cannot be done on the phone
        if (arCameraManager.TryGetIntrinsics(out XRCameraIntrinsics intrinsics)) {
            cameraFOV = 2.0f * Mathf.Atan((intrinsics.resolution.y) / (2.0f * intrinsics.focalLength.y)) * Mathf.Rad2Deg;
        }

        RunDetection(_colorBuffer, cameraFOV, targetWidth, targetHeight);
    }

    void RunDetection(Color32[] pixels, float fov, int w, int h)
    {
        _detector.ProcessImage(pixels, fov, tagSizeMeters);
        
        // Build Debug String
        string status = $"Frame: {_frameCount}\n" +
                        $"Res: {w}x{h}\n" +
                        $"Decimation: {decimation}\n" +
                        $"Tag Family: 41h12\n"; 

        foreach (var tag in _detector.DetectedTags)
        {
            status += $"\n[ID {tag.ID}] \nPos: {tag.Position.ToString("F2")} \nOri: {tag.Rotation.ToString("F2")}";
            
            // Search for profile
            bool foundProfile = false;
            foreach (var profile in tagProfiles) {
                if (profile.tagID == tag.ID && profile.linkedObject != null) {
                    profile.linkedObject.SetActive(true);
                    // profile.linkedObject.transform.localPosition = tag.Position;
                    profile.linkedObject.transform.localPosition = new Vector3(-tag.Position.x, -tag.Position.y, tag.Position.z);
                    // profile.linkedObject.transform.localRotation = tag.Rotation;
                    profile.linkedObject.transform.localRotation = new Quaternion(-tag.Rotation.x, -tag.Rotation.y, tag.Rotation.z, tag.Rotation.w);
                    foundProfile = true;
                }
            }
            if (!foundProfile) status += " (No Profile)";
        }
        
        _debugText = status;
    }
}
