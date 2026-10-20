using UnityEngine;

public class HazardGridVisualizer : MonoBehaviour
{
    [Header("References")]
    public HazardGrid hazardGrid;

    [Header("Tile Setup")]
    [Tooltip("Optional tile prefab. Leave empty to create simple cubes automatically.")]
    public GameObject tilePrefab;

    [Tooltip("Height above the grid floor.")]
    public float tileHeightOffset = 50f;

    [Tooltip("Thickness of each visual tile.")]
    public float tileThickness = 20f;

    [Tooltip("Makes small gaps between cells.")]
    [Range(0.5f, 1f)]
    public float tileScaleMultiplier = 0.92f;

    [Header("Visualization")]
    public bool showCleanCells = false;

    [Range(0f, 1f)]
    public float minimumVisibleSmoke = 0.01f;

    [Range(0f, 1f)]
    public float maximumAlpha = 0.75f;

    public float updateInterval = 0.1f;

    [Header("Smoke Colors")]
    public Color cleanColor =
        new Color(0f, 0.5f, 1f, 0.05f);

    public Color lightSmokeColor =
        new Color(0f, 1f, 0f, 0.3f);

    public Color mediumSmokeColor =
        new Color(1f, 1f, 0f, 0.5f);

    public Color heavySmokeColor =
        new Color(1f, 0.25f, 0f, 0.65f);

    public Color extremeSmokeColor =
        new Color(0.05f, 0.05f, 0.05f, 0.75f);

    [Header("Optional Temperature Display")]
    public bool blendTemperatureIntoColor = false;
    public float dangerousTemperature = 60f;
    public float maximumTemperature = 600f;

    [Header("Debug")]
    public bool showDebugLogs = false;

    private GameObject[,] visualTiles;
    private Renderer[,] tileRenderers;
    private Material[,] tileMaterials;

    private float updateTimer;
    private bool visualsCreated;

    private void Awake()
    {
        if (hazardGrid == null)
        {
            hazardGrid =
                FindAnyObjectByType<HazardGrid>();
        }
    }

    private void Start()
    {
        CreateVisualTiles();
        UpdateVisualization();
    }

    private void Update()
    {
        if (!visualsCreated)
            return;

        updateTimer += Time.deltaTime;

        if (updateTimer < updateInterval)
            return;

        updateTimer = 0f;
        UpdateVisualization();
    }

    public void CreateVisualTiles()
    {
        if (hazardGrid == null)
        {
            Debug.LogError(
                "HazardGridVisualizer: HazardGrid is not assigned."
            );

            return;
        }

        if (hazardGrid.Grid == null)
        {
            hazardGrid.GenerateGrid();
        }

        ClearExistingTiles();

        int width = hazardGrid.gridWidth;
        int height = hazardGrid.gridHeight;

        visualTiles = new GameObject[width, height];
        tileRenderers = new Renderer[width, height];
        tileMaterials = new Material[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                HazardCell cell =
                    hazardGrid.GetCell(x, z);

                if (cell == null)
                    continue;

                GameObject tile =
                    CreateTileObject(x, z, cell);

                if (tile == null)
                    continue;

                visualTiles[x, z] = tile;

                Renderer tileRenderer =
                    tile.GetComponentInChildren<Renderer>();

                tileRenderers[x, z] =
                    tileRenderer;

                if (tileRenderer != null)
                {
                    Material material =
                        CreateRuntimeMaterial();

                    tileRenderer.material = material;
                    tileMaterials[x, z] = material;
                }
            }
        }

        visualsCreated = true;

