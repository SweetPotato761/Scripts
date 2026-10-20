using UnityEngine;

public class SimpleJoystick : MonoBehaviour
{
    [Header("References")]
    public RectTransform background;
    public RectTransform handle;

    [Header("Settings")]
    [Range(0.1f, 1f)]
    public float handleRange = 0.6f;

    [Range(0f, 0.5f)]
    public float deadZone = 0.05f;

    [Header("Return")]
    public bool smoothReturn = false;
    public float returnSpeed = 15f;

    [Header("Debug")]
    public bool showDebugLogs = false;

    private Vector2 inputDirection = Vector2.zero;

    private int activeFingerId = -1;

    private Camera uiCamera;

    private Canvas canvas;

    public Vector2 Direction
    {
        get
        {
            if (inputDirection.magnitude < deadZone)
                return Vector2.zero;

            return inputDirection;
        }
    }

    public float Horizontal
    {
        get { return Direction.x; }
    }

    public float Vertical
    {
        get { return Direction.y; }
    }

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();

        if (background == null)
            background = GetComponent<RectTransform>();

        if (canvas != null &&
            canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = canvas.worldCamera;
        }

        ResetJoystick();
    }

    private void Update()
    {
#if UNITY_ANDROID || UNITY_IOS
        HandleMobileTouch();
#endif

        if (activeFingerId == -1 &&
            smoothReturn &&
            handle != null)
        {
            handle.anchoredPosition = Vector2.Lerp(
                handle.anchoredPosition,
                Vector2.zero,
                returnSpeed * Time.unscaledDeltaTime
            );
        }
    }

    private void HandleMobileTouch()
    {
        if (Input.touchCount == 0)
        {
            if (activeFingerId != -1)
                ResetJoystick();

            return;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);

            if (touch.phase == TouchPhase.Began)
            {
                if (activeFingerId != -1)
                    continue;

                if (IsInsideJoystick(touch.position))
                {
                    activeFingerId = touch.fingerId;

                    UpdateJoystickPosition(
                        touch.position
                    );

                    if (showDebugLogs)
                    {
                        Debug.Log(
                            "Joystick finger started: " +
                            activeFingerId
                        );
                    }
                }
            }

            if (touch.fingerId != activeFingerId)
                continue;

            if (touch.phase == TouchPhase.Moved ||
                touch.phase == TouchPhase.Stationary)
            {
                UpdateJoystickPosition(
                    touch.position
                );
            }

            if (touch.phase == TouchPhase.Ended ||
                touch.phase == TouchPhase.Canceled)
            {
                ResetJoystick();

                if (showDebugLogs)
                {
                    Debug.Log(
                        "Joystick finger released."
                    );
                }
            }
        }
    }

    private bool IsInsideJoystick(Vector2 screenPosition)
    {
        if (background == null)
            return false;

        return RectTransformUtility.RectangleContainsScreenPoint(
            background,
            screenPosition,
            uiCamera
        );
    }

    private void UpdateJoystickPosition(
        Vector2 screenPosition)
    {
        if (background == null ||
            handle == null)
        {
            return;
        }

        Vector2 localPoint;

        bool success =
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                background,
                screenPosition,
                uiCamera,
                out localPoint
            );

        if (!success)
            return;

        float radius =
            Mathf.Min(
                background.rect.width,
                background.rect.height
            ) * 0.5f;

        if (radius <= 0f)
            return;

        Vector2 direction =
            localPoint / radius;

        direction =
            Vector2.ClampMagnitude(
                direction,
                1f
            );

        if (direction.magnitude < deadZone)
        {
            inputDirection = Vector2.zero;
        }
        else
        {
            inputDirection = direction;
        }

        float handleRadius =
            radius * handleRange;

        handle.anchoredPosition =
            inputDirection * handleRadius;

        if (showDebugLogs)
        {
            Debug.Log(
                "Joystick Direction: " +
                inputDirection
            );
        }
    }

    public void ResetJoystick()
    {
        inputDirection = Vector2.zero;
        activeFingerId = -1;

        if (handle != null)
        {
            handle.anchoredPosition =
                Vector2.zero;
        }
    }

    public void SetJoystickEnabled(bool value)
    {
        enabled = value;

        if (!value)
        {
            ResetJoystick();
        }
    }
}