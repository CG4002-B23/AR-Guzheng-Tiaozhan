using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARPlaneManager))]
public class AutoEnemySpawner : MonoBehaviour
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

    void OnEnable()
    {
        // Subscribe to the event that tells us when planes are found/updated
        planeManager.planesChanged += OnPlanesChanged;
    }

    void OnDisable()
    {
        planeManager.planesChanged -= OnPlanesChanged;
    }

    private void OnPlanesChanged(ARPlanesChangedEventArgs args)
    {
        // If we already spawned the object, do nothing
        if (objectSpawned) return;

        // Check newly added planes
        foreach (var plane in args.added)
        {
            CheckAndSpawn(plane);
        }

        // Check updated planes (they might have grown big enough now)
        foreach (var plane in args.updated)
        {
            CheckAndSpawn(plane);
        }
    }

    // Spawn object only if the plane is large enough
    private void CheckAndSpawn(ARPlane plane)
    {
        if (objectSpawned) return;
        if (plane.alignment != PlaneAlignment.HorizontalUp) return; // ensure plane is horizontal
        float area = plane.size.x * plane.size.y;

        if (area >= minPlaneArea)
        {
            SpawnObject(plane);
        }
    }

    private void SpawnObject(ARPlane plane)
    {
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