using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class HazardSimulation : MonoBehaviour
{
    [Header("Grid Reference")]
    public HazardGrid hazardGrid;

    [Header("Main Fire Source")]
    public Transform fireOrigin;

    [Header("Additional Fire Points")]
    [Tooltip(
        "Optional manual fire points. " +
        "Your FirePlacementManager firePoints are also detected automatically."
    )]
    public List<Transform> additionalFireOrigins =
        new List<Transform>();

    [Header("Fire Point Settings")]
    [Tooltip("If true, inactive fire points are ignored.")]
    public bool ignoreInactiveFirePoints = true;

    [Header("Simulation Timing")]
    public float simulationStep = 0.25f;

    [Header("FDS Calibration")]
    public float referenceMaxHRR = 7500f;
    public float referenceSmokeValue = 8f;

    [Header("Smoke Production")]
    public float minimumSmokeInjection = 0.50f;
    public float maximumSmokeInjection = 1.00f;

    [Range(0f, 1f)]
    public float smokeSpreadRate = 0.40f;

    [Range(0f, 1f)]
    public float smokeDissipationRate = 0f;

    [Header("Temperature")]
    public float ambientTemperature = 27f;
    public float maximumFireTemperature = 600f;

    [Range(0f, 1f)]
    public float heatSpreadRate = 0.08f;

    public float coolingRate = 1f;

    [Header("Obstacle Behaviour")]
    public bool blockSmokeThroughObstacles = true;

    [Header("Playback")]
    [Min(0f)]
    public float playbackSpeed = 1f;

    [Header("Debug")]
    public bool showDebugLogs = true;
    public bool logFireCell = true;

    private float[,] nextSmoke;
    private float[,] nextTemperature;

    private float currentHRR;
    private float currentFDSSmoke;

    private bool simulationRunning;
    private float simulationTimer;

    private FirePlacementManager firePlacementManager;

    private FieldInfo firePointsField;

    private void Awake()
    {
        if (hazardGrid == null)
        {
            hazardGrid =
                FindAnyObjectByType<HazardGrid>();
        }

        firePlacementManager =
            FindAnyObjectByType<FirePlacementManager>();

        FindFirePointsField();
    }

    private void Start()
    {
        PrepareSimulationArrays();
        ResetSimulation();
    }

    private void Update()
    {
        if (!simulationRunning)
            return;

        float scaledDeltaTime =
            Time.deltaTime *
            Mathf.Max(
                0f,
                playbackSpeed
            );

        simulationTimer +=
            scaledDeltaTime;

        if (simulationTimer <
            simulationStep)
        {
            return;
        }

        simulationTimer = 0f;

        RunSimulationStep();
    }

    // =========================================================
    // PLAYBACK SPEED
    // =========================================================

    public void SetPlaybackSpeed(
        float speed)
    {
        playbackSpeed =
            Mathf.Max(
                0f,
                speed
            );
    }

    // =========================================================
    // PREPARE
    // =========================================================

    private void PrepareSimulationArrays()
    {
        if (hazardGrid == null)
        {
            Debug.LogError(
                "HazardSimulation: HazardGrid is not assigned."
            );

            return;
        }

        if (hazardGrid.Grid == null)
        {
            hazardGrid.GenerateGrid();
        }

        nextSmoke =
            new float[
                hazardGrid.gridWidth,
                hazardGrid.gridHeight
            ];

        nextTemperature =
            new float[
                hazardGrid.gridWidth,
                hazardGrid.gridHeight
            ];
    }

    // =========================================================
    // FDS INPUT
    // =========================================================

    public void UpdateFromFDS(
        float hrr,
        float smokeValue)
    {
        currentHRR =
            Mathf.Max(
                0f,
                hrr
            );

        currentFDSSmoke =
            Mathf.Max(
                0f,
                smokeValue
            );

        simulationRunning =
            currentHRR > 0.01f ||
            currentFDSSmoke > 0.001f;
    }

    // =========================================================
    // SIMULATION STEP
    // =========================================================

    private void RunSimulationStep()
    {
        if (hazardGrid == null ||
            hazardGrid.Grid == null)
        {
            return;
        }

        InjectFireSmokeAndHeat();

        CalculateNextGridState();

        ApplyNextGridState();

        if (showDebugLogs)
        {
            Debug.Log(
                $"Hazard simulation | " +
                $"HRR: {currentHRR:F0} | " +
                $"FDS Smoke: {currentFDSSmoke:F3} | " +
                $"Speed: {playbackSpeed:F1}x"
            );
        }
    }

    // =========================================================
    // FIRE SOURCES
    // =========================================================

    private void InjectFireSmokeAndHeat()
    {
        List<Vector3> firePositions =
            GetAllFirePositions();

        if (firePositions.Count == 0)
        {
            if (fireOrigin != null)
            {
                firePositions.Add(
                    fireOrigin.position
                );
            }
        }

        if (firePositions.Count == 0)
        {
            Debug.LogWarning(
                "HazardSimulation: No fire sources found."
            );

            return;
        }

        float normalizedHRR =
            Mathf.Clamp01(
                currentHRR /
                Mathf.Max(
                    referenceMaxHRR,
                    1f
                )
            );

        float normalizedFDSSmoke =
            Mathf.Clamp01(
                currentFDSSmoke /
                Mathf.Max(
                    referenceSmokeValue,
                    0.001f
                )
            );

        float fireStrength =
            Mathf.Max(
                normalizedHRR,
                normalizedFDSSmoke
            );

        fireStrength =
            Mathf.Pow(
                fireStrength,
                0.65f
            );

        float smokeInjection =
            Mathf.Lerp(
                minimumSmokeInjection,
                maximumSmokeInjection,
                fireStrength
            );

        float fireTemperature =
            Mathf.Lerp(
                ambientTemperature,
                maximumFireTemperature,
                fireStrength
            );

        foreach (Vector3 position in firePositions)
        {
            InjectAtFirePosition(
                position,
                smokeInjection,
                fireTemperature
            );
        }
    }

    private void InjectAtFirePosition(
        Vector3 firePosition,
        float smokeInjection,
        float fireTemperature)
    {
        HazardCell fireCell =
            hazardGrid.GetCell(
                firePosition
            );

        if (fireCell == null)
        {
            if (showDebugLogs)
            {
                Debug.LogWarning(
                    "HazardSimulation: " +
                    "A fire source is outside the hazard grid."
                );
            }

            return;
        }

        if (logFireCell)
        {
            Debug.Log(
                $"Fire Cell: " +
                $"{fireCell.x},{fireCell.z}"
            );
        }

        fireCell.containsFire = true;

        fireCell.AddSmoke(
            smokeInjection
        );

        fireCell.SetTemperature(
            Mathf.Max(
                fireCell.temperature,
                fireTemperature
            )
        );
    }

    // =========================================================
    // GET ALL FIRE POSITIONS
    // =========================================================

    private List<Vector3> GetAllFirePositions()
    {
        List<Vector3> positions =
            new List<Vector3>();

        /*
         * -----------------------------------------------------
         * MAIN FIRE
         * -----------------------------------------------------
         */

        if (fireOrigin != null)
        {
            if (!ignoreInactiveFirePoints ||
                fireOrigin.gameObject.activeInHierarchy)
            {
                positions.Add(
                    fireOrigin.position
                );
            }
        }

        /*
         * -----------------------------------------------------
         * MANUAL ADDITIONAL FIRE POINTS
         * -----------------------------------------------------
         */

        if (additionalFireOrigins != null)
        {
            foreach (
                Transform fireTransform
                in additionalFireOrigins)
            {
                if (fireTransform == null)
                    continue;

                if (ignoreInactiveFirePoints &&
                    !fireTransform.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!positions.Contains(
                        fireTransform.position))
                {
                    positions.Add(
                        fireTransform.position
                    );
                }
            }
        }

        /*
         * -----------------------------------------------------
         * FIRE PLACEMENT MANAGER
         * -----------------------------------------------------
         *
         * This reads the existing FirePlacementManager
         * firePoints list without requiring changes to that
         * script.
         */

        if (firePlacementManager != null &&
            firePointsField != null)
        {
            try
            {
                object fieldValue =
                    firePointsField.GetValue(
                        firePlacementManager
                    );

                IEnumerable firePoints =
                    fieldValue as IEnumerable;

                if (firePoints != null)
                {
                    foreach (
                        object item
                        in firePoints)
                    {
                        GameObject fireObject =
                            item as GameObject;

                        if (fireObject == null)
                            continue;

                        if (ignoreInactiveFirePoints &&
                            !fireObject.activeInHierarchy)
                        {
                            continue;
                        }

                        Vector3 position =
                            fireObject.transform.position;

                        if (!positions.Contains(position))
                        {
                            positions.Add(position);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "HazardSimulation: Could not read " +
                    "FirePlacementManager firePoints. " +
                    exception.Message
                );
            }
        }

        return positions;
    }

    private void FindFirePointsField()
    {
        if (firePlacementManager == null)
            return;

        Type type =
            firePlacementManager.GetType();

        firePointsField =
            type.GetField(
                "firePoints",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic
            );

        if (firePointsField == null)
        {
            Debug.LogWarning(
                "HazardSimulation: " +
                "FirePlacementManager firePoints field " +
                "was not found."
            );
        }
        else
        {
            Debug.Log(
                "HazardSimulation: Connected to " +
                "FirePlacementManager firePoints."
            );
        }
    }

    // =========================================================
    // SMOKE + HEAT PROPAGATION
    // =========================================================

    private void CalculateNextGridState()
    {
        for (
            int x = 0;
            x < hazardGrid.gridWidth;
            x++)
        {
            for (
                int z = 0;
                z < hazardGrid.gridHeight;
                z++)
            {
                HazardCell cell =
                    hazardGrid.GetCell(
                        x,
                        z
                    );

                if (cell == null)
                    continue;

                if (blockSmokeThroughObstacles &&
                    cell.isObstacle)
                {
                    nextSmoke[x, z] = 0f;

                    nextTemperature[x, z] =
                        ambientTemperature;

                    continue;
                }

                float neighborSmokeAverage =
                    GetNeighborSmokeAverage(
                        x,
                        z
                    );

                float smokeAfterSpread =
                    Mathf.Lerp(
                        cell.smokeDensity,
                        neighborSmokeAverage,
                        smokeSpreadRate
                    );

                smokeAfterSpread -=
                    smokeDissipationRate;

                nextSmoke[x, z] =
                    Mathf.Clamp01(
                        smokeAfterSpread
                    );

                float neighborTemperatureAverage =
                    GetNeighborTemperatureAverage(
                        x,
                        z
                    );

                float temperatureAfterSpread =
                    Mathf.Lerp(
                        cell.temperature,
                        neighborTemperatureAverage,
                        heatSpreadRate
                    );

                if (!cell.containsFire)
                {
                    temperatureAfterSpread =
                        Mathf.MoveTowards(
                            temperatureAfterSpread,
                            ambientTemperature,
                            coolingRate *
                            simulationStep *
                            Mathf.Max(
                                playbackSpeed,
                                0.0001f
                            )
                        );
                }

                nextTemperature[x, z] =
                    Mathf.Max(
                        ambientTemperature,
                        temperatureAfterSpread
                    );
            }
        }
    }

    // =========================================================
    // SMOKE NEIGHBORS
    // =========================================================

    private float GetNeighborSmokeAverage(
        int centerX,
        int centerZ)
    {
        float total = 0f;
        int count = 0;

        AddNeighborSmoke(
            centerX,
            centerZ,
            ref total,
            ref count
        );

        AddNeighborSmoke(
            centerX + 1,
            centerZ,
            ref total,
            ref count
        );

        AddNeighborSmoke(
            centerX - 1,
            centerZ,
            ref total,
            ref count
        );

        AddNeighborSmoke(
            centerX,
            centerZ + 1,
            ref total,
            ref count
        );

        AddNeighborSmoke(
            centerX,
            centerZ - 1,
            ref total,
            ref count
        );

        return count > 0
            ? total / count
            : 0f;
    }

    private void AddNeighborSmoke(
        int x,
        int z,
        ref float total,
        ref int count)
    {
        HazardCell neighbor =
            hazardGrid.GetCell(
                x,
                z
            );

        if (neighbor == null)
            return;

        if (blockSmokeThroughObstacles &&
            neighbor.isObstacle)
        {
            return;
        }

        total +=
            neighbor.smokeDensity;

        count++;
    }

    // =========================================================
    // TEMPERATURE NEIGHBORS
    // =========================================================

    private float GetNeighborTemperatureAverage(
        int centerX,
        int centerZ)
    {
        float total = 0f;
        int count = 0;

        AddNeighborTemperature(
            centerX,
            centerZ,
            ref total,
            ref count
        );

        AddNeighborTemperature(
            centerX + 1,
            centerZ,
            ref total,
            ref count
        );

        AddNeighborTemperature(
            centerX - 1,
            centerZ,
            ref total,
            ref count
        );

        AddNeighborTemperature(
            centerX,
            centerZ + 1,
            ref total,
            ref count
        );

        AddNeighborTemperature(
            centerX,
            centerZ - 1,
            ref total,
            ref count
        );

        return count > 0
            ? total / count
            : ambientTemperature;
    }

    private void AddNeighborTemperature(
        int x,
        int z,
        ref float total,
        ref int count)
    {
        HazardCell neighbor =
            hazardGrid.GetCell(
                x,
                z
            );

        if (neighbor == null)
            return;

        if (blockSmokeThroughObstacles &&
            neighbor.isObstacle)
        {
            return;
        }

        total +=
            neighbor.temperature;

        count++;
    }

    // =========================================================
    // APPLY GRID
    // =========================================================

    private void ApplyNextGridState()
    {
        for (
            int x = 0;
            x < hazardGrid.gridWidth;
            x++)
        {
            for (
                int z = 0;
                z < hazardGrid.gridHeight;
                z++)
            {
                HazardCell cell =
                    hazardGrid.GetCell(
                        x,
                        z
                    );

                if (cell == null)
                    continue;

                cell.SetSmokeDensity(
                    nextSmoke[x, z]
                );

                cell.SetTemperature(
                    nextTemperature[x, z]
                );
            }
        }
    }

    // =========================================================
    // START / PAUSE
    // =========================================================

    public void StartSimulation()
    {
        simulationRunning = true;
    }

    public void PauseSimulation()
    {
        simulationRunning = false;
    }

    // =========================================================
    // RESET
    // =========================================================

    public void ResetSimulation()
    {
        simulationRunning = false;

        simulationTimer = 0f;

        currentHRR = 0f;

        currentFDSSmoke = 0f;

        if (hazardGrid == null ||
            hazardGrid.Grid == null)
        {
            return;
        }

        for (
            int x = 0;
            x < hazardGrid.gridWidth;
            x++)
        {
            for (
                int z = 0;
                z < hazardGrid.gridHeight;
                z++)
            {
                HazardCell cell =
                    hazardGrid.GetCell(
                        x,
                        z
                    );

                if (cell != null)
                {
                    cell.ResetCell(
                        ambientTemperature
                    );
                }

                nextSmoke[x, z] = 0f;

                nextTemperature[x, z] =
                    ambientTemperature;
            }
        }
    }
}