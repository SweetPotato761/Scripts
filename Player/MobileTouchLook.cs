using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MobileTouchLook : MonoBehaviour
{
    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("References")]
    public Transform playerBody;
    public Transform cameraPivot;


    // =========================================================
    // SENSITIVITY
    // =========================================================

    [Header("Sensitivity")]
    public float horizontalSensitivity = 0.15f;
    public float verticalSensitivity = 0.15f;


    // =========================================================
    // VERTICAL LOOK LIMITS
    // =========================================================

    [Header("Vertical Look Limits")]
    public float minimumPitch = -60f;
    public float maximumPitch = 75f;


    // =========================================================
    // TOUCH AREA
    // =========================================================

    [Header("Touch Area")]

    [Range(0f, 1f)]
    [Tooltip(
        "Touches beginning left of this percentage " +
        "are reserved for the joystick."
    )]
    public float joystickSideLimit = 0.40f;


    // =========================================================
    // PC / WINDOWS
    // =========================================================

    [Header("PC / Windows Mouse")]

    [Tooltip(
        "Enable mouse camera control in Unity Editor and Windows build."
    )]
    public bool enableMouseLook = true;

    [Tooltip(
        "If enabled, hold left mouse button and drag to look around."
    )]
    public bool requireMouseDrag = true;


    // =========================================================
    // SMOOTHING
    // =========================================================

    [Header("Smoothing")]
    public bool useSmoothing = true;
    public float smoothingSpeed = 15f;


    // =========================================================
    // DEBUG
    // =========================================================

    [Header("Debug")]
    public bool showDebugLogs = false;


    // =========================================================
    // INTERNAL
    // =========================================================

    private int activeFingerId = -1;

    private float targetYaw;
    private float targetPitch;

    private float currentYaw;
    private float currentPitch;

    private Vector2 previousMousePosition;

    private bool mouseDragging = false;

    private bool lookEnabled = true;


    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        if (playerBody == null)
        {
            playerBody = transform;
        }


        if (cameraPivot == null)
        {
            Debug.LogError(
                "MobileTouchLook: Camera Pivot is not assigned."
            );

            enabled = false;

            return;
        }


        targetYaw =
            playerBody.eulerAngles.y;

        currentYaw =
            targetYaw;


        targetPitch =
            NormalizeAngle(
                cameraPivot.localEulerAngles.x
            );

        currentPitch =
            targetPitch;
    }


    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (!lookEnabled)
        {
            return;
        }


#if UNITY_EDITOR || UNITY_STANDALONE

        if (enableMouseLook)
        {
            HandleMouseInput();
        }

#else

        HandleTouchInput();

