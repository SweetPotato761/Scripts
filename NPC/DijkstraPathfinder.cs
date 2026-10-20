using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class DijkstraPathfinder : MonoBehaviour
{
    [Header("Player")]
    public Transform player;

    [Header("Floor Grids")]
    [Tooltip("Assign Floor1Grid, Floor2Grid, Floor3Grid, Floor4Grid, Floor5Grid.")]
    public AStarGrid[] grids;

    [Header("Building Exits")]
    [Tooltip("Assign every real evacuation exit here.")]
    public Transform[] exits;

    [Header("Floor Connections")]
    [Tooltip("Assign F5_to_F4, F4_to_F3 and F1_to_F2.")]
    public AStarFloorLink[] floorLinks;

    [Header("Path Line")]
    public LineRenderer pathLine;

    [Header("Path Settings")]
    public bool calculateOnStart = true;
    public bool updateWhilePlaying = false;
    public float updateInterval = 1f;

    [Header("Line Settings")]
    public float pathHeightOffset = 30f;
    public bool drawExactStairPoints = true;
    public bool drawExactExitPoint = true;

    [Header("Path Appearance")]
    public Color pathColor = Color.cyan;
    public float pathWidth = 0.15f;

    [Header("FDS Hazard Integration")]
    [Tooltip("Assign the FDSPlaybackController from the Fire System.")]
    public FDSPlaybackController fdsPlaybackController;

    [Tooltip("Assign the FirePlacementManager used by the fire setup panel.")]
    public FirePlacementManager firePlacementManager;

    [Header("Dynamic Rerouting")]
    public bool enableDynamicRerouting = true;

    [Tooltip("How often Dijkstra checks whether the FDS/fire hazard changed.")]
    public float hazardUpdateInterval = 1f;

    [Tooltip("Minimum smoke change required before recalculating.")]
    public float smokeChangeThreshold = 0.01f;

    [Tooltip("Minimum temperature change required before recalculating.")]
    public float temperatureChangeThreshold = 2f;

    [Tooltip("Minimum HRR change required before recalculating.")]
    public float hrrChangeThreshold = 100f;

    [Tooltip("Minimum fire-position movement required before recalculating.")]
    public float firePositionChangeThreshold = 0.5f;

    [Header("Active Fire Point Hazard")]
    [Tooltip("Include currently active fire points from FirePlacementManager in Dijkstra hazard calculation.")]
    public bool includeActiveFirePoints = true;

    [Tooltip("0 = use all active fire points. Otherwise limit the number of active fire points checked.")]
    public int maximumActiveFirePoints = 0;

    [Header("FDS Smoke Thresholds")]
    public float smokeCaution = 0.05f;
    public float smokeDanger = 0.10f;
    public float smokeCritical = 0.20f;

    [Header("FDS Temperature Thresholds")]
    public float temperatureCaution = 40f;
    public float temperatureDanger = 60f;
    public float temperatureCritical = 80f;

    [Header("FDS HRR Thresholds")]
    public float hrrCaution = 500f;
    public float hrrDanger = 1500f;
    public float hrrCritical = 2500f;

    [Header("Fire Spatial Hazard")]
    [Tooltip("Beyond this distance, fire position adds no penalty.")]
    public float fireHazardStartDistance = 15f;

    [Tooltip("Distance where the fire penalty becomes very high.")]
    public float fireCriticalDistance = 3f;

    [Tooltip("Maximum additional cost applied by the fire hazard.")]
    public int maximumFirePenalty = 100000;

    [Tooltip("Multiplier applied to FDS smoke/temperature/HRR penalties.")]
    public float fdsPenaltyMultiplier = 1f;

    [Tooltip("At critical fire conditions, nodes inside this radius are blocked.")]
    public float criticalFireBlockDistance = 2f;

    [Tooltip("If enabled, critically close nodes to the fire cannot be used.")]
    public bool blockCriticalFireArea = true;

    [Header("Current FDS Hazard")]
    [SerializeField] private float currentFDSSmoke = 0f;
    [SerializeField] private float currentFDSTemperature = 27f;
    [SerializeField] private float currentFDSHRR = 0f;

    [Tooltip("0 = Safe, 1 = Caution, 2 = Danger, 3 = Critical")]
    [SerializeField] private int currentHazardLevel = 0;

    [Header("Debug")]
    public bool showDebugLogs = true;

    [Header("Dijkstra Analysis Results")]
    public double executionTimeMs = 0;
    public int visitedNodes = 0;
    public int pathCost = 0;
    public float pathDistance = 0f;
    public bool pathFound = false;

    [Header("Chosen Exit")]
    public Transform selectedExit;

    // ============================================================
    // INTERNAL DATA
    // ============================================================

    private float updateTimer = 0f;
    private float hazardTimer = 0f;

    private float lastFDSSmoke = -1f;
    private float lastFDSTemperature = -999f;
    private float lastFDSHRR = -1f;

    private Vector3 lastFirePosition;
    private bool hadPreviousFirePosition = false;

    private int lastActiveFirePointCount = -1;

    private readonly Dictionary<AStarGrid.Node, AStarGrid> nodeGridMap =
        new Dictionary<AStarGrid.Node, AStarGrid>();

    private readonly Dictionary<AStarGrid.Node, Transform> exitNodeMap =
        new Dictionary<AStarGrid.Node, Transform>();

    private readonly Dictionary<AStarGrid.Node, AStarFloorLink> stairArrivalLink =
        new Dictionary<AStarGrid.Node, AStarFloorLink>();

    private readonly Dictionary<AStarGrid.Node, AStarGrid.Node> stairFromNode =
        new Dictionary<AStarGrid.Node, AStarGrid.Node>();

    private List<AStarGrid.Node> finalNodePath =
        new List<AStarGrid.Node>();

    private List<Vector3> finalVisualPath =
        new List<Vector3>();

    // ============================================================
    // UNITY
    // ============================================================

    private void Start()
    {
        Debug.Log("==================================================");
        Debug.Log("========== DIJKSTRA PATHFINDER STARTED ===========");
        Debug.Log("==================================================");

        ClearPathLine();
        ResetAnalysis();

        UpdateCurrentFDSHazard();

        if (calculateOnStart)
        {
            Debug.Log("Dijkstra: Calculate On Start is ON.");
            CalculatePath();
        }
        else
        {
            Debug.LogWarning("Dijkstra: Calculate On Start is OFF.");
        }
    }

    private void Update()
    {
        // --------------------------------------------------------
        // DYNAMIC FDS / FIRE REROUTING
        // --------------------------------------------------------

        if (enableDynamicRerouting)
        {
            hazardTimer += Time.deltaTime;

            if (hazardTimer >= Mathf.Max(0.05f, hazardUpdateInterval))
            {
                hazardTimer = 0f;

                if (HazardChanged())
                {
                    if (showDebugLogs)
                    {
                        Debug.Log(
                            "Dijkstra: FDS/fire hazard changed. Recalculating safest route."
                        );
                    }

                    CalculatePath();
                }
            }
        }

        // --------------------------------------------------------
        // ORIGINAL MANUAL UPDATE OPTION
        // --------------------------------------------------------

        if (!updateWhilePlaying)
            return;

        updateTimer += Time.deltaTime;

        if (updateTimer >= Mathf.Max(0.05f, updateInterval))
        {
            updateTimer = 0f;
            CalculatePath();
        }
    }

    // ============================================================
    // MAIN PATH CALCULATION
    // ============================================================

    public void CalculatePath()
    {
        Debug.Log("==================================================");
        Debug.Log("========== DIJKSTRA CALCULATE PATH CALLED =========");
        Debug.Log("==================================================");

        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        ResetAnalysis();
        ClearPathLine();

        UpdateCurrentFDSHazard();

        // --------------------------------------------------------
        // BASIC VALIDATION
        // --------------------------------------------------------

        if (player == null)
        {
            Debug.LogError("Dijkstra ERROR: Player is not assigned.");
            FinishFailedSearch(stopwatch);
            return;
        }

        if (grids == null || grids.Length == 0)
        {
            Debug.LogError("Dijkstra ERROR: No floor grids are assigned.");
            FinishFailedSearch(stopwatch);
            return;
        }

        if (exits == null || exits.Length == 0)
        {
            Debug.LogError("Dijkstra ERROR: No building exits are assigned.");
            FinishFailedSearch(stopwatch);
            return;
        }

        Debug.Log("Dijkstra: Player position = " + player.position);
        Debug.Log("Dijkstra: Number of grids = " + grids.Length);
        Debug.Log("Dijkstra: Number of exits = " + exits.Length);
        Debug.Log(
            "Dijkstra: Number of floor links = " +
            (floorLinks != null ? floorLinks.Length : 0)
        );

        if (showDebugLogs)
        {
            Debug.Log(
                "Dijkstra FDS: Smoke = " +
                currentFDSSmoke.ToString("F4") +
                " | Temperature = " +
                currentFDSTemperature.ToString("F1") +
                " C | HRR = " +
                currentFDSHRR.ToString("F1") +
                " kW | Hazard Level = " +
                currentHazardLevel
            );
        }

        // --------------------------------------------------------
        // PREPARE ALL GRIDS
        // --------------------------------------------------------

        BuildNodeGridMap();

        if (nodeGridMap.Count == 0)
        {
            Debug.LogError(
                "Dijkstra ERROR: Node/Grid map is empty. " +
                "The floor grids may not have been created."
            );

            FinishFailedSearch(stopwatch);
            return;
        }

        ResetAllGridNodes();

        stairArrivalLink.Clear();
        stairFromNode.Clear();

        // --------------------------------------------------------
        // LOCATE PLAYER FLOOR
        // --------------------------------------------------------

        AStarGrid startGrid = FindGridForPosition(player.position);

        if (startGrid == null)
        {
            Debug.LogError(
                "Dijkstra ERROR: Could not determine which floor grid contains the Player."
            );

            Debug.LogError(
                "Player position: " + player.position
            );

            FinishFailedSearch(stopwatch);
            return;
        }

        Debug.Log(
            "Dijkstra: Player detected on grid = " +
            startGrid.name
        );

        AStarGrid.Node startNode =
            startGrid.NodeFromWorldPoint(player.position);

        if (startNode == null)
        {
            Debug.LogError(
                "Dijkstra ERROR: Could not obtain a starting node."
            );

            FinishFailedSearch(stopwatch);
            return;
        }

        if (!startGrid.IsNodeTraversable(startNode))
        {
            Debug.LogWarning(
                "Dijkstra: Player is currently over a blocked node. " +
                "Searching for closest safe/walkable node."
            );

            startNode =
                startGrid.FindClosestWalkableNode(startNode);
        }

        if (startNode == null)
        {
            Debug.LogError(
                "Dijkstra ERROR: No safe/walkable starting node could be found."
            );

            FinishFailedSearch(stopwatch);
            return;
        }

        Debug.Log(
            "Dijkstra: Start node = [" +
            startNode.gridX + "," +
            startNode.gridY + "] on " +
            startGrid.name
        );

        // --------------------------------------------------------
        // MAP REAL BUILDING EXITS TO GRID NODES
        // --------------------------------------------------------

        BuildExitNodeMap();

        if (exitNodeMap.Count == 0)
        {
            Debug.LogError(
                "Dijkstra ERROR: None of the assigned exits could be mapped to a safe/walkable grid node."
            );

            FinishFailedSearch(stopwatch);
            return;
        }

        Debug.Log(
            "Dijkstra: Valid mapped exits = " +
            exitNodeMap.Count
        );

        // --------------------------------------------------------
        // RUN MULTI-FLOOR DIJKSTRA
        // --------------------------------------------------------

        AStarGrid.Node goalNode =
            FindMultiFloorPath(startNode);

        stopwatch.Stop();

        executionTimeMs =
            stopwatch.Elapsed.TotalMilliseconds;

        if (goalNode == null)
        {
            pathFound = false;
            selectedExit = null;

            Debug.LogWarning(
                "Dijkstra: NO EVACUATION ROUTE FOUND."
            );

            Debug.LogWarning(
                "Check that the stair points and exits are located " +
                "on or near safe/walkable nodes."
            );

            PrintAnalysis();
            return;
        }

        // --------------------------------------------------------
        // RETRACE RESULT
        // --------------------------------------------------------

        finalNodePath =
            RetracePath(startNode, goalNode);

        if (finalNodePath == null ||
            finalNodePath.Count == 0)
        {
            pathFound = false;
            selectedExit = null;

            Debug.LogError(
                "Dijkstra ERROR: Goal was reached but path retracing failed."
            );

            PrintAnalysis();
            return;
        }

        pathFound = true;

        if (exitNodeMap.ContainsKey(goalNode))
            selectedExit = exitNodeMap[goalNode];

        pathCost = goalNode.gCost;

        // --------------------------------------------------------
        // BUILD EXACT VISUAL ROUTE
        // --------------------------------------------------------

        BuildVisualPath(finalNodePath);

        CalculatePathDistance(finalVisualPath);

        DrawPath(finalVisualPath);

        // --------------------------------------------------------
        // SUCCESS LOG
        // --------------------------------------------------------

        Debug.Log("==================================================");
        Debug.Log("Dijkstra: PATH FOUND");

        Debug.Log(
            "Dijkstra: Start Grid = " +
            startGrid.name
        );

        Debug.Log(
            "Dijkstra: Selected Exit = " +
            (selectedExit != null
                ? selectedExit.name
                : "NONE")
        );

        Debug.Log(
            "Dijkstra: Path Nodes = " +
            finalNodePath.Count
        );

        Debug.Log(
            "Dijkstra: Visual Points = " +
            finalVisualPath.Count
        );

        PrintAnalysis();

        Debug.Log("==================================================");
    }

    // ============================================================
    // FDS HAZARD UPDATE
    // ============================================================

    private void UpdateCurrentFDSHazard()
    {
        if (fdsPlaybackController == null)
        {
            currentFDSSmoke = 0f;
            currentFDSTemperature = 27f;
            currentFDSHRR = 0f;
            currentHazardLevel = 0;
            return;
        }

        if (!fdsPlaybackController.IsDatasetReady())
        {
            return;
        }

        currentFDSSmoke =
            Mathf.Max(
                0f,
                fdsPlaybackController.GetCurrentSmoke()
            );

        currentFDSHRR =
            Mathf.Max(
                0f,
                fdsPlaybackController.GetCurrentHRR()
            );

        FDSFrame frame =
            fdsPlaybackController.GetCurrentFrame();

        currentFDSTemperature =
            GetFrameTemperature(frame);

        currentHazardLevel =
            CalculateHazardLevel(
                currentFDSSmoke,
                currentFDSTemperature,
                currentFDSHRR
            );
    }

    // ============================================================
    // TEMPERATURE FROM FDS FRAME
    // ============================================================

    private float GetFrameTemperature(FDSFrame frame)
    {
        if (frame == null)
            return 27f;

        if (frame.deviceValues != null)
        {
            float value;

            if (frame.deviceValues.TryGetValue(
                    "TEMP_CENTER",
                    out value))
            {
                return value;
            }

            if (frame.deviceValues.TryGetValue(
                    "TEMP_EXIT",
                    out value))
            {
                return value;
            }
        }

        if (frame.tempCenter > 0f)
            return frame.tempCenter;

        if (frame.tempExit > 0f)
            return frame.tempExit;

        return 27f;
    }

    // ============================================================
    // CALCULATE OVERALL FDS HAZARD LEVEL
    // ============================================================

    private int CalculateHazardLevel(
        float smoke,
        float temperature,
        float hrr)
    {
        int level = 0;

        if (smoke >= smokeCritical)
            level = Mathf.Max(level, 3);
        else if (smoke >= smokeDanger)
            level = Mathf.Max(level, 2);
        else if (smoke >= smokeCaution)
            level = Mathf.Max(level, 1);

        if (temperature >= temperatureCritical)
            level = Mathf.Max(level, 3);
        else if (temperature >= temperatureDanger)
            level = Mathf.Max(level, 2);
        else if (temperature >= temperatureCaution)
            level = Mathf.Max(level, 1);

        if (hrr >= hrrCritical)
            level = Mathf.Max(level, 3);
        else if (hrr >= hrrDanger)
            level = Mathf.Max(level, 2);
        else if (hrr >= hrrCaution)
            level = Mathf.Max(level, 1);

        return level;
    }

    // ============================================================
    // DETECT HAZARD CHANGES
    // ============================================================

    private bool HazardChanged()
    {
        UpdateCurrentFDSHazard();

        bool changed = false;

        if (lastFDSSmoke < 0f)
        {
            changed = true;
        }
        else if (Mathf.Abs(
                     currentFDSSmoke -
                     lastFDSSmoke) >=
                 smokeChangeThreshold)
        {
            changed = true;
        }

        if (lastFDSTemperature < -900f)
        {
            changed = true;
        }
        else if (Mathf.Abs(
                     currentFDSTemperature -
                     lastFDSTemperature) >=
                 temperatureChangeThreshold)
        {
            changed = true;
        }

        if (lastFDSHRR < 0f)
        {
            changed = true;
        }
        else if (Mathf.Abs(
                     currentFDSHRR -
                     lastFDSHRR) >=
                 hrrChangeThreshold)
        {
            changed = true;
        }

        Vector3 firePosition;

        if (TryGetFirePosition(out firePosition))
        {
            if (!hadPreviousFirePosition)
            {
                changed = true;
            }
            else if (Vector3.Distance(
                         firePosition,
                         lastFirePosition) >=
                     firePositionChangeThreshold)
            {
                changed = true;
            }

            lastFirePosition = firePosition;
            hadPreviousFirePosition = true;
        }

        int activeFirePointCount =
            GetActiveFirePointCount();

        if (lastActiveFirePointCount < 0)
        {
            lastActiveFirePointCount =
                activeFirePointCount;
        }
        else if (activeFirePointCount !=
                 lastActiveFirePointCount)
        {
            changed = true;

            if (showDebugLogs)
            {
                Debug.Log(
                    "Dijkstra: Active fire point count changed from " +
                    lastActiveFirePointCount +
                    " to " +
                    activeFirePointCount +
                    "."
                );
            }

            lastActiveFirePointCount =
                activeFirePointCount;
        }

        lastFDSSmoke = currentFDSSmoke;
        lastFDSTemperature = currentFDSTemperature;
        lastFDSHRR = currentFDSHRR;

        return changed;
    }

    // ============================================================
    // FIRE POSITION
    // ============================================================

    private bool TryGetFirePosition(
        out Vector3 firePosition)
    {
        firePosition = Vector3.zero;

        if (firePlacementManager == null)
            return false;

        if (!firePlacementManager.HasConfirmedFireLocation())
            return false;

        firePosition =
            firePlacementManager.GetSelectedFirePosition();

        return true;
    }

    // ============================================================
    // ACTIVE FIRE POINTS
    // ============================================================

    private int GetActiveFirePointCount()
    {
        if (!includeActiveFirePoints ||
            firePlacementManager == null ||
            firePlacementManager.firePoints == null)
        {
            return 0;
        }

        int count = 0;

        for (int i = 0;
             i < firePlacementManager.firePoints.Count;
             i++)
        {
            GameObject firePoint =
                firePlacementManager.firePoints[i];

            if (firePoint == null)
                continue;

            if (!firePoint.activeInHierarchy)
                continue;

            count++;
        }

        return count;
    }

    private bool TryGetNearestFireHazardDistance(
        Vector3 nodePosition,
        out float nearestDistance)
    {
        nearestDistance = Mathf.Infinity;

        bool foundFire = false;

        // Main confirmed fire.
        Vector3 mainFirePosition;

        if (TryGetFirePosition(
                out mainFirePosition))
        {
            nearestDistance =
                Vector3.Distance(
                    nodePosition,
                    mainFirePosition
                );

            foundFire = true;
        }

        // Active spreading fire points.
        if (includeActiveFirePoints &&
            firePlacementManager != null &&
            firePlacementManager.firePoints != null)
        {
            int checkedPoints = 0;

            for (int i = 0;
                 i < firePlacementManager.firePoints.Count;
                 i++)
            {
                GameObject firePoint =
                    firePlacementManager.firePoints[i];

                if (firePoint == null)
                    continue;

                if (!firePoint.activeInHierarchy)
                    continue;

                if (maximumActiveFirePoints > 0 &&
                    checkedPoints >=
                    maximumActiveFirePoints)
                {
                    break;
                }

                float distance =
                    Vector3.Distance(
                        nodePosition,
                        firePoint.transform.position
                    );

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                }

                foundFire = true;
                checkedPoints++;
            }
        }

        return foundFire;
    }

    // ============================================================
    // DYNAMIC HAZARD PENALTY
    // ============================================================

    private int GetDynamicHazardPenalty(
        AStarGrid.Node node)
    {
        if (node == null)
            return 0;

        int totalPenalty = 0;

        // --------------------------------------------------------
        // SMOKE
        // --------------------------------------------------------

        totalPenalty =
            SafeAdd(
                totalPenalty,
                CalculateSmokePenalty(
                    currentFDSSmoke
                )
            );

        // --------------------------------------------------------
        // TEMPERATURE
        // --------------------------------------------------------

        totalPenalty =
            SafeAdd(
                totalPenalty,
                CalculateTemperaturePenalty(
                    currentFDSTemperature
                )
            );

        // --------------------------------------------------------
        // HRR
        // --------------------------------------------------------

        totalPenalty =
            SafeAdd(
                totalPenalty,
                CalculateHRRPenalty(
                    currentFDSHRR
                )
            );

        // --------------------------------------------------------
        // FIRE + ACTIVE FIRE POINT DISTANCE
        // --------------------------------------------------------

        float nearestFireDistance;

        if (TryGetNearestFireHazardDistance(
                node.worldPosition,
                out nearestFireDistance))
        {
            bool critical =
                currentHazardLevel >= 3;

            if (blockCriticalFireArea &&
                critical &&
                nearestFireDistance <=
                criticalFireBlockDistance)
            {
                return int.MaxValue;
            }

            totalPenalty =
                SafeAdd(
                    totalPenalty,
                    CalculateFireDistancePenalty(
                        nearestFireDistance
                    )
                );
        }

        if (fdsPenaltyMultiplier != 1f)
        {
            float scaled =
                totalPenalty *
                Mathf.Max(
                    0f,
                    fdsPenaltyMultiplier
                );

            if (scaled >= int.MaxValue)
                return int.MaxValue;

            totalPenalty =
                Mathf.RoundToInt(scaled);
        }

        return totalPenalty;
    }

    // ============================================================
    // SMOKE PENALTY
    // ============================================================

    private int CalculateSmokePenalty(
        float smoke)
    {
        if (smoke < smokeCaution)
            return 0;

        if (smoke < smokeDanger)
        {
            float t =
                Mathf.InverseLerp(
                    smokeCaution,
                    smokeDanger,
                    smoke
                );

            return Mathf.RoundToInt(
                Mathf.Lerp(
                    500f,
                    5000f,
                    t
                )
            );
        }

        if (smoke < smokeCritical)
        {
            float t =
                Mathf.InverseLerp(
                    smokeDanger,
                    smokeCritical,
                    smoke
                );

            return Mathf.RoundToInt(
                Mathf.Lerp(
                    5000f,
                    30000f,
                    t
                )
            );
        }

        return 30000;
    }

    // ============================================================
    // TEMPERATURE PENALTY
    // ============================================================

    private int CalculateTemperaturePenalty(
        float temperature)
    {
        if (temperature < temperatureCaution)
            return 0;

        if (temperature < temperatureDanger)
        {
            float t =
                Mathf.InverseLerp(
                    temperatureCaution,
                    temperatureDanger,
                    temperature
                );

            return Mathf.RoundToInt(
                Mathf.Lerp(
                    500f,
                    5000f,
                    t
                )
            );
        }

        if (temperature < temperatureCritical)
        {
            float t =
                Mathf.InverseLerp(
                    temperatureDanger,
                    temperatureCritical,
                    temperature
                );

            return Mathf.RoundToInt(
                Mathf.Lerp(
                    5000f,
                    30000f,
                    t
                )
            );
        }

        return 30000;
    }

    // ============================================================
    // HRR PENALTY
    // ============================================================

    private int CalculateHRRPenalty(
        float hrr)
    {
        if (hrr < hrrCaution)
            return 0;

        if (hrr < hrrDanger)
        {
            float t =
                Mathf.InverseLerp(
                    hrrCaution,
                    hrrDanger,
                    hrr
                );

            return Mathf.RoundToInt(
                Mathf.Lerp(
                    500f,
                    5000f,
                    t
                )
            );
        }

        if (hrr < hrrCritical)
        {
            float t =
                Mathf.InverseLerp(
                    hrrDanger,
                    hrrCritical,
                    hrr
                );

            return Mathf.RoundToInt(
                Mathf.Lerp(
                    5000f,
                    30000f,
                    t
                )
            );
        }

        return 30000;
    }

    // ============================================================
    // FIRE DISTANCE PENALTY
    // ============================================================

    private int CalculateFireDistancePenalty(
        float distance)
    {
        if (distance >= fireHazardStartDistance)
            return 0;

        if (distance <= fireCriticalDistance)
            return maximumFirePenalty;

        float t =
            Mathf.InverseLerp(
                fireHazardStartDistance,
                fireCriticalDistance,
                distance
            );

        return Mathf.RoundToInt(
            Mathf.Lerp(
                1000f,
                10000f,
                t
            )
        );
    }

    // ============================================================
    // BUILD NODE -> GRID LOOKUP
    // ============================================================

    private void BuildNodeGridMap()
    {
        nodeGridMap.Clear();

        for (int i = 0; i < grids.Length; i++)
        {
            AStarGrid grid = grids[i];

            if (grid == null)
            {
                Debug.LogWarning(
                    "Dijkstra: Grid element " +
                    i +
                    " is empty."
                );

                continue;
            }

            if (grid.grid == null)
            {
                Debug.LogWarning(
                    "Dijkstra: " +
                    grid.name +
                    " grid data was null. Rebuilding it."
                );

                grid.CreateGrid();
            }

            if (grid.grid == null)
                continue;

            int added = 0;

            for (int x = 0; x < grid.gridSizeX; x++)
            {
                for (int y = 0; y < grid.gridSizeY; y++)
                {
                    AStarGrid.Node node =
                        grid.grid[x, y];

                    if (node == null)
                        continue;

                    if (!nodeGridMap.ContainsKey(node))
                    {
                        nodeGridMap.Add(node, grid);
                        added++;
                    }
                }
            }

            if (showDebugLogs)
            {
                Debug.Log(
                    "Dijkstra: Registered " +
                    added +
                    " nodes from " +
                    grid.name
                );
            }
        }

        Debug.Log(
            "Dijkstra: Total registered nodes = " +
            nodeGridMap.Count
        );
    }

    // ============================================================
    // RESET ALL NODE SEARCH VALUES
    // ============================================================

    private void ResetAllGridNodes()
    {
        for (int i = 0; i < grids.Length; i++)
        {
            if (grids[i] != null)
                grids[i].ResetNodes();
        }
    }

    // ============================================================
    // MAP EXITS
    // ============================================================

    private void BuildExitNodeMap()
    {
        exitNodeMap.Clear();

        for (int i = 0; i < exits.Length; i++)
        {
            Transform exit = exits[i];

            if (exit == null)
            {
                Debug.LogWarning(
                    "Dijkstra: Exit Element " +
                    i +
                    " is empty."
                );

                continue;
            }

            AStarGrid exitGrid =
                FindGridForPosition(exit.position);

            if (exitGrid == null)
            {
                Debug.LogWarning(
                    "Dijkstra: Could not find a floor grid for exit " +
                    exit.name +
                    " at " +
                    exit.position
                );

                continue;
            }

            AStarGrid.Node exitNode =
                exitGrid.NodeFromWorldPoint(exit.position);

            if (exitNode == null)
            {
                Debug.LogWarning(
                    "Dijkstra: Could not map exit " +
                    exit.name +
                    " to a grid node."
                );

                continue;
            }

            if (!exitGrid.IsNodeTraversable(exitNode))
            {
                AStarGrid.Node original =
                    exitNode;

                exitNode =
                    exitGrid.FindClosestWalkableNode(exitNode);

                if (exitNode == null)
                {
                    Debug.LogWarning(
                        "Dijkstra: Exit " +
                        exit.name +
                        " is on a blocked/hazardous node and no safe node could be found."
                    );

                    continue;
                }

                if (showDebugLogs)
                {
                    Debug.LogWarning(
                        "Dijkstra: Exit " +
                        exit.name +
                        " was on blocked node [" +
                        original.gridX + "," +
                        original.gridY +
                        "]. Using nearest safe/walkable node [" +
                        exitNode.gridX + "," +
                        exitNode.gridY + "]."
                    );
                }
            }

            if (!exitNodeMap.ContainsKey(exitNode))
            {
                exitNodeMap.Add(
                    exitNode,
                    exit
                );
            }

            if (showDebugLogs)
            {
                Debug.Log(
                    "Dijkstra EXIT: " +
                    exit.name +
                    " -> " +
                    exitGrid.name +
                    " node [" +
                    exitNode.gridX +
                    "," +
                    exitNode.gridY +
                    "]"
                );
            }
        }
    }

    // ============================================================
    // DETERMINE FLOOR FROM WORLD POSITION
    // ============================================================

    private AStarGrid FindGridForPosition(
        Vector3 worldPosition)
    {
        AStarGrid bestGrid = null;
        float bestHeightDifference =
            Mathf.Infinity;

        // First prefer grids whose X/Z boundary contains the point.
        for (int i = 0; i < grids.Length; i++)
        {
            AStarGrid grid = grids[i];

            if (grid == null)
                continue;

            if (!grid.ContainsWorldPoint(worldPosition))
                continue;

            float difference =
                Mathf.Abs(
                    worldPosition.y -
                    grid.GetFloorHeight()
                );

            if (difference <
                bestHeightDifference)
            {
                bestHeightDifference =
                    difference;

                bestGrid = grid;
            }
        }

        if (bestGrid != null)
            return bestGrid;

        // Fallback: choose the closest grid by height.
        for (int i = 0; i < grids.Length; i++)
        {
            AStarGrid grid = grids[i];

            if (grid == null)
                continue;

            float difference =
                Mathf.Abs(
                    worldPosition.y -
                    grid.GetFloorHeight()
                );

            if (difference <
                bestHeightDifference)
            {
                bestHeightDifference =
                    difference;

                bestGrid = grid;
            }
        }

        return bestGrid;
    }

    // ============================================================
    // MULTI-FLOOR DIJKSTRA
    // ============================================================

    private AStarGrid.Node FindMultiFloorPath(
        AStarGrid.Node startNode)
    {
        List<AStarGrid.Node> openSet =
            new List<AStarGrid.Node>();

        HashSet<AStarGrid.Node> closedSet =
            new HashSet<AStarGrid.Node>();

        startNode.gCost = 0;

        // Dijkstra does NOT use a heuristic.
        startNode.hCost = 0;

        startNode.parent = null;

        openSet.Add(startNode);

        while (openSet.Count > 0)
        {
            AStarGrid.Node currentNode =
                GetBestNode(openSet);

            if (currentNode == null)
                break;

            openSet.Remove(currentNode);

            if (closedSet.Contains(currentNode))
                continue;

            closedSet.Add(currentNode);
            visitedNodes++;

            // ----------------------------------------------------
            // EXIT REACHED
            // ----------------------------------------------------

            if (exitNodeMap.ContainsKey(currentNode))
            {
                if (showDebugLogs)
                {
                    Debug.Log(
                        "Dijkstra: Exit node reached after visiting " +
                        visitedNodes +
                        " nodes."
                    );
                }

                return currentNode;
            }

            if (!nodeGridMap.ContainsKey(currentNode))
                continue;

            AStarGrid currentGrid =
                nodeGridMap[currentNode];

            // ----------------------------------------------------
            // NORMAL SAME-FLOOR NEIGHBOURS
            // ----------------------------------------------------

            List<AStarGrid.Node> neighbours =
                currentGrid.GetNeighbours(currentNode);

            for (int i = 0;
                 i < neighbours.Count;
                 i++)
            {
                AStarGrid.Node neighbour =
                    neighbours[i];

                if (neighbour == null)
                    continue;

                if (!currentGrid.IsNodeTraversable(neighbour))
                    continue;

                if (closedSet.Contains(neighbour))
                    continue;

                int existingGridHazard =
                    currentGrid.GetHazardPenalty(
                        neighbour
                    );

                int dynamicFDSHazard =
                    GetDynamicHazardPenalty(
                        neighbour
                    );

                if (dynamicFDSHazard == int.MaxValue)
                    continue;

                int movementCost =
                    SafeAdd(
                        GetGridMovementCost(
                            currentNode,
                            neighbour
                        ),
                        existingGridHazard
                    );

                movementCost =
                    SafeAdd(
                        movementCost,
                        dynamicFDSHazard
                    );

                TryUpdateNode(
                    currentNode,
                    neighbour,
                    movementCost,
                    openSet
                );
            }

            // ----------------------------------------------------
            // STAIR/FLOOR CONNECTIONS
            // ----------------------------------------------------

            ProcessFloorLinks(
                currentNode,
                currentGrid,
                openSet,
                closedSet
            );
        }

        return null;
    }

    // ============================================================
    // FLOOR LINK PROCESSING
    // ============================================================

    private void ProcessFloorLinks(
        AStarGrid.Node currentNode,
        AStarGrid currentGrid,
        List<AStarGrid.Node> openSet,
        HashSet<AStarGrid.Node> closedSet)
    {
        if (floorLinks == null)
            return;

        for (int i = 0;
             i < floorLinks.Length;
             i++)
        {
            AStarFloorLink link =
                floorLinks[i];

            if (link == null)
                continue;

            if (!link.IsValid())
                continue;

            if (!link.CanTravelFrom(currentGrid))
                continue;

            Transform entryPoint =
                link.GetEntryPoint(currentGrid);

            Transform exitPoint =
                link.GetExitPoint(currentGrid);

            AStarGrid destinationGrid =
                link.GetDestinationGrid(currentGrid);

            if (entryPoint == null ||
                exitPoint == null ||
                destinationGrid == null)
            {
                continue;
            }

            // ----------------------------------------------------
            // FIND WALKABLE ENTRY NODE
            // ----------------------------------------------------

            AStarGrid.Node entryNode =
                currentGrid.NodeFromWorldPoint(
                    entryPoint.position
                );

            if (entryNode == null)
                continue;

            if (!currentGrid.IsNodeTraversable(entryNode))
            {
                entryNode =
                    currentGrid.FindClosestWalkableNode(
                        entryNode
                    );
            }

            if (entryNode == null)
                continue;

            /*
             * The stair transition is available only when Dijkstra
             * actually reaches the stair node.
             */
            if (currentNode != entryNode)
                continue;

            // ----------------------------------------------------
            // FIND WALKABLE ARRIVAL NODE
            // ----------------------------------------------------

            AStarGrid.Node destinationNode =
                destinationGrid.NodeFromWorldPoint(
                    exitPoint.position
                );

            if (destinationNode == null)
                continue;

            if (!destinationGrid.IsNodeTraversable(
                    destinationNode))
            {
                destinationNode =
                    destinationGrid.FindClosestWalkableNode(
                        destinationNode
                    );
            }

            if (destinationNode == null)
                continue;

            if (closedSet.Contains(destinationNode))
                continue;

            // ----------------------------------------------------
            // DYNAMIC HAZARD CHECK
            // ----------------------------------------------------

            int dynamicHazard =
                GetDynamicHazardPenalty(
                    destinationNode
                );

            if (dynamicHazard == int.MaxValue)
                continue;

            // ----------------------------------------------------
            // STAIR COST
            // ----------------------------------------------------

            int transitionCost =
                CalculateStairCost(
                    link,
                    entryPoint.position,
                    exitPoint.position,
                    currentGrid,
                    destinationGrid
                );

            int newCost =
                SafeAdd(
                    currentNode.gCost,
                    transitionCost
                );

            newCost =
                SafeAdd(
                    newCost,
                    destinationGrid.GetHazardPenalty(
                        destinationNode
                    )
                );

            newCost =
                SafeAdd(
                    newCost,
                    dynamicHazard
                );

            if (newCost <
                destinationNode.gCost)
            {
                destinationNode.gCost =
                    newCost;

                destinationNode.hCost = 0;

                destinationNode.parent =
                    currentNode;

                stairArrivalLink[
                    destinationNode
                ] = link;

                stairFromNode[
                    destinationNode
                ] = currentNode;

                if (!openSet.Contains(
                        destinationNode))
                {
                    openSet.Add(
                        destinationNode
                    );
                }

                if (showDebugLogs)
                {
                    Debug.Log(
                        "Dijkstra STAIR: " +
                        currentGrid.name +
                        " -> " +
                        destinationGrid.name +
                        " using " +
                        link.name
                    );
                }
            }
        }
    }

    // ============================================================
    // UPDATE A NODE
    // ============================================================

    private void TryUpdateNode(
        AStarGrid.Node currentNode,
        AStarGrid.Node neighbour,
        int movementCost,
        List<AStarGrid.Node> openSet)
    {
        if (movementCost == int.MaxValue)
            return;

        int newMovementCost =
            SafeAdd(
                currentNode.gCost,
                movementCost
            );

        if (newMovementCost <
            neighbour.gCost)
        {
            neighbour.gCost =
                newMovementCost;

            neighbour.hCost = 0;

            neighbour.parent =
                currentNode;

            /*
             * If a node previously happened to be reached
             * via a stair but now has a cheaper normal-grid
             * route, clear the stair metadata.
             */
            stairArrivalLink.Remove(neighbour);
            stairFromNode.Remove(neighbour);

            if (!openSet.Contains(neighbour))
                openSet.Add(neighbour);
        }
    }

    // ============================================================
    // STAIR COST
    // ============================================================

    private int CalculateStairCost(
        AStarFloorLink link,
        Vector3 fromPosition,
        Vector3 toPosition,
        AStarGrid fromGrid,
        AStarGrid toGrid)
    {
        float spacing =
            Mathf.Min(
                Mathf.Max(
                    0.01f,
                    fromGrid.nodeSpacing
                ),
                Mathf.Max(
                    0.01f,
                    toGrid.nodeSpacing
                )
            );

        float worldDistance =
            Vector3.Distance(
                fromPosition,
                toPosition
            );

        int distanceCost =
            Mathf.Max(
                1,
                Mathf.RoundToInt(
                    (worldDistance / spacing)
                    * 10f
                )
            );

        int extra =
            Mathf.Max(
                0,
                link.extraTransitionCost
            );

        return distanceCost + extra;
    }

    // ============================================================
    // DIJKSTRA HEURISTIC (NOT USED)
    // ============================================================

    private int GetHeuristicToClosestExit(
        AStarGrid.Node node)
    {
        if (node == null ||
            exitNodeMap.Count == 0)
        {
            return 0;
        }

        if (!nodeGridMap.ContainsKey(node))
            return 0;

        float minimumDistance =
            Mathf.Infinity;

        foreach (
            KeyValuePair<
                AStarGrid.Node,
                Transform
            > pair in exitNodeMap)
        {
            AStarGrid.Node exitNode =
                pair.Key;

            if (exitNode == null)
                continue;

            float distance =
                Vector3.Distance(
                    node.worldPosition,
                    exitNode.worldPosition
                );

            if (distance <
                minimumDistance)
            {
                minimumDistance =
                    distance;
            }
        }

        if (float.IsInfinity(
                minimumDistance))
        {
            return 0;
        }

        float smallestSpacing =
            GetSmallestNodeSpacing();

        int heuristic =
            Mathf.RoundToInt(
                (minimumDistance /
                 smallestSpacing) *
                10f
            );

        return Mathf.Max(
            0,
            heuristic
        );
    }

    private float GetSmallestNodeSpacing()
    {
        float smallest =
            Mathf.Infinity;

        for (int i = 0;
             i < grids.Length;
             i++)
        {
            if (grids[i] == null)
                continue;

            if (grids[i].nodeSpacing <= 0f)
                continue;

            if (grids[i].nodeSpacing <
                smallest)
            {
                smallest =
                    grids[i].nodeSpacing;
            }
        }

        if (float.IsInfinity(smallest))
            return 1f;

        return Mathf.Max(
            0.01f,
            smallest
        );
    }

    // ============================================================
    // OPEN SET BEST NODE
    // ============================================================

    private AStarGrid.Node GetBestNode(
        List<AStarGrid.Node> openSet)
    {
        if (openSet == null ||
            openSet.Count == 0)
        {
            return null;
        }

        AStarGrid.Node best =
            openSet[0];

        for (int i = 1;
             i < openSet.Count;
             i++)
        {
            AStarGrid.Node node =
                openSet[i];

            if (node == null)
                continue;

            if (best == null)
            {
                best = node;
                continue;
            }

            // Dijkstra chooses ONLY the smallest accumulated cost.
            if (node.gCost < best.gCost)
            {
                best = node;
            }
        }

        return best;
    }

    // ============================================================
    // GRID MOVEMENT COST
    // ============================================================

    private int GetGridMovementCost(
        AStarGrid.Node nodeA,
        AStarGrid.Node nodeB)
    {
        int dstX =
            Mathf.Abs(
                nodeA.gridX -
                nodeB.gridX
            );

        int dstY =
            Mathf.Abs(
                nodeA.gridY -
                nodeB.gridY
            );

        if (dstX > dstY)
        {
            return
                14 * dstY +
                10 * (dstX - dstY);
        }

        return
            14 * dstX +
            10 * (dstY - dstX);
    }

    // ============================================================
    // RETRACE NODE PATH
    // ============================================================

    private List<AStarGrid.Node> RetracePath(
        AStarGrid.Node startNode,
        AStarGrid.Node endNode)
    {
        List<AStarGrid.Node> path =
            new List<AStarGrid.Node>();

        AStarGrid.Node currentNode =
            endNode;

        int safetyCounter = 0;

        int maximumNodes =
            Mathf.Max(
                100,
                nodeGridMap.Count + 10
            );

        while (
            currentNode != null &&
            currentNode != startNode)
        {
            path.Add(currentNode);

            currentNode =
                currentNode.parent;

            safetyCounter++;

            if (safetyCounter >
                maximumNodes)
            {
                Debug.LogError(
                    "Dijkstra ERROR: Path retrace safety limit reached."
                );

                return new List<AStarGrid.Node>();
            }
        }

        if (currentNode == startNode)
            path.Add(startNode);
        else
            return new List<AStarGrid.Node>();

        path.Reverse();

        return path;
    }

    // ============================================================
    // BUILD VISUAL PATH
    // ============================================================

    private void BuildVisualPath(
        List<AStarGrid.Node> nodePath)
    {
        finalVisualPath.Clear();

        if (nodePath == null ||
            nodePath.Count == 0)
        {
            return;
        }

        // Start exactly at the player.
        AddVisualPoint(
            player.position +
            Vector3.up *
            pathHeightOffset
        );

        for (int i = 0;
             i < nodePath.Count;
             i++)
        {
            AStarGrid.Node node =
                nodePath[i];

            if (node == null)
                continue;

            /*
             * For a stair arrival node, insert the actual
             * stair entrance and actual stair arrival point.
             */
            if (i > 0 &&
                stairArrivalLink.ContainsKey(node))
            {
                AStarFloorLink link =
                    stairArrivalLink[node];

                AStarGrid.Node previousNode =
                    nodePath[i - 1];

                if (link != null &&
                    previousNode != null &&
                    nodeGridMap.ContainsKey(previousNode))
                {
                    AStarGrid previousGrid =
                        nodeGridMap[previousNode];

                    Transform exactEntry =
                        link.GetEntryPoint(
                            previousGrid
                        );

                    Transform exactExit =
                        link.GetExitPoint(
                            previousGrid
                        );

                    if (drawExactStairPoints)
                    {
                        if (exactEntry != null)
                        {
                            AddVisualPoint(
                                exactEntry.position +
                                Vector3.up *
                                pathHeightOffset
                            );
                        }

                        if (exactExit != null)
                        {
                            AddVisualPoint(
                                exactExit.position +
                                Vector3.up *
                                pathHeightOffset
                            );
                        }
                    }
                }
            }

            AddVisualPoint(
                node.worldPosition +
                Vector3.up *
                pathHeightOffset
            );
        }

        // Finish exactly at the physical exit.
        if (drawExactExitPoint &&
            selectedExit != null)
        {
            AddVisualPoint(
                selectedExit.position +
                Vector3.up *
                pathHeightOffset
            );
        }
    }

    private void AddVisualPoint(
        Vector3 point)
    {
        if (finalVisualPath.Count == 0)
        {
            finalVisualPath.Add(point);
            return;
        }

        Vector3 previous =
            finalVisualPath[
                finalVisualPath.Count - 1
            ];

        // Avoid duplicate points.
        if (Vector3.Distance(
                previous,
                point) > 0.01f)
        {
            finalVisualPath.Add(point);
        }
    }

    // ============================================================
    // PATH DISTANCE
    // ============================================================

    private void CalculatePathDistance(
        List<Vector3> visualPath)
    {
        pathDistance = 0f;

        if (visualPath == null ||
            visualPath.Count < 2)
        {
            return;
        }

        for (int i = 1;
             i < visualPath.Count;
             i++)
        {
            pathDistance +=
                Vector3.Distance(
                    visualPath[i - 1],
                    visualPath[i]
                );
        }
    }

    // ============================================================
    // DRAW LINE
    // ============================================================

    private void DrawPath(
        List<Vector3> visualPath)
    {
        if (pathLine == null)
        {
            Debug.LogError(
                "Dijkstra ERROR: Path Line is not assigned."
            );

            return;
        }

        if (visualPath == null ||
            visualPath.Count < 2)
        {
            Debug.LogWarning(
                "Dijkstra: Not enough visual points to draw a line."
            );

            pathLine.positionCount = 0;
            return;
        }

        pathLine.enabled = true;
        pathLine.useWorldSpace = true;

        // ========================================================
        // DIJKSTRA PATH = CYAN
        // ========================================================

        pathLine.startColor = pathColor;
        pathLine.endColor = pathColor;

        pathLine.startWidth = pathWidth;
        pathLine.endWidth = pathWidth;

        pathLine.positionCount =
            visualPath.Count;

        pathLine.SetPositions(
            visualPath.ToArray()
        );

        Debug.Log(
            "Dijkstra: LineRenderer received " +
            visualPath.Count +
            " positions."
        );
    }

    // ============================================================
    // CLEAR LINE
    // ============================================================

    private void ClearPathLine()
    {
        finalNodePath.Clear();
        finalVisualPath.Clear();

        if (pathLine != null)
        {
            pathLine.positionCount = 0;
        }
    }

    // ============================================================
    // RESET ANALYSIS
    // ============================================================

    private void ResetAnalysis()
    {
        executionTimeMs = 0;
        visitedNodes = 0;
        pathCost = 0;
        pathDistance = 0f;
        pathFound = false;
        selectedExit = null;
    }

    // ============================================================
    // FAILED SEARCH
    // ============================================================

    private void FinishFailedSearch(
        Stopwatch stopwatch)
    {
        if (stopwatch.IsRunning)
            stopwatch.Stop();

        executionTimeMs =
            stopwatch.Elapsed.TotalMilliseconds;

        pathFound = false;
        selectedExit = null;
        pathCost = 0;
        pathDistance = 0f;

        PrintAnalysis();
    }

    // ============================================================
    // PRINT ANALYSIS
    // ============================================================

    private void PrintAnalysis()
    {
        if (!showDebugLogs)
            return;

        Debug.Log(
            "=============== DIJKSTRA ANALYSIS ==============="
        );

        Debug.Log(
            "Path Found: " +
            pathFound
        );

        Debug.Log(
            "Selected Exit: " +
            (selectedExit != null
                ? selectedExit.name
                : "None")
        );

        Debug.Log(
            "Execution Time: " +
            executionTimeMs.ToString("F4") +
            " ms"
        );

        Debug.Log(
            "Visited Nodes: " +
            visitedNodes
        );

        Debug.Log(
            "Path Cost: " +
            pathCost
        );

        Debug.Log(
            "Path Distance: " +
            pathDistance.ToString("F2")
        );

        Debug.Log(
            "FDS Smoke: " +
            currentFDSSmoke.ToString("F4")
        );

        Debug.Log(
            "FDS Temperature: " +
            currentFDSTemperature.ToString("F1") +
            " C"
        );

        Debug.Log(
            "FDS HRR: " +
            currentFDSHRR.ToString("F1") +
            " kW"
        );

        Debug.Log(
            "FDS Hazard Level: " +
            currentHazardLevel
        );

        Debug.Log(
            "==========================================="
        );
    }

    // ============================================================
    // PUBLIC MANUAL RECALCULATION
    // ============================================================

    public void ForceRecalculate()
    {
        Debug.Log(
            "Dijkstra: ForceRecalculate() called."
        );

        CalculatePath();
    }

    // ============================================================
    // PUBLIC PLAYER SETTER
    // ============================================================

    public void SetPlayer(
        Transform newPlayer)
    {
        player = newPlayer;

        if (showDebugLogs)
        {
            Debug.Log(
                "Dijkstra: Player changed to " +
                (player != null
                    ? player.name
                    : "None")
            );
        }
    }

    // ============================================================
    // PUBLIC FDS GETTERS
    // ============================================================

    public float GetCurrentFDSSmoke()
    {
        return currentFDSSmoke;
    }

    public float GetCurrentFDSTemperature()
    {
        return currentFDSTemperature;
    }

    public float GetCurrentFDSHRR()
    {
        return currentFDSHRR;
    }

    public int GetCurrentHazardLevel()
    {
        return currentHazardLevel;
    }

    public Transform GetSelectedExit()
    {
        return selectedExit;
    }

    public bool IsPathFound()
    {
        return pathFound;
    }

    // ============================================================
    // SAFE INTEGER ADD
    // ============================================================

    private int SafeAdd(
        int a,
        int b)
    {
        if (a == int.MaxValue)
            return int.MaxValue;

        if (b == int.MaxValue)
            return int.MaxValue;

        if (b > 0 &&
            a > int.MaxValue - b)
        {
            return int.MaxValue;
        }

        return a + b;
    }
}