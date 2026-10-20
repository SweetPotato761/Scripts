using System.Collections.Generic;
using UnityEngine;

public class AStarGrid : MonoBehaviour
{
    // =========================================================
    // NODE CLASS
    // =========================================================

    [System.Serializable]
    public class Node
    {
        public bool walkable;

        public Vector3 worldPosition;

        public int gridX;
        public int gridY;

        public int gCost;
        public int hCost;

        public Node parent;

        public int fCost
        {
            get
            {
                return gCost + hCost;
            }
        }

        public Node(
            bool isWalkable,
            Vector3 position,
            int x,
            int y)
        {
            walkable = isWalkable;

            worldPosition = position;

            gridX = x;
            gridY = y;

            gCost = int.MaxValue;
            hCost = 0;

            parent = null;
        }
    }

    // =========================================================
    // GRID SETTINGS
    // =========================================================

    [Header("Grid World Size")]

    [Tooltip("Total width of this floor grid.")]
    public float gridWorldSizeX = 23000f;

    [Tooltip("Total depth of this floor grid.")]
    public float gridWorldSizeZ = 15000f;


    [Header("Node Settings")]

    [Tooltip("Distance between grid nodes.")]
    public float nodeSpacing = 2500f;

    [Tooltip("Radius used to check walls/obstacles.")]
    public float obstacleCheckRadius = 500f;


    [Header("Obstacle Detection")]

    [Tooltip("Only select your Walls / Obstacles layer here.")]
    public LayerMask unwalkableMask;


    [Header("Grid Position")]

    [Tooltip(
        "Vertical adjustment of the grid. " +
        "Normally keep this at 0 and move the Grid GameObject instead."
    )]
    public float gridHeightOffset = 0f;


    // =========================================================
    // HAZARD DETECTION
    // =========================================================

    [Header("Hazard Detection")]

    [Tooltip(
        "Assign the HazardGrid that belongs to this floor. " +
        "Example: Floor1Grid -> HazardGrid_F1."
    )]
    public HazardGrid hazardGrid;

    [Tooltip(
        "If enabled, fire, smoke and temperature can affect pathfinding."
    )]
    public bool useHazardAvoidance = true;


    [Header("Fire Safety")]

    [Tooltip("Fire cells are completely blocked.")]
    public bool blockFire = true;


    [Header("Smoke Safety")]

    [Range(0f, 1f)]
    [Tooltip(
        "Smoke at or above this value is considered heavy smoke " +
        "and cannot be used for evacuation."
    )]
    public float heavySmokeThreshold = 0.50f;

    [Tooltip(
        "Completely block nodes containing heavy smoke."
    )]
    public bool blockHeavySmoke = true;

    [Tooltip(
        "Maximum extra path cost caused by smoke below the blocking threshold."
    )]
    public int smokePenaltyMaximum = 100;


    [Header("Temperature Safety")]

    [Tooltip(
        "Temperature at or above this value is considered extremely dangerous."
    )]
    public float extremeHeatTemperature = 80f;

    [Tooltip(
        "Completely block extremely hot nodes."
    )]
    public bool blockExtremeHeat = true;

    [Tooltip(
        "Temperature begins adding path cost above this value."
    )]
    public float heatPenaltyStartTemperature = 40f;

    [Tooltip(
        "Extra path cost for every degree above the heat penalty start temperature."
    )]
    public float heatPenaltyPerDegree = 2f;


    // =========================================================
    // GRID VISUALIZATION
    // =========================================================

    [Header("Grid Visualization")]

    public bool showGrid = true;

    [Range(0.1f, 1f)]
    public float nodeDisplaySize = 0.75f;

    [Tooltip(
        "When enabled during Play Mode, hazard nodes are shown using different colors."
    )]
    public bool showHazards = true;


    // =========================================================
    // RUNTIME GRID DATA
    // =========================================================

    [Header("Runtime Grid Information")]

    public int gridSizeX;
    public int gridSizeY;

