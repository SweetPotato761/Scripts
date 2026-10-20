using UnityEngine;
using TMPro;

public class FireScenarioManager : MonoBehaviour
{
    // =========================================================
    // FIRE SCENARIO
    // =========================================================

    public enum FireScenario
    {
        SingleFire,
        MultipleFireOrigins
    }

    public static FireScenarioManager Instance;

    // =========================================================
    // CURRENT SCENARIO
    // =========================================================

    [Header("CURRENT SCENARIO")]
    [SerializeField]
    private FireScenario currentScenario = FireScenario.SingleFire;

    // =========================================================
    // FIRE MODE DROPDOWN
    // =========================================================

    [Header("FIRE MODE DROPDOWN")]
    [Tooltip("Dropdown used to select Single Fire or Multiple Fire.")]
    public TMP_Dropdown fireModeDropdown;

    // =========================================================
    // OPTIONAL DISPLAY
    // =========================================================

    [Header("OPTIONAL DISPLAY")]
    [Tooltip("Optional text showing the currently selected fire mode.")]
    public TMP_Text scenarioText;

    // =========================================================
    // SETTINGS
    // =========================================================

    [Header("SETTINGS")]
    [Tooltip("If enabled, the selected fire mode survives scene changes.")]
    public bool persistBetweenScenes = true;

    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Awake()
    {
        // -----------------------------------------------------
        // SINGLETON
        // -----------------------------------------------------

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // -----------------------------------------------------
        // PERSIST BETWEEN SCENES
        // -----------------------------------------------------

        if (persistBetweenScenes)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    private void Start()
    {
        SetupDropdown();
        UpdateUI();
    }

    // =========================================================
    // DROPDOWN SETUP
    // =========================================================

    private void SetupDropdown()
    {
        if (fireModeDropdown == null)
        {
            Debug.LogWarning(
                "[FireScenarioManager] " +
                "Fire Mode Dropdown is not assigned."
            );

            return;
        }

        // -----------------------------------------------------
        // REMOVE OLD LISTENER
        // -----------------------------------------------------

        fireModeDropdown.onValueChanged.RemoveListener(
            OnFireModeDropdownChanged
        );

        // -----------------------------------------------------
        // CREATE OPTIONS
        // -----------------------------------------------------

        SetupDropdownOptions();

        // -----------------------------------------------------
        // CONNECT DROPDOWN EVENT
        // -----------------------------------------------------

        fireModeDropdown.onValueChanged.AddListener(
            OnFireModeDropdownChanged
        );

        // -----------------------------------------------------
        // SET CURRENT VALUE
        // -----------------------------------------------------

        fireModeDropdown.SetValueWithoutNotify(
            currentScenario == FireScenario.SingleFire
                ? 0
                : 1
        );

        fireModeDropdown.RefreshShownValue();
    }

    // =========================================================
    // DROPDOWN OPTIONS
    // =========================================================

    private void SetupDropdownOptions()
    {
        if (fireModeDropdown == null)
            return;

        fireModeDropdown.ClearOptions();

        fireModeDropdown.AddOptions(
            new System.Collections.Generic.List<string>
            {
                "Single Fire",
                "Multiple Fire"
            }
        );
    }

    // =========================================================
    // DROPDOWN EVENT
    // =========================================================

    private void OnFireModeDropdownChanged(int value)
    {
        if (value == 0)
        {
            SetScenario(FireScenario.SingleFire);
        }
        else
        {
            SetScenario(FireScenario.MultipleFireOrigins);
        }
    }

    // =========================================================
    // SET SCENARIO
    // =========================================================

    public void SetScenario(FireScenario scenario)
    {
        currentScenario = scenario;

        UpdateUI();

        Debug.Log(
            "[FireScenarioManager] Fire Mode: "
            + GetScenarioName()
        );
    }

    // =========================================================
    // SINGLE FIRE
    // =========================================================

    public void SelectSingleFire()
    {
        SetScenario(
            FireScenario.SingleFire
        );
    }

    // =========================================================
    // MULTIPLE FIRE
    // =========================================================

    public void SelectMultipleFire()
    {
        SetScenario(
            FireScenario.MultipleFireOrigins
        );
    }

    // =========================================================
    // UI UPDATE
    // =========================================================

    private void UpdateUI()
    {
        // -----------------------------------------------------
        // UPDATE DROPDOWN
        // -----------------------------------------------------

        if (fireModeDropdown != null)
        {
            fireModeDropdown.SetValueWithoutNotify(
                currentScenario == FireScenario.SingleFire
                    ? 0
                    : 1
            );

            fireModeDropdown.RefreshShownValue();
        }

        // -----------------------------------------------------
        // OPTIONAL TEXT
        // -----------------------------------------------------

        if (scenarioText != null)
        {
            scenarioText.text =
                "Fire Mode: " +
                GetScenarioName();
        }
    }

    // =========================================================
    // GET CURRENT SCENARIO
    // =========================================================

    public FireScenario GetScenario()
    {
        return currentScenario;
    }

    // =========================================================
    // GET SCENARIO NAME
    // =========================================================

    public string GetScenarioName()
    {
        if (currentScenario ==
            FireScenario.MultipleFireOrigins)
        {
            return "Multiple Fire";
        }

        return "Single Fire";
    }

    // =========================================================
    // CHECK SINGLE FIRE
    // =========================================================

    public bool IsSingleFire()
    {
        return currentScenario ==
               FireScenario.SingleFire;
    }

    // =========================================================
    // CHECK MULTIPLE FIRE
    // =========================================================

    public bool IsMultipleFire()
    {
        return currentScenario ==
               FireScenario.MultipleFireOrigins;
    }

    // =========================================================
    // RESET TO SINGLE FIRE
    // =========================================================

    public void ResetToSingleFire()
    {
        SetScenario(
            FireScenario.SingleFire
        );
    }
}