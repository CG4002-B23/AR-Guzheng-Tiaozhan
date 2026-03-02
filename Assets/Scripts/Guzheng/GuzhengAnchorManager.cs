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
                
                GuzhengAlignmentChecker alignmentChecker = FindFirstObjectByType<GuzhengAlignmentChecker>();
                if (alignmentChecker != null)
                    alignmentChecker.guzhengSpawner = spawnedGuzheng.GetComponent<ARStringSpawner>();
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
        StateManager.Instance.ChangeState(StateManager.GameState.FieldScanning);
    }

    public void DestroyGuzheng()
    {
        if (spawnedGuzheng != null)
        {
            Destroy(spawnedGuzheng);
            spawnedGuzheng = null;
            trackingTimer = 0f;
            isAnchored = false;
        }
    }

    public void AutoAlignGuzheng(Vector3 targetPosition)
    {
        if (spawnedGuzheng == null) return;

        // remove current anchor
        ARAnchor currentAnchor = spawnedGuzheng.GetComponent<ARAnchor>();
        if (currentAnchor != null)
            DestroyImmediate(currentAnchor); // so we can add a new one right away

        // compute rotation to face enemy
        Vector3 directionToEnemy = targetPosition - spawnedGuzheng.transform.position;
        directionToEnemy.y = 0; // rotation along y axis
        
        if (directionToEnemy != Vector3.zero)
            spawnedGuzheng.transform.rotation = Quaternion.LookRotation(directionToEnemy);

        // reanchor guzheng
        guzhengAnchor = spawnedGuzheng.AddComponent<ARAnchor>();
    }
}