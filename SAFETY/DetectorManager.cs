using System.Collections.Generic;
using UnityEngine;

public class DetectorManager : MonoBehaviour
{
    [System.Serializable]
    public class DetectorVisual
    {
        public string deviceId;

        [Header("Visuals")]
        public GameObject detectorModel;     // the actual 3D detector model (always visible)
        public GameObject indicatorObject;   // small alarm indicator object that turns ON when triggered
        public Light indicatorLight;         // optional light that turns ON when triggered

        [Header("Detector Settings")]
        public bool isSmokeDetector = true;
        public float threshold = 0.1f;
    }

    [Header("Detector visuals")]
    public List<DetectorVisual> detectors = new List<DetectorVisual>();

    private Dictionary<string, DetectorVisual> detectorMap = new Dictionary<string, DetectorVisual>();

    void Awake()
    {
        detectorMap.Clear();

        foreach (var d in detectors)
        {
            if (!string.IsNullOrWhiteSpace(d.deviceId) && !detectorMap.ContainsKey(d.deviceId))
                detectorMap.Add(d.deviceId, d);

            // Detector model should stay visible
            if (d.detectorModel != null)
                d.detectorModel.SetActive(true);

            // Indicator starts OFF
            if (d.indicatorObject != null)
                d.indicatorObject.SetActive(false);

            // Light starts OFF
            if (d.indicatorLight != null)
                d.indicatorLight.enabled = false;
        }
    }

    public void UpdateDetectors(FDSFrame frame)
    {
        if (frame == null) return;

        foreach (var kv in detectorMap)
        {
            string deviceId = kv.Key;
            DetectorVisual visual = kv.Value;

            if (!frame.deviceValues.TryGetValue(deviceId, out float value))
                continue;

            bool active = value >= visual.threshold;

            // keep detector model visible
            if (visual.detectorModel != null)
                visual.detectorModel.SetActive(true);

            // only indicator turns on/off
            if (visual.indicatorObject != null)
                visual.indicatorObject.SetActive(active);

            if (visual.indicatorLight != null)
                visual.indicatorLight.enabled = active;
        }
    }

    public float GetAverageSmokeValue(FDSFrame frame)
    {
        if (frame == null) return 0f;

        float total = 0f;
        int count = 0;

        foreach (var d in detectors)
        {
            if (!d.isSmokeDetector) continue;

            if (frame.deviceValues.TryGetValue(d.deviceId, out float value))
            {
                total += value;
                count++;
            }
        }

        return count > 0 ? total / count : 0f;
    }

    public bool AnySmokeDetectorTriggered(FDSFrame frame)
    {
        if (frame == null) return false;

        foreach (var d in detectors)
        {
            if (!d.isSmokeDetector) continue;

            if (frame.deviceValues.TryGetValue(d.deviceId, out float value))
            {
                if (value >= d.threshold)
                    return true;
            }
        }

        return false;
    }
}