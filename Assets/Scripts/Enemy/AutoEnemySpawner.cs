using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARPlaneManager))]
public class AutoEnemySpawner : StateListener
{
    [Header("Settings")]
    public GameObject objectToSpawn;
    public float minPlaneArea = 10.0f; // Minimum area required before object is spawned

    [Header("Data (Auto-Assigned)")]
    public Transform guzhengTransform;

    [Tooltip("Possible spawn locations for the enemy")]
    public List<Vector3> savedLocations = new List<Vector3>();

    private GuzhengAnchorGetter anchorGetter;
    private ARPlaneManager planeManager;
    private GameObject spawnedObject;
    private bool objectSpawned = false;

    void Awake()
    {
        planeManager = GetComponent<ARPlaneManager>();
        anchorGetter = FindFirstObjectByType<GuzhengAnchorGetter>();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        // Subscribe to the event that tells us when planes are found/updated
        planeManager.planesChanged += OnPlanesChanged;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        planeManager.planesChanged -= OnPlanesChanged;
    }

    protected override void OnStateToggled(bool isNowActive)
    {
        if (isNowActive)
        {
            if (objectSpawned) return;

            planeManager.enabled = true; // allow planes to be created

            // reenable any previously hidden planes (there should be none)
            foreach (var plane in planeManager.trackables)
                plane.gameObject.SetActive(true);
        }
        else
        {
            planeManager.enabled = false; // stop new planes from being created

            // hide all existing hidden planes (cannot call Destroy according to docs)
            foreach (var plane in planeManager.trackables)
                plane.gameObject.SetActive(false);
        }
    }

    private void OnPlanesChanged(ARPlanesChangedEventArgs args)
    {
        if (objectSpawned) return;
        if (guzhengTransform == null && anchorGetter != null) 
            guzhengTransform = anchorGetter.FindGuzhengAnchor();
        if (guzhengTransform == null) return; // if still don't have guzheng transform, we need to wait for it to be available

        // Check newly added planes
        foreach (var plane in args.added)
        {
            if (areaIsLargeEnough(plane))  {
                SpawnObject(plane);
                StateManager.Instance.ChangeState(StateManager.GameState.GuzhengAlignment);
            }
        }

        // Check updated planes (they might have grown big enough now)
        foreach (var plane in args.updated)
        {
            if (areaIsLargeEnough(plane)) {
                SpawnObject(plane);
                StateManager.Instance.ChangeState(StateManager.GameState.GuzhengAlignment);
            }
        }
    }
    private bool areaIsLargeEnough(ARPlane plane)
    {
        if (objectSpawned) return false;
        if (plane.alignment != PlaneAlignment.HorizontalUp) return false; // ensure plane is horizontal
        float area = plane.size.x * plane.size.y;

        if (area >= minPlaneArea)
        {
            return true;
        }
        return false;
    }

    private void SpawnObject(ARPlane plane)
    {
        if (objectSpawned) return;

        // convert boundary vertices of the plane to world space
        List<Vector3> worldBoundaryPoints = new List<Vector3>();
        foreach (Vector2 boundaryPoint in plane.boundary)
        {
            // ARPlane boundary points are 2D (X, Y) in the plane's local space. 
            // Y is actually the Z-axis in local 3D space.
            Vector3 localPoint = new Vector3(boundaryPoint.x, 0, boundaryPoint.y);
            Vector3 worldPoint = plane.transform.TransformPoint(localPoint);
            worldBoundaryPoints.Add(worldPoint);
        }

        if (worldBoundaryPoints.Count < 3) return;

        // sort the boundary points according to distance from guzheng (furthest first)
        Vector3 guzhengPos = guzhengTransform.position;
        List<Vector3> sortedFurthestPoints = worldBoundaryPoints
            .OrderByDescending(p => Vector3.Distance(p, guzhengPos))
            .ToList();

        List<Vector3> top3Points = sortedFurthestPoints.Take(3).ToList();

        int selectedIndex = Random.Range(0, top3Points.Count);
        Vector3 spawnLocation = top3Points[selectedIndex]; // randomly select index for immediate spawn location

        // save all the possible spawn locations
        savedLocations.Clear();
        for (int i = 0; i < top3Points.Count; i++)
        {
            savedLocations.Add(top3Points[i]);
        }

        // rotation settings
        Vector3 directionToGuzheng = guzhengTransform.position - spawnLocation;
        directionToGuzheng.y = 0; // keep the enemy horizontal on the AR plane
        Quaternion spawnRotation = Quaternion.LookRotation(directionToGuzheng);

        // Spawn the object at the furthest point from the guzheng
        spawnedObject = Instantiate(objectToSpawn, spawnLocation, spawnRotation);
        objectSpawned = true;

        // need to update the enemy spawner object for lane manager to get string information from
        LaneManager laneManager = FindFirstObjectByType<LaneManager>();
        if (laneManager != null)
            laneManager.enemySpawner = spawnedObject.GetComponent<ARStringSpawner>();

        GuzhengAlignmentChecker alignmentChecker = FindFirstObjectByType<GuzhengAlignmentChecker>();
        if (alignmentChecker != null)
            alignmentChecker.enemySpawner = spawnedObject.GetComponent<ARStringSpawner>();

        // disable plane detection after spawning to save performance
        planeManager.enabled = false; 
        
        // hide all planes visuals so only the cube is visible
        // foreach (var p in planeManager.trackables)
        // {
        //     p.gameObject.SetActive(false);
        // }
    }
}