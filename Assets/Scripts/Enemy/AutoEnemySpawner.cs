using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARPlaneManager))]
public class AutoEnemySpawner : StateListener
{
    [Header("Settings")]
    public GameObject objectToSpawn;
    public float minPlaneArea = 10.0f; // Minimum area required before object is spawned

    private ARPlaneManager planeManager;
    private GameObject spawnedObject;
    private bool objectSpawned = false;

    void Awake()
    {
        planeManager = GetComponent<ARPlaneManager>();
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

        // Check newly added planes
        foreach (var plane in args.added)
        {
            if (areaIsLargeEnough(plane))  {
                SpawnObject(plane);
                GameManager.Instance.ChangeState(GameManager.GameState.Playing);
            }
        }

        // Check updated planes (they might have grown big enough now)
        foreach (var plane in args.updated)
        {
            if (areaIsLargeEnough(plane)) {
                SpawnObject(plane);
                GameManager.Instance.ChangeState(GameManager.GameState.Playing);
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

        // Spawn the object at the center of the plane
        spawnedObject = Instantiate(objectToSpawn, plane.center, Quaternion.identity);
        objectSpawned = true;

        // disable plane detection after spawning to save performance
        planeManager.enabled = false; 
        
        // hide all planes visuals so only the cube is visible
        // foreach (var p in planeManager.trackables)
        // {
        //     p.gameObject.SetActive(false);
        // }
    }
}