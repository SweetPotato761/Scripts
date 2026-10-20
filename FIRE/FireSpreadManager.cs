using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FireSpreadManager : MonoBehaviour
{
    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("References")]
    public FDSPlaybackController playbackController;

    public SmokeManager smokeManager;

    [Tooltip("Existing spread fire prefab containing the fire and SpreadSmoke child.")]
    public ParticleSystem spreadFirePrefab;

    [Tooltip("Separate ceiling smoke prefab.")]
    public ParticleSystem ceilingSmokePrefab;


    // =========================================================
    // SPREAD POINTS
    // =========================================================

    [Header("Spread Points")]
    public Transform[] fireSpreadPoints;


    // =========================================================
    // SPREAD SETTINGS
    // =========================================================

    [Header("Spread Settings")]

    [Tooltip("Minimum HRR before fire begins spreading.")]
    public float minimumSpreadHRR = 1000f;

    [Tooltip("HRR where spreading becomes fastest.")]
    public float maximumSpreadHRR = 7500f;

    [Tooltip("Fastest delay between new spread fires.")]
    public float minimumSpreadDelay = 2f;

    [Tooltip("Slowest delay between new spread fires.")]
    public float maximumSpreadDelay = 8f;

    [Tooltip("Maximum number of simultaneously active spread fires.")]
    public int maximumActiveFires = 8;


    // =========================================================
    // FIRE SCALE
    // =========================================================

    [Header("Fire Visual Scaling")]
    public float minimumFireScale = 1.5f;
    public float maximumFireScale = 3.5f;


    // =========================================================
    // NORMAL SPREAD SMOKE
    // =========================================================

    [Header("Normal Spread Smoke")]

    [Tooltip("Exact child name inside SpreadFire prefab.")]
    public string spreadSmokeObjectName = "SpreadSmoke";

    [Tooltip("Seconds before normal smoke reaches full emission.")]
    public float spreadSmokeFadeInTime = 8f;

    [Tooltip("Maximum Rate Over Time for normal spread smoke.")]
    public float spreadSmokeMaxEmission = 40f;


    // =========================================================
    // CEILING SMOKE
    // =========================================================

    [Header("Ceiling Smoke")]

    public bool enableCeilingSmoke = true;

    [Tooltip("Height above each fire point.")]
    public float ceilingSmokeHeight = 700f;

    [Tooltip("Seconds before ceiling smoke reaches full emission.")]
    public float ceilingSmokeFadeInTime = 12f;

    [Tooltip("Maximum Rate Over Time for ceiling smoke.")]
    public float ceilingSmokeMaxEmission = 50f;

    [Tooltip("Scale applied to the spawned ceiling smoke prefab.")]
    public float ceilingSmokeScale = 1f;

    [Tooltip("Optional random X/Z placement around the fire.")]
    public float ceilingSmokeRandomOffset = 0f;


    // =========================================================
    // SMOKE BUILDUP
    // =========================================================

    [Header("Smoke Buildup")]
    public bool smoothSmokeBuildup = true;


    // =========================================================
    // RESTART
    // =========================================================

    [Header("Restart Detection")]
    public float restartTimeThreshold = 0.2f;


    // =========================================================
    // DEBUG
    // =========================================================

    [Header("Debug")]
    public bool showDebugLogs = true;

    public bool showCeilingSmokeGizmos = true;

    public float ceilingSmokeGizmoSize = 150f;


    // =========================================================
    // RUNTIME
    // =========================================================

    private readonly List<ParticleSystem> activeFires =
        new List<ParticleSystem>();

    private readonly List<ParticleSystem> activeSpreadSmokes =
        new List<ParticleSystem>();

    private readonly List<ParticleSystem> activeCeilingSmokes =
        new List<ParticleSystem>();

    private readonly HashSet<int> activatedPoints =
        new HashSet<int>();

    private readonly List<Coroutine> smokeFadeCoroutines =
        new List<Coroutine>();


    private float spreadTimer = 0f;

    private float nextSpreadDelay = 5f;

    private bool spreadRunning = false;

    private bool previousPlaybackState = false;


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        if (playbackController == null)
        {
            playbackController =
                FindAnyObjectByType<FDSPlaybackController>();
        }

        if (smokeManager == null)
        {
            smokeManager =
                FindAnyObjectByType<SmokeManager>();
        }

        ResetSpread();
    }


    private void Update()
    {
        if (playbackController == null)
        {
            return;
        }

        bool playbackIsRunning =
            playbackController.IsPlaying();


        // =====================================================
        // PAUSE / END
        // =====================================================

        if (previousPlaybackState &&
            !playbackIsRunning)
        {
            PauseAllSpreadEffects();

            spreadRunning = false;

            if (showDebugLogs)
            {
                Debug.Log(
                    "FireSpreadManager: FDS playback stopped. " +
                    "Spread effects paused."
                );
            }
        }

        previousPlaybackState =
            playbackIsRunning;


        if (!spreadRunning ||
            !playbackIsRunning)
        {
            return;
        }


        float currentHRR =
            playbackController.GetCurrentHRR();


        // =====================================================
        // WAIT FOR HRR
        // =====================================================

        if (currentHRR <
            minimumSpreadHRR)
        {
            return;
        }


        // =====================================================
        // TIMER
        // =====================================================

        spreadTimer +=
            Time.deltaTime;


        if (spreadTimer >=
            nextSpreadDelay)
        {
            spreadTimer = 0f;

            TrySpreadFire(
                currentHRR
            );

            CalculateNextSpreadDelay(
                currentHRR
            );
        }


        UpdateActiveFireScale(
            currentHRR
        );
    }


    // =========================================================
    // START / RESUME
    // =========================================================

    public void StartSpread()
    {
        if (playbackController == null)
        {
            Debug.LogError(
                "FireSpreadManager: Playback Controller is not assigned."
            );

            return;
        }


        float currentTime =
            playbackController.GetCurrentTime();


        if (currentTime <=
                restartTimeThreshold &&
            activeFires.Count > 0)
        {
            ClearSpawnedEffects();
        }


        spreadRunning = true;

        spreadTimer = 0f;


        previousPlaybackState =
            playbackController.IsPlaying();


        ResumeAllSpreadEffects();


        CalculateNextSpreadDelay(
            playbackController.GetCurrentHRR()
        );


        if (showDebugLogs)
        {
            Debug.Log(
                "FireSpreadManager: Spread started/resumed."
            );
        }
    }


    // =========================================================
    // PAUSE
    // =========================================================

    public void PauseSpread()
    {
        spreadRunning = false;

        PauseAllSpreadEffects();


        if (showDebugLogs)
        {
            Debug.Log(
                "FireSpreadManager: Spread paused."
            );
        }
    }


    private void PauseAllSpreadEffects()
    {
        foreach (ParticleSystem fire in activeFires)
        {
            if (fire == null)
                continue;

            fire.Pause(true);
        }


        foreach (ParticleSystem smoke in activeSpreadSmokes)
        {
            if (smoke == null)
                continue;

            smoke.Pause(true);
        }


        foreach (ParticleSystem smoke in activeCeilingSmokes)
        {
            if (smoke == null)
                continue;

            smoke.Pause(true);
        }
    }


    // =========================================================
    // RESUME
    // =========================================================

    private void ResumeAllSpreadEffects()
    {
        foreach (ParticleSystem fire in activeFires)
        {
            if (fire == null)
                continue;

            fire.Play(true);
        }


        foreach (ParticleSystem smoke in activeSpreadSmokes)
        {
            if (smoke == null)
                continue;

            smoke.Play(true);
        }


        foreach (ParticleSystem smoke in activeCeilingSmokes)
        {
            if (smoke == null)
                continue;

            smoke.Play(true);
        }
    }


    // =========================================================
    // RESET
    // =========================================================

    public void ResetSpread()
    {
        spreadRunning = false;

        spreadTimer = 0f;

        previousPlaybackState = false;


        StopSmokeFadeCoroutines();

        ClearSpawnedEffects();


        if (showDebugLogs)
        {
            Debug.Log(
                "FireSpreadManager: Spread reset."
            );
        }
    }


    // =========================================================
    // STOP FADE COROUTINES
    // =========================================================

    private void StopSmokeFadeCoroutines()
    {
        foreach (Coroutine routine in smokeFadeCoroutines)
        {
            if (routine == null)
                continue;

            StopCoroutine(
                routine
            );
        }

        smokeFadeCoroutines.Clear();
    }


    // =========================================================
    // CLEAR EFFECTS
    // =========================================================

    private void ClearSpawnedEffects()
    {
        StopSmokeFadeCoroutines();


        foreach (ParticleSystem fire in activeFires)
        {
            if (fire == null)
                continue;

            fire.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );

            Destroy(
                fire.gameObject
            );
        }


        foreach (ParticleSystem ceilingSmoke in activeCeilingSmokes)
        {
            if (ceilingSmoke == null)
                continue;

            ceilingSmoke.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );

            Destroy(
                ceilingSmoke.gameObject
            );
        }


        activeFires.Clear();

        activeSpreadSmokes.Clear();

        activeCeilingSmokes.Clear();

        activatedPoints.Clear();
    }


    // =========================================================
    // FIRE SPREAD
    // =========================================================

    private void TrySpreadFire(
        float currentHRR)
    {
        if (spreadFirePrefab == null)
        {
            Debug.LogWarning(
                "FireSpreadManager: Spread Fire Prefab is not assigned."
            );

            return;
        }


        if (fireSpreadPoints == null ||
            fireSpreadPoints.Length == 0)
        {
            Debug.LogWarning(
                "FireSpreadManager: No Fire Spread Points assigned."
            );

            return;
        }


        if (activeFires.Count >=
            maximumActiveFires)
        {
            return;
        }


        List<int> availablePoints =
            new List<int>();


        for (int i = 0;
             i < fireSpreadPoints.Length;
             i++)
        {
            if (fireSpreadPoints[i] == null)
            {
                continue;
            }

            if (activatedPoints.Contains(i))
            {
                continue;
            }

            availablePoints.Add(i);
        }


        if (availablePoints.Count == 0)
        {
            return;
        }


        int chosenIndex =
            availablePoints[
                Random.Range(
                    0,
                    availablePoints.Count
                )
            ];


        Transform chosenPoint =
            fireSpreadPoints[
                chosenIndex
            ];


        // =====================================================
        // SPAWN FIRE
        // =====================================================

        ParticleSystem newFire =
            Instantiate(
                spreadFirePrefab,
                chosenPoint.position,
                chosenPoint.rotation,
                chosenPoint
            );


        newFire.transform.localPosition =
            Vector3.zero;


        newFire.transform.localRotation =
            Quaternion.identity;


        float normalizedHRR =
            Mathf.InverseLerp(
                minimumSpreadHRR,
                maximumSpreadHRR,
                currentHRR
            );


        float scale =
            Mathf.Lerp(
                minimumFireScale,
                maximumFireScale,
                normalizedHRR
            );


        newFire.transform.localScale =
            Vector3.one *
            scale;


        newFire.Play(true);


        // =====================================================
        // NORMAL SMOKE
        // =====================================================

        ParticleSystem spreadSmoke =
            FindSpreadSmoke(
                newFire.transform
            );


        if (spreadSmoke != null)
        {
            spreadSmoke.gameObject.SetActive(
                true
            );


            activeSpreadSmokes.Add(
                spreadSmoke
            );


            if (smokeManager != null)
            {
                smokeManager.RegisterSpreadSmoke(
                    spreadSmoke
                );
            }


            SetSmokeEmission(
                spreadSmoke,
                0f
            );


            spreadSmoke.Play(true);


            Coroutine normalSmokeRoutine =
                StartCoroutine(
                    FadeInSmoke(
                        spreadSmoke,
                        spreadSmokeFadeInTime,
                        spreadSmokeMaxEmission,
                        "SpreadSmoke"
                    )
                );


            smokeFadeCoroutines.Add(
                normalSmokeRoutine
            );
        }
        else
        {
            Debug.LogWarning(
                "FireSpreadManager: Could not find SpreadSmoke inside " +
                newFire.name
            );
        }


        // =====================================================
        // CEILING SMOKE
        // =====================================================

        if (enableCeilingSmoke)
        {
            SpawnCeilingSmoke(
                chosenPoint
            );
        }


        // =====================================================
        // REGISTER FIRE
        // =====================================================

        activeFires.Add(
            newFire
        );


        activatedPoints.Add(
            chosenIndex
        );


        if (showDebugLogs)
        {
            Debug.Log(
                "Fire spread to " +
                chosenPoint.name +
                " | HRR: " +
                currentHRR.ToString("F0") +
                " kW"
            );
        }
    }


    // =========================================================
    // SPAWN CEILING SMOKE
    // =========================================================

    private void SpawnCeilingSmoke(
        Transform firePoint)
    {
        if (ceilingSmokePrefab == null)
        {
            Debug.LogWarning(
                "FireSpreadManager: Ceiling Smoke Prefab is not assigned."
            );

            return;
        }


        Vector3 randomOffset =
            new Vector3(
                Random.Range(
                    -ceilingSmokeRandomOffset,
                    ceilingSmokeRandomOffset
                ),
                0f,
                Random.Range(
                    -ceilingSmokeRandomOffset,
                    ceilingSmokeRandomOffset
                )
            );


        Vector3 ceilingPosition =
            firePoint.position +
            Vector3.up *
            ceilingSmokeHeight +
            randomOffset;


        ParticleSystem newCeilingSmoke =
            Instantiate(
                ceilingSmokePrefab,
                ceilingPosition,
                Quaternion.identity
            );


        newCeilingSmoke.name =
            "CeilingSmoke_" +
            firePoint.name;


        newCeilingSmoke.transform.localScale =
            Vector3.one *
            ceilingSmokeScale;


        newCeilingSmoke.gameObject.SetActive(
            true
        );


        activeCeilingSmokes.Add(
            newCeilingSmoke
        );


        SetSmokeEmission(
            newCeilingSmoke,
            0f
        );


        newCeilingSmoke.Play(
            true
        );


        Coroutine ceilingRoutine =
            StartCoroutine(
                FadeInSmoke(
                    newCeilingSmoke,
                    ceilingSmokeFadeInTime,
                    ceilingSmokeMaxEmission,
                    "CeilingSmoke"
                )
            );


        smokeFadeCoroutines.Add(
            ceilingRoutine
        );


        if (showDebugLogs)
        {
            Debug.Log(
                "Ceiling smoke spawned at " +
                firePoint.name +
                " | Y: " +
                ceilingPosition.y.ToString("F0")
            );
        }
    }


    // =========================================================
    // SLOW SMOKE APPEARANCE
    // =========================================================

    private IEnumerator FadeInSmoke(
        ParticleSystem smoke,
        float fadeTime,
        float maximumEmission,
        string smokeType)
    {
        if (smoke == null)
        {
            yield break;
        }


        if (fadeTime <= 0f)
        {
            SetSmokeEmission(
                smoke,
                maximumEmission
            );

            yield break;
        }


        float elapsed = 0f;


        SetSmokeEmission(
            smoke,
            0f
        );


        while (elapsed <
               fadeTime)
        {
            if (smoke == null)
            {
                yield break;
            }


            if (!spreadRunning ||
                playbackController == null ||
                !playbackController.IsPlaying())
            {
                yield return null;

                continue;
            }


            elapsed +=
                Time.deltaTime;


            float progress =
                Mathf.Clamp01(
                    elapsed /
                    fadeTime
                );


            if (smoothSmokeBuildup)
            {
                progress =
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        progress
                    );
            }


            float currentEmission =
                Mathf.Lerp(
                    0f,
                    maximumEmission,
                    progress
                );


            SetSmokeEmission(
                smoke,
                currentEmission
            );


            yield return null;
        }


        if (smoke != null)
        {
            SetSmokeEmission(
                smoke,
                maximumEmission
            );


            if (showDebugLogs)
            {
                Debug.Log(
                    smokeType +
                    " reached full emission."
                );
            }
        }
    }


    // =========================================================
    // SET EMISSION
    // =========================================================

    private void SetSmokeEmission(
        ParticleSystem smoke,
        float rate)
    {
        if (smoke == null)
        {
            return;
        }


        ParticleSystem.EmissionModule emission =
            smoke.emission;


        emission.enabled =
            true;


        emission.rateOverTime =
            new ParticleSystem.MinMaxCurve(
                rate
            );
    }


    // =========================================================
    // FIND SPREAD SMOKE
    // =========================================================

    private ParticleSystem FindSpreadSmoke(
        Transform root)
    {
        if (root == null)
        {
            return null;
        }


        ParticleSystem[] systems =
            root.GetComponentsInChildren<
                ParticleSystem
            >(true);


        foreach (ParticleSystem system in systems)
        {
            if (system == null)
            {
                continue;
            }


            if (system.transform ==
                root)
            {
                continue;
            }


            if (system.gameObject.name ==
                spreadSmokeObjectName)
            {
                return system;
            }
        }


        return null;
    }


    // =========================================================
    // NEXT SPREAD DELAY
    // =========================================================

    private void CalculateNextSpreadDelay(
        float currentHRR)
    {
        float normalizedHRR =
            Mathf.InverseLerp(
                minimumSpreadHRR,
                maximumSpreadHRR,
                currentHRR
            );


        nextSpreadDelay =
            Mathf.Lerp(
                maximumSpreadDelay,
                minimumSpreadDelay,
                normalizedHRR
            );
    }


    // =========================================================
    // FIRE SCALE
    // =========================================================

    private void UpdateActiveFireScale(
        float currentHRR)
    {
        float normalizedHRR =
            Mathf.InverseLerp(
                minimumSpreadHRR,
                maximumSpreadHRR,
                currentHRR
            );


        float scale =
            Mathf.Lerp(
                minimumFireScale,
                maximumFireScale,
                normalizedHRR
            );


        foreach (ParticleSystem fire in activeFires)
        {
            if (fire == null)
            {
                continue;
            }


            fire.transform.localScale =
                Vector3.one *
                scale;
        }
    }


    // =========================================================
    // CEILING POSITION GIZMOS
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        if (!showCeilingSmokeGizmos)
        {
            return;
        }


        if (fireSpreadPoints == null)
        {
            return;
        }


        foreach (Transform point in fireSpreadPoints)
        {
            if (point == null)
            {
                continue;
            }


            Vector3 ceilingPosition =
                point.position +
                Vector3.up *
                ceilingSmokeHeight;


            Gizmos.color =
                Color.gray;


            Gizmos.DrawWireSphere(
                ceilingPosition,
                ceilingSmokeGizmoSize
            );


            Gizmos.DrawLine(
                point.position,
                ceilingPosition
            );
        }
    }
}