#endif
    }


    // =========================================================
    // LATE UPDATE
    // =========================================================

    private void LateUpdate()
    {
        if (!lookEnabled)
        {
            return;
        }


        if (playerBody == null ||
            cameraPivot == null)
        {
            return;
        }


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


        // Horizontal rotation
        playerBody.rotation =
            Quaternion.Euler(
                0f,
                currentYaw,
                0f
            );


        // Vertical rotation
        cameraPivot.localRotation =
            Quaternion.Euler(
                currentPitch,
                0f,
                0f
            );
    }


    // =========================================================
    // MOBILE TOUCH INPUT
    // =========================================================

    private void HandleTouchInput()
    {
        if (Input.touchCount == 0)
        {
            activeFingerId = -1;

            return;
        }


        foreach (Touch touch in Input.touches)
        {
            // =================================================
            // TOUCH START
            // =================================================

            if (touch.phase ==
                TouchPhase.Began)
            {
                if (activeFingerId != -1)
                {
                    continue;
                }


                bool isOnLeftSide =
                    touch.position.x <
                    Screen.width *
                    joystickSideLimit;


                if (isOnLeftSide)
                {
                    continue;
                }


                if (IsPointerOverInteractiveControl(
                        touch.position))
                {
                    continue;
                }


                activeFingerId =
                    touch.fingerId;


                if (showDebugLogs)
                {
                    Debug.Log(
                        "Camera look finger started: " +
                        activeFingerId
                    );
                }
            }


            // =================================================
            // IGNORE OTHER FINGERS
            // =================================================

            if (touch.fingerId !=
                activeFingerId)
            {
                continue;
            }


            // =================================================
            // TOUCH MOVEMENT
            // =================================================

            if (touch.phase ==
                TouchPhase.Moved)
            {
                ApplyLookDelta(
                    touch.deltaPosition
                );
            }


            // =================================================
            // TOUCH END
            // =================================================

            if (touch.phase ==
                    TouchPhase.Ended ||
                touch.phase ==
                    TouchPhase.Canceled)
            {
                activeFingerId = -1;
            }
        }
    }


    // =========================================================
    // PC / WINDOWS MOUSE
    // =========================================================

    private void HandleMouseInput()
    {
        // =====================================================
        // MOUSE DRAG MODE
        // =====================================================

        if (requireMouseDrag)
        {
            if (Input.GetMouseButtonDown(0))
            {
                Vector2 mousePosition =
                    Input.mousePosition;


                bool isOnLeftSide =
                    mousePosition.x <
                    Screen.width *
                    joystickSideLimit;


                if (!isOnLeftSide &&
                    !IsPointerOverInteractiveControl(
                        mousePosition))
                {
                    mouseDragging =
                        true;


                    previousMousePosition =
                        mousePosition;
                }
            }


            if (Input.GetMouseButton(0) &&
                mouseDragging)
            {
                Vector2 currentPosition =
                    Input.mousePosition;


                Vector2 delta =
                    currentPosition -
                    previousMousePosition;


                previousMousePosition =
                    currentPosition;


                ApplyLookDelta(
                    delta
                );
            }


            if (Input.GetMouseButtonUp(0))
            {
                mouseDragging =
                    false;
            }
        }

        // =====================================================
        // FREE MOUSE LOOK MODE
        // =====================================================

        else
        {
            Vector2 mousePosition =
                Input.mousePosition;


            if (IsPointerOverInteractiveControl(
                    mousePosition))
            {
                return;
            }


            float mouseX =
                Input.GetAxisRaw("Mouse X");


            float mouseY =
                Input.GetAxisRaw("Mouse Y");


            Vector2 delta =
                new Vector2(
                    mouseX * 10f,
                    mouseY * 10f
                );


            ApplyLookDelta(
                delta
            );
        }
    }


    // =========================================================
    // UI CHECK
    // =========================================================

    private bool IsPointerOverInteractiveControl(
        Vector2 screenPosition)
    {
        if (EventSystem.current == null)
        {
            return false;
        }


        PointerEventData pointerData =
            new PointerEventData(
                EventSystem.current
            );


        pointerData.position =
            screenPosition;


        List<RaycastResult> results =
            new List<RaycastResult>();


        EventSystem.current.RaycastAll(
            pointerData,
            results
        );


        foreach (
            RaycastResult result
            in results)
        {
            if (result.gameObject == null)
            {
                continue;
            }


            // Buttons, sliders, dropdowns, etc.
            if (result.gameObject
                    .GetComponentInParent<Selectable>()
                != null)
            {
                return true;
            }


            // Joystick
            if (result.gameObject
                    .GetComponentInParent<SimpleJoystick>()
                != null)
            {
                return true;
            }
        }


        return false;
    }


    // =========================================================
    // APPLY LOOK
    // =========================================================

    private void ApplyLookDelta(
        Vector2 delta)
    {
        targetYaw +=
            delta.x *
            horizontalSensitivity;


        targetPitch -=
            delta.y *
            verticalSensitivity;


        targetPitch =
            Mathf.Clamp(
                targetPitch,
                minimumPitch,
                maximumPitch
            );
    }


    // =========================================================
    // ENABLE / DISABLE LOOK
    // =========================================================

    public void SetLookEnabled(
        bool isEnabled)
    {
        lookEnabled =
            isEnabled;


        activeFingerId =
            -1;


        mouseDragging =
            false;


        if (showDebugLogs)
        {
            Debug.Log(
                "Camera look enabled: " +
                isEnabled
            );
        }
    }


    // =========================================================
    // RESET LOOK
    // =========================================================

    public void ResetLook()
    {
        if (playerBody == null ||
            cameraPivot == null)
        {
            return;
        }


        targetYaw =
            playerBody.eulerAngles.y;


        currentYaw =
            targetYaw;


        targetPitch =
            NormalizeAngle(
                cameraPivot.localEulerAngles.x
            );


        currentPitch =
            targetPitch;


        activeFingerId =
            -1;


        mouseDragging =
            false;
    }


    // =========================================================
    // NORMALIZE ANGLE
    // =========================================================

    private float NormalizeAngle(
        float angle)
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