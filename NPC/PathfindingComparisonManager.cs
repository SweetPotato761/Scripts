using TMPro;
using UnityEngine;

public class PathfindingComparisonManager : MonoBehaviour
{
    [Header("Pathfinding Algorithms")]
    public AStarPathfinder aStarPathfinder;
    public DijkstraPathfinder dijkstraPathfinder;

    [Header("A* UI")]
    public TMP_Text aStarTimeText;
    public TMP_Text aStarVisitedText;
    public TMP_Text aStarCostText;
    public TMP_Text aStarDistanceText;
    public TMP_Text aStarExitText;

    [Header("Dijkstra UI")]
    public TMP_Text dijkstraTimeText;
    public TMP_Text dijkstraVisitedText;
    public TMP_Text dijkstraCostText;
    public TMP_Text dijkstraDistanceText;
    public TMP_Text dijkstraExitText;

    [Header("Display Settings")]
    public bool updateContinuously = true;

    [Tooltip("How often the comparison UI refreshes.")]
    public float updateInterval = 0.25f;

    private float nextUpdateTime;


    private void Start()
    {
        FindMissingReferences();

        UpdateComparisonUI();
    }


    private void Update()
    {
        if (!updateContinuously)
            return;

        if (Time.unscaledTime < nextUpdateTime)
            return;

        nextUpdateTime =
            Time.unscaledTime + updateInterval;

        UpdateComparisonUI();
    }


    private void FindMissingReferences()
    {
        if (aStarPathfinder == null)
        {
            aStarPathfinder =
                FindAnyObjectByType<AStarPathfinder>();
        }

        if (dijkstraPathfinder == null)
        {
            dijkstraPathfinder =
                FindAnyObjectByType<DijkstraPathfinder>();
        }
    }


    public void UpdateComparisonUI()
    {
        UpdateAStarUI();
        UpdateDijkstraUI();
    }


    private void UpdateAStarUI()
    {
        if (aStarPathfinder == null)
        {
            SetAStarUnavailable();
            return;
        }

        if (aStarTimeText != null)
        {
            aStarTimeText.text =
                "Time: " +
                aStarPathfinder.executionTimeMs.ToString("F3") +
                " ms";
        }

        if (aStarVisitedText != null)
        {
            aStarVisitedText.text =
                "Visited Nodes: " +
                aStarPathfinder.visitedNodes;
        }

        if (aStarCostText != null)
        {
            aStarCostText.text =
                "Path Cost: " +
                aStarPathfinder.pathCost;
        }

        if (aStarDistanceText != null)
        {
            aStarDistanceText.text =
                "Distance: " +
                aStarPathfinder.pathDistance.ToString("F2");
        }

        if (aStarExitText != null)
        {
            if (
                aStarPathfinder.pathFound &&
                aStarPathfinder.selectedExit != null
            )
            {
                aStarExitText.text =
                    "Exit: " +
                    aStarPathfinder.selectedExit.name;
            }
            else
            {
                aStarExitText.text =
                    "Exit: None";
            }
        }
    }


    private void UpdateDijkstraUI()
    {
        if (dijkstraPathfinder == null)
        {
            SetDijkstraUnavailable();
            return;
        }

        if (dijkstraTimeText != null)
        {
            dijkstraTimeText.text =
                "Time: " +
                dijkstraPathfinder.executionTimeMs.ToString("F3") +
                " ms";
        }

        if (dijkstraVisitedText != null)
        {
            dijkstraVisitedText.text =
                "Visited Nodes: " +
                dijkstraPathfinder.visitedNodes;
        }

        if (dijkstraCostText != null)
        {
            dijkstraCostText.text =
                "Path Cost: " +
                dijkstraPathfinder.pathCost;
        }

        if (dijkstraDistanceText != null)
        {
            dijkstraDistanceText.text =
                "Distance: " +
                dijkstraPathfinder.pathDistance.ToString("F2");
        }

        if (dijkstraExitText != null)
        {
            if (
                dijkstraPathfinder.pathFound &&
                dijkstraPathfinder.selectedExit != null
            )
            {
                dijkstraExitText.text =
                    "Exit: " +
                    dijkstraPathfinder.selectedExit.name;
            }
            else
            {
                dijkstraExitText.text =
                    "Exit: None";
            }
        }
    }


    private void SetAStarUnavailable()
    {
        if (aStarTimeText != null)
            aStarTimeText.text = "Time: N/A";

        if (aStarVisitedText != null)
            aStarVisitedText.text = "Visited Nodes: N/A";

        if (aStarCostText != null)
            aStarCostText.text = "Path Cost: N/A";

        if (aStarDistanceText != null)
            aStarDistanceText.text = "Distance: N/A";

        if (aStarExitText != null)
            aStarExitText.text = "Exit: N/A";
    }


    private void SetDijkstraUnavailable()
    {
        if (dijkstraTimeText != null)
            dijkstraTimeText.text = "Time: N/A";

        if (dijkstraVisitedText != null)
            dijkstraVisitedText.text =
                "Visited Nodes: N/A";

        if (dijkstraCostText != null)
            dijkstraCostText.text =
                "Path Cost: N/A";

        if (dijkstraDistanceText != null)
            dijkstraDistanceText.text =
                "Distance: N/A";

        if (dijkstraExitText != null)
            dijkstraExitText.text =
                "Exit: N/A";
    }


    public void RefreshNow()
    {
        FindMissingReferences();
        UpdateComparisonUI();
    }
}