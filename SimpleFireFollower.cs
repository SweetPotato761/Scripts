using UnityEngine;

public class SimpleFireFollower : MonoBehaviour
{
    public Transform fireSource;

    void LateUpdate()
    {
        if (fireSource != null)
            transform.position = fireSource.position;
    }
}