using System.Collections;
using UnityEngine;

public class HazardSmokeRenderer : MonoBehaviour
{
    [Header("References")]
    public HazardGrid hazardGrid;
    public ParticleSystem smokePatchPrefab;

    [Header("Placement")]
    [Tooltip("Height of market smoke above the floor.")]
    public float heightOffset = 500f;

    [Range(0.1f, 1f)]
    public float patchScaleMultiplier = 0.35f;

    [Header("Activation")]
    [Range(0f, 1f)]
    public float minimumVisibleDensity = 0.02f;

    public float updateInterval = 0.15f;

    [Header("Emission")]
    public float minimumEmission = 0f;
    public float maximumEmission = 20f;

    [Header("Particle Size")]
    public float minimumParticleSize = 8f;
    public float maximumParticleSize = 40f;

    [Header("Lifetime")]
    public float minimumLifetime = 4f;
    public float maximumLifetime = 10f;

    [Header("Opacity")]
    [Range(0f, 1f)]
    public float minimumAlpha = 0.08f;

    [Range(0f, 1f)]
    public float maximumAlpha = 0.55f;

    [Header("Performance")]
    public bool hideObstacleCells = true;

    [Header("Playback")]
    [Min(0f)]
    public float playbackSpeed = 1f;

    [Header("Debug")]
    public bool showDebugLogs = false;

    private ParticleSystem[,] smokePatches;

    private ParticleSystem.MainModule[,] mainModules;

    private ParticleSystem.EmissionModule[,] emissionModules;

    private ParticleSystem.VelocityOverLifetimeModule[,] velocityModules;

    private ParticleSystem.NoiseModule[,] noiseModules;

    private bool patchesCreated;
    private bool rendererPaused;

    private float updateTimer;

    private void Awake()
    {
        if (hazardGrid == null)
        {
            hazardGrid =
                FindAnyObjectByType<HazardGrid>();
        }
    }

    private IEnumerator Start()
    {
        yield return null;

        if (hazardGrid == null)
        {
            Debug.LogError(
                "HazardSmokeRenderer: HazardGrid was not found."
            );

            yield break;
        }

        if (smokePatchPrefab == null)
        {
            Debug.LogError(
                "HazardSmokeRenderer: Smoke Patch Prefab is not assigned."
            );

            yield break;
        }

        if (hazardGrid.Grid == null)
        {
            hazardGrid.GenerateGrid();
        }

        CreateSmokePatches();

        UpdateSmokePatches();
    }

    private void Update()
    {
        if (!patchesCreated ||
            rendererPaused)
        {
            return;
        }

        float scaledDeltaTime =
            Time.deltaTime *
            Mathf.Max(
                0f,
                playbackSpeed
            );

        updateTimer +=
            scaledDeltaTime;

        if (updateTimer <
            updateInterval)
        {
            return;
        }

        updateTimer = 0f;

        UpdateSmokePatches();
    }

    // =========================================================
    // PLAYBACK SPEED
    // =========================================================

    public void SetPlaybackSpeed(
        float speed)
    {
        playbackSpeed =
            Mathf.Max(
                0f,
                speed
            );

        if (!patchesCreated)
            return;

        for (
            int x = 0;
            x < hazardGrid.gridWidth;
            x++)
        {
            for (
                int z = 0;
                z < hazardGrid.gridHeight;
                z++)
            {
                ParticleSystem patch =
                    smokePatches[x, z];

                if (patch == null)
                    continue;

                ParticleSystem.MainModule main =
                    mainModules[x, z];

                main.simulationSpeed =
                    playbackSpeed;
            }
        }
    }

    // =========================================================
    // CREATE PATCHES
    // =========================================================

