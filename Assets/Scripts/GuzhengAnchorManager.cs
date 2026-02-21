using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARTrackedImageManager))]
public class GuzhengAnchorManager : MonoBehaviour
{
    [Header("Guzheng Settings")]
    [Tooltip("The 3D Guzheng prefab to spawn for the battle.")]
    public GameObject guzhengPrefab;
    
    [Tooltip("How long the image must be continuously tracked before anchoring.")]
    public float requiredTrackingTime = 2.0f;

    private ARTrackedImageManager imageManager;
    
    private Dictionary<TrackableId, GameObject> spawnedGuzhengs = new Dictionary<TrackableId, GameObject>();
    private Dictionary<TrackableId, float> trackingTimers = new Dictionary<TrackableId, float>();
    private Dictionary<TrackableId, bool> isAnchored = new Dictionary<TrackableId, bool>();

    public string DebugStatusText { get; private set; } = "";

    void Awake()
    {
        imageManager = GetComponent<ARTrackedImageManager>();
    }

    void Update()
    {
        foreach (var trackedImage in imageManager.trackables)
        {
            TrackableId id = trackedImage.trackableId;

            // spawn the guzheng immediately if the marker has not been seen before
            if (!spawnedGuzhengs.ContainsKey(id))
            {
                GameObject instance = Instantiate(guzhengPrefab, trackedImage.transform);
                
                spawnedGuzhengs[id] = instance;
                trackingTimers[id] = 0f;
                isAnchored[id] = false;
            }

            if (isAnchored[id]) continue;

            // temporary timer logic to determine when to anchor the guzheng to the marker
            // replace this with isStringsAligned boolean later on
            if (trackedImage.trackingState == TrackingState.Tracking)
            {
                trackingTimers[id] += Time.deltaTime;

                if (trackingTimers[id] >= requiredTrackingTime)
                {
                    AnchorGuzheng(id);
                }
            }
            else
            {
                // tracking dropped to Limited or None before the timer was up. reset the timer, because we cannot guarantee a detection
                trackingTimers[id] = 0f;
            }
        }
    }

    private void AnchorGuzheng(TrackableId id)
    {
        GameObject guzhengInstance = spawnedGuzhengs[id];

        // decouple the guzheng model from the marker
        guzhengInstance.transform.SetParent(null, true);

        // ARAnchor components takes over the transform and stabilise using SLAM
        if (guzhengInstance.GetComponent<ARAnchor>() == null)
        {
            guzhengInstance.AddComponent<ARAnchor>();
        }

        isAnchored[id] = true;
        DebugStatusText = "Guzheng Anchored";
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