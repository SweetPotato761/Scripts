
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FirePlacementManager : MonoBehaviour
{
    [System.Serializable]
    public class FireSection
    {
        public string sectionName;

        [Tooltip("0 = Parking, 1 = Floor 1, 2 = Floor 2, 3 = Floor 3, 4 = Floor 4")]
        [Range(0, 4)]
        public int floorNumber;

        public Collider spawnArea;
        public LayerMask blockedLayers;
        public int randomAttempts = 100;

        [HideInInspector]
        public Vector3 lastRandomPosition;
    }

    [Header("MAIN FIRE")]
    public GameObject fireObject;
    public ParticleSystem fireParticle;

    [Header("MAIN FIRE SMOKE")]
    public ParticleSystem smokeParticle;
    public ParticleSystem ceilingSmokeParticle;

    [Tooltip("Offset above the main fire for ceiling smoke.")]
    public Vector3 ceilingSmokeOffset = new Vector3(0f, 5f, 0f);

    [Header("ADDITIONAL FIRE POINTS")]
    public List<GameObject> firePoints = new List<GameObject>();

    [Header("FLOOR REFERENCES")]
    [Tooltip("Element 0 = Parking, 1 = Floor 1, 2 = Floor 2, 3 = Floor 3, 4 = Floor 4.")]
    public Transform[] floorGridReferences = new Transform[5];

    [Header("FIRE SECTIONS")]
    public List<FireSection> fireSections = new List<FireSection>();

    [Header("FIRE HEIGHT")]
    public bool useSpawnAreaHeight = true;
    public float fireHeightOffset = 0f;

    [Header("PLACEMENT SETTINGS")]
    public bool hideFireUntilConfirmed = true;

    [Tooltip("Radius used to test blocked layers.")]
    public float minimumFireHeight = 0.5f;

    public float maximumFireHeight = 2f;

    [Header("UI")]
    public TMP_Dropdown floorDropdown;
    public TMP_Dropdown sectionDropdown;
    public Button randomizeButton;
    public Button confirmButton;
    public TMP_Text selectedPositionText;

    private readonly string[] floorNames =
    {
        "Parking",
        "Floor 1",
        "Floor 2",
        "Floor 3",
        "Floor 4"
    };

    private int selectedFloor;
    private FireSection selectedSection;
    private Vector3 selectedFirePosition;

    private bool hasRandomized;
    private bool hasConfirmed;

    public bool HasConfirmedFire => hasConfirmed;

    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Awake()
    {
        if (floorDropdown != null)
        {
            floorDropdown.onValueChanged.RemoveListener(OnFloorChanged);
            floorDropdown.ClearOptions();
            floorDropdown.AddOptions(new List<string>(floorNames));

            floorDropdown.SetValueWithoutNotify(
                Mathf.Clamp(floorDropdown.value, 0, 4)
            );

            floorDropdown.RefreshShownValue();
            floorDropdown.onValueChanged.AddListener(OnFloorChanged);
        }
        else
        {
            Debug.LogError(
                "FirePlacementManager: Assign the Floor Dropdown in the Inspector."
            );
        }

        if (sectionDropdown != null)
        {
            sectionDropdown.onValueChanged.RemoveListener(OnSectionChanged);
            sectionDropdown.onValueChanged.AddListener(OnSectionChanged);
        }

        if (randomizeButton != null)
        {
            randomizeButton.onClick.RemoveListener(RandomizeFirePosition);
            randomizeButton.onClick.AddListener(RandomizeFirePosition);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(ConfirmFirePosition);
            confirmButton.onClick.AddListener(ConfirmFirePosition);
        }
    }

    private void Start()
    {
        selectedFloor = floorDropdown != null
            ? Mathf.Clamp(floorDropdown.value, 0, 4)
            : 0;

        RefreshSectionDropdown();

        if (hideFireUntilConfirmed)
            HideMainFire();

        UpdatePositionText();
    }

    private void OnDestroy()
    {
        if (floorDropdown != null)
            floorDropdown.onValueChanged.RemoveListener(OnFloorChanged);

        if (sectionDropdown != null)
            sectionDropdown.onValueChanged.RemoveListener(OnSectionChanged);

        if (randomizeButton != null)
            randomizeButton.onClick.RemoveListener(RandomizeFirePosition);

        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(ConfirmFirePosition);
    }

    // =========================================================
    // FLOOR SELECTION
    // =========================================================

    public void OnFloorChanged(int floorIndex)
    {
        selectedFloor = Mathf.Clamp(floorIndex, 0, 4);

        hasRandomized = false;
        hasConfirmed = false;
        selectedFirePosition = Vector3.zero;

        RefreshSectionDropdown();

        if (hideFireUntilConfirmed)
            HideMainFire();

        UpdatePositionText();

        Debug.Log("Selected floor: " + floorNames[selectedFloor]);
    }

    // =========================================================
    // SECTION SELECTION
    // =========================================================

    public void OnSectionChanged(int sectionIndex)
    {
        hasRandomized = false;
        hasConfirmed = false;
        selectedFirePosition = Vector3.zero;

        UpdateSelectedSection();

        if (hideFireUntilConfirmed)
            HideMainFire();

        UpdatePositionText();
    }

    private void RefreshSectionDropdown()
    {
        if (sectionDropdown == null)
        {
            UpdateSelectedSection();
            return;
        }

        List<FireSection> availableSections =
            GetSectionsForFloor(selectedFloor);

        sectionDropdown.onValueChanged.RemoveListener(OnSectionChanged);
        sectionDropdown.ClearOptions();

        if (availableSections.Count == 0)
        {
            selectedSection = null;
            sectionDropdown.interactable = false;
            sectionDropdown.RefreshShownValue();
            sectionDropdown.onValueChanged.AddListener(OnSectionChanged);

            Debug.LogWarning(
                "No sections configured for " + floorNames[selectedFloor] +
                ". Add a Fire Section with the matching Floor Number and Spawn Area."
            );

            return;
        }

        List<string> options = new List<string>();

        foreach (FireSection section in availableSections)
            options.Add(section.sectionName);

        sectionDropdown.AddOptions(options);
        sectionDropdown.interactable = true;
        sectionDropdown.SetValueWithoutNotify(0);
        sectionDropdown.RefreshShownValue();

        selectedSection = availableSections[0];

        sectionDropdown.onValueChanged.AddListener(OnSectionChanged);
    }

    private void UpdateSelectedSection()
    {
        selectedSection = null;

        List<FireSection> availableSections =
            GetSectionsForFloor(selectedFloor);

        if (availableSections.Count == 0)
            return;

        if (sectionDropdown == null)
        {
            selectedSection = availableSections[0];
            return;
        }

        int index = Mathf.Clamp(
            sectionDropdown.value,
            0,
            availableSections.Count - 1
        );

        selectedSection = availableSections[index];
    }

    private List<FireSection> GetSectionsForFloor(int floor)
    {
        List<FireSection> result = new List<FireSection>();

        foreach (FireSection section in fireSections)
        {
            if (section != null && section.floorNumber == floor)
                result.Add(section);
        }

        return result;
    }

    // =========================================================
    // RANDOMIZE FIRE POSITION
    // =========================================================

    public void RandomizeFirePosition()
    {
        UpdateSelectedSection();

        if (selectedSection == null)
        {
            Debug.LogWarning(
                "No section selected for " + floorNames[selectedFloor] +
                ". Check Fire Sections and Section Dropdown."
            );

            UpdatePositionText();
            return;
        }

        if (selectedSection.spawnArea == null)
        {
            Debug.LogWarning(
                "Spawn Area is missing for section: " +
                selectedSection.sectionName
            );

            UpdatePositionText();
            return;
        }

        Collider area = selectedSection.spawnArea;
        Bounds bounds = area.bounds;

        float y = GetFireHeight(selectedSection);

        int attempts = Mathf.Max(1, selectedSection.randomAttempts);

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            float x = Random.Range(bounds.min.x, bounds.max.x);
            float z = Random.Range(bounds.min.z, bounds.max.z);

            Vector3 candidate = new Vector3(x, y, z);

            // Use X/Z bounds only. Floor height may be outside
            // the collider's vertical extent.
            if (x < bounds.min.x || x > bounds.max.x ||
                z < bounds.min.z || z > bounds.max.z)
            {
                continue;
            }

            // Reject positions only when an explicitly configured
            // blocked layer overlaps the candidate.
            if (selectedSection.blockedLayers.value != 0)
            {
                float radius = Mathf.Max(0.1f, minimumFireHeight);

                if (Physics.CheckSphere(
                    candidate,
                    radius,
                    selectedSection.blockedLayers,
                    QueryTriggerInteraction.Ignore))
                {
                    continue;
                }
            }

            selectedFirePosition = candidate;
            selectedSection.lastRandomPosition = candidate;

            hasRandomized = true;
            hasConfirmed = false;

            if (hideFireUntilConfirmed)
                HideMainFire();

            UpdatePositionText();

            Debug.Log(
                "Fire randomized successfully. Floor: " +
                floorNames[selectedFloor] +
                ", Section: " + selectedSection.sectionName +
                ", Position: " + selectedFirePosition
            );

            return;
        }

        Debug.LogWarning(
            "Could not find a valid fire position in section '" +
            selectedSection.sectionName +
            "'. Bounds: " + bounds +
            ", Height: " + y +
            ", Blocked Layers: " +
            selectedSection.blockedLayers.value +
            ". Try setting Blocked Layers to Nothing to test."
        );
    }

    private float GetFireHeight(FireSection section)
    {
        int floorIndex = Mathf.Clamp(section.floorNumber, 0, 4);

        if (floorGridReferences != null &&
            floorIndex < floorGridReferences.Length &&
            floorGridReferences[floorIndex] != null)
        {
            return floorGridReferences[floorIndex].position.y
                   + fireHeightOffset;
        }

        if (useSpawnAreaHeight && section.spawnArea != null)
        {
            return section.spawnArea.bounds.min.y + fireHeightOffset;
        }

        return transform.position.y + fireHeightOffset;
    }

    private bool IsBlocked(Vector3 position, FireSection section)
    {
        if (section == null || section.blockedLayers.value == 0)
            return false;

        return Physics.CheckSphere(
            position,
            Mathf.Max(0.1f, minimumFireHeight),
            section.blockedLayers,
            QueryTriggerInteraction.Ignore
        );
    }

    // =========================================================
    // CONFIRM FIRE POSITION
    // =========================================================

    public void ConfirmFirePosition()
    {
        if (!hasRandomized)
        {
            Debug.LogWarning("Randomize the fire position before confirming.");
            return;
        }

        MoveMainFire(selectedFirePosition);

        hasConfirmed = true;
        UpdatePositionText();

        Debug.Log(
            "Main fire confirmed at: " + selectedFirePosition +
            " on " + floorNames[selectedFloor]
        );
    }

    // =========================================================
    // MOVE FIRE AND SMOKE
    // =========================================================

    private void MoveMainFire(Vector3 position)
    {
        if (fireObject != null)
        {
            fireObject.transform.position = position;
            fireObject.SetActive(true);
        }

        PositionParticleSystem(fireParticle, position);
        PositionParticleSystem(smokeParticle, position);

        PositionParticleSystem(
            ceilingSmokeParticle,
            position + ceilingSmokeOffset
        );
    }

    private void PositionParticleSystem(
        ParticleSystem particle,
        Vector3 worldPosition)
    {
        if (particle == null)
            return;

        particle.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );

        particle.Clear(true);
        particle.transform.position = worldPosition;
        particle.gameObject.SetActive(true);
        particle.Play(true);
    }

    // =========================================================
    // HIDE MAIN FIRE
    // =========================================================

    private void HideMainFire()
    {
        StopAndHideParticle(fireParticle);
        StopAndHideParticle(smokeParticle);
        StopAndHideParticle(ceilingSmokeParticle);

        if (fireObject != null)
            fireObject.SetActive(false);
    }

    private void StopAndHideParticle(ParticleSystem particle)
    {
        if (particle == null)
            return;

        particle.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );

        particle.Clear(true);
        particle.gameObject.SetActive(false);
    }

    // =========================================================
    // RESET
    // =========================================================

    public void ResetFirePlacement()
    {
        hasRandomized = false;
        hasConfirmed = false;
        selectedFirePosition = Vector3.zero;

        HideMainFire();
        UpdatePositionText();
    }

    // =========================================================
    // PUBLIC ACCESSORS
    // =========================================================

    public bool HasConfirmedFireLocation()
    {
        return hasConfirmed;
    }

    public Vector3 GetSelectedFirePosition()
    {
        return selectedFirePosition;
    }

    public int GetSelectedFloor()
    {
        return selectedFloor;
    }

    public FireSection GetSelectedSection()
    {
        return selectedSection;
    }

    // =========================================================
    // POSITION TEXT
    // =========================================================

    private void UpdatePositionText()
    {
        if (selectedPositionText == null)
            return;

        if (hasConfirmed)
        {
            selectedPositionText.text =
                "Confirmed Position: " +
                selectedFirePosition.ToString("F1");
        }
        else if (hasRandomized)
        {
            selectedPositionText.text =
                "Randomized Position: " +
                selectedFirePosition.ToString("F1");
        }
        else
        {
            selectedPositionText.text =
                "Select floor and section, then randomize.";
        }
    }

    // =========================================================
    // GIZMOS
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        if (!hasRandomized)
            return;

        Gizmos.color = hasConfirmed ? Color.green : Color.yellow;

        Gizmos.DrawWireSphere(selectedFirePosition, 20f);

        Gizmos.DrawLine(
            selectedFirePosition,
            selectedFirePosition + Vector3.up * 100f
        );
    }
}
