using UnityEngine;

public class BoundsDebugger : MonoBehaviour
{
    void Start()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            Debug.Log("No renderers found.");
            return;
        }

        Bounds bounds = renderers[0].bounds;

        foreach (Renderer r in renderers)
            bounds.Encapsulate(r.bounds);

        Debug.Log("===== MARKET SIZE =====");
        Debug.Log("Center : " + bounds.center);
        Debug.Log("Size   : " + bounds.size);
        Debug.Log("Min    : " + bounds.min);
        Debug.Log("Max    : " + bounds.max);
        Debug.Log("=======================");
    }
}