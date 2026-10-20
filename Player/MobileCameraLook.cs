using UnityEngine;
using UnityEngine.EventSystems;

public class MobileCameraLook :
    MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler,
    IDragHandler
{
    [Header("Camera References")]
    public Transform playerBody;
    public Transform cameraPivot;

    [Header("Sensitivity")]
    public float horizontalSensitivity = 0.15f;
    public float verticalSensitivity = 0.15f;

    [Header("Vertical Limits")]
    public float minimumLookAngle = -60f;
    public float maximumLookAngle = 75f;

    [Header("Smoothing")]
    public bool useSmoothing = true;
    public float smoothingSpeed = 15f;

    private int activePointerId = -1;

    private float targetYaw;
    private float targetPitch;

    private float currentYaw;
    private float currentPitch;

    private bool lookEnabled = true;

    private void Start()
    {
        if (playerBody != null)
        {
            targetYaw =
                playerBody.eulerAngles.y;

            currentYaw =
                targetYaw;
        }

        if (cameraPivot != null)
        {
            targetPitch =
                NormalizeAngle(
                    cameraPivot.localEulerAngles.x
                );

            currentPitch =
                targetPitch;
        }
    }

    private void LateUpdate()
    {
        if (!lookEnabled)
            return;

        if (useSmoothing)
        {
            currentYaw =
                Mathf.LerpAngle(
                    currentYaw,
                    targetYaw,
                    smoothingSpeed *
                    Time.deltaTime
                );

            currentPitch =
                Mathf.Lerp(
                    currentPitch,
                    targetPitch,
                    smoothingSpeed *
                    Time.deltaTime
                );
        }
        else
        {
            currentYaw =
                targetYaw;

            currentPitch =
                targetPitch;
        }

        if (playerBody != null)
        {
            playerBody.rotation =
                Quaternion.Euler(
                    0f,
                    currentYaw,
                    0f
                );
        }

        if (cameraPivot != null)
        {
            cameraPivot.localRotation =
                Quaternion.Euler(
                    currentPitch,
                    0f,
                    0f
                );
        }
    }

    public void OnPointerDown(
        PointerEventData eventData)
    {
        if (!lookEnabled)
            return;

        if (activePointerId == -1)
        {
            activePointerId =
                eventData.pointerId;
        }
    }

    public void OnDrag(
        PointerEventData eventData)
    {
        if (!lookEnabled)
            return;

        if (eventData.pointerId !=
            activePointerId)
        {
            return;
        }

        Vector2 dragDelta =
            eventData.delta;

        targetYaw +=
            dragDelta.x *
            horizontalSensitivity;

        targetPitch -=
            dragDelta.y *
            verticalSensitivity;

        targetPitch =
            Mathf.Clamp(
                targetPitch,
                minimumLookAngle,
                maximumLookAngle
            );
    }

    public void OnPointerUp(
        PointerEventData eventData)
    {
        if (eventData.pointerId ==
            activePointerId)
        {
            activePointerId = -1;
        }
    }

    public void SetLookEnabled(bool enabled)
    {
        lookEnabled = enabled;

        if (!enabled)
        {
            activePointerId = -1;
        }
    }

    private float NormalizeAngle(float angle)
    {
        while (angle > 180f)
        {
            angle -= 360f;
        }

        while (angle < -180f)
        {
            angle += 360f;
        }

        return angle;
    }
}