        if (showDebugLogs)
        {
            Debug.Log(
                $"Hazard visualizer created {width * height} tiles."
            );
        }
    }

    private GameObject CreateTileObject(
        int x,
        int z,
        HazardCell cell)
    {
        GameObject tile;

        if (tilePrefab != null)
        {
            tile = Instantiate(
                tilePrefab,
                transform
            );
        }
        else
        {
            tile = GameObject.CreatePrimitive(
                PrimitiveType.Cube
            );

            tile.transform.SetParent(
                transform,
                true
            );

            Collider tileCollider =
                tile.GetComponent<Collider>();

            if (tileCollider != null)
            {
                Destroy(tileCollider);
            }
        }

        tile.name = $"HazardTile_{x}_{z}";

        tile.transform.position =
            cell.worldPosition +
            Vector3.up * tileHeightOffset;

        float horizontalSize =
            hazardGrid.cellSize *
            tileScaleMultiplier;

        tile.transform.localScale =
            new Vector3(
                horizontalSize,
                tileThickness,
                horizontalSize
            );

        return tile;
    }

    private Material CreateRuntimeMaterial()
    {
        Shader shader = Shader.Find(
            "Universal Render Pipeline/Unlit"
        );

        if (shader == null)
        {
            shader = Shader.Find(
                "Unlit/Color"
            );
        }

        Material material =
            new Material(shader);

        ConfigureTransparentMaterial(material);

        material.color = cleanColor;

        return material;
    }

    private void ConfigureTransparentMaterial(
        Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 0f);
        }

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat(
                "_SrcBlend",
                (float)UnityEngine.Rendering.BlendMode.SrcAlpha
            );
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat(
                "_DstBlend",
                (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha
            );
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }

        material.renderQueue =
            (int)UnityEngine.Rendering.RenderQueue.Transparent;

        material.EnableKeyword(
            "_SURFACE_TYPE_TRANSPARENT"
        );
    }

    public void UpdateVisualization()
    {
        if (!visualsCreated ||
            hazardGrid == null ||
            hazardGrid.Grid == null)
        {
            return;
        }

        for (int x = 0;
             x < hazardGrid.gridWidth;
             x++)
        {
            for (int z = 0;
                 z < hazardGrid.gridHeight;
                 z++)
            {
                HazardCell cell =
                    hazardGrid.GetCell(x, z);

                GameObject tile =
                    visualTiles[x, z];

                Material material =
                    tileMaterials[x, z];

                if (cell == null ||
                    tile == null ||
                    material == null)
                {
                    continue;
                }

                if (cell.isObstacle)
                {
                    tile.SetActive(false);
                    continue;
                }

                bool shouldShow =
                    showCleanCells ||
                    cell.smokeDensity >=
                    minimumVisibleSmoke;

                tile.SetActive(shouldShow);

                if (!shouldShow)
                    continue;

                Color cellColor =
                    GetSmokeColor(
                        cell.smokeDensity
                    );

                if (blendTemperatureIntoColor)
                {
                    cellColor =
                        BlendTemperatureColor(
                            cellColor,
                            cell.temperature
                        );
                }

                material.color = cellColor;

                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor(
                        "_BaseColor",
                        cellColor
                    );
                }
            }
        }
    }

    private Color GetSmokeColor(float density)
    {
        density = Mathf.Clamp01(density);

        Color result;

        if (density < 0.25f)
        {
            result = Color.Lerp(
                cleanColor,
                lightSmokeColor,
                density / 0.25f
            );
        }
        else if (density < 0.5f)
        {
            result = Color.Lerp(
                lightSmokeColor,
                mediumSmokeColor,
                (density - 0.25f) / 0.25f
            );
        }
        else if (density < 0.75f)
        {
            result = Color.Lerp(
                mediumSmokeColor,
                heavySmokeColor,
                (density - 0.5f) / 0.25f
            );
        }
        else
        {
            result = Color.Lerp(
                heavySmokeColor,
                extremeSmokeColor,
                (density - 0.75f) / 0.25f
            );
        }

        result.a = Mathf.Lerp(
            0f,
            maximumAlpha,
            density
        );

        return result;
    }

    private Color BlendTemperatureColor(
        Color smokeColor,
        float temperature)
    {
        float heatStrength =
            Mathf.InverseLerp(
                dangerousTemperature,
                maximumTemperature,
                temperature
            );

        Color heatColor =
            new Color(
                1f,
                0f,
                0f,
                smokeColor.a
            );

        return Color.Lerp(
            smokeColor,
            heatColor,
            heatStrength
        );
    }

    public void ResetVisualization()
    {
        if (!visualsCreated)
            return;

        for (int x = 0;
             x < visualTiles.GetLength(0);
             x++)
        {
            for (int z = 0;
                 z < visualTiles.GetLength(1);
                 z++)
            {
                GameObject tile =
                    visualTiles[x, z];

                Material material =
                    tileMaterials[x, z];

                if (tile != null)
                {
                    tile.SetActive(showCleanCells);
                }

                if (material != null)
                {
                    material.color = cleanColor;

                    if (material.HasProperty("_BaseColor"))
                    {
                        material.SetColor(
                            "_BaseColor",
                            cleanColor
                        );
                    }
                }
            }
        }
    }

    private void ClearExistingTiles()
    {
        for (int i = transform.childCount - 1;
             i >= 0;
             i--)
        {
            Destroy(
                transform.GetChild(i).gameObject
            );
        }

        visualTiles = null;
        tileRenderers = null;
        tileMaterials = null;
        visualsCreated = false;
    }

    private void OnDestroy()
    {
        if (tileMaterials == null)
            return;

        foreach (Material material in tileMaterials)
        {
            if (material != null)
            {
                Destroy(material);
            }
        }
    }
}   