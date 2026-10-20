using UnityEngine;

public class HazardGrid : MonoBehaviour
{
    [Header("Grid Size")]
    public int gridWidth = 25;
    public int gridHeight = 15;

    [Header("Cell Size")]
    [Tooltip("Because your OBJ uses about 700 Unity units per real meter, use 1400 for 2-meter cells.")]
    public float cellSize = 1400f;

    [Header("Grid Origin")]
    public Transform origin;

    [Header("Obstacle Detection")]
    public LayerMask obstacleMask;
    public float obstacleCheckHeight = 1000f;
    public float obstacleCheckRadiusMultiplier = 0.4f;

    [Header("Debug")]
    public bool drawGrid = true;
    public bool drawObstacleCells = true;

    private HazardCell[,] grid;

    public HazardCell[,] Grid
    {
        get { return grid; }
    }

    private void Awake()
    {
        GenerateGrid();
    }

    public void GenerateGrid()
    {
        if (gridWidth <= 0 || gridHeight <= 0)
        {
            Debug.LogError("HazardGrid: Grid width and height must be greater than zero.");
            return;
        }

        if (cellSize <= 0f)
        {
            Debug.LogError("HazardGrid: Cell size must be greater than zero.");
            return;
        }

        grid = new HazardCell[gridWidth, gridHeight];

        Vector3 startPosition = GetOriginPosition();

        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridHeight; z++)
            {
                Vector3 worldPosition =
                    startPosition +
                    new Vector3(
                        (x + 0.5f) * cellSize,
                        0f,
                        (z + 0.5f) * cellSize
                    );

                HazardCell cell =
                    new HazardCell(
                        x,
                        z,
                        worldPosition
                    );

                cell.isObstacle =
                    CheckForObstacle(worldPosition);

                grid[x, z] = cell;
            }
        }

        Debug.Log(
            $"Hazard Grid Created: {gridWidth} x {gridHeight}, " +
            $"Cell Size: {cellSize}"
        );
    }

    private Vector3 GetOriginPosition()
    {
        if (origin != null)
            return origin.position;

        return transform.position;
    }

    private bool CheckForObstacle(Vector3 cellPosition)
    {
        Vector3 checkPosition =
            cellPosition +
            Vector3.up * (obstacleCheckHeight * 0.5f);

        Vector3 halfExtents = new Vector3(
            cellSize * obstacleCheckRadiusMultiplier,
            obstacleCheckHeight * 0.5f,
            cellSize * obstacleCheckRadiusMultiplier
        );

        return Physics.CheckBox(
            checkPosition,
            halfExtents,
            Quaternion.identity,
            obstacleMask,
            QueryTriggerInteraction.Ignore
        );
    }

    public HazardCell GetCell(int x, int z)
    {
        if (grid == null)
            return null;

        if (x < 0 ||
            x >= gridWidth ||
            z < 0 ||
            z >= gridHeight)
        {
            return null;
        }

        return grid[x, z];
    }

    public HazardCell GetCell(Vector3 worldPosition)
    {
        if (grid == null)
            return null;

        Vector3 startPosition = GetOriginPosition();

        int x = Mathf.FloorToInt(
            (worldPosition.x - startPosition.x) /
            cellSize
        );

        int z = Mathf.FloorToInt(
            (worldPosition.z - startPosition.z) /
            cellSize
        );

        return GetCell(x, z);
    }

    public Vector3 GetWorldPosition(int x, int z)
    {
        Vector3 startPosition = GetOriginPosition();

        return startPosition +
               new Vector3(
                   (x + 0.5f) * cellSize,
                   0f,
                   (z + 0.5f) * cellSize
               );
    }

    public bool IsInsideGrid(Vector3 worldPosition)
    {
        return GetCell(worldPosition) != null;
    }

    public void ResetGrid(float ambientTemperature = 27f)
    {
        if (grid == null)
            return;

        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridHeight; z++)
            {
                HazardCell cell = grid[x, z];

                if (cell != null)
                {
                    bool obstacleState =
                        cell.isObstacle;

                    cell.ResetCell(
                        ambientTemperature
                    );

                    cell.isObstacle =
                        obstacleState;
                }
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (!drawGrid)
            return;

        Vector3 startPosition = GetOriginPosition();

        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridHeight; z++)
            {
                Vector3 center =
                    startPosition +
                    new Vector3(
                        (x + 0.5f) * cellSize,
                        0f,
                        (z + 0.5f) * cellSize
                    );

                bool obstacle = false;

                if (Application.isPlaying &&
                    grid != null &&
                    x < grid.GetLength(0) &&
                    z < grid.GetLength(1) &&
                    grid[x, z] != null)
                {
                    obstacle =
                        grid[x, z].isObstacle;
                }

                if (obstacle &&
                    drawObstacleCells)
                {
                    Gizmos.color =
                        new Color(
                            1f,
                            0f,
                            0f,
                            0.45f
                        );
                }
                else
                {
                    Gizmos.color =
                        new Color(
                            0f,
                            1f,
                            1f,
                            0.2f
                        );
                }

                Gizmos.DrawWireCube(
                    center,
                    new Vector3(
                        cellSize,
                        20f,
                        cellSize
                    )
                );
            }
        }
    }
}