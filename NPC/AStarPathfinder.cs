    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using UnityEngine;
    using Debug = UnityEngine.Debug;

    public class AStarPathfinder : MonoBehaviour
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
        public Color pathColor = Color.green;
        public float pathWidth = 0.15f;

        [Header("FDS Hazard Integration")]
        [Tooltip("Assign the existing FDSPlaybackController.")]
        public FDSPlaybackController fdsPlaybackController;

        [Tooltip("Assign the existing FirePlacementManager.")]
        public FirePlacementManager firePlacementManager;

        [Tooltip("Automatically recalculate the route when FDS hazard changes.")]
        public bool enableDynamicRerouting = true;

        [Tooltip("How often the FDS hazard is checked for route changes.")]
        public float hazardUpdateInterval = 1f;

        [Tooltip("Minimum smoke change required before rerouting.")]
        public float smokeChangeThreshold = 0.01f;

        [Tooltip("Minimum temperature change required before rerouting.")]
        public float temperatureChangeThreshold = 2f;

        [Tooltip("Minimum HRR change required before rerouting.")]
        public float hrrChangeThreshold = 100f;

        [Tooltip("Minimum fire-position movement required before rerouting.")]
        public float firePositionChangeThreshold = 0.5f;

        [Header("Active Fire Point Hazard")]
        [Tooltip("Include every currently active fire point in the spatial hazard calculation.")]
        public bool includeActiveFirePoints = true;

        [Tooltip("0 = use all active fire points. This is recommended for the evacuation route.")]
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
        [Tooltip("Distance at which the fire begins adding route cost.")]
        public float fireHazardStartDistance = 15f;

        [Tooltip("Distance at which fire hazard becomes extremely high.")]
        public float fireCriticalDistance = 3f;

        [Tooltip("Maximum additional A* cost near the fire.")]
        public int maximumFirePenalty = 100000;

        [Tooltip("Multiplier applied to FDS hazard penalties.")]
        public float fdsPenaltyMultiplier = 1f;

        [Header("Hazard Behaviour")]
        [Tooltip("At critical FDS hazard, nodes close to the fire can become effectively impassable.")]
        public bool blockCriticalFireArea = true;

        [Tooltip("Distance around the fire that becomes blocked during critical hazard.")]
        public float criticalFireBlockDistance = 2f;

        [Header("Debug")]
        public bool showDebugLogs = true;

        [Header("A* Analysis Results")]
        public double executionTimeMs = 0;
        public int visitedNodes = 0;
        public int pathCost = 0;
        public float pathDistance = 0f;
        public bool pathFound = false;

        [Header("Chosen Exit")]
        public Transform selectedExit;

        [Header("Current FDS Hazard")]
        public float currentFDSSmoke = 0f;
        public float currentFDSTemperature = 27f;
        public float currentFDSHRR = 0f;
        public int currentHazardLevel = 0;

        // ============================================================
        // INTERNAL DATA
        // ============================================================

        private float updateTimer = 0f;
        private float hazardTimer = 0f;

        private float lastSmoke = -999f;
        private float lastTemperature = -999f;
        private float lastHRR = -999f;

        private Vector3 lastFirePosition;
        private bool hasLastFirePosition = false;

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
            Debug.Log("========== ASTAR PATHFINDER STARTED =============");
            Debug.Log("==================================================");

            ClearPathLine();
            ResetAnalysis();

            if (calculateOnStart)
            {
                Debug.Log("AStar: Calculate On Start is ON.");
                CalculatePath();
            }
            else
            {
                Debug.LogWarning("AStar: Calculate On Start is OFF.");
            }
        }

        private void Update()
        {
            if (updateWhilePlaying)
            {
                updateTimer += Time.deltaTime;

                if (updateTimer >= Mathf.Max(0.05f, updateInterval))
                {
                    updateTimer = 0f;
                    CalculatePath();
                }
            }

            if (enableDynamicRerouting)
            {
                hazardTimer += Time.deltaTime;

                if (hazardTimer >= Mathf.Max(0.1f, hazardUpdateInterval))
                {
                    hazardTimer = 0f;

                    if (HazardChanged())
                    {
                        if (showDebugLogs)
                        {
                            Debug.Log(
                                "AStar: FDS/fire hazard changed. Recalculating evacuation route."
                            );
                        }

                        CalculatePath();
                    }
                }
            }
        }

        // ============================================================
        // MAIN PATH CALCULATION
        // ============================================================

        public void CalculatePath()
        {
            Debug.Log("==================================================");
            Debug.Log("========== ASTAR CALCULATE PATH CALLED ==========");
            Debug.Log("==================================================");

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            ResetAnalysis();
            ClearPathLine();

            UpdateCurrentFDSHazard();

            if (player == null)
            {
                Debug.LogError("AStar ERROR: Player is not assigned.");
                FinishFailedSearch(stopwatch);
                return;
            }

            if (grids == null || grids.Length == 0)
            {
                Debug.LogError("AStar ERROR: No floor grids are assigned.");
                FinishFailedSearch(stopwatch);
                return;
            }

            if (exits == null || exits.Length == 0)
            {
                Debug.LogError("AStar ERROR: No building exits are assigned.");
                FinishFailedSearch(stopwatch);
                return;
            }

            Debug.Log("AStar: Player position = " + player.position);
            Debug.Log("AStar: Number of grids = " + grids.Length);
            Debug.Log("AStar: Number of exits = " + exits.Length);

            Debug.Log(
                "AStar: Number of floor links = " +
                (floorLinks != null ? floorLinks.Length : 0)
            );

            Debug.Log(
                "AStar FDS: Smoke = " +
                currentFDSSmoke.ToString("F3") +
                " | Temperature = " +
                currentFDSTemperature.ToString("F1") +
                " C | HRR = " +
                currentFDSHRR.ToString("F1") +
                " kW | Hazard Level = " +
                currentHazardLevel
            );

            BuildNodeGridMap();

            if (nodeGridMap.Count == 0)
            {
                Debug.LogError(
                    "AStar ERROR: Node/Grid map is empty. " +
                    "The floor grids may not have been created."
                );

                FinishFailedSearch(stopwatch);
                return;
            }

            ResetAllGridNodes();

            stairArrivalLink.Clear();
            stairFromNode.Clear();

            AStarGrid startGrid =
                FindGridForPosition(player.position);

            if (startGrid == null)
            {
                Debug.LogError(
                    "AStar ERROR: Could not determine which floor grid contains the Player."
                );

                Debug.LogError(
                    "Player position: " + player.position
                );

                FinishFailedSearch(stopwatch);
                return;
            }

            Debug.Log(
                "AStar: Player detected on grid = " +
                startGrid.name
            );

            AStarGrid.Node startNode =
                startGrid.NodeFromWorldPoint(player.position);

            if (startNode == null)
            {
                Debug.LogError(
                    "AStar ERROR: Could not obtain a starting node."
                );

                FinishFailedSearch(stopwatch);
                return;
            }

            if (!startGrid.IsNodeTraversable(startNode))
            {
                Debug.LogWarning(
                    "AStar: Player is currently over a blocked node. " +
                    "Searching for closest safe/walkable node."
                );

                startNode =
                    startGrid.FindClosestWalkableNode(startNode);
            }

            if (startNode == null)
            {
                Debug.LogError(
                    "AStar ERROR: No safe/walkable starting node could be found."
                );

                FinishFailedSearch(stopwatch);
                return;
            }

            Debug.Log(
                "AStar: Start node = [" +
                startNode.gridX + "," +
                startNode.gridY + "] on " +
                startGrid.name
            );

            BuildExitNodeMap();

            if (exitNodeMap.Count == 0)
            {
                Debug.LogError(
                    "AStar ERROR: None of the assigned exits could be mapped to a safe/walkable grid node."
                );

                FinishFailedSearch(stopwatch);
                return;
            }

            Debug.Log(
                "AStar: Valid mapped exits = " +
                exitNodeMap.Count
            );

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
                    "AStar: NO EVACUATION ROUTE FOUND."
                );

                Debug.LogWarning(
                    "Check that the stair points and exits are located " +
                    "on or near safe/walkable nodes."
                );

                PrintAnalysis();
                return;
            }

            finalNodePath =
                RetracePath(startNode, goalNode);

            if (finalNodePath == null ||
                finalNodePath.Count == 0)
            {
                pathFound = false;
                selectedExit = null;

                Debug.LogError(
                    "AStar ERROR: Goal was reached but path retracing failed."
                );

                PrintAnalysis();
                return;
            }

            pathFound = true;

            if (exitNodeMap.ContainsKey(goalNode))
                selectedExit = exitNodeMap[goalNode];

            pathCost = goalNode.gCost;

            BuildVisualPath(finalNodePath);

            CalculatePathDistance(finalVisualPath);

            DrawPath(finalVisualPath);

            Debug.Log("==================================================");
            Debug.Log("AStar: PATH FOUND");

            Debug.Log(
                "AStar: Start Grid = " +
                startGrid.name
            );

            Debug.Log(
                "AStar: Selected Exit = " +
                (selectedExit != null
                    ? selectedExit.name
                    : "NONE")
            );

            Debug.Log(
                "AStar: Path Nodes = " +
                finalNodePath.Count
            );

            Debug.Log(
                "AStar: Visual Points = " +
                finalVisualPath.Count
            );

            Debug.Log(
                "AStar: Current Hazard Level = " +
                currentHazardLevel
            );

            PrintAnalysis();

            Debug.Log("==================================================");
        }

        // ============================================================
        // FDS HAZARD
        // ============================================================

        private void UpdateCurrentFDSHazard()
        {
            currentFDSSmoke = 0f;
            currentFDSTemperature = 27f;
            currentFDSHRR = 0f;
            currentHazardLevel = 0;

            if (fdsPlaybackController == null)
            {
                if (showDebugLogs)
                    Debug.LogWarning(
                        "AStar FDS: FDSPlaybackController is not assigned. FDS hazard disabled."
                    );

                return;
            }

            if (!fdsPlaybackController.IsDatasetReady())
            {
                if (showDebugLogs)
                    Debug.LogWarning(
                        "AStar FDS: Dataset is not ready yet."
                    );

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

            if (frame != null)
            {
                currentFDSTemperature =
                    GetFrameTemperature(frame);
            }

            currentHazardLevel =
                CalculateHazardLevel(
                    currentFDSSmoke,
                    currentFDSTemperature,
                    currentFDSHRR
                );
        }

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
        // DYNAMIC HAZARD CHANGE DETECTION
        // ============================================================

        private bool HazardChanged()
        {
            UpdateCurrentFDSHazard();

            bool changed = false;

            if (lastSmoke < -100f)
            {
                changed = true;
            }
            else
            {
                if (Mathf.Abs(
                        currentFDSSmoke -
                        lastSmoke) >=
                    smokeChangeThreshold)
                {
                    changed = true;
                }

                if (Mathf.Abs(
                        currentFDSTemperature -
                        lastTemperature) >=
                    temperatureChangeThreshold)
                {
                    changed = true;
                }

                if (Mathf.Abs(
                        currentFDSHRR -
                        lastHRR) >=
                    hrrChangeThreshold)
                {
                    changed = true;
                }
            }

            Vector3 currentFirePosition;

            if (TryGetFirePosition(
                    out currentFirePosition))
            {
                if (!hasLastFirePosition)
                {
                    changed = true;
                }
                else if (
                    Vector3.Distance(
                        currentFirePosition,
                        lastFirePosition
                    ) >= firePositionChangeThreshold)
                {
                    changed = true;
                }
            }

            int activeFirePointCount =
                GetActiveFirePointCount();

            if (lastActiveFirePointCount < 0)
            {
                lastActiveFirePointCount =
                    activeFirePointCount;
            }
            else if (activeFirePointCount != lastActiveFirePointCount)
            {
                changed = true;

                if (showDebugLogs)
                {
                    Debug.Log(
                        "AStar: Active fire point count changed from " +
                        lastActiveFirePointCount +
                        " to " +
                        activeFirePointCount +
                        "."
                    );
                }

                lastActiveFirePointCount =
                    activeFirePointCount;
            }

            lastSmoke = currentFDSSmoke;
            lastTemperature = currentFDSTemperature;
            lastHRR = currentFDSHRR;

            if (TryGetFirePosition(
                    out currentFirePosition))
            {
                lastFirePosition = currentFirePosition;
                hasLastFirePosition = true;
            }

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
        // ACTIVE FIRE POINT HAZARD
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

        private bool TryGetNearestActiveFireHazard(
            Vector3 nodePosition,
            out Vector3 nearestFirePosition,
            out float nearestFireDistance)
        {
            nearestFirePosition = Vector3.zero;
            nearestFireDistance = Mathf.Infinity;

            bool found = false;

            // Always include the confirmed main fire.
            Vector3 mainFirePosition;

            if (TryGetFirePosition(out mainFirePosition))
            {
                nearestFirePosition = mainFirePosition;
                nearestFireDistance =
                    Vector3.Distance(
                        nodePosition,
                        mainFirePosition
                    );

                found = true;
            }

            if (!includeActiveFirePoints ||
                firePlacementManager == null ||
                firePlacementManager.firePoints == null)
            {
                return found;
            }

            int activeCount = 0;

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

                activeCount++;

                if (maximumActiveFirePoints > 0 &&
                    activeCount > maximumActiveFirePoints)
                {
                    break;
                }

                float distance =
                    Vector3.Distance(
                        nodePosition,
                        firePoint.transform.position
                    );

                if (!found ||
                    distance < nearestFireDistance)
                {
                    nearestFireDistance = distance;
                    nearestFirePosition =
                        firePoint.transform.position;
                    found = true;
                }
            }

            return found;
        }

        // ============================================================
        // NODE HAZARD COST
        // ============================================================

        private int GetDynamicHazardPenalty(
            AStarGrid.Node node)
        {
            if (node == null)
                return 0;

            float totalPenalty = 0f;

            // --------------------------------------------------------
            // FDS GLOBAL HAZARD
            // --------------------------------------------------------

            totalPenalty +=
                GetSmokePenalty(
                    currentFDSSmoke
                );

            totalPenalty +=
                GetTemperaturePenalty(
                    currentFDSTemperature
                );

            totalPenalty +=
                GetHRRPenalty(
                    currentFDSHRR
                );

            // --------------------------------------------------------
            // FIRE SPATIAL HAZARD
            // --------------------------------------------------------

            // Main fire + every active spreading fire point.
            Vector3 nearestFirePosition;
            float nearestFireDistance;

            if (TryGetNearestActiveFireHazard(
                    node.worldPosition,
                    out nearestFirePosition,
                    out nearestFireDistance))
            {
                totalPenalty +=
                    GetFireDistancePenalty(
                        nearestFireDistance
                    );
            }

            totalPenalty *=
                Mathf.Max(
                    0f,
                    fdsPenaltyMultiplier
                );

            if (totalPenalty <= 0f)
                return 0;

            if (totalPenalty >= int.MaxValue)
                return int.MaxValue;

            return Mathf.RoundToInt(
                totalPenalty
            );
        }

        // ============================================================
        // SMOKE PENALTY
        // ============================================================

        private float GetSmokePenalty(
            float smoke)
        {
            if (smoke <= smokeCaution)
                return 0f;

            if (smoke >= smokeCritical)
                return 30000f;

            if (smoke >= smokeDanger)
            {
                float t =
                    Mathf.InverseLerp(
                        smokeDanger,
                        smokeCritical,
                        smoke
                    );

                return Mathf.Lerp(
                    5000f,
                    30000f,
                    t
                );
            }

            float cautionT =
                Mathf.InverseLerp(
                    smokeCaution,
                    smokeDanger,
                    smoke
                );

            return Mathf.Lerp(
                500f,
                5000f,
                cautionT
            );
        }

        // ============================================================
        // TEMPERATURE PENALTY
        // ============================================================

        private float GetTemperaturePenalty(
            float temperature)
        {
            if (temperature <= temperatureCaution)
                return 0f;

            if (temperature >= temperatureCritical)
                return 30000f;

            if (temperature >= temperatureDanger)
            {
                float t =
                    Mathf.InverseLerp(
                        temperatureDanger,
                        temperatureCritical,
                        temperature
                    );

                return Mathf.Lerp(
                    5000f,
                    30000f,
                    t
                );
            }

            float cautionT =
                Mathf.InverseLerp(
                    temperatureCaution,
                    temperatureDanger,
                    temperature
                );

            return Mathf.Lerp(
                500f,
                5000f,
                cautionT
            );
        }

        // ============================================================
        // HRR PENALTY
        // ============================================================

        private float GetHRRPenalty(
            float hrr)
        {
            if (hrr <= hrrCaution)
                return 0f;

            if (hrr >= hrrCritical)
                return 30000f;

            if (hrr >= hrrDanger)
            {
                float t =
                    Mathf.InverseLerp(
                        hrrDanger,
                        hrrCritical,
                        hrr
                    );

                return Mathf.Lerp(
                    5000f,
                    30000f,
                    t
                );
            }

            float cautionT =
                Mathf.InverseLerp(
                    hrrCaution,
                    hrrDanger,
                    hrr
                );

            return Mathf.Lerp(
                500f,
                5000f,
                cautionT
            );
        }

        // ============================================================
        // FIRE DISTANCE PENALTY
        // ============================================================

        private float GetFireDistancePenalty(
            float distance)
        {
            if (distance >= fireHazardStartDistance)
                return 0f;

            if (distance <= criticalFireBlockDistance &&
                blockCriticalFireArea &&
                currentHazardLevel >= 3)
            {
                return maximumFirePenalty;
            }

            if (distance <= fireCriticalDistance)
            {
                float t =
                    Mathf.InverseLerp(
                        fireCriticalDistance,
                        0f,
                        distance
                    );

                return Mathf.Lerp(
                    10000f,
                    maximumFirePenalty,
                    t
                );
            }

            float hazardT =
                Mathf.InverseLerp(
                    fireHazardStartDistance,
                    fireCriticalDistance,
                    distance
                );

            return Mathf.Lerp(
                1000f,
                10000f,
                hazardT
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
                        "AStar: Grid element " +
                        i +
                        " is empty."
                    );

                    continue;
                }

                if (grid.grid == null)
                {
                    Debug.LogWarning(
                        "AStar: " +
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
                        "AStar: Registered " +
                        added +
                        " nodes from " +
                        grid.name
                    );
                }
            }

            Debug.Log(
                "AStar: Total registered nodes = " +
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
                        "AStar: Exit Element " +
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
                        "AStar: Could not find a floor grid for exit " +
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
                        "AStar: Could not map exit " +
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
                            "AStar: Exit " +
                            exit.name +
                            " is on a blocked/hazardous node and no safe node could be found."
                        );

                        continue;
                    }

                    if (showDebugLogs)
                    {
                        Debug.LogWarning(
                            "AStar: Exit " +
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
                        "AStar EXIT: " +
                        exit.name +
                        " -> " +
                        exitGrid.name +
                        " node [" +
                        exitNode.gridX + "," +
                        exitNode.gridY + "]"
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
        // MULTI-FLOOR A*
        // ============================================================

        private AStarGrid.Node FindMultiFloorPath(
            AStarGrid.Node startNode)
        {
            List<AStarGrid.Node> openSet =
                new List<AStarGrid.Node>();

            HashSet<AStarGrid.Node> closedSet =
                new HashSet<AStarGrid.Node>();

            startNode.gCost = 0;

            startNode.hCost =
                GetHeuristicToClosestExit(startNode);

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

                if (exitNodeMap.ContainsKey(currentNode))
                {
                    if (showDebugLogs)
                    {
                        Debug.Log(
                            "AStar: Exit node reached after visiting " +
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

                    int movementCost =
                        SafeAdd(
                            GetGridMovementCost(
                                currentNode,
                                neighbour
                            ),
                            currentGrid.GetHazardPenalty(
                                neighbour
                            )
                        );

                    int dynamicHazard =
                        GetDynamicHazardPenalty(
                            neighbour
                        );

                    movementCost =
                        SafeAdd(
                            movementCost,
                            dynamicHazard
                        );

                    TryUpdateNode(
                        currentNode,
                        neighbour,
                        movementCost,
                        openSet
                    );
                }

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

                if (currentNode != entryNode)
                    continue;

                AStarGrid.Node destinationNode =
                    destinationGrid.NodeFromWorldPoint(
                        exitPoint.position
                    );

                if (destinationNode == null)
                    continue;

                if (!destinationGrid.IsNodeTraversable(destinationNode))
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

                int transitionCost =
                    CalculateStairCost(
                        link,
                        entryPoint.position,
                        exitPoint.position,
                        currentGrid,
                        destinationGrid
                    );

                int destinationHazard =
                    destinationGrid.GetHazardPenalty(
                        destinationNode
                    );

                int dynamicHazard =
                    GetDynamicHazardPenalty(
                        destinationNode
                    );

                int newCost =
                    SafeAdd(
                        currentNode.gCost,
                        transitionCost
                    );

                newCost =
                    SafeAdd(
                        newCost,
                        destinationHazard
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

                    destinationNode.hCost =
                        GetHeuristicToClosestExit(
                            destinationNode
                        );

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
                            "AStar STAIR: " +
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

                neighbour.hCost =
                    GetHeuristicToClosestExit(
                        neighbour
                    );

                neighbour.parent =
                    currentNode;

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
        // A* HEURISTIC
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

                if (node.fCost <
                    best.fCost)
                {
                    best = node;
                }
                else if (
                    node.fCost ==
                    best.fCost &&
                    node.hCost <
                    best.hCost)
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
                        "AStar ERROR: Path retrace safety limit reached."
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
                    "AStar ERROR: Path Line is not assigned."
                );

                return;
            }

            if (visualPath == null ||
                visualPath.Count < 2)
            {
                Debug.LogWarning(
                    "AStar: Not enough visual points to draw a line."
                );

                pathLine.positionCount = 0;
                return;
            }

            pathLine.enabled = true;
            pathLine.useWorldSpace = true;

            // A* PATH = GREEN
            pathLine.startColor = Color.green;
            pathLine.endColor = Color.green;

            pathLine.startWidth = pathWidth;
            pathLine.endWidth = pathWidth;

            pathLine.positionCount =
                visualPath.Count;

            pathLine.SetPositions(
                visualPath.ToArray()
            );

            Debug.Log(
                "AStar: LineRenderer received " +
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
                "=============== A* ANALYSIS ==============="
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
                currentFDSSmoke.ToString("F3")
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
                "AStar: ForceRecalculate() called."
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
                    "AStar: Player changed to " +
                    (player != null
                        ? player.name
                        : "None")
                );
            }
        }

        // ============================================================
        // PUBLIC HAZARD GETTERS
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

            if (b > 0 &&
                a > int.MaxValue - b)
            {
                return int.MaxValue;
            }

            return a + b;
        }
    }