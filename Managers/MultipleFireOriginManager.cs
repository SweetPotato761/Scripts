using System.Collections.Generic;
using UnityEngine;

public class MultipleFireOriginManager : MonoBehaviour
{
    [Header("MANAGERS")]
    public FirePlacementManager firePlacementManager;
    public FireScenarioManager fireScenarioManager;

    [Header("ADDITIONAL FIRE OBJECTS")]
    [Tooltip("Additional fire objects only. Do not include the main fire.")]
    public List<GameObject> fireOriginObjects = new List<GameObject>();

    [Header("FLOOR HEIGHTS - FALLBACK ONLY")]
    [Tooltip("Used only when a floor grid reference is not assigned.")]
    public float[] floorHeights = new float[5]
    {
        0f, 300f, 600f, 900f, 1200f
    };

    [Header("RANDOM POSITION")]
    [Tooltip("Random horizontal distance from a floor grid if no floor bounds are found.")]
    public float randomRadius = 100f;

    [Tooltip("Fallback building center in world X and Z.")]
    public Vector2 buildingCenter = Vector2.zero;

    [Tooltip("Vertical adjustment for fire placement.")]
    public float heightOffset = 0f;

    [Tooltip("Use floor collider/renderer bounds when available.")]
    public bool useFloorBoundsWhenAvailable = true;

    [Header("MULTIPLE FIRE SETTINGS")]
    [Min(1)]
    public int multipleFireCount = 3;

    [Tooltip("Place additional fires on different floors from each other and the main fire when possible.")]
    public bool spreadAcrossFloors = true;

    [Header("BEHAVIOR")]
    public bool generateAfterMainFireConfirmation = true;
    public bool hideWhenSingleFire = true;

    [Header("DEBUG")]
    public bool showDebugLogs = true;

    private readonly List<Vector3> generatedFirePositions =
        new List<Vector3>();

    private bool previousMainFireConfirmed;
    private bool hasGeneratedMultipleFires;

    private readonly string[] floorNames =
    {
        "Parking",
        "Floor 1",
        "Floor 2",
        "Floor 3",
        "Floor 4"
    };

    private void Awake()
    {
        FindManagers();
    }

    private void Start()
    {
        HideAllMultipleFireOrigins();
    }

    private void FindManagers()
    {
        if (firePlacementManager == null)
        {
            firePlacementManager =
                FindAnyObjectByType<FirePlacementManager>();
        }

        if (fireScenarioManager == null)
        {
            fireScenarioManager =
                FindAnyObjectByType<FireScenarioManager>();
        }
    }

    private void Update()
    {
        if (fireScenarioManager == null)
            FindManagers();

        if (fireScenarioManager == null)
            return;

        // Single Fire mode: keep all additional fires hidden.
        if (fireScenarioManager.IsSingleFire())
        {
            if (hideWhenSingleFire)
                HideAllMultipleFireOrigins();

            generatedFirePositions.Clear();
            hasGeneratedMultipleFires = false;
            previousMainFireConfirmed = false;
            return;
        }

        if (!fireScenarioManager.IsMultipleFire())
            return;

        if (firePlacementManager == null)
            return;

        bool confirmed =
            firePlacementManager.HasConfirmedFireLocation();

        if (generateAfterMainFireConfirmation &&
            confirmed &&
            !previousMainFireConfirmed)
        {
            RandomizeMultipleFireOrigins();
        }

        previousMainFireConfirmed = confirmed;
    }

    public void RandomizeMultipleFireOrigins()
    {
        FindManagers();

        if (fireScenarioManager != null &&
            !fireScenarioManager.IsMultipleFire())
        {
            return;
        }

        if (firePlacementManager == null)
        {
            Debug.LogError(
                "[MultipleFireOriginManager] FirePlacementManager is missing.");
            return;
        }

        Transform[] floorGrids =
            firePlacementManager.floorGridReferences;

        if (floorGrids == null || floorGrids.Length == 0)
        {
            Debug.LogError(
                "[MultipleFireOriginManager] No floor grid references found.");
            return;
        }

        HideAllMultipleFireOrigins();
        generatedFirePositions.Clear();
        hasGeneratedMultipleFires = false;

        int count = Mathf.Min(
            multipleFireCount,
            fireOriginObjects.Count);

        if (count <= 0)
        {
            Debug.LogWarning(
                "[MultipleFireOriginManager] Assign additional fire objects in the Inspector.");
            return;
        }

        // Build a list of valid floor indices.
        List<int> availableFloors = new List<int>();

        for (int i = 0; i < floorGrids.Length; i++)
        {
            if (floorGrids[i] != null)
                availableFloors.Add(i);
        }

        if (availableFloors.Count == 0)
        {
            Debug.LogError(
                "[MultipleFireOriginManager] All floor grid references are empty.");
            return;
        }

        // Avoid putting an additional fire on the main fire's floor
        // when there are other floors available.
        if (spreadAcrossFloors &&
            firePlacementManager.HasConfirmedFireLocation())
        {
            int mainFloor =
                firePlacementManager.GetSelectedFloor();

            if (availableFloors.Count > 1)
                availableFloors.Remove(mainFloor);
        }

        // Shuffle the floor order.
        for (int i = availableFloors.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);

            int temp = availableFloors[i];
            availableFloors[i] = availableFloors[j];
            availableFloors[j] = temp;
        }

