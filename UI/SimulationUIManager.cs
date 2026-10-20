
using UnityEngine;

public class SimulationUIManager : MonoBehaviour
{
    [Header("FDS PLAYBACK")]
    public FDSPlaybackController playbackController;

    [Header("EVACUATION RESULT PANELS")]
    public GameObject evacuationSuccessfulPanel;
    public GameObject evacuationFailedPanel;

    [Header("PLAYER RESET")]
    public PlayerFireDeath playerFireDeath;
    public GameObject playerObject;
    public Transform playerSpawnPoint;

    [Header("FIRE RESET")]
    public MultipleFireOriginManager multipleFireOriginManager;

    [Header("SIMULATION FLOW")]
    public SimulationFlowManager simulationFlowManager;

    private void Awake()
    {
        if (simulationFlowManager == null)
        {
            simulationFlowManager =
                FindAnyObjectByType<SimulationFlowManager>();
        }
    }

    // PLAY FDS SIMULATION
    public void PlaySimulation()
    {
        if (simulationFlowManager != null &&
            simulationFlowManager.IsSimulationEnded)
        {
            return;
        }

        if (playbackController != null)
            playbackController.Play();
    }

    // PAUSE FDS SIMULATION
    public void PauseSimulation()
    {
        if (playbackController != null)
            playbackController.Pause();
    }

    // SHOW SUCCESS PANEL
    public void ShowEvacuationSuccessful()
    {
        if (simulationFlowManager != null)
        {
            simulationFlowManager.PlayerReachedExit();
            return;
        }

        Debug.LogWarning(
            "[SimulationUIManager] SimulationFlowManager is not assigned."
        );
    }

    // SHOW FAILURE PANEL
    public void ShowEvacuationFailed()
    {
        if (simulationFlowManager != null)
        {
            simulationFlowManager.PlayerDied();
            return;
        }

        Debug.LogWarning(
            "[SimulationUIManager] SimulationFlowManager is not assigned."
        );
    }

    // PLAYER DEATH EVENT
    public void PlayerDied()
    {
        ShowEvacuationFailed();
    }

    // RESET SIMULATION
    public void ResetSimulation()
    {
        if (playbackController != null)
            playbackController.StopAndReset();

        if (simulationFlowManager != null)
        {
            simulationFlowManager.RestartSimulation();
            return;
        }

        // Fallback reset if SimulationFlowManager is not assigned.
        ResetPlayerAndFire();
    }

    // START A NEW SESSION
    public void StartNewSession()
    {
        ResetSimulation();
    }

    // RESET PLAYER AND MULTIPLE FIRES
    private void ResetPlayerAndFire()
    {
        if (evacuationSuccessfulPanel != null)
            evacuationSuccessfulPanel.SetActive(false);

        if (evacuationFailedPanel != null)
            evacuationFailedPanel.SetActive(false);

        if (playerFireDeath != null)
            playerFireDeath.ResetDeath();

        if (playerObject != null)
        {
            playerObject.SetActive(true);

            if (playerSpawnPoint != null)
            {
                playerObject.transform.SetPositionAndRotation(
                    playerSpawnPoint.position,
                    playerSpawnPoint.rotation
                );
            }
        }

        if (multipleFireOriginManager != null)
            multipleFireOriginManager.ResetMultipleFireScenario();

        Debug.Log("[SimulationUIManager] Player and fire reset complete.");
    }
}
