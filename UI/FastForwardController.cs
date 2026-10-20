using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class FastForwardController : MonoBehaviour
{
    [Header("BUTTON")]
    public Button fastForwardButton;

    [Header("FDS PLAYBACK")]
    public FDSPlaybackController fdsPlaybackController;

    [Header("OPTIONAL TEXT")]
    public TMP_Text speedText;

    [Header("SPEED SETTINGS")]
    public float[] speeds = { 1f, 2f, 4f, 8f };

    private int currentSpeedIndex = 0;

    private void Start()
    {
        // Automatically find the FDS controller if not assigned.
        if (fdsPlaybackController == null)
        {
            fdsPlaybackController =
                FindAnyObjectByType<FDSPlaybackController>();
        }

        // Start at normal speed.
        SetSpeed(0);

        if (fastForwardButton != null)
        {
            fastForwardButton.onClick.AddListener(
                ToggleFastForward
            );
        }
    }

    public void ToggleFastForward()
    {
        if (speeds == null || speeds.Length == 0)
            return;

        currentSpeedIndex++;

        if (currentSpeedIndex >= speeds.Length)
        {
            currentSpeedIndex = 0;
        }

        SetSpeed(currentSpeedIndex);
    }

    private void SetSpeed(int index)
    {
        if (speeds == null || speeds.Length == 0)
        {
            UpdateUI(1f);

            if (fdsPlaybackController != null)
            {
                fdsPlaybackController.SetPlaybackSpeed(1f);
            }

            return;
        }

        currentSpeedIndex =
            Mathf.Clamp(
                index,
                0,
                speeds.Length - 1
            );

        float selectedSpeed =
            Mathf.Max(
                0f,
                speeds[currentSpeedIndex]
            );

        /*
         * IMPORTANT:
         *
         * We do NOT use Time.timeScale.
         *
         * The FDSPlaybackController distributes
         * this speed to the fire simulation,
         * smoke simulation and hazard simulation.
         *
         * Player movement and camera remain normal.
         */
        if (fdsPlaybackController != null)
        {
            fdsPlaybackController.SetPlaybackSpeed(
                selectedSpeed
            );
        }

        UpdateUI(selectedSpeed);

        Debug.Log(
            "FIRE SIMULATION SPEED: " +
            selectedSpeed.ToString("0.##") +
            "x"
        );
    }

    private void UpdateUI(float speed)
    {
        if (speedText == null)
            return;

        if (Mathf.Approximately(speed, 1f))
        {
            speedText.text =
                "FAST FORWARD";
        }
        else
        {
            speedText.text =
                "SPEED " +
                speed.ToString("0.##") +
                "X";
        }
    }

    public void ResetSpeed()
    {
        currentSpeedIndex = 0;

        SetSpeed(0);
    }

    private void OnDisable()
    {
        /*
         * Return only the fire simulation to normal.
         *
         * Time.timeScale is never changed.
         */
        if (fdsPlaybackController != null)
        {
            fdsPlaybackController.SetPlaybackSpeed(1f);
        }
    }

    private void OnDestroy()
    {
        if (fastForwardButton != null)
        {
            fastForwardButton.onClick.RemoveListener(
                ToggleFastForward
            );
        }
    }
}