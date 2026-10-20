using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class MobilePlayerController : MonoBehaviour
{
    [Header("References")]
    public SimpleJoystick movementJoystick;
    public Transform cameraPivot;

    [Header("Movement")]
    public float movementSpeed = 1400f;
    public float rotationSmoothness = 12f;

    [Header("Gravity")]
    public float gravity = -6867f;
    public float groundedForce = -140f;

    [Header("Optional Running")]
    public bool allowRunning = false;
    public float runningMultiplier = 1.6f;

    [Header("Fall Recovery")]
    public Transform respawnPoint;
    public float fallLimitY = -3000f;
    public float respawnDelay = 0f;

    [Header("Debug")]
    public bool showDebugLogs = false;

    private CharacterController characterController;
    private Vector3 verticalVelocity;

    private bool movementEnabled = true;
    private bool isRespawning;

    private Vector3 startingPosition;
    private Quaternion startingRotation;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        startingPosition = transform.position;
        startingRotation = transform.rotation;
    }

    private void Update()
    {
        if (isRespawning)
            return;

        if (movementEnabled)
            HandleMovement();

        ApplyGravity();
        CheckForFall();
    }

    private void HandleMovement()
    {
        if (movementJoystick == null)
            return;

        Vector2 input = movementJoystick.Direction;

        if (input.sqrMagnitude > 1f)
            input.Normalize();

        if (input.sqrMagnitude < 0.001f)
            return;

        Transform directionReference =
            cameraPivot != null
                ? cameraPivot
                : transform;

        Vector3 forward = directionReference.forward;
        Vector3 right = directionReference.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 movementDirection =
            forward * input.y +
            right * input.x;

        if (movementDirection.sqrMagnitude > 1f)
            movementDirection.Normalize();

        float speed = movementSpeed;

        if (allowRunning && input.magnitude > 0.9f)
            speed *= runningMultiplier;

        characterController.Move(
            movementDirection *
            speed *
            Time.deltaTime
        );

        if (movementDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(
                    movementDirection,
                    Vector3.up
                );

            transform.rotation =
                Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    rotationSmoothness *
                    Time.deltaTime
                );
        }
    }

    private void ApplyGravity()
    {
        if (characterController.isGrounded &&
            verticalVelocity.y < 0f)
        {
            verticalVelocity.y = groundedForce;
        }
        else
        {
            verticalVelocity.y +=
                gravity *
                Time.deltaTime;
        }

        characterController.Move(
            verticalVelocity *
            Time.deltaTime
        );
    }

    private void CheckForFall()
    {
        if (transform.position.y <= fallLimitY)
        {
            if (respawnDelay > 0f)
                StartCoroutine(RespawnAfterDelay());
            else
                RespawnPlayer();
        }
    }

    private System.Collections.IEnumerator RespawnAfterDelay()
    {
        isRespawning = true;

        yield return new WaitForSeconds(respawnDelay);

        RespawnPlayer();

        isRespawning = false;
    }

    public void RespawnPlayer()
    {
        Vector3 targetPosition =
            respawnPoint != null
                ? respawnPoint.position
                : startingPosition;

        Quaternion targetRotation =
            respawnPoint != null
                ? respawnPoint.rotation
                : startingRotation;

        bool wasEnabled =
            characterController.enabled;

        characterController.enabled = false;

        transform.position = targetPosition;
        transform.rotation = targetRotation;

        characterController.enabled = wasEnabled;

        verticalVelocity = Vector3.zero;

        if (movementJoystick != null)
        {
            movementJoystick.SendMessage(
                "ResetJoystick",
                SendMessageOptions.DontRequireReceiver
            );
        }
    }

    public void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;

        if (!enabled)
            verticalVelocity = Vector3.zero;
    }

    public void TeleportPlayer(Vector3 worldPosition)
    {
        bool wasEnabled =
            characterController.enabled;

        characterController.enabled = false;

        transform.position = worldPosition;

        characterController.enabled = wasEnabled;

        verticalVelocity = Vector3.zero;
    }
}