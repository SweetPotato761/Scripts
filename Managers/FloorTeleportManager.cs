using UnityEngine;

public class FloorTeleportManager : MonoBehaviour
{
    [Header("Player")]
    public MobilePlayerController player;

    [Header("Floor Teleport Points")]
    public Transform floor1Point;
    public Transform floor2Point;
    public Transform floor3Point;
    public Transform floor4Point;
    public Transform floor5Point;

    [Header("Optional Menu")]
    public GameObject floorPanel;

    [Header("Settings")]
    public bool matchFloorRotation = true;

    public void GoToFloor1()
    {
        TeleportTo(floor1Point);
    }

    public void GoToFloor2()
    {
        TeleportTo(floor2Point);
    }

    public void GoToFloor3()
    {
        TeleportTo(floor3Point);
    }

    public void GoToFloor4()
    {
        TeleportTo(floor4Point);
    }

    public void GoToFloor5()
    {
        TeleportTo(floor5Point);
    }

    private void TeleportTo(Transform target)
    {
        if (player == null)
        {
            Debug.LogError(
                "FloorTeleportManager: Player is not assigned."
            );

            return;
        }

        if (target == null)
        {
            Debug.LogError(
                "FloorTeleportManager: Teleport point is not assigned."
            );

            return;
        }

        player.TeleportPlayer(
            target.position
        );

        if (matchFloorRotation)
        {
            player.transform.rotation =
                target.rotation;
        }

        if (floorPanel != null)
        {
            floorPanel.SetActive(false);
        }

        Debug.Log(
            "Player teleported to " +
            target.name
        );
    }
}