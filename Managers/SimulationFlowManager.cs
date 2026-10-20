
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class SimulationFlowManager : MonoBehaviour
{
    [Header("UI PANELS")]
    public GameObject mainInterfacePanel;
    public GameObject fireSetupPanel;
    public GameObject gameplayUI;
    public GameObject evacuationCompletePanel;
    public GameObject evacuationFailedPanel;

    [Header("RESULT TIMER TEXT")]
    public TMP_Text evacuationCompleteTimeText;
    public TMP_Text evacuationFailedTimeText;

    [Header("PLAYER")]
    public GameObject playerObject;
    public Transform player;
    public Transform playerStartPosition;
    public PlayerFireDeath playerFireDeath;

    [Header("MANAGERS")]
    public FirePlacementManager firePlacementManager;
    public MultipleFireOriginManager multipleFireOriginManager;
    public FDSPlaybackController fdsPlaybackController;
    public FireSpreadManager fireSpreadManager;

    [Header("PLAYER CONTROLS")]
    public Behaviour[] playerControlsToDisable;

    [Header("TIMER SETTINGS")]
    public bool startTimerWhenSimulationStarts = true;

    private float evacuationStartTime;
    private float evacuationElapsedTime;
    private bool evacuationTimerRunning;
    private bool simulationEnded;
    private bool simulationStarted;

    public float EvacuationElapsedTime
    {
        get
        {
            if (evacuationTimerRunning)
                return Mathf.Max(
                    0f,
                    Time.unscaledTime - evacuationStartTime
                );

            return evacuationElapsedTime;
        }
    }

    public bool IsSimulationEnded => simulationEnded;
    public bool IsSimulationStarted => simulationStarted;
    public bool IsTimerRunning => evacuationTimerRunning;

    public bool IsSimulationRunning()
    {
        return simulationStarted && !simulationEnded;
    }

    private void Start()
    {
        ShowMainInterface();
        UpdateResultTimerTexts();
    }

    private void Update()
    {
        // The timer is managed ONLY by this script.
        if (!evacuationTimerRunning)
            return;

        if (simulationEnded)
        {
            evacuationTimerRunning = false;
            return;
        }

        evacuationElapsedTime = Mathf.Max(
            0f,
            Time.unscaledTime - evacuationStartTime
        );
    }

    // MAIN INTERFACE
    public void ShowMainInterface()
    {
        SetPanel(mainInterfacePanel, true);
        SetPanel(fireSetupPanel, false);
        SetPanel(gameplayUI, false);
        SetPanel(evacuationCompletePanel, false);
        SetPanel(evacuationFailedPanel, false);

        SetPlayerControls(false);
    }

    // OPEN FIRE SETUP
    public void OpenFireSetup()
    {
        SetPanel(mainInterfacePanel, false);
        SetPanel(fireSetupPanel, true);
        SetPanel(gameplayUI, false);
        SetPanel(evacuationCompletePanel, false);
        SetPanel(evacuationFailedPanel, false);

        SetPlayerControls(false);
    }

    // CONFIRM FIRE LOCATION
    public void ConfirmFireLocation()
    {
        if (firePlacementManager != null &&
            !firePlacementManager.HasConfirmedFire)
        {
            Debug.LogWarning(
                "Please randomize and confirm the fire location first."
            );
            return;
        }

        StartSimulation();
    }

    // START SIMULATION AND TIMER
    public void StartSimulation()
    {
        if (IsSimulationRunning())
        {
            Debug.LogWarning("Simulation is already running.");
            return;
        }

        simulationEnded = false;
        simulationStarted = true;

        evacuationElapsedTime = 0f;
        evacuationStartTime = Time.unscaledTime;
        evacuationTimerRunning = false;

        SetPanel(mainInterfacePanel, false);
        SetPanel(fireSetupPanel, false);
        SetPanel(gameplayUI, true);
        SetPanel(evacuationCompletePanel, false);
        SetPanel(evacuationFailedPanel, false);

        SetPlayerControls(true);

        if (fireSpreadManager != null)
            fireSpreadManager.enabled = true;

        if (fdsPlaybackController != null)
            fdsPlaybackController.enabled = true;

        UpdateResultTimerTexts();

        if (startTimerWhenSimulationStarts)
            StartEvacuationTimer();

        Debug.Log("[SimulationFlowManager] Simulation started.");
    }

    public void StartEvacuationTimer()
    {
        if (!simulationStarted ||
            simulationEnded ||
            evacuationTimerRunning)
        {
            return;
        }

        evacuationStartTime =
            Time.unscaledTime - evacuationElapsedTime;

        evacuationTimerRunning = true;

        Debug.Log("[SimulationFlowManager] Timer started.");
    }

    public void PauseEvacuationTimer()
    {
        if (!evacuationTimerRunning)
            return;

        evacuationElapsedTime = EvacuationElapsedTime;
        evacuationTimerRunning = false;

        Debug.Log("[SimulationFlowManager] Timer paused.");
    }

    public void ResumeEvacuationTimer()
    {
        if (simulationStarted && !simulationEnded)
            StartEvacuationTimer();
    }

    // CALL THIS WHEN THE PLAYER REACHES AN EXIT
    public void PlayerReachedExit()
    {
        Debug.Log("[SimulationFlowManager] Player reached exit.");
        EndEvacuation(true);
    }

    // CALL THIS WHEN THE PLAYER DIES
    public void PlayerDied()
    {
        Debug.Log("[SimulationFlowManager] Player died.");
        EndEvacuation(false);
    }

    // STOP TIMER AND SHOW RESULT
    private void EndEvacuation(bool successful)
    {
        if (!IsSimulationRunning())
        {
            Debug.LogWarning(
                "[SimulationFlowManager] Cannot end evacuation: " +
                "simulation is not running or has already ended."
            );
            return;
        }

        // Capture the final time BEFORE stopping the timer.
        evacuationElapsedTime = EvacuationElapsedTime;

        // Permanently stop this session's timer.
        evacuationTimerRunning = false;
        simulationEnded = true;

        // Update both result labels with the frozen time.
        UpdateResultTimerTexts();

        // Show the correct result panel.
        SetPanel(gameplayUI, false);
        SetPanel(evacuationCompletePanel, successful);
        SetPanel(evacuationFailedPanel, !successful);

        StopSimulationEffects();

        Debug.Log(
            "[SimulationFlowManager] " +
            (successful ? "EVACUATION SUCCESSFUL" : "EVACUATION FAILED") +
            " | Final time: " + FormatEvacuationTime() +
            " | Timer running: " + evacuationTimerRunning
        );
    }

    // UPDATE BOTH RESULT LABELS
    private void UpdateResultTimerTexts()
    {
        string result = FormatEvacuationTime();

        if (evacuationCompleteTimeText != null)
            evacuationCompleteTimeText.text = result;

        if (evacuationFailedTimeText != null)
            evacuationFailedTimeText.text = result;
    }

    // FORMAT: Evacuation Time: 00:00
    private string FormatEvacuationTime()
    {
        int totalSeconds = Mathf.FloorToInt(
            Mathf.Max(0f, evacuationElapsedTime)
        );

        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        return string.Format(
            "Evacuation Time: {0:00}:{1:00}",
            minutes,
            seconds
        );
    }

    // STOP SIMULATION EFFECTS
    private void StopSimulationEffects()
    {
        evacuationTimerRunning = false;

        if (fireSpreadManager != null)
            fireSpreadManager.enabled = false;

        if (fdsPlaybackController != null)
            fdsPlaybackController.enabled = false;

        SetPlayerControls(false);
    }

    // QUIT APPLICATION
    public void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void ExitButton()
    {
        QuitApplication();
    }

    public void ExitToInterface()
    {
        QuitApplication();
    }

    // RESTART CURRENT SCENE
    public void RestartSimulation()
    {
        Time.timeScale = 1f;

        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
    }

    private void SetPlayerControls(bool state)
    {
        if (playerControlsToDisable == null)
            return;

        foreach (Behaviour control in playerControlsToDisable)
        {
            if (control != null)
                control.enabled = state;
        }
    }

    private void SetPanel(GameObject panel, bool state)
    {
        if (panel != null)
            panel.SetActive(state);
    }
}
