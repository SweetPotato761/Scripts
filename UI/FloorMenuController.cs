using UnityEngine;

public class FloorMenuController : MonoBehaviour
{
    [Header("Floor Menu")]
    public GameObject floorPanel;

    private void Start()
    {
        // Floor selection menu starts hidden.
        if (floorPanel != null)
        {
            floorPanel.SetActive(false);
        }
        else
        {
            Debug.LogWarning(
                "FloorMenuController: FloorPanel is not assigned."
            );
        }
    }

    public void ToggleFloorMenu()
    {
        if (floorPanel == null)
            return;

        floorPanel.SetActive(!floorPanel.activeSelf);
    }

    public void OpenFloorMenu()
    {
        if (floorPanel == null)
            return;

        floorPanel.SetActive(true);
    }

    public void CloseFloorMenu()
    {
        if (floorPanel == null)
            return;

        floorPanel.SetActive(false);
    }
}