    private void CreateSmokePatches()
    {
        int width =
            hazardGrid.gridWidth;

        int height =
            hazardGrid.gridHeight;

        smokePatches =
            new ParticleSystem[
                width,
                height
            ];

        mainModules =
            new ParticleSystem.MainModule[
                width,
                height
            ];

        emissionModules =
            new ParticleSystem.EmissionModule[
                width,
                height
            ];

        velocityModules =
            new ParticleSystem.VelocityOverLifetimeModule[
                width,
                height
            ];

        noiseModules =
            new ParticleSystem.NoiseModule[
                width,
                height
            ];

        int createdCount = 0;

        for (
            int x = 0;
            x < width;
            x++)
        {
            for (
                int z = 0;
                z < height;
                z++)
            {
                HazardCell cell =
                    hazardGrid.GetCell(
                        x,
                        z
                    );

                if (cell == null)
                    continue;

                if (hideObstacleCells &&
                    cell.isObstacle)
                {
                    continue;
                }

                ParticleSystem patch =
                    Instantiate(
                        smokePatchPrefab,
                        transform
                    );

                patch.name =
                    $"SmokePatch_{x}_{z}";

                patch.transform.position =
                    cell.worldPosition +
                    Vector3.up *
                    heightOffset;

                patch.transform.rotation =
                    Quaternion.identity;

                float horizontalScale =
                    patchScaleMultiplier;

                patch.transform.localScale =
                    new Vector3(
                        horizontalScale,
                        horizontalScale,
                        horizontalScale
                    );

                ParticleSystem.MainModule main =
                    patch.main;

                ParticleSystem.EmissionModule emission =
                    patch.emission;

                ParticleSystem.ShapeModule shape =
                    patch.shape;

                ParticleSystem.VelocityOverLifetimeModule velocity =
                    patch.velocityOverLifetime;

                ParticleSystem.ForceOverLifetimeModule force =
                    patch.forceOverLifetime;

                ParticleSystem.LimitVelocityOverLifetimeModule limit =
                    patch.limitVelocityOverLifetime;

                ParticleSystem.NoiseModule noise =
                    patch.noise;

                main.loop = true;

                main.playOnAwake = false;

                main.simulationSpace =
                    ParticleSystemSimulationSpace.World;

                main.simulationSpeed =
                    playbackSpeed;

                main.startSpeed =
                    new ParticleSystem.MinMaxCurve(
                        0f
                    );

                main.gravityModifier = 0f;

                main.maxParticles = 300;

                emission.enabled = true;

                emission.rateOverTime =
                    new ParticleSystem.MinMaxCurve(
                        0f
                    );

                shape.enabled = true;

                shape.shapeType =
                    ParticleSystemShapeType.Box;

                shape.scale =
                    new Vector3(
                        hazardGrid.cellSize * 0.75f,
                        100f,
                        hazardGrid.cellSize * 0.75f
                    );

                velocity.enabled = false;

                force.enabled = false;

                limit.enabled = false;

                noise.enabled = true;

                noise.separateAxes = true;

                noise.strengthX =
                    new ParticleSystem.MinMaxCurve(
                        0.2f
                    );

                noise.strengthY =
                    new ParticleSystem.MinMaxCurve(
                        0f
                    );

                noise.strengthZ =
                    new ParticleSystem.MinMaxCurve(
                        0.2f
                    );

                noise.frequency = 0.15f;

                noise.scrollSpeed = 0.1f;

                noise.damping = true;

                patch.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear
                );

                patch.gameObject.SetActive(false);

                smokePatches[x, z] =
                    patch;

                mainModules[x, z] =
                    main;

                emissionModules[x, z] =
                    emission;

                velocityModules[x, z] =
                    velocity;

                noiseModules[x, z] =
                    noise;

                createdCount++;
            }
        }

        patchesCreated = true;

