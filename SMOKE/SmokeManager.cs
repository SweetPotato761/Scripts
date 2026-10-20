using System.Collections.Generic;
using UnityEngine;

public class SmokeManager : MonoBehaviour
{
    [Header("Smoke Particles")]
    [Tooltip(
        "Original smoke coming directly from the fire."
    )]
    public ParticleSystem smokeParticle;

    [Tooltip(
        "Horizontal smoke layer below the ceiling."
    )]
    public ParticleSystem ceilingSmokeParticle;

    [Header("FDS Calibration")]
    public float referenceMaxHRR = 7500f;
    public float referenceSmokeValue = 8f;

    [Header("Source Smoke Activation")]
    [Range(0f, 1f)]
    public float activationThreshold = 0.02f;

    [Header("Ceiling Smoke Activation")]
    [Range(0f, 1f)]
    public float ceilingSmokeThreshold = 0.20f;

    [Header("Source Smoke Puff Timing")]
    public float maximumPuffInterval = 0.70f;
    public float minimumPuffInterval = 0.12f;

    [Header("Source Smoke Particles Per Puff")]
    public int minimumParticlesPerPuff = 5;
    public int maximumParticlesPerPuff = 30;

    [Header("Source Smoke Scale")]
    public float minimumAssetScale = 10f;
    public float maximumAssetScale = 20f;

    [Header("Source Smoke Startup")]
    public int startupPuffCount = 12;

    [Header("Ceiling Smoke Emission")]
    public float maximumCeilingEmission = 30f;

    [Tooltip(
        "How quickly ceiling smoke density increases/decreases."
    )]
    public float ceilingEmissionRampSpeed = 6f;

    [Header("Spread Fire Smoke")]
    [Tooltip(
        "Minimum emission for smoke attached to spread fires."
    )]
    public float minimumSpreadSmokeEmission = 20f;

    [Tooltip(
        "Maximum emission for smoke attached to spread fires."
    )]
    public float maximumSpreadSmokeEmission = 120f;

    [Tooltip(
        "How quickly spread-fire smoke emission changes."
    )]
    public float spreadSmokeRampSpeed = 40f;

    [Header("Playback")]
    [Min(0f)]
    public float playbackSpeed = 1f;

    [Header("Debug")]
    public bool showDebugLogs = false;

    private float currentStrength = 0f;

    private float puffTimer = 0f;

    private float currentCeilingEmission = 0f;

    private bool simulationRunning = false;

    private bool startupPuffEmitted = false;

    private readonly List<ParticleSystem>
        spreadSmokes =
        new List<ParticleSystem>();

    private readonly Dictionary<
        ParticleSystem,
        float>
        currentSpreadEmission =
        new Dictionary<
            ParticleSystem,
            float>();

    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        ConfigureSourceSmoke();

        ConfigureCeilingSmoke();

