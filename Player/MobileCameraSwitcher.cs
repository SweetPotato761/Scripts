using UnityEngine;

public class MobileCameraSwitcher : MonoBehaviour
{
    [Header("Cameras")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Camera overviewCamera;
    [SerializeField] private Camera[] floorCameras;

    [Header("Player Controls")]
    [SerializeField] private MobilePlayerController playerController;
    [SerializeField] private MobileTouchLook touchLook;

    [Header("Mobile UI")]
    [SerializeField] private GameObject movementJoystick;

    [Header("Start Mode")]
    [SerializeField] private bool startInOverview;

    /*
     * View indexes:
     * 0 = Player Camera
     * 1 = Overview Camera
     * 2 = Floor Camera 1
     * 3 = Floor Camera 2
     * and so on.
     */
    private int currentViewIndex;

    public bool IsOverviewActive()
    {
        return currentViewIndex != 0;
    }

    private void Start()
    {
        if (floorCameras == null)
        {
            floorCameras = new Camera[0];
        }

        currentViewIndex = startInOverview ? 1 : 0;
        ShowView(currentViewIndex);
    }

    /// <summary>
    /// Connect this method to your existing CAMERA button.
    /// </summary>
    public void ToggleCamera()
    {
        int totalViews = 2 + floorCameras.Length;

        currentViewIndex++;

        if (currentViewIndex >= totalViews)
        {
            currentViewIndex = 0;
        }

        ShowView(currentViewIndex);
    }

    public void ShowPlayerCamera()
    {
        currentViewIndex = 0;
        ShowView(currentViewIndex);
    }

    public void ShowOverviewCamera()
    {
        currentViewIndex = 1;
        ShowView(currentViewIndex);
    }

    /// <summary>
    /// Floor number starts at 1.
    /// Example: ShowFloorCamera(1) opens Floor1Camera.
    /// </summary>
    public void ShowFloorCamera(int floorNumber)
    {
        if (floorNumber < 1 || floorNumber > floorCameras.Length)
        {
            Debug.LogWarning(
                $"{name}: Floor camera {floorNumber} is not assigned."
            );
            return;
        }

        currentViewIndex = floorNumber + 1;
        ShowView(currentViewIndex);
    }

    private void ShowView(int viewIndex)
    {
        bool playerViewActive = viewIndex == 0;

        SetCameraState(
            playerCamera,
            playerViewActive
        );

        SetCameraState(
            overviewCamera,
            viewIndex == 1
        );

        for (int i = 0; i < floorCameras.Length; i++)
        {
            bool floorCameraActive = viewIndex == i + 2;

            SetCameraState(
                floorCameras[i],
                floorCameraActive
            );
        }

        SetPlayerControls(playerViewActive);
    }

    private void SetPlayerControls(bool enabledState)
    {
        if (playerController != null)
        {
            playerController.SetMovementEnabled(
                enabledState
            );
        }

        if (touchLook != null)
        {
            touchLook.enabled = enabledState;
        }

        if (movementJoystick != null)
        {
            movementJoystick.SetActive(
                enabledState
            );
        }
    }

    private static void SetCameraState(
        Camera targetCamera,
        bool state)
    {
        if (targetCamera == null)
        {
            return;
        }

        targetCamera.enabled = state;

        AudioListener audioListener =
            targetCamera.GetComponent<AudioListener>();

        if (audioListener != null)
        {
            audioListener.enabled = state;
        }
    }
}