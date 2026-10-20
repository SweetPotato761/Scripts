using UnityEngine;

public class FireUIFlowController : MonoBehaviour
{
    [Header("UI PANELS")]

    [Tooltip("The first screen shown after pressing Play.")]
    public GameObject mainInterfacePanel;

    [Tooltip("Fire location selection screen.")]
    public GameObject fireSetupPanel;

    [Tooltip("Gameplay controls shown after fire location is confirmed.")]
    public GameObject gameplayUI;


    private void Start()
    {
        ShowMainInterface();
    }


    // =========================================================
    // PLAY
    // =========================================================

    public void ShowMainInterface()
    {
        if (mainInterfacePanel != null)
            mainInterfacePanel.SetActive(true);

        if (fireSetupPanel != null)
            fireSetupPanel.SetActive(false);

        if (gameplayUI != null)
            gameplayUI.SetActive(false);
    }


    // =========================================================
    // MAIN INTERFACE → FIRE SETUP
    // =========================================================

    public void OpenFireSetup()
    {
        if (mainInterfacePanel != null)
            mainInterfacePanel.SetActive(false);

        if (fireSetupPanel != null)
            fireSetupPanel.SetActive(true);

        if (gameplayUI != null)
            gameplayUI.SetActive(false);
    }


    // =========================================================
    // FIRE SETUP → GAMEPLAY
    // =========================================================

    public void OpenGameplay()
    {
        if (mainInterfacePanel != null)
            mainInterfacePanel.SetActive(false);

        if (fireSetupPanel != null)
            fireSetupPanel.SetActive(false);

        if (gameplayUI != null)
            gameplayUI.SetActive(true);
    }
}