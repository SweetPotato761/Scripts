using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FDSPlaybackController : MonoBehaviour
{
    [Header("FDS Data")]
    public FDSCSVReader csvReader;

    [Header("Managers")]
    public FireManager fireManager;
    public SmokeManager smokeManager;
    public DetectorManager detectorManager;
    public HazardSimulation hazardSimulation;
    public HazardSmokeRenderer hazardSmokeRenderer;

    [Header("Timer UI")]
    public TMP_Text timerTMP;
    public Text timerLegacy;

    [Header("HRR UI")]
    public TMP_Text hrrTMP;
    public Text hrrLegacy;

    [Header("Smoke UI")]
    public TMP_Text smokeTMP;
    public Text smokeLegacy;

    [Header("Temperature UI - Optional")]
    public TMP_Text temperatureTMP;
    public Text temperatureLegacy;

    [Header("Loading UI - Optional")]
    public TMP_Text loadingTMP;
    public Text loadingLegacy;

    [Header("Playback Settings")]
    public bool playOnStart = false;

    [Min(0f)]
    public float playbackSpeed = 1f;

    [Header("Debug")]
    public bool showFrameLogs = true;

    private FDSDataModel dataset;
    private FDSFrame currentFrame;

    private int currentFrameIndex;
    private float playbackTime;

    private bool isPlaying;
    private bool datasetReady;
    private bool initializationFailed;

    private void Awake()
    {
        FindMissingReferences();
    }

    private IEnumerator Start()
    {
        SetLoadingText("Loading FDS data...");

        yield return InitializeDataset();

        if (!datasetReady)
        {
            initializationFailed = true;

            SetLoadingText(
                "FDS data failed to load."
            );

            yield break;
        }

        SetLoadingText(string.Empty);

        // Apply current speed to all simulation systems.
        ApplyPlaybackSpeedToSimulationManagers();

        if (playOnStart)
        {
            Play();
        }
    }

    // =========================================================
    // FIND REFERENCES
    // =========================================================

    private void FindMissingReferences()
    {
        if (csvReader == null)
        {
            csvReader =
                FindAnyObjectByType<FDSCSVReader>();
        }

        if (fireManager == null)
        {
            fireManager =
                FindAnyObjectByType<FireManager>();
        }

        if (smokeManager == null)
        {
            smokeManager =
                FindAnyObjectByType<SmokeManager>();
        }

        if (detectorManager == null)
        {
            detectorManager =
                FindAnyObjectByType<DetectorManager>();
        }

        if (hazardSimulation == null)
        {
            hazardSimulation =
                FindAnyObjectByType<HazardSimulation>();
        }

        if (hazardSmokeRenderer == null)
        {
            hazardSmokeRenderer =
                FindAnyObjectByType<HazardSmokeRenderer>();
        }
    }

    // =========================================================
    // PUBLIC PLAYBACK SPEED METHOD
    // =========================================================

    /*
     * THIS METHOD FIXES THE CS1061 ERROR.
     *
     * FastForwardController calls:
     *
     * fdsPlaybackController.SetPlaybackSpeed(speed);
     *
     * This method then sends the same speed to:
     *
     * FireManager
     * SmokeManager
     * HazardSimulation
     * HazardSmokeRenderer
     *
     * Time.timeScale is NOT changed.
     */

    public void SetPlaybackSpeed(float speed)
    {
        playbackSpeed =
            Mathf.Max(
                0f,
                speed
            );

        ApplyPlaybackSpeedToSimulationManagers();

        Debug.Log(
            "FDS Simulation Speed: " +
            playbackSpeed.ToString("0.##") +
            "x"
        );
    }

    // =========================================================
    // APPLY SPEED TO ALL FIRE SYSTEMS
    // =========================================================

    private void ApplyPlaybackSpeedToSimulationManagers()
    {
        float speed =
            Mathf.Max(
                0f,
                playbackSpeed
            );

        if (fireManager != null)
        {
            fireManager.SetPlaybackSpeed(
                speed
            );
        }

        if (smokeManager != null)
        {
            smokeManager.SetPlaybackSpeed(
                speed
            );
        }

        if (hazardSimulation != null)
        {
            hazardSimulation.SetPlaybackSpeed(
                speed
            );
        }

        if (hazardSmokeRenderer != null)
        {
            hazardSmokeRenderer.SetPlaybackSpeed(
                speed
            );
        }
    }

    // =========================================================
    // INITIALIZE DATASET
    // =========================================================

    private IEnumerator InitializeDataset()
    {
        if (csvReader == null)
        {
            Debug.LogError(
                "FDSPlaybackController: " +
                "FDSCSVReader was not found."
            );

            yield break;
        }

        while (csvReader.isLoading)
        {
            yield return null;
        }

        if (!csvReader.isLoaded)
        {
            yield return csvReader.LoadCSVCoroutine();
        }

        while (csvReader.isLoading)
        {
            yield return null;
        }

        if (!csvReader.isLoaded)
        {
            Debug.LogError(
                "FDSPlaybackController: CSV loading failed. " +
                csvReader.lastError
            );

            yield break;
        }

        dataset =
            csvReader.dataModel;

        if (dataset == null ||
            dataset.FrameCount == 0)
        {
            Debug.LogError(
                "FDSPlaybackController: " +
                "Dataset contains no frames."
            );

            yield break;
        }

        datasetReady = true;
        initializationFailed = false;

        currentFrameIndex = 0;

        currentFrame =
            dataset.GetFrame(0);

        playbackTime =
            currentFrame != null
                ? currentFrame.time
                : 0f;

        ApplyCurrentFrame();

        StopVisualEffectsAtStartup();

        ApplyPlaybackSpeedToSimulationManagers();

        Debug.Log(
            "FDS playback ready. Frames: " +
            dataset.FrameCount
        );
    }

    // =========================================================
    // STARTUP RESET
    // =========================================================

    private void StopVisualEffectsAtStartup()
    {
        if (fireManager != null)
        {
            fireManager.StopFire();
        }

        if (smokeManager != null)
        {
            smokeManager.StopSmoke();
        }

        if (hazardSimulation != null)
        {
            hazardSimulation.ResetSimulation();
        }

        if (hazardSmokeRenderer != null)
        {
            hazardSmokeRenderer.ResetRenderer();
        }
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (!isPlaying ||
            !datasetReady ||
            dataset == null)
        {
            return;
        }

        /*
         * IMPORTANT:
         *
         * We intentionally DO NOT modify
         * Time.timeScale.
         *
         * This means:
         *
         * Player = normal speed
         * Camera = normal speed
         * Joystick = normal speed
         *
         * Fire simulation = playbackSpeed
         */

        playbackTime +=
            Time.deltaTime *
            Mathf.Max(
                0f,
                playbackSpeed
            );

        AdvanceFrames();

        if (currentFrameIndex >=
            dataset.FrameCount - 1)
        {
            FinishPlayback();
        }
    }

    // =========================================================
    // ADVANCE FDS FRAMES
    // =========================================================

    private void AdvanceFrames()
    {
        while (
            currentFrameIndex <
            dataset.FrameCount - 1)
        {
            FDSFrame nextFrame =
                dataset.GetFrame(
                    currentFrameIndex + 1
                );

            if (nextFrame == null)
            {
                break;
            }

            if (playbackTime <
                nextFrame.time)
            {
                break;
            }

            currentFrameIndex++;

            currentFrame =
                dataset.GetFrame(
                    currentFrameIndex
                );

            ApplyCurrentFrame();
        }
    }

    // =========================================================
    // FINISH
    // =========================================================

    private void FinishPlayback()
    {
        currentFrameIndex =
            dataset.FrameCount - 1;

        currentFrame =
            dataset.GetFrame(
                currentFrameIndex
            );

        if (currentFrame != null)
        {
            playbackTime =
                currentFrame.time;
        }

        isPlaying = false;

        ApplyCurrentFrame();

        if (fireManager != null)
        {
            fireManager.PauseFire();
        }

        if (smokeManager != null)
        {
            smokeManager.PauseSmoke();
        }

        if (hazardSimulation != null)
        {
            hazardSimulation.PauseSimulation();
        }

        if (hazardSmokeRenderer != null)
        {
            hazardSmokeRenderer.PauseRenderer();
        }

        Debug.Log(
            "FDS playback finished."
        );
    }

    // =========================================================
    // PLAY
    // =========================================================

    public void Play()
    {
        if (initializationFailed)
        {
            Debug.LogError(
                "FDSPlaybackController: " +
                "Playback cannot start because CSV loading failed."
            );

            return;
        }

        if (!datasetReady ||
            dataset == null ||
            dataset.FrameCount == 0)
        {
            Debug.LogWarning(
                "FDSPlaybackController: " +
                "Dataset is not ready yet."
            );

            return;
        }

        if (currentFrameIndex >=
            dataset.FrameCount - 1)
        {
            ResetPlaybackState();
        }

        isPlaying = true;

        // Make sure every simulation system has
        // the current speed before playing.
        ApplyPlaybackSpeedToSimulationManagers();

        if (fireManager != null)
        {
            fireManager.Initialize();
        }

        if (smokeManager != null)
        {
            smokeManager.Initialize();
        }

        if (hazardSimulation != null)
        {
            hazardSimulation.StartSimulation();
        }

        if (hazardSmokeRenderer != null)
        {
            hazardSmokeRenderer.ResumeRenderer();
        }

        ApplyCurrentFrame();

        Debug.Log(
            "FDS playback started at " +
            playbackSpeed.ToString("0.##") +
            "x."
        );
    }

    // =========================================================
    // PAUSE
    // =========================================================

    public void Pause()
    {
        if (!datasetReady)
        {
            return;
        }

        isPlaying = false;

        if (fireManager != null)
        {
            fireManager.PauseFire();
        }

        if (smokeManager != null)
        {
            smokeManager.PauseSmoke();
        }

        if (hazardSimulation != null)
        {
            hazardSimulation.PauseSimulation();
        }

        if (hazardSmokeRenderer != null)
        {
            hazardSmokeRenderer.PauseRenderer();
        }

        Debug.Log(
            "FDS playback paused."
        );
    }

    // =========================================================
    // STOP / RESET
    // =========================================================

    public void StopAndReset()
    {
        isPlaying = false;

        ResetPlaybackState();

        Debug.Log(
            "FDS playback reset."
        );
    }

    private void ResetPlaybackState()
    {
        currentFrameIndex = 0;

        if (datasetReady &&
            dataset != null &&
            dataset.FrameCount > 0)
        {
            currentFrame =
                dataset.GetFrame(0);

            playbackTime =
                currentFrame != null
                    ? currentFrame.time
                    : 0f;
        }
        else
        {
            currentFrame = null;

            playbackTime = 0f;
        }

        if (fireManager != null)
        {
            fireManager.StopFire();
        }

        if (smokeManager != null)
        {
            smokeManager.StopSmoke();
        }

        if (hazardSimulation != null)
        {
            hazardSimulation.ResetSimulation();
        }

        if (hazardSmokeRenderer != null)
        {
            hazardSmokeRenderer.ResetRenderer();
        }

        if (detectorManager != null &&
            currentFrame != null)
        {
            detectorManager.UpdateDetectors(
                currentFrame
            );
        }

        if (currentFrame != null)
        {
            UpdateUI(
                currentFrame.time,
                currentFrame.hrr,
                GetAverageFDSSmoke(
                    currentFrame
                ),
                GetFrameTemperature(
                    currentFrame
                )
            );
        }
        else
        {
            UpdateUI(
                0f,
                0f,
                0f,
                27f
            );
        }
    }

    // =========================================================
    // APPLY CURRENT FRAME
    // =========================================================

    private void ApplyCurrentFrame()
    {
        if (currentFrame == null)
        {
            UpdateUI(
                0f,
                0f,
                0f,
                27f
            );

            return;
        }

        float smokeValue =
            GetAverageFDSSmoke(
                currentFrame
            );

        float temperature =
            GetFrameTemperature(
                currentFrame
            );

        if (fireManager != null)
        {
            fireManager.SetFireFromHRR(
                currentFrame.hrr
            );
        }

        if (smokeManager != null)
        {
            smokeManager.SetSmokeFromDensity(
                smokeValue,
                currentFrame.hrr
            );
        }

        if (hazardSimulation != null)
        {
            hazardSimulation.UpdateFromFDS(
                currentFrame.hrr,
                smokeValue
            );
        }

        if (detectorManager != null)
        {
            detectorManager.UpdateDetectors(
                currentFrame
            );
        }

        UpdateUI(
            currentFrame.time,
            currentFrame.hrr,
            smokeValue,
            temperature
        );

        if (showFrameLogs)
        {
            Debug.Log(
                "FDS frame " +
                currentFrameIndex +
                " | Time: " +
                currentFrame.time.ToString("F2") +
                " | HRR: " +
                currentFrame.hrr.ToString("F1") +
                " | Smoke: " +
                smokeValue.ToString("F4") +
                " | Temperature: " +
                temperature.ToString("F1")
            );
        }
    }

    // =========================================================
    // SMOKE
    // =========================================================

    private float GetAverageFDSSmoke(
        FDSFrame frame)
    {
        if (frame == null ||
            frame.deviceValues == null)
        {
            return 0f;
        }

        float total = 0f;
        int count = 0;

        AddSmokeValue(
            frame,
            "SMOKE_01",
            ref total,
            ref count
        );

        AddSmokeValue(
            frame,
            "SMOKE_02",
            ref total,
            ref count
        );

        AddSmokeValue(
            frame,
            "SMOKE_03",
            ref total,
            ref count
        );

        AddSmokeValue(
            frame,
            "SMOKE_04",
            ref total,
            ref count
        );

        return count > 0
            ? total / count
            : 0f;
    }

    private void AddSmokeValue(
        FDSFrame frame,
        string deviceId,
        ref float total,
        ref int count)
    {
        if (frame.deviceValues.TryGetValue(
                deviceId,
                out float value))
        {
            total +=
                Mathf.Max(
                    0f,
                    value
                );

            count++;
        }
    }

    // =========================================================
    // TEMPERATURE
    // =========================================================

    private float GetFrameTemperature(
        FDSFrame frame)
    {
        if (frame == null)
        {
            return 27f;
        }

        if (frame.deviceValues != null &&
            frame.deviceValues.TryGetValue(
                "TEMP_CENTER",
                out float centerTemperature))
        {
            return centerTemperature;
        }

        if (frame.tempCenter > 0f)
        {
            return frame.tempCenter;
        }

        return 27f;
    }

    // =========================================================
    // UI
    // =========================================================

    private void UpdateUI(
        float simulationTime,
        float hrr,
        float smoke,
        float temperature)
    {
        int minutes =
            Mathf.FloorToInt(
                simulationTime / 60f
            );

        int seconds =
            Mathf.FloorToInt(
                simulationTime % 60f
            );

        string timerString =
            "Time: " +
            minutes.ToString("00") +
            ":" +
            seconds.ToString("00");

        string hrrString =
            "HRR: " +
            hrr.ToString("F0") +
            " kW";

        string smokeString =
            "Smoke: " +
            smoke.ToString("F3") +
            " 1/m";

        string temperatureString =
            "Center Temp: " +
            temperature.ToString("F1") +
            " °C";

        if (timerTMP != null)
        {
            timerTMP.text =
                timerString;
        }

        if (timerLegacy != null)
        {
            timerLegacy.text =
                timerString;
        }

        if (hrrTMP != null)
        {
            hrrTMP.text =
                hrrString;
        }

        if (hrrLegacy != null)
        {
            hrrLegacy.text =
                hrrString;
        }

        if (smokeTMP != null)
        {
            smokeTMP.text =
                smokeString;
        }

        if (smokeLegacy != null)
        {
            smokeLegacy.text =
                smokeString;
        }

        if (temperatureTMP != null)
        {
            temperatureTMP.text =
                temperatureString;
        }

        if (temperatureLegacy != null)
        {
            temperatureLegacy.text =
                temperatureString;
        }
    }

    // =========================================================
    // LOADING UI
    // =========================================================

    private void SetLoadingText(
        string message)
    {
        if (loadingTMP != null)
        {
            loadingTMP.text =
                message;
        }

        if (loadingLegacy != null)
        {
            loadingLegacy.text =
                message;
        }
    }

    // =========================================================
    // PUBLIC GETTERS
    // =========================================================

    public FDSFrame GetCurrentFrame()
    {
        return currentFrame;
    }

    public bool IsPlaying()
    {
        return isPlaying;
    }

    public bool IsDatasetReady()
    {
        return datasetReady;
    }

    public int GetCurrentFrameIndex()
    {
        return currentFrameIndex;
    }

    public int GetFrameCount()
    {
        return dataset != null
            ? dataset.FrameCount
            : 0;
    }

    public float GetCurrentTime()
    {
        return currentFrame != null
            ? currentFrame.time
            : 0f;
    }

    public float GetCurrentHRR()
    {
        return currentFrame != null
            ? currentFrame.hrr
            : 0f;
    }

    public float GetCurrentSmoke()
    {
        return currentFrame != null
            ? GetAverageFDSSmoke(
                currentFrame
            )
            : 0f;
    }
}