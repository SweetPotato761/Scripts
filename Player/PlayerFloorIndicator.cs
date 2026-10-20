using UnityEngine;
using TMPro;

public class PlayerFloorIndicator : MonoBehaviour
{
    [Header("PLAYER")]
    public Transform player;

[Header("FLOOR GRIDS")]
    public AStarGrid[] floorGrids = new AStarGrid[5];

    [Header("UI")]
    public TMP_Text currentFloorLabel;
    public TMP_Text currentFloorValue;

    [Header("UPDATE")]
    public float updateInterval = 0.1f;

    private int currentFloor = 0;
    private float updateTimer;

    private void Start()
    {
        if (currentFloorLabel != null)
            currentFloorLabel.text = "Current Floor:";

        UpdateFloor(true);
    }

    private void Update()
    {
        updateTimer += Time.deltaTime;

        if (updateTimer >= updateInterval)
        {
            updateTimer = 0f;
            UpdateFloor(false);
        }
    }

    private void UpdateFloor(bool forceUpdate)
    {
        if (player == null)
            return;

        if (floorGrids == null || floorGrids.Length < 5)
            return;

        float playerY = player.position.y;

        float parkingY = GetFloorY(0);
        float floor1Y = GetFloorY(1);
        float floor2Y = GetFloorY(2);
        float floor3Y = GetFloorY(3);
        float floor4Y = GetFloorY(4);

        float boundaryParkingFloor1 =
            (parkingY + floor1Y) / 2f;

        float boundaryFloor1Floor2 =
            (floor1Y + floor2Y) / 2f;

        float boundaryFloor2Floor3 =
            (floor2Y + floor3Y) / 2f;

        float boundaryFloor3Floor4 =
            (floor3Y + floor4Y) / 2f;

        int detectedFloor;

        if (playerY < boundaryParkingFloor1)
        {
            detectedFloor = 0;
        }
        else if (playerY < boundaryFloor1Floor2)
        {
            detectedFloor = 1;
        }
        else if (playerY < boundaryFloor2Floor3)
        {
            detectedFloor = 2;
        }
        else if (playerY < boundaryFloor3Floor4)
        {
            detectedFloor = 3;
        }
        else
        {
            detectedFloor = 4;
        }

        if (forceUpdate || detectedFloor != currentFloor)
        {
            currentFloor = detectedFloor;

            UpdateUI();

            Debug.Log(
                "Current Floor: " +
                GetFloorName(currentFloor) +
                " | Player Y: " +
                playerY
            );
        }
    }

    private float GetFloorY(int index)
    {
        if (floorGrids[index] == null)
            return 0f;

        return floorGrids[index].transform.position.y;
    }

    private void UpdateUI()
    {
        if (currentFloorLabel != null)
            currentFloorLabel.text = "Current Floor:";

        if (currentFloorValue != null)
            currentFloorValue.text = GetFloorName(currentFloor);
    }

    private string GetFloorName(int floor)
    {
        switch (floor)
        {
            case 0:
                return "PARKING";

            case 1:
                return "FLOOR 1";

            case 2:
                return "FLOOR 2";

            case 3:
                return "FLOOR 3";

            case 4:
                return "FLOOR 4";

            default:
                return "UNKNOWN";
        }
    }

    public int GetCurrentFloor()
    {
        return currentFloor;
    }

    public bool IsOnFloor(int floorNumber)
    {
        return currentFloor == floorNumber;
    }

    public string GetCurrentFloorName()
    {
        return GetFloorName(currentFloor);
    }

}