        Debug.Log(
            $"HazardSmokeRenderer created " +
            $"{createdCount} smoke patches."
        );
    }

    // =========================================================
    // UPDATE PATCHES
    // =========================================================

    private void UpdateSmokePatches()
    {
        if (rendererPaused)
            return;

        for (
            int x = 0;
            x < hazardGrid.gridWidth;
            x++)
        {
            for (
                int z = 0;
                z < hazardGrid.gridHeight;
                z++)
            {
                HazardCell cell =
                    hazardGrid.GetCell(
                        x,
                        z
                    );

                ParticleSystem patch =
                    smokePatches[x, z];

                if (cell == null ||
                    patch == null)
                {
                    continue;
                }

                float density =
                    Mathf.Clamp01(
                        cell.smokeDensity
                    );

                if (density <
                    minimumVisibleDensity)
                {
                    DisablePatch(
                        patch,
                        x,
                        z
                    );

                    continue;
                }

                EnablePatch(
                    patch,
                    density,
                    x,
                    z
                );
            }
        }
    }

    // =========================================================
    // ENABLE
    // =========================================================

    private void EnablePatch(
        ParticleSystem patch,
        float density,
        int x,
        int z)
    {
        if (!patch.gameObject.activeSelf)
        {
            patch.gameObject.SetActive(true);
        }

        ParticleSystem.MainModule main =
            mainModules[x, z];

        ParticleSystem.EmissionModule emission =
            emissionModules[x, z];

        main.simulationSpeed =
            playbackSpeed;

        float emissionRate =
            Mathf.Lerp(
                minimumEmission,
                maximumEmission,
                density
            );

        float particleSize =
            Mathf.Lerp(
                minimumParticleSize,
                maximumParticleSize,
                density
            );

        float lifetime =
            Mathf.Lerp(
                minimumLifetime,
                maximumLifetime,
                density
            );

        float alpha =
            Mathf.Lerp(
                minimumAlpha,
                maximumAlpha,
                density
            );

        main.startSpeed =
            new ParticleSystem.MinMaxCurve(
                0f
            );

        main.startSize =
            new ParticleSystem.MinMaxCurve(
                particleSize * 0.7f,
                particleSize
            );

        main.startLifetime =
            new ParticleSystem.MinMaxCurve(
                lifetime * 0.8f,
                lifetime
            );

        main.startColor =
            new ParticleSystem.MinMaxGradient(
                new Color(
                    0.18f,
                    0.18f,
                    0.18f,
                    alpha
                )
            );

        emission.enabled = true;

        emission.rateOverTime =
            new ParticleSystem.MinMaxCurve(
                emissionRate
            );

        if (!patch.isPlaying)
        {
            patch.Play(true);
        }
    }

    // =========================================================
    // DISABLE
    // =========================================================

    private void DisablePatch(
        ParticleSystem patch,
        int x,
        int z)
    {
        emissionModules[x, z]
            .rateOverTime =
            new ParticleSystem.MinMaxCurve(
                0f
            );

        patch.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );

        patch.Clear(true);

        patch.gameObject.SetActive(false);
    }

    // =========================================================
    // RESUME
    // =========================================================

    public void ResumeRenderer()
    {
        rendererPaused = false;

        if (showDebugLogs)
        {
            Debug.Log(
                "Hazard smoke renderer resumed."
            );
        }
    }

    // =========================================================
    // PAUSE
    // =========================================================

    public void PauseRenderer()
    {
        rendererPaused = true;

        if (!patchesCreated)
            return;

        for (
            int x = 0;
            x < hazardGrid.gridWidth;
            x++)
        {
            for (
                int z = 0;
                z < hazardGrid.gridHeight;
                z++)
            {
                ParticleSystem patch =
                    smokePatches[x, z];

                if (patch != null &&
                    patch.gameObject.activeSelf)
                {
                    patch.Pause(true);
                }
            }
        }

        if (showDebugLogs)
        {
            Debug.Log(
                "Hazard smoke renderer paused."
            );
        }
    }

    // =========================================================
    // RESET
    // =========================================================

    public void ResetRenderer()
    {
        rendererPaused = false;

        updateTimer = 0f;

        if (!patchesCreated)
            return;

        for (
            int x = 0;
            x < hazardGrid.gridWidth;
            x++)
        {
            for (
                int z = 0;
                z < hazardGrid.gridHeight;
                z++)
            {
                ParticleSystem patch =
                    smokePatches[x, z];

                if (patch == null)
                    continue;

                emissionModules[x, z]
                    .rateOverTime =
                    new ParticleSystem.MinMaxCurve(
                        0f
                    );

                patch.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear
                );

                patch.Clear(true);

                patch.gameObject.SetActive(false);
            }
        }

        if (showDebugLogs)
        {
            Debug.Log(
                "Hazard smoke renderer reset."
            );
        }
    }
}