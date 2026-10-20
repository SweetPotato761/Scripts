using UnityEngine;

public class AStarFloorLink : MonoBehaviour
{
    // =========================================================
    // FLOOR CONNECTION
    // =========================================================

    [Header("Floor Connection")]

    [Tooltip("Grid where this stair route starts.")]
    public AStarGrid fromGrid;

    [Tooltip("Grid where this stair route ends.")]
    public AStarGrid toGrid;


    // =========================================================
    // STAIR POINTS
    // =========================================================

    [Header("Stair Points")]

    [Tooltip(
        "Empty GameObject located at the stair entrance " +
        "on the starting floor."
    )]
    public Transform fromPoint;


    [Tooltip(
        "Empty GameObject located at the stair arrival point " +
        "on the destination floor."
    )]
    public Transform toPoint;


    // =========================================================
    // SETTINGS
    // =========================================================

    [Header("Direction")]

    [Tooltip(
        "OFF = only From Grid → To Grid.\n" +
        "ON = movement allowed in both directions."
    )]
    public bool bidirectional = false;


    [Header("Transition Cost")]

    [Tooltip(
        "Additional A* cost for using this stair connection."
    )]
    public int extraTransitionCost = 10;


    // =========================================================
    // CHECK VALID
    // =========================================================

    public bool IsValid()
    {
        return
            fromGrid != null &&
            toGrid != null &&
            fromPoint != null &&
            toPoint != null;
    }


    // =========================================================
    // CAN TRAVEL FROM GRID?
    // =========================================================

    public bool CanTravelFrom(
        AStarGrid currentGrid)
    {
        if (!IsValid())
        {
            return false;
        }


        if (
            currentGrid ==
            fromGrid
        )
        {
            return true;
        }


        if (
            bidirectional &&
            currentGrid ==
            toGrid
        )
        {
            return true;
        }


        return false;
    }


    // =========================================================
    // DESTINATION GRID
    // =========================================================

    public AStarGrid GetDestinationGrid(
        AStarGrid currentGrid)
    {
        if (!IsValid())
        {
            return null;
        }


        if (
            currentGrid ==
            fromGrid
        )
        {
            return toGrid;
        }


        if (
            bidirectional &&
            currentGrid ==
            toGrid
        )
        {
            return fromGrid;
        }


        return null;
    }


    // =========================================================
    // STAIR ENTRY POINT
    // =========================================================

    public Transform GetEntryPoint(
        AStarGrid currentGrid)
    {
        if (!IsValid())
        {
            return null;
        }


        if (
            currentGrid ==
            fromGrid
        )
        {
            return fromPoint;
        }


        if (
            bidirectional &&
            currentGrid ==
            toGrid
        )
        {
            return toPoint;
        }


        return null;
    }


    // =========================================================
    // STAIR EXIT POINT
    // =========================================================

    public Transform GetExitPoint(
        AStarGrid currentGrid)
    {
        if (!IsValid())
        {
            return null;
        }


        if (
            currentGrid ==
            fromGrid
        )
        {
            return toPoint;
        }


        if (
            bidirectional &&
            currentGrid ==
            toGrid
        )
        {
            return fromPoint;
        }


        return null;
    }


    // =========================================================
    // DEBUG DRAWING
    // =========================================================

    private void OnDrawGizmos()
    {
        if (
            fromPoint == null ||
            toPoint == null
        )
        {
            return;
        }


        Gizmos.color =
            Color.cyan;


        float sphereSize =
            150f;


        Gizmos.DrawSphere(
            fromPoint.position,
            sphereSize
        );


        Gizmos.DrawSphere(
            toPoint.position,
            sphereSize
        );


        Gizmos.DrawLine(
            fromPoint.position,
            toPoint.position
        );
    }
}