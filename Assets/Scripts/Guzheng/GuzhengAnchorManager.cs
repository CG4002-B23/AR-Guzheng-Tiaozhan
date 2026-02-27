using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARTrackedImageManager))]
public class GuzhengAnchorManager : StateListener
{
    [Header("Guzheng Settings")]
    [Tooltip("The 3D Guzheng prefab to spawn for the battle.")]
    public GameObject guzhengPrefab;
    
    [Tooltip("How long the image must be continuously tracked before anchoring.")]
    public float requiredTrackingTime = 2.0f;

    private ARTrackedImageManager imageManager;
    
    private GameObject spawnedGuzheng;
    private ARAnchor guzhengAnchor;
    private float trackingTimer = 0f;
    private bool isAnchored = false;

    public string DebugStatusText { get; private set; } = "";

    void Awake()
    {
        imageManager = GetComponent<ARTrackedImageManager>();
    }

    void Update()
    {
        if (!isActiveState) return;

        foreach (var trackedImage in imageManager.trackables)
        {
            // spawn the guzheng immediately if the marker has not been seen before
            if (spawnedGuzheng == null)
            {
                spawnedGuzheng = Instantiate(guzhengPrefab, trackedImage.transform);
                trackingTimer = 0f;
                isAnchored = false;

                // need to update the guzheng spawner object for lane manager to get string information from
                // later since the guzheng will despawn, we need to update the guzheng spawner again
                LaneManager laneManager = FindFirstObjectByType<LaneManager>();
                if (laneManager != null)
                    laneManager.guzhengSpawner = spawnedGuzheng.GetComponent<ARStringSpawner>();
            }

            if (isAnchored) continue;

            // temporary timer logic to determine when to anchor the guzheng to the marker
            // replace this with isStringsAligned boolean later on
            if (trackedImage.trackingState == TrackingState.Tracking)
            {
                trackingTimer += Time.deltaTime;
                if (trackingTimer >= requiredTrackingTime) 
                    AnchorGuzheng();
            }
            else
            {
                // tracking dropped to Limited or None before the timer was up. reset the timer, because we cannot guarantee a detection
                trackingTimer = 0f;
                DestroyGuzheng();
            }
        }
    }

    private void AnchorGuzheng()
    {
        // decouple the guzheng model from the marker
        spawnedGuzheng.transform.SetParent(null, true);

        // ARAnchor components takes over the transform and stabilise using SLAM
        if (spawnedGuzheng.GetComponent<ARAnchor>() == null) 
            guzhengAnchor = spawnedGuzheng.AddComponent<ARAnchor>();

        isAnchored = true;
        DebugStatusText = "Guzheng Anchored";
        GameManager.Instance.ChangeState(GameManager.GameState.FieldScanning);
    }

    public void DestroyGuzheng()
    {
        if (spawnedGuzheng != null)
        {
            Destroy(spawnedGuzheng);
            spawnedGuzheng = null;
            trackingTimer = 0f;
            isAnchored = false;
            DebugStatusText = "";
        }
    }

    // debug statements showing on phone screen
    void OnGUI()
    {
        GUIStyle style = new GUIStyle();
        style.fontSize = 40;
        style.normal.textColor = Color.green;
        style.fontStyle = FontStyle.Bold;

        // Draw the debug info at the top right
        GUI.Label(new Rect(800, 50, 800, 1000), DebugStatusText, style);
    }
}