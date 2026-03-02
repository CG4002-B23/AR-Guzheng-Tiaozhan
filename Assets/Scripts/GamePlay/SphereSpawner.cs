using System.Collections.Generic;
using UnityEngine;

// use object poolling to store the spheres so we don't instantiate and destroy spheres in every frame
public class SphereSpawner : MonoBehaviour
{
    [Header("Pool Settings")]
    public GameObject spherePrefab;
    [Tooltip("How many spheres to keep in memory at once")]
    public int poolSize = 80;

    private Queue<GameObject> pool = new Queue<GameObject>();

    void Awake()
    {
        InitializePool();
    }

    private void InitializePool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = Instantiate(spherePrefab, transform);
            obj.SetActive(false);
            pool.Enqueue(obj);
        }
    }

    public GameObject GetSphere()
    {
        if (pool.Count > 0)
        {
            GameObject obj = pool.Dequeue();
            obj.SetActive(true);
            return obj;
        }
        else
        {
            // failsafe: make a new sphere if we run out of spheres
            GameObject obj = Instantiate(spherePrefab, transform);
            return obj;
        }
    }

    public void ReturnSphere(GameObject obj)
    {
        if (!pool.Contains(obj)) // don't take the sphere back if already in queue
        {
            obj.SetActive(false);
            pool.Enqueue(obj);
        }
    }
}