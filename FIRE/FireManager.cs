using System.Collections.Generic;
using UnityEngine;

public class FireManager : MonoBehaviour
{
    [Header("Fire Prefab Root")]
    [Tooltip(
        "Assign the parent fireParticle object containing all flame effects."
    )]
    public Transform fireRoot;

    [Header("FDS HRR Calibration")]
    [Tooltip(
        "Approximate maximum HRR from Market_hrr.csv."
    )]
    public float referenceMaxHRR = 7500f;

    [Header("Whole Fire Scale")]
    public float minimumRootScale = 0.6f;
    public float maximumRootScale = 3.5f;

    [Header("Flame Growth")]
    public float minimumFlameSizeMultiplier = 0.5f;
    public float maximumFlameSizeMultiplier = 2.8f;

    public float minimumFlameSpeedMultiplier = 0.7f;
    public float maximumFlameSpeedMultiplier = 1.8f;

    public float maximumFlameEmissionMultiplier = 3f;

    [Header("Ash and Spark Growth")]
    public float ashStartThreshold = 0.15f;
    public float sparkStartThreshold = 0.25f;

    public float maximumAshEmissionMultiplier = 1.5f;
    public float maximumSparkEmissionMultiplier = 2f;

    [Header("Light")]
    public float minimumLightIntensityMultiplier = 0f;
    public float maximumLightIntensityMultiplier = 2.5f;

    [Header("Playback")]
    [Min(0f)]
    public float playbackSpeed = 1f;

    [Header("Debug")]
    public bool showDebugLogs = false;

    private readonly List<ParticleRecord>
        particleRecords =
        new List<ParticleRecord>();

    private readonly List<LightRecord>
        lightRecords =
        new List<LightRecord>();

    private Vector3 originalRootScale =
        Vector3.one;

    private bool initialized;

    private class ParticleRecord
    {
        public ParticleSystem particle;

        public float originalSizeMultiplier;
        public float originalSpeedMultiplier;
        public float originalLifetimeMultiplier;
        public float originalEmissionMultiplier;

        public bool isFlame;
        public bool isSpark;
        public bool isAsh;
        public bool isDistortion;
    }

    private class LightRecord
    {
        public Light light;

        public float originalIntensity;
        public float originalRange;
    }

    private void Awake()
    {
        CacheFireComponents();
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

        foreach (
            ParticleRecord record
            in particleRecords)
        {
            if (record.particle == null)
                continue;

            ParticleSystem.MainModule main =
                record.particle.main;

            main.simulationSpeed =
                playbackSpeed;
        }
    }

    // =========================================================
    // CACHE
    // =========================================================

    private void CacheFireComponents()
    {
        particleRecords.Clear();

        lightRecords.Clear();

        if (fireRoot == null)
        {
            Debug.LogError(
                "FireManager: Fire Root is not assigned. " +
                "Assign the parent fireParticle object."
            );

            initialized = false;

            return;
        }

        originalRootScale =
            fireRoot.localScale;

        ParticleSystem[] particles =
            fireRoot.GetComponentsInChildren<ParticleSystem>(
                true
            );

        foreach (
            ParticleSystem particle
            in particles)
        {
            ParticleSystem.MainModule main =
                particle.main;

            ParticleSystem.EmissionModule emission =
                particle.emission;

            string lowerName =
                particle.gameObject.name
                    .ToLowerInvariant();

            ParticleRecord record =
                new ParticleRecord
                {
                    particle = particle,

                    originalSizeMultiplier =
                        main.startSizeMultiplier,

                    originalSpeedMultiplier =
                        main.startSpeedMultiplier,

                    originalLifetimeMultiplier =
                        main.startLifetimeMultiplier,

                    originalEmissionMultiplier =
                        emission.rateOverTimeMultiplier,

                    isFlame =
                        lowerName.Contains("flame") ||
                        lowerName.Contains("fire"),

                    isSpark =
                        lowerName.Contains("spark"),

                    isAsh =
                        lowerName.Contains("ash"),

                    isDistortion =
                        lowerName.Contains("distortion") ||
                        lowerName.Contains("heat")
                };

            particleRecords.Add(
                record
            );
        }

        Light[] lights =
            fireRoot.GetComponentsInChildren<Light>(
                true
            );

        foreach (
            Light fireLight
            in lights)
        {
            LightRecord record =
                new LightRecord
                {
                    light = fireLight,

                    originalIntensity =
                        fireLight.intensity,

                    originalRange =
                        fireLight.range
                };

            lightRecords.Add(
                record
            );
        }

        initialized =
            particleRecords.Count > 0;

        if (!initialized)
        {
            Debug.LogError(
                "FireManager: No Particle Systems " +
                "were found under Fire Root."
            );
        }
        else
        {
            Debug.Log(
                $"FireManager found " +
                $"{particleRecords.Count} particle systems " +
                $"and {lightRecords.Count} lights."
            );
        }
    }

    // =========================================================
    // INITIALIZE
    // =========================================================

    public void Initialize()
    {
        if (!initialized)
        {
            CacheFireComponents();
        }

        if (!initialized)
            return;

        if (!fireRoot.gameObject.activeSelf)
        {
            fireRoot.gameObject.SetActive(true);
        }

        foreach (
            ParticleRecord record
            in particleRecords)
        {
            if (record.particle == null)
                continue;

            if (!record.particle.gameObject.activeSelf)
            {
                record.particle.gameObject.SetActive(true);
            }

            ParticleSystem.MainModule main =
                record.particle.main;

            main.simulationSpeed =
                playbackSpeed;

            if (!record.particle.isPlaying)
            {
                record.particle.Play(true);
            }
        }
    }

    // =========================================================
    // FDS HRR
    // =========================================================

    public void SetFireFromHRR(
        float hrr)
    {
        if (!initialized)
        {
            CacheFireComponents();
        }

        if (!initialized)
            return;

        float normalizedHRR =
            Mathf.Clamp01(
                hrr /
                Mathf.Max(
                    referenceMaxHRR,
                    1f
                )
            );

        float visualStrength =
            Mathf.Pow(
                normalizedHRR,
                0.65f
            );

        UpdateRootScale(
            visualStrength
        );

        UpdateParticleSystems(
            visualStrength
        );

        UpdateLights(
            visualStrength
        );

        if (showDebugLogs)
        {
            Debug.Log(
                $"Fire updated | " +
                $"HRR: {hrr:F0} kW | " +
                $"Strength: {visualStrength:F2} | " +
                $"Speed: {playbackSpeed:F1}x"
            );
        }
    }

    // =========================================================
    // ROOT SCALE
    // =========================================================

    private void UpdateRootScale(
        float strength)
    {
        float scaleMultiplier =
            Mathf.Lerp(
                minimumRootScale,
                maximumRootScale,
                strength
            );

        fireRoot.localScale =
            originalRootScale *
            scaleMultiplier;
    }

    // =========================================================
    // PARTICLES
    // =========================================================

    private void UpdateParticleSystems(
        float strength)
    {
        foreach (
            ParticleRecord record
            in particleRecords)
        {
            if (record.particle == null)
                continue;

            ParticleSystem.MainModule main =
                record.particle.main;

            ParticleSystem.EmissionModule emission =
                record.particle.emission;

            float sizeMultiplier;
            float speedMultiplier;
            float lifetimeMultiplier;
            float emissionMultiplier;

            if (record.isFlame)
            {
                sizeMultiplier =
                    Mathf.Lerp(
                        minimumFlameSizeMultiplier,
                        maximumFlameSizeMultiplier,
                        strength
                    );

                speedMultiplier =
                    Mathf.Lerp(
                        minimumFlameSpeedMultiplier,
                        maximumFlameSpeedMultiplier,
                        strength
                    );

                lifetimeMultiplier =
                    Mathf.Lerp(
                        0.8f,
                        1.4f,
                        strength
                    );

                emissionMultiplier =
                    Mathf.Lerp(
                        0f,
                        maximumFlameEmissionMultiplier,
                        strength
                    );
            }
            else if (record.isSpark)
            {
                float sparkStrength =
                    Mathf.InverseLerp(
                        sparkStartThreshold,
                        1f,
                        strength
                    );

                sizeMultiplier =
                    Mathf.Lerp(
                        0.5f,
                        1.3f,
                        sparkStrength
                    );

                speedMultiplier =
                    Mathf.Lerp(
                        0.7f,
                        1.8f,
                        sparkStrength
                    );

                lifetimeMultiplier =
                    Mathf.Lerp(
                        0.6f,
                        1.1f,
                        sparkStrength
                    );

                emissionMultiplier =
                    Mathf.Lerp(
                        0f,
                        maximumSparkEmissionMultiplier,
                        sparkStrength
                    );
            }
            else if (record.isAsh)
            {
                float ashStrength =
                    Mathf.InverseLerp(
                        ashStartThreshold,
                        1f,
                        strength
                    );

                sizeMultiplier =
                    Mathf.Lerp(
                        0.6f,
                        1.5f,
                        ashStrength
                    );

                speedMultiplier =
                    Mathf.Lerp(
                        0.7f,
                        1.3f,
                        ashStrength
                    );

                lifetimeMultiplier =
                    Mathf.Lerp(
                        0.8f,
                        1.8f,
                        ashStrength
                    );

                emissionMultiplier =
                    Mathf.Lerp(
                        0f,
                        maximumAshEmissionMultiplier,
                        ashStrength
                    );
            }
            else if (record.isDistortion)
            {
                sizeMultiplier =
                    Mathf.Lerp(
                        0.5f,
                        1.8f,
                        strength
                    );

                speedMultiplier = 1f;

                lifetimeMultiplier =
                    Mathf.Lerp(
                        0.8f,
                        1.3f,
                        strength
                    );

                emissionMultiplier =
                    Mathf.Lerp(
                        0f,
                        1.5f,
                        strength
                    );
            }
            else
            {
                sizeMultiplier =
                    Mathf.Lerp(
                        0.6f,
                        1.6f,
                        strength
                    );

                speedMultiplier =
                    Mathf.Lerp(
                        0.8f,
                        1.3f,
                        strength
                    );

                lifetimeMultiplier =
                    Mathf.Lerp(
                        0.8f,
                        1.3f,
                        strength
                    );

                emissionMultiplier =
                    Mathf.Lerp(
                        0f,
                        1.5f,
                        strength
                    );
            }

            main.simulationSpeed =
                playbackSpeed;

            main.startSizeMultiplier =
                record.originalSizeMultiplier *
                sizeMultiplier;

            main.startSpeedMultiplier =
                record.originalSpeedMultiplier *
                speedMultiplier;

            main.startLifetimeMultiplier =
                record.originalLifetimeMultiplier *
                lifetimeMultiplier;

            emission.enabled = true;

            emission.rateOverTimeMultiplier =
                record.originalEmissionMultiplier *
                emissionMultiplier;

            if (strength > 0f &&
                !record.particle.isPlaying)
            {
                record.particle.Play(true);
            }
        }
    }

    // =========================================================
    // LIGHTS
    // =========================================================

    private void UpdateLights(
        float strength)
    {
        foreach (
            LightRecord record
            in lightRecords)
        {
            if (record.light == null)
                continue;

            float lightMultiplier =
                Mathf.Lerp(
                    minimumLightIntensityMultiplier,
                    maximumLightIntensityMultiplier,
                    strength
                );

            record.light.intensity =
                record.originalIntensity *
                lightMultiplier;

            record.light.range =
                record.originalRange *
                Mathf.Lerp(
                    0.7f,
                    1.8f,
                    strength
                );

            record.light.enabled =
                strength > 0.01f;
        }
    }

    // =========================================================
    // PAUSE
    // =========================================================

    public void PauseFire()
    {
        if (!initialized)
            return;

        foreach (
            ParticleRecord record
            in particleRecords)
        {
            if (record.particle != null &&
                record.particle.isPlaying)
            {
                record.particle.Pause(true);
            }
        }
    }

    // =========================================================
    // RESET
    // =========================================================

    public void StopFire()
    {
        if (!initialized)
            return;

        foreach (
            ParticleRecord record
            in particleRecords)
        {
            if (record.particle == null)
                continue;

            record.particle.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }

        foreach (
            LightRecord record
            in lightRecords)
        {
            if (record.light == null)
                continue;

            record.light.intensity = 0f;

            record.light.enabled = false;
        }

        if (fireRoot != null)
        {
            fireRoot.localScale =
                originalRootScale *
                minimumRootScale;
        }
    }
}