        StopSmoke();
    }

    private void Update()
    {
        if (!simulationRunning)
        {
            return;
        }

        UpdateSourceSmoke();

        UpdateCeilingSmoke();

        UpdateSpreadSmokes();
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

        ApplyParticlePlaybackSpeed();
    }

    private void ApplyParticlePlaybackSpeed()
    {
        if (smokeParticle != null)
        {
            ParticleSystem.MainModule main =
                smokeParticle.main;

            main.simulationSpeed =
                playbackSpeed;
        }

        if (ceilingSmokeParticle != null)
        {
            ParticleSystem.MainModule main =
                ceilingSmokeParticle.main;

            main.simulationSpeed =
                playbackSpeed;
        }

        foreach (
            ParticleSystem smoke
            in spreadSmokes)
        {
            if (smoke == null)
                continue;

            ParticleSystem.MainModule main =
                smoke.main;

            main.simulationSpeed =
                playbackSpeed;
        }
    }

    // =========================================================
    // SOURCE SMOKE SETUP
    // =========================================================

    private void ConfigureSourceSmoke()
    {
        if (smokeParticle == null)
        {
            Debug.LogWarning(
                "SmokeManager: " +
                "Source smoke is not assigned."
            );

            return;
        }

        ParticleSystem.MainModule main =
            smokeParticle.main;

        main.loop = false;

        main.playOnAwake = false;

        main.simulationSpeed =
            playbackSpeed;

        ParticleSystem.EmissionModule emission =
            smokeParticle.emission;

        emission.enabled = false;

        smokeParticle.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );
    }

    // =========================================================
    // CEILING SMOKE SETUP
    // =========================================================

    private void ConfigureCeilingSmoke()
    {
        if (ceilingSmokeParticle == null)
        {
            Debug.LogWarning(
                "SmokeManager: " +
                "Ceiling smoke is not assigned."
            );

            return;
        }

        ParticleSystem.MainModule main =
            ceilingSmokeParticle.main;

        main.loop = true;

        main.playOnAwake = false;

        main.simulationSpeed =
            playbackSpeed;

        ParticleSystem.EmissionModule emission =
            ceilingSmokeParticle.emission;

        emission.enabled = true;

        emission.rateOverTime = 0f;

        ceilingSmokeParticle.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );
    }

    // =========================================================
    // START
    // =========================================================

    public void Initialize()
    {
        simulationRunning = true;

        puffTimer = 0f;

        ApplyParticlePlaybackSpeed();

        if (smokeParticle != null)
        {
            smokeParticle.gameObject.SetActive(true);

            smokeParticle.Play(true);
        }

        if (ceilingSmokeParticle != null)
        {
            ceilingSmokeParticle.gameObject.SetActive(true);

            ParticleSystem.EmissionModule emission =
                ceilingSmokeParticle.emission;

            emission.rateOverTime =
                currentCeilingEmission;

            ceilingSmokeParticle.Play(true);
        }

        ResumeSpreadSmokes();

        if (!startupPuffEmitted &&
            smokeParticle != null)
        {
            smokeParticle.Emit(
                Mathf.Max(
                    1,
                    startupPuffCount
                )
            );

            startupPuffEmitted = true;
        }

        if (showDebugLogs)
        {
            Debug.Log(
                "SmokeManager started."
            );
        }
    }

    // =========================================================
    // FDS INPUT
    // =========================================================

    public void SetSmokeFromDensity(
        float smokeValue,
        float hrr)
    {
        float normalizedHRR =
            Mathf.Clamp01(
                hrr /
                Mathf.Max(
                    referenceMaxHRR,
                    1f
                )
            );

        float normalizedSmoke =
            Mathf.Clamp01(
                smokeValue /
                Mathf.Max(
                    referenceSmokeValue,
                    0.001f
                )
            );

        currentStrength =
            Mathf.Max(
                normalizedHRR,
                normalizedSmoke
            );

        currentStrength =
            Mathf.Pow(
                currentStrength,
                0.65f
            );

        UpdateSourceSmokeScale();

        if (showDebugLogs)
        {
            Debug.Log(
                $"Smoke FDS | " +
                $"HRR: {hrr:F0} | " +
                $"Smoke: {smokeValue:F3} | " +
                $"Strength: {currentStrength:F3} | " +
                $"Speed: {playbackSpeed:F1}x"
            );
        }
    }

    // =========================================================
    // SOURCE SMOKE
    // =========================================================

    private void UpdateSourceSmoke()
    {
        if (smokeParticle == null)
        {
            return;
        }

        if (currentStrength <
            activationThreshold)
        {
            return;
        }

        float puffInterval =
            Mathf.Lerp(
                maximumPuffInterval,
                minimumPuffInterval,
                currentStrength
            );

        puffTimer +=
            Time.deltaTime *
            Mathf.Max(
                0f,
                playbackSpeed
            );

        if (puffTimer <
            puffInterval)
        {
            return;
        }

        puffTimer = 0f;

        int particleCount =
            Mathf.RoundToInt(
                Mathf.Lerp(
                    minimumParticlesPerPuff,
                    maximumParticlesPerPuff,
                    currentStrength
                )
            );

        smokeParticle.Emit(
            Mathf.Max(
                1,
                particleCount
            )
        );

        if (showDebugLogs)
        {
            Debug.Log(
                $"Source smoke puff: " +
                $"{particleCount}"
            );
        }
    }

    private void UpdateSourceSmokeScale()
    {
        if (smokeParticle == null)
        {
            return;
        }

        float scale =
            Mathf.Lerp(
                minimumAssetScale,
                maximumAssetScale,
                currentStrength
            );

        smokeParticle.transform.localScale =
            Vector3.one * scale;
    }

    // =========================================================
    // CEILING SMOKE
    // =========================================================

    private void UpdateCeilingSmoke()
    {
        if (ceilingSmokeParticle == null)
        {
            return;
        }

        float targetEmission = 0f;

        if (currentStrength >=
            ceilingSmokeThreshold)
        {
            float ceilingStrength =
                Mathf.InverseLerp(
                    ceilingSmokeThreshold,
                    1f,
                    currentStrength
                );

            targetEmission =
                Mathf.Lerp(
                    0f,
                    maximumCeilingEmission,
                    ceilingStrength
                );
        }

        currentCeilingEmission =
            Mathf.MoveTowards(
                currentCeilingEmission,
                targetEmission,
                ceilingEmissionRampSpeed *
                Time.deltaTime *
                Mathf.Max(
                    0f,
                    playbackSpeed
                )
            );

        ParticleSystem.EmissionModule emission =
            ceilingSmokeParticle.emission;

        emission.rateOverTime =
            currentCeilingEmission;

        if (!ceilingSmokeParticle.isPlaying)
        {
            ceilingSmokeParticle.Play(true);
        }

        if (showDebugLogs)
        {
            Debug.Log(
                $"Ceiling smoke | " +
                $"Target: {targetEmission:F1} | " +
                $"Current: {currentCeilingEmission:F1}"
            );
        }
    }

    // =========================================================
    // REGISTER SPREAD SMOKE
    // =========================================================

    public void RegisterSpreadSmoke(
        ParticleSystem spreadSmoke)
    {
        if (spreadSmoke == null)
        {
            return;
        }

        if (spreadSmokes.Contains(
                spreadSmoke))
        {
            return;
        }

        spreadSmokes.Add(
            spreadSmoke
        );

        currentSpreadEmission[
            spreadSmoke] = 0f;

        ParticleSystem.MainModule main =
            spreadSmoke.main;

        main.loop = true;

        main.playOnAwake = false;

        main.simulationSpeed =
            playbackSpeed;

        ParticleSystem.EmissionModule emission =
            spreadSmoke.emission;

        emission.enabled = true;

        emission.rateOverTime = 0f;

        spreadSmoke.gameObject.SetActive(
            true
        );

        if (simulationRunning)
        {
            spreadSmoke.Play(true);
        }

        if (showDebugLogs)
        {
            Debug.Log(
                "SmokeManager: Registered spread smoke " +
                spreadSmoke.name
            );
        }
    }

    // =========================================================
    // SPREAD FIRE SMOKE
    // =========================================================

    private void UpdateSpreadSmokes()
    {
        RemoveDestroyedSpreadSmokes();

        if (spreadSmokes.Count == 0)
        {
            return;
        }

        float targetEmission = 0f;

        if (currentStrength >=
            activationThreshold)
        {
            targetEmission =
                Mathf.Lerp(
                    minimumSpreadSmokeEmission,
                    maximumSpreadSmokeEmission,
                    currentStrength
                );
        }

        foreach (
            ParticleSystem smoke
            in spreadSmokes)
        {
            if (smoke == null)
            {
                continue;
            }

            float currentEmission = 0f;

            if (currentSpreadEmission.TryGetValue(
                    smoke,
                    out float storedEmission))
            {
                currentEmission =
                    storedEmission;
            }

            currentEmission =
                Mathf.MoveTowards(
                    currentEmission,
                    targetEmission,
                    spreadSmokeRampSpeed *
                    Time.deltaTime *
                    Mathf.Max(
                        0f,
                        playbackSpeed
                    )
                );

            currentSpreadEmission[
                smoke] =
                currentEmission;

            ParticleSystem.EmissionModule emission =
                smoke.emission;

            emission.rateOverTime =
                currentEmission;

            ParticleSystem.MainModule main =
                smoke.main;

            main.simulationSpeed =
                playbackSpeed;

            if (!smoke.isPlaying)
            {
                smoke.Play(true);
            }
        }
    }

    // =========================================================
    // REMOVE DESTROYED
    // =========================================================

    private void RemoveDestroyedSpreadSmokes()
    {
        for (
            int i =
                spreadSmokes.Count - 1;
            i >= 0;
            i--)
        {
            ParticleSystem smoke =
                spreadSmokes[i];

            if (smoke != null)
            {
                continue;
            }

            spreadSmokes.RemoveAt(i);
        }

        List<ParticleSystem> removeKeys =
            new List<ParticleSystem>();

        foreach (
            KeyValuePair<
                ParticleSystem,
                float>
            pair
            in currentSpreadEmission)
        {
            if (pair.Key == null)
            {
                removeKeys.Add(
                    pair.Key
                );
            }
        }

        foreach (
            ParticleSystem key
            in removeKeys)
        {
            currentSpreadEmission.Remove(
                key
            );
        }
    }

    // =========================================================
    // PAUSE
    // =========================================================

    public void PauseSmoke()
    {
        simulationRunning = false;

        if (smokeParticle != null)
        {
            smokeParticle.Pause(true);
        }

        if (ceilingSmokeParticle != null)
        {
            ceilingSmokeParticle.Pause(true);
        }

        foreach (
            ParticleSystem smoke
            in spreadSmokes)
        {
            if (smoke != null)
            {
                smoke.Pause(true);
            }
        }

        if (showDebugLogs)
        {
            Debug.Log(
                "SmokeManager paused."
            );
        }
    }

    // =========================================================
    // RESUME SPREAD SMOKE
    // =========================================================

    private void ResumeSpreadSmokes()
    {
        foreach (
            ParticleSystem smoke
            in spreadSmokes)
        {
            if (smoke == null)
            {
                continue;
            }

            smoke.gameObject.SetActive(
                true
            );

            ParticleSystem.MainModule main =
                smoke.main;

            main.simulationSpeed =
                playbackSpeed;

            if (!smoke.isPlaying)
            {
                smoke.Play(true);
            }
        }
    }

    // =========================================================
    // RESET
    // =========================================================

    public void StopSmoke()
    {
        simulationRunning = false;

        currentStrength = 0f;

        puffTimer = 0f;

        currentCeilingEmission = 0f;

        startupPuffEmitted = false;

        if (smokeParticle != null)
        {
            smokeParticle.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );

            smokeParticle.Clear(true);

            smokeParticle.transform.localScale =
                Vector3.one *
                minimumAssetScale;
        }

        if (ceilingSmokeParticle != null)
        {
            ParticleSystem.EmissionModule emission =
                ceilingSmokeParticle.emission;

            emission.rateOverTime = 0f;

            ceilingSmokeParticle.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );

            ceilingSmokeParticle.Clear(true);
        }

        foreach (
            ParticleSystem smoke
            in spreadSmokes)
        {
            if (smoke == null)
            {
                continue;
            }

            ParticleSystem.EmissionModule emission =
                smoke.emission;

            emission.rateOverTime = 0f;

            smoke.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );

            smoke.Clear(true);
        }

        spreadSmokes.Clear();

        currentSpreadEmission.Clear();

        if (showDebugLogs)
        {
            Debug.Log(
                "SmokeManager reset."
            );
        }
    }
}