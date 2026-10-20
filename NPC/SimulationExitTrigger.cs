
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SimulationExitTrigger : MonoBehaviour
{
    [Header("Simulation Manager")]
    public SimulationFlowManager simulationFlowManager;

    [Header("Player")]
    public string playerTag = "Player";

    [Header("Exit")]
    public string exitName = "Exit";

    [Header("Settings")]
    public bool triggerOnlyOnce = true;
    public bool showDebugLog = true;

    private bool exitTriggered;

    private void Awake()
    {
        Collider exitCollider = GetComponent<Collider>();

        if (exitCollider != null)
            exitCollider.isTrigger = true;

        if (simulationFlowManager == null && showDebugLog)
        {
            Debug.LogWarning(
                gameObject.name +
                ": Assign SimulationFlowManager in the Inspector."
            );
        }
    }

    private bool IsPlayer(Collider other)
    {
        // Check the collider object and each parent.
        Transform current = other.transform;

        while (current != null)
        {
            if (current.CompareTag(playerTag))
                return true;

            current = current.parent;
        }

        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggerOnlyOnce && exitTriggered)
            return;

        if (!IsPlayer(other))
            return;

        if (simulationFlowManager == null)
        {
            Debug.LogError(
                gameObject.name +
                ": SimulationFlowManager is not assigned."
            );
            return;
        }

        if (!simulationFlowManager.IsSimulationRunning())
        {
            if (showDebugLog)
            {
                Debug.Log(
                    gameObject.name +
                    ": Player entered the exit, but simulation is not running."
                );
            }

            return;
        }

        exitTriggered = true;

        if (showDebugLog)
        {
            Debug.Log(
                "PLAYER REACHED EXIT: " + exitName +
                " | Object: " + gameObject.name
            );
        }

        simulationFlowManager.PlayerReachedExit();
    }

    public void ResetExitTrigger()
    {
        exitTriggered = false;
    }
}