        for (int i = 0; i < count; i++)
        {
            GameObject fireObject = fireOriginObjects[i];

            if (fireObject == null)
                continue;

            int floorIndex;

            if (spreadAcrossFloors)
            {
                // Use each available floor before repeating one.
                floorIndex =
                    availableFloors[i % availableFloors.Count];
            }
            else
            {
                floorIndex =
                    availableFloors[Random.Range(
                        0, availableFloors.Count)];
            }

            Transform floorGrid = floorGrids[floorIndex];

            Vector3 position = GetRandomPositionOnFloor(
                floorGrid,
                floorIndex);

            // Position before activating the object.
            fireObject.transform.position = position;
            fireObject.SetActive(true);

            // Start particle systems attached to this fire object.
            ParticleSystem[] particles =
                fireObject.GetComponentsInChildren<ParticleSystem>(true);

            foreach (ParticleSystem particle in particles)
            {
                particle.gameObject.SetActive(true);
                particle.Clear(true);
                particle.Play(true);
            }

            generatedFirePositions.Add(position);

            if (showDebugLogs)
            {
                string floorName =
                    floorIndex < floorNames.Length
                        ? floorNames[floorIndex]
                        : "Floor " + floorIndex;

                Debug.Log(
                    "[MultipleFireOriginManager] Additional fire " +
                    (i + 1) + " placed on " + floorName +
                    " at world position " + position);
            }
        }

        hasGeneratedMultipleFires =
            generatedFirePositions.Count > 0;

        if (showDebugLogs)
        {
            Debug.Log(
                "[MultipleFireOriginManager] Generated " +
                generatedFirePositions.Count +
                " additional fires.");
        }
    }

    private Vector3 GetRandomPositionOnFloor(
        Transform floorGrid,
        int floorIndex)
    {
        Vector3 center = floorGrid.position;

        float minX = center.x - Mathf.Abs(randomRadius);
        float maxX = center.x + Mathf.Abs(randomRadius);
        float minZ = center.z - Mathf.Abs(randomRadius);
        float maxZ = center.z + Mathf.Abs(randomRadius);

        bool foundBounds = false;
        Bounds bounds = new Bounds(center, Vector3.zero);

        if (useFloorBoundsWhenAvailable)
        {
            // Prefer the floor's colliders, if available.
            Collider[] colliders =
                floorGrid.GetComponentsInChildren<Collider>(true);

            foreach (Collider col in colliders)
            {
                if (col == null || !col.enabled)
                    continue;

                if (!foundBounds)
                {
                    bounds = col.bounds;
                    foundBounds = true;
                }
                else
                {
                    bounds.Encapsulate(col.bounds);
                }
            }

            // Otherwise, use the floor's renderers.
            if (!foundBounds)
            {
                Renderer[] renderers =
                    floorGrid.GetComponentsInChildren<Renderer>(true);

                foreach (Renderer rend in renderers)
                {
                    if (rend == null)
                        continue;

                    if (!foundBounds)
                    {
                        bounds = rend.bounds;
                        foundBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(rend.bounds);
                    }
                }
            }
        }

        if (foundBounds)
        {
            minX = bounds.min.x;
            maxX = bounds.max.x;
            minZ = bounds.min.z;
            maxZ = bounds.max.z;
        }
        else if (floorGrid == null)
        {
            minX = buildingCenter.x - Mathf.Abs(randomRadius);
            maxX = buildingCenter.x + Mathf.Abs(randomRadius);
            minZ = buildingCenter.y - Mathf.Abs(randomRadius);
            maxZ = buildingCenter.y + Mathf.Abs(randomRadius);
        }

        // Avoid errors if a floor has zero-size bounds.
        if (maxX < minX)
        {
            float temp = minX;
            minX = maxX;
            maxX = temp;
        }

        if (maxZ < minZ)
        {
            float temp = minZ;
            minZ = maxZ;
            maxZ = temp;
        }

        float x = Random.Range(minX, maxX);
        float z = Random.Range(minZ, maxZ);

        float y;

        if (floorGrid != null)
        {
            // Use the actual floor grid height.
            y = floorGrid.position.y;
        }
        else if (floorHeights != null &&
                 floorIndex < floorHeights.Length)
        {
            y = floorHeights[floorIndex];
        }
        else
        {
            y = center.y;
        }

        return new Vector3(x, y + heightOffset, z);
    }

    public void HideAllMultipleFireOrigins()
    {
        if (fireOriginObjects == null)
            return;

        foreach (GameObject fireObject in fireOriginObjects)
        {
            if (fireObject == null)
                continue;

            ParticleSystem[] particles =
                fireObject.GetComponentsInChildren<ParticleSystem>(true);

            foreach (ParticleSystem particle in particles)
            {
                particle.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            fireObject.SetActive(false);
        }
    }

    public void ClearMultipleFireOrigins()
    {
        HideAllMultipleFireOrigins();
        generatedFirePositions.Clear();
        hasGeneratedMultipleFires = false;
    }

    public void ResetMultipleFireScenario()
    {
        ClearMultipleFireOrigins();
        previousMainFireConfirmed = false;
    }

    public List<Vector3> GetMultipleFirePositions()
    {
        return new List<Vector3>(generatedFirePositions);
    }

    public int GetMultipleFireCount()
    {
        return generatedFirePositions.Count;
    }

    public bool HasGeneratedMultipleFires()
    {
        return hasGeneratedMultipleFires;
    }

    public Vector3 GetMultipleFirePosition(int index)
    {
        if (index < 0 || index >= generatedFirePositions.Count)
            return Vector3.zero;

        return generatedFirePositions[index];
    }

    public List<Vector3> GetAllFirePositions()
    {
        List<Vector3> positions = new List<Vector3>();

        if (firePlacementManager != null &&
            firePlacementManager.HasConfirmedFireLocation())
        {
            positions.Add(
                firePlacementManager.GetSelectedFirePosition());
        }

        positions.AddRange(generatedFirePositions);
        return positions;
    }
}