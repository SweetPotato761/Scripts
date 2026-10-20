using UnityEngine;

public class PlayerFireDeath : MonoBehaviour
{
    [Header("PLAYER")]
    public Transform player;

    [Header("FIRE MANAGERS")]
    public FirePlacementManager firePlacementManager;
    public MultipleFireOriginManager multipleFireOriginManager;

    [Header("SIMULATION")]
    public SimulationUIManager simulationUIManager;

    [Header("DEATH SETTINGS")]
    public float deathRadiusMeters = 2f;
    public float distanceScale = 0.01f;

    [Header("DEATH UI")]
    public GameObject deathPanel;

    public bool IsDead { get; private set; }

    private void Start()
    {
        IsDead = false;

        if (deathPanel != null)
            deathPanel.SetActive(false);
    }

    private void Update()
    {
        if (IsDead || player == null)
            return;

        if (IsNearFire())
            Die();
    }

    private bool IsNearFire()
    {
        // Check the main fire.
        if (firePlacementManager != null)
        {
            if (IsNearObject(firePlacementManager.fireObject))
                return true;

            // Check any additional fire points.
            if (firePlacementManager.firePoints != null)
            {
                foreach (GameObject fire in
                         firePlacementManager.firePoints)
                {
                    if (IsNearObject(fire))
                        return true;
                }
            }
        }

        // Check fires on other floors.
        if (multipleFireOriginManager != null &&
            multipleFireOriginManager.fireOriginObjects != null)
        {
            foreach (GameObject fire in
                     multipleFireOriginManager.fireOriginObjects)
            {
                if (IsNearObject(fire))
                    return true;
            }
        }

        return false;
    }

    private bool IsNearObject(GameObject fire)
    {
        if (fire == null || !fire.activeInHierarchy)
            return false;

        float distance = Vector3.Distance(
            player.position,
            fire.transform.position
        );

        return distance * distanceScale <= deathRadiusMeters;
    }

    private void Die()
    {
        if (IsDead)
            return;

        IsDead = true;

        Debug.Log("PLAYER DIED: Too close to fire!");

        // Stop timer and show the evacuation failure result.
        if (simulationUIManager != null)
        {
            simulationUIManager.PlayerDied();
        }
        else
        {
            Debug.LogWarning(
                "[PlayerFireDeath] SimulationUIManager is not assigned.");
        }

        // Show the existing death panel.
        if (deathPanel != null)
            deathPanel.SetActive(true);

        // Disable the player after recording the time.
        if (player != null)
            player.gameObject.SetActive(false);
    }

    public void ResetDeath()
    {
        IsDead = false;

        if (deathPanel != null)
            deathPanel.SetActive(false);
    }
}