    [HideInInspector]
    public Node[,] grid;


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        CreateGrid();
    }

    private void Start()
    {
        /*
         * This makes sure the grid is generated
         * after the scene starts as well.
         */

        if (grid == null)
        {
            CreateGrid();
        }
    }


    // =========================================================
    // CREATE GRID
    // =========================================================

    public void CreateGrid()
    {
        if (nodeSpacing <= 0f)
        {
            Debug.LogError(
                name +
                ": Node Spacing must be greater than 0."
            );

            return;
        }

        gridSizeX =
            Mathf.Max(
                1,
                Mathf.RoundToInt(
                    gridWorldSizeX /
                    nodeSpacing
                )
            );

        gridSizeY =
            Mathf.Max(
                1,
                Mathf.RoundToInt(
                    gridWorldSizeZ /
                    nodeSpacing
                )
            );

        grid =
            new Node[
                gridSizeX,
                gridSizeY
            ];

        Vector3 gridCenter =
            GetGridCenter();

        Vector3 bottomLeft =
            gridCenter
            -
            Vector3.right
            *
            (
                gridWorldSizeX /
                2f
            )
            -
            Vector3.forward
            *
            (
                gridWorldSizeZ /
                2f
            );

        for (
            int x = 0;
            x < gridSizeX;
            x++
        )
        {
            for (
                int y = 0;
                y < gridSizeY;
                y++
            )
            {
                Vector3 worldPoint =
                    bottomLeft
                    +
                    Vector3.right
                    *
                    (
                        x *
                        nodeSpacing
                        +
                        nodeSpacing /
                        2f
                    )
                    +
                    Vector3.forward
                    *
                    (
                        y *
                        nodeSpacing
                        +
                        nodeSpacing /
                        2f
                    );

                bool blocked =
                    Physics.CheckSphere(
                        worldPoint,
                        obstacleCheckRadius,
                        unwalkableMask,
                        QueryTriggerInteraction.Ignore
                    );

                bool walkable =
                    !blocked;

                grid[x, y] =
                    new Node(
                        walkable,
                        worldPoint,
                        x,
                        y
                    );
            }
        }

        Debug.Log(
            name +
            " grid created: " +
            gridSizeX +
            " x " +
            gridSizeY +
            " = " +
            (
                gridSizeX *
                gridSizeY
            ) +
            " nodes."
        );
    }


    // =========================================================
    // GRID CENTER
    // =========================================================

    public Vector3 GetGridCenter()
    {
        Vector3 center =
            transform.position;

        center.y +=
            gridHeightOffset;

        return center;
    }


    // =========================================================
    // FLOOR HEIGHT
    // =========================================================

    public float GetFloorHeight()
    {
        return
            transform.position.y
            +
            gridHeightOffset;
    }


    // =========================================================
    // WORLD POSITION -> NODE
    // =========================================================

    public Node NodeFromWorldPoint(
        Vector3 worldPosition)
    {
        if (
            grid == null ||
            gridSizeX <= 0 ||
            gridSizeY <= 0
        )
        {
            return null;
        }

        Vector3 center =
            GetGridCenter();

        float left =
            center.x
            -
            gridWorldSizeX /
            2f;

        float bottom =
            center.z
            -
            gridWorldSizeZ /
            2f;

        float percentX =
            (
                worldPosition.x -
                left
            )
            /
            gridWorldSizeX;

        float percentY =
            (
                worldPosition.z -
                bottom
            )
            /
            gridWorldSizeZ;

        percentX =
            Mathf.Clamp01(
                percentX
            );

        percentY =
            Mathf.Clamp01(
                percentY
            );

        int x =
            Mathf.RoundToInt(
                (
                    gridSizeX -
                    1
                )
                *
                percentX
            );

        int y =
            Mathf.RoundToInt(
                (
                    gridSizeY -
                    1
                )
                *
                percentY
            );

        x =
            Mathf.Clamp(
                x,
                0,
                gridSizeX - 1
            );

        y =
            Mathf.Clamp(
                y,
                0,
                gridSizeY - 1
            );

        return
            grid[x, y];
    }


    // =========================================================
    // GET NEIGHBOURS
    // =========================================================

    public List<Node> GetNeighbours(
        Node node)
    {
        List<Node> neighbours =
            new List<Node>();

        if (
            grid == null ||
            node == null
        )
        {
            return neighbours;
        }

        for (
            int x = -1;
            x <= 1;
            x++
        )
        {
            for (
                int y = -1;
                y <= 1;
                y++
            )
            {
                if (
                    x == 0 &&
                    y == 0
                )
                {
                    continue;
                }

                int checkX =
                    node.gridX +
                    x;

                int checkY =
                    node.gridY +
                    y;

                if (
                    checkX >= 0 &&
                    checkX < gridSizeX &&
                    checkY >= 0 &&
                    checkY < gridSizeY
                )
                {
                    neighbours.Add(
                        grid[
                            checkX,
                            checkY
                        ]
                    );
                }
            }
        }

        return neighbours;
    }


    // =========================================================
    // HAZARD CELL FOR NODE
    // =========================================================

    public HazardCell GetHazardCell(
        Node node)
    {
        if (
            node == null ||
            hazardGrid == null
        )
        {
            return null;
        }

        return
            hazardGrid.GetCell(
                node.worldPosition
            );
    }


    // =========================================================
    // CHECK IF NODE IS BLOCKED BY HAZARD
    // =========================================================

    public bool IsNodeBlockedByHazard(
        Node node)
    {
        if (
            !useHazardAvoidance ||
            node == null ||
            hazardGrid == null
        )
        {
            return false;
        }

        HazardCell cell =
            GetHazardCell(node);

        /*
         * A node outside the HazardGrid is not automatically
         * considered dangerous.
         */
        if (cell == null)
        {
            return false;
        }

        if (
            blockFire &&
            cell.containsFire
        )
        {
            return true;
        }

        if (
            blockHeavySmoke &&
            cell.smokeDensity >=
            heavySmokeThreshold
        )
        {
            return true;
        }

        if (
            blockExtremeHeat &&
            cell.temperature >=
            extremeHeatTemperature
        )
        {
            return true;
        }

        return false;
    }


    // =========================================================
    // IS NODE SAFE / TRAVERSABLE
    // =========================================================

    public bool IsNodeTraversable(
        Node node)
    {
        if (node == null)
        {
            return false;
        }

        if (!node.walkable)
        {
            return false;
        }

        if (
            IsNodeBlockedByHazard(node)
        )
        {
            return false;
        }

        return true;
    }


    // =========================================================
    // HAZARD PATH COST
    // =========================================================

    public int GetHazardPenalty(
        Node node)
    {
        if (
            !useHazardAvoidance ||
            node == null ||
            hazardGrid == null
        )
        {
            return 0;
        }

        HazardCell cell =
            GetHazardCell(node);

        if (cell == null)
        {
            return 0;
        }

        /*
         * Blocked hazards are handled by
         * IsNodeBlockedByHazard().
         */
        if (
            IsNodeBlockedByHazard(node)
        )
        {
            return 0;
        }

        // -----------------------------------------------------
        // LIGHT / MODERATE SMOKE
        // -----------------------------------------------------

        float smokeRatio = 0f;

        if (heavySmokeThreshold > 0f)
        {
            smokeRatio =
                Mathf.Clamp01(
                    cell.smokeDensity /
                    heavySmokeThreshold
                );
        }

        int smokePenalty =
            Mathf.RoundToInt(
                smokeRatio *
                Mathf.Max(
                    0,
                    smokePenaltyMaximum
                )
            );

        // -----------------------------------------------------
        // TEMPERATURE
        // -----------------------------------------------------

        float excessTemperature =
            Mathf.Max(
                0f,
                cell.temperature -
                heatPenaltyStartTemperature
            );

        int heatPenalty =
            Mathf.RoundToInt(
                excessTemperature *
                Mathf.Max(
                    0f,
                    heatPenaltyPerDegree
                )
            );

        return
            SafeAdd(
                smokePenalty,
                heatPenalty
            );
    }


    // =========================================================
    // RESET PATHFINDING VALUES
    // =========================================================

    public void ResetNodes()
    {
        if (grid == null)
            return;

        for (
            int x = 0;
            x < gridSizeX;
            x++
        )
        {
            for (
                int y = 0;
                y < gridSizeY;
                y++
            )
            {
                Node node =
                    grid[x, y];

                if (node == null)
                    continue;

                node.gCost =
                    int.MaxValue;

                node.hCost =
                    0;

                node.parent =
                    null;
            }
        }
    }


    // =========================================================
    // CHECK IF POSITION IS INSIDE THIS FLOOR GRID
    // =========================================================

    public bool ContainsWorldPoint(
        Vector3 worldPosition)
    {
        Vector3 center =
            GetGridCenter();

        float halfX =
            gridWorldSizeX /
            2f;

        float halfZ =
            gridWorldSizeZ /
            2f;

        bool insideX =
            worldPosition.x >=
            center.x - halfX
            &&
            worldPosition.x <=
            center.x + halfX;

        bool insideZ =
            worldPosition.z >=
            center.z - halfZ
            &&
            worldPosition.z <=
            center.z + halfZ;

        return
            insideX &&
            insideZ;
    }


    // =========================================================
    // FIND CLOSEST WALKABLE / SAFE NODE
    // =========================================================

    public Node FindClosestWalkableNode(
        Node startingNode)
    {
        if (
            grid == null ||
            startingNode == null
        )
        {
            return null;
        }

        if (
            IsNodeTraversable(
                startingNode
            )
        )
        {
            return startingNode;
        }

        Node closestNode =
            null;

        float closestDistance =
            Mathf.Infinity;

        for (
            int x = 0;
            x < gridSizeX;
            x++
        )
        {
            for (
                int y = 0;
                y < gridSizeY;
                y++
            )
            {
                Node node =
                    grid[x, y];

                if (
                    node == null ||
                    !IsNodeTraversable(node)
                )
                {
                    continue;
                }

                float distance =
                    Vector3.SqrMagnitude(
                        node.worldPosition -
                        startingNode.worldPosition
                    );

                if (
                    distance <
                    closestDistance
                )
                {
                    closestDistance =
                        distance;

                    closestNode =
                        node;
                }
            }
        }

        return closestNode;
    }


    // =========================================================
    // SAFE INTEGER ADD
    // =========================================================

    private int SafeAdd(
        int a,
        int b)
    {
        if (
            a == int.MaxValue ||
            b == int.MaxValue
        )
        {
            return int.MaxValue;
        }

        long result =
            (long)a +
            (long)b;

        if (result >= int.MaxValue)
        {
            return int.MaxValue;
        }

        return
            (int)result;
    }


    // =========================================================
    // DRAW GRID
    // =========================================================

    private void OnDrawGizmos()
    {
        Vector3 center =
            GetGridCenter();

        Gizmos.color =
            Color.white;

        Gizmos.DrawWireCube(
            center,
            new Vector3(
                gridWorldSizeX,
                100f,
                gridWorldSizeZ
            )
        );

        if (!showGrid)
            return;

        if (grid == null)
            return;

        float displaySize =
            nodeSpacing *
            Mathf.Clamp(
                nodeDisplaySize,
                0.1f,
                1f
            );

        Vector3 nodeSize =
            new Vector3(
                displaySize,
                Mathf.Max(
                    50f,
                    displaySize *
                    0.05f
                ),
                displaySize
            );

        for (
            int x = 0;
            x < gridSizeX;
            x++
        )
        {
            for (
                int y = 0;
                y < gridSizeY;
                y++
            )
            {
                Node node =
                    grid[x, y];

                if (node == null)
                    continue;

                // Wall / obstacle
                if (!node.walkable)
                {
                    Gizmos.color =
                        Color.red;
                }
                else if (
                    Application.isPlaying &&
                    showHazards &&
                    useHazardAvoidance &&
                    hazardGrid != null
                )
                {
                    HazardCell cell =
                        GetHazardCell(node);

                    if (cell == null)
                    {
                        Gizmos.color =
                            Color.green;
                    }
                    else if (cell.containsFire)
                    {
                        Gizmos.color =
                            new Color(
                                1f,
                                0f,
                                0f,
                                1f
                            );
                    }
                    else if (
                        cell.smokeDensity >=
                        heavySmokeThreshold
                    )
                    {
                        Gizmos.color =
                            new Color(
                                1f,
                                0.4f,
                                0f,
                                1f
                            );
                    }
                    else if (
                        cell.temperature >=
                        extremeHeatTemperature
                    )
                    {
                        Gizmos.color =
                            new Color(
                                1f,
                                0.75f,
                                0f,
                                1f
                            );
                    }
                    else if (
                        cell.smokeDensity > 0f ||
                        cell.temperature >
                        heatPenaltyStartTemperature
                    )
                    {
                        Gizmos.color =
                            Color.yellow;
                    }
                    else
                    {
                        Gizmos.color =
                            Color.green;
                    }
                }
                else
                {
                    Gizmos.color =
                        Color.green;
                }

                Gizmos.DrawCube(
                    node.worldPosition,
                    nodeSize
                );
            }
        }
    }
}