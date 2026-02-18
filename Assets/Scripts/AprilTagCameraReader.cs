using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.Collections;
using System;

public class AprilTagCameraReader : MonoBehaviour
{
    // define an event so the detector (in AprilTagProcessor.cs) can subscribe to new frames from here
    public event Action<Color32[], int, int, float> OnFrameAvailable;

    [Header("AR Settings")]
    [Tooltip("Put AR main camera here")]
    public ARCameraManager arCameraManager;

    [Tooltip("Input cameraFOV from phone specification if known. The value will also be computed in code")]
    public float defaultCameraFOV = 80.0f;
    [Tooltip("Select width of image to downscale to (max 640). The image will be process at this dimension. The image height will be scaled accordingly.")]
    public float targetWidthToProcess = 640.0f;

    private NativeArray<byte> _rawPixelBuffer;
    private Color32[] _colorBuffer;

    void Start()
    {
        // force auto focus on the camera
        if (arCameraManager != null) {
            arCameraManager.autoFocusRequested = true;
            arCameraManager.frameReceived += OnCameraFrameReceived;
        }
    }

    void OnDestroy()
    {
        if (arCameraManager != null) arCameraManager.frameReceived -= OnCameraFrameReceived;
        if (_rawPixelBuffer.IsCreated) _rawPixelBuffer.Dispose();
    }

    void OnCameraFrameReceived(ARCameraFrameEventArgs args)
    {
        if (!arCameraManager.TryAcquireLatestCpuImage(out XRCpuImage image)) {
            return;
        }

        // Downscale logic
        float ratio = (float)image.width / targetWidthToProcess;
        int targetWidth = image.width;
        int targetHeight = image.height;
        if (ratio > 1.0f) {
            targetWidth = Mathf.RoundToInt(image.width / ratio);
            targetHeight = Mathf.RoundToInt(image.height / ratio);
        }

        // init color buffer
        int numPixels = targetWidth * targetHeight;
        if (_colorBuffer == null || _colorBuffer.Length != numPixels) {
            _colorBuffer = new Color32[numPixels];
        }

        // Convert Image to format for detection
        var conversionParams = new XRCpuImage.ConversionParams {
            inputRect = new RectInt(0, 0, image.width, image.height),
            outputDimensions = new Vector2Int(targetWidth, targetHeight),
            outputFormat = TextureFormat.RGBA32, // AprilTag unity repo method takes in RGB image, not grayscale, though tag detection is usually done in grayscale
            transformation = XRCpuImage.Transformation.MirrorY
        };

        // init buffer to store raw pixel data
        int size = image.GetConvertedDataSize(conversionParams);
        if (!_rawPixelBuffer.IsCreated || _rawPixelBuffer.Length < size) {
            if(_rawPixelBuffer.IsCreated) _rawPixelBuffer.Dispose();
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
        float currentFov = defaultCameraFOV;
        if (arCameraManager.TryGetIntrinsics(out XRCameraIntrinsics intrinsics)) {
            currentFov = 2.0f * Mathf.Atan((intrinsics.resolution.y) / (2.0f * intrinsics.focalLength.y)); // in radians
        }

        // send the processed data to the detector script
        OnFrameAvailable?.Invoke(_colorBuffer, targetWidth, targetHeight, currentFov);
    }
}