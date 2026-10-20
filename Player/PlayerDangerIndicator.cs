
using UnityEngine;
using TMPro;

public class PlayerDangerIndicator : MonoBehaviour
{
    [Header("PLAYER")]
    public Transform player;

    [Header("FIRE PLACEMENT")]
    public FirePlacementManager firePlacementManager;

    [Header("UI")]
    public TMP_Text dangerText;

    [Header("HEAT FLUX ESTIMATION")]
    [Tooltip("Estimated heat flux at the reference distance, in kW/m².")]
    public float referenceHeatFlux = 50f;

    [Tooltip("Distance from the fire where reference heat flux applies.")]
    public float referenceDistance = 1f;

    [Tooltip("Minimum distance used to prevent division by zero.")]
    public float minimumDistance = 0.5f;

    [Tooltip("Maximum distance at which heat flux is displayed.")]
    public float maximumDistance = 1000f;

    [Header("HEAT FLUX DANGER LEVELS (kW/m²)")]
    public float safeHeatFlux = 1f;
    public float minorHazardHeatFlux = 2.5f;
    public float highDangerHeatFlux = 5f;
    public float lethalHeatFlux = 10f;

    [Header("FLOOR CHECK")]
    public float floorTolerance = 400f;

    [Header("UPDATE")]
    public float updateInterval = 0.2f;

    private float updateTimer;
    private float currentDistance = Mathf.Infinity;
    private float currentHeatFlux;
    private Vector3 nearestFirePosition;
    private GameObject nearestFireObject;

    private void Start()
    {
        UpdateDangerIndicator();
    }

    private void Update()
    {
        updateTimer -= Time.deltaTime;

        if (updateTimer <= 0f)
        {
            updateTimer = updateInterval;
            UpdateDangerIndicator();
        }
    }

    private void UpdateDangerIndicator()
    {
        if (player == null)
        {
            SetDangerText("DANGER LEVEL: NO PLAYER\nHEAT FLUX: -- kW/m²");
            return;
        }

        if (firePlacementManager == null)
        {
            SetDangerText("DANGER LEVEL: NO FIRE DATA\nHEAT FLUX: -- kW/m²");
            return;
        }

        if (!FindNearestActiveFire())
        {
            currentDistance = Mathf.Infinity;
            currentHeatFlux = 0f;
            nearestFireObject = null;

            SetDangerText(
                "DANGER LEVEL: SAFE ZONE\n" +
                "HEAT FLUX: 0.0 kW/m²"
            );

            return;
        }

        bool sameFloor = IsSameFloor(
            player.position,
            nearestFirePosition
        );

        if (!sameFloor)
        {
            currentHeatFlux = 0f;
        }
        else
        {
            currentHeatFlux = CalculateHeatFlux(currentDistance);
        }

        string dangerLevel;

        if (currentHeatFlux >= lethalHeatFlux)
        {
            dangerLevel = "EXTREME DANGER / LETHAL ZONE";
        }
        else if (currentHeatFlux >= highDangerHeatFlux)
        {
            dangerLevel = "HIGH DANGER";
        }
        else if (currentHeatFlux >= minorHazardHeatFlux)
        {
            dangerLevel = "MINOR HAZARD";
        }
        else
        {
            dangerLevel = "SAFE ZONE";
        }

        SetDangerText(
            "DANGER LEVEL: " + dangerLevel +
            "\nHEAT FLUX: " + currentHeatFlux.ToString("0.0") + " kW/m²"
        );
    }

    // ============================================================
    // ESTIMATED HEAT FLUX
    // ============================================================

    private float CalculateHeatFlux(float distance)
    {
        float safeDistance = Mathf.Max(
            distance * distanceScaleForHeatFlux,
            minimumDistance
        );

        // Inverse-square approximation:
        // closer to the fire = higher estimated heat flux.
        float heatFlux = referenceHeatFlux *
            Mathf.Pow(referenceDistance / safeDistance, 2f);

        return Mathf.Clamp(heatFlux, 0f, referenceHeatFlux * 100f);
    }

    // Conversion from your Unity world units to distance units.
    // Keep this consistent with your building model scale.
    public float distanceScaleForHeatFlux = 0.01f;

    // ============================================================
    // FIND NEAREST ACTIVE FIRE
    // ============================================================

    private bool FindNearestActiveFire()
    {
        currentDistance = Mathf.Infinity;
        nearestFireObject = null;
        nearestFirePosition = Vector3.zero;

        bool foundFire = false;
        Vector3 playerPosition = player.position;

        // MAIN FIRE
        if (firePlacementManager.fireObject != null)
        {
            GameObject mainFire = firePlacementManager.fireObject;

            if (mainFire.activeInHierarchy)
            {
                CheckFireDistance(
                    mainFire,
                    playerPosition,
                    ref foundFire
                );
            }
        }

        // ADDITIONAL FIRE POINTS
        if (firePlacementManager.firePoints != null)
        {
            foreach (GameObject firePoint in firePlacementManager.firePoints)
            {
                if (firePoint == null || !firePoint.activeInHierarchy)
                    continue;

                CheckFireDistance(
                    firePoint,
                    playerPosition,
                    ref foundFire
                );
            }
        }

        return foundFire;
    }

    private void CheckFireDistance(
        GameObject fire,
        Vector3 playerPosition,
        ref bool foundFire
    )
    {
        float distance = Vector3.Distance(
            playerPosition,
            fire.transform.position
        );

        if (distance < currentDistance)
        {
            currentDistance = distance;
            nearestFirePosition = fire.transform.position;
            nearestFireObject = fire;
            foundFire = true;
        }
    }

    // ============================================================
    // FLOOR CHECK
    // ============================================================

    private bool IsSameFloor(
        Vector3 playerPosition,
        Vector3 firePosition
    )
    {
        float verticalDifference =
            Mathf.Abs(playerPosition.y - firePosition.y);

        return verticalDifference <= floorTolerance;
    }

    // ============================================================
    // UI
    // ============================================================

    private void SetDangerText(string message)
    {
        if (dangerText != null)
            dangerText.text = message;
    }

    // ============================================================
    // PUBLIC INFORMATION
    // ============================================================

    public float GetCurrentDistance()
    {
        return currentDistance;
    }

    public float GetCurrentHeatFlux()
    {
        return currentHeatFlux;
    }

    public Vector3 GetNearestFirePosition()
    {
        return nearestFirePosition;
    }

    public GameObject GetNearestFireObject()
    {
        return nearestFireObject;
    }
}
