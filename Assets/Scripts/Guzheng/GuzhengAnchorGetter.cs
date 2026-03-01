using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class GuzhengAnchorGetter : MonoBehaviour
{
    [Tooltip("Part of the Guzheng anchor's GameObject name to look for (e.g., 'Guzheng')")]
    public string guzhengAnchorName = "guzheng";
    
    // cache the transform so we don't do expensive searches multiple times
    // FindObjectsOfType is an expensive operation
    private Transform cachedGuzhengTransform;

    public Transform FindGuzhengAnchor()
    {
        if (cachedGuzhengTransform != null) return cachedGuzhengTransform;

        // find all active anchors in the scene
        ARAnchor[] anchors = FindObjectsOfType<ARAnchor>();

        foreach (var anchor in anchors)
        {
            if (anchor.gameObject.name.Contains(guzhengAnchorName))
            {
                cachedGuzhengTransform = anchor.transform;
                Debug.Log("Guzheng Anchor found by name!");
                return cachedGuzhengTransform;
            }
        }

        // Fallback: If we didn't find it by name, but there is exactly ONE anchor, it must be the Guzheng
        if (cachedGuzhengTransform == null && anchors.Length == 1)
        {
            cachedGuzhengTransform = anchors[0].transform;
            Debug.Log("Guzheng Anchor found by fallback (only anchor in scene)!");
            return cachedGuzhengTransform;
        }

        return null;
    }
}