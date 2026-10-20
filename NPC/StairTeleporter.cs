using UnityEngine;

public class StairTeleporter : MonoBehaviour
{
    [Header("Teleport Destination")]
    public Transform destinationPoint;

    [Header("Player Detection")]
    public string playerTag = "Player";

    [Header("Teleport Settings")]
    public bool preserveRotation = true;
    public float exitForwardOffset = 500f;
    public float cooldown = 1f;

    private bool teleportLocked = false;

    private void Start()
    {
        Collider triggerCollider = GetComponent<Collider>();

        if (triggerCollider == null)
        {
            Debug.LogError(
                name + ": StairTeleporter needs a Collider."
            );
        }
        else
        {
            triggerCollider.isTrigger = true;
        }

        if (destinationPoint == null)
        {
            Debug.LogWarning(
                name + ": Destination Point is not assigned."
            );
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log(
            name + " trigger entered by: " +
            other.name
        );

        if (teleportLocked)
            return;

        Transform playerTransform = FindPlayerTransform(other);

        if (playerTransform == null)
            return;

        TeleportPlayer(playerTransform);
    }

    private Transform FindPlayerTransform(Collider other)
    {
        if (other.CompareTag(playerTag))
            return other.transform;

        Transform root = other.transform.root;

        if (root != null &&
            root.CompareTag(playerTag))
        {
            return root;
        }

        Transform current = other.transform.parent;

        while (current != null)
        {
            if (current.CompareTag(playerTag))
                return current;

            current = current.parent;
        }

        return null;
    }

    private void TeleportPlayer(Transform playerTransform)
    {
        if (destinationPoint == null)
        {
            Debug.LogError(
                name + ": Cannot teleport because Destination Point is empty."
            );

            return;
        }

        teleportLocked = true;

        Vector3 destinationPosition =
            destinationPoint.position +
            destinationPoint.forward *
            exitForwardOffset;

        CharacterController characterController =
            playerTransform.GetComponent<CharacterController>();

        if (characterController == null)
        {
            characterController =
                playerTransform.GetComponentInChildren<CharacterController>();
        }

        Rigidbody rb =
            playerTransform.GetComponent<Rigidbody>();

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        playerTransform.position =
            destinationPosition;

        if (!preserveRotation)
        {
            playerTransform.rotation =
                destinationPoint.rotation;
        }

        if (characterController != null)
        {
            characterController.enabled = true;
        }

        Debug.Log(
            "STAIR TELEPORT SUCCESS: " +
            playerTransform.name +
            " moved from " +
            name +
            " to " +
            destinationPoint.name
        );

        Invoke(
            nameof(UnlockTeleport),
            cooldown
        );
    }

    private void UnlockTeleport()
    {
        teleportLocked = false;
    }

    private void OnDrawGizmos()
    {
        Collider col = GetComponent<Collider>();

        if (col != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(
                col.bounds.center,
                col.bounds.size
            );
        }

        if (destinationPoint != null)
        {
            Gizmos.color = Color.yellow;

            Gizmos.DrawSphere(
                destinationPoint.position,
                150f
            );

            Gizmos.DrawLine(
                transform.position,
                destinationPoint.position
            );
        }
    }
}