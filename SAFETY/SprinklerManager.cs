using System.Collections.Generic;
using UnityEngine;

public class SprinklerManager : MonoBehaviour
{
    [System.Serializable]
    public class SprinklerVisual
    {
        public string deviceId;
        public ParticleSystem waterParticle;
        public float activationThreshold = 68f;
        [HideInInspector] public bool activated;
    }

    public List<SprinklerVisual> sprinklers = new List<SprinklerVisual>();

    private Dictionary<string, SprinklerVisual> sprinklerMap = new Dictionary<string, SprinklerVisual>();

    void Awake()
    {
        sprinklerMap.Clear();

        foreach (var s in sprinklers)
        {
            if (!string.IsNullOrWhiteSpace(s.deviceId) && !sprinklerMap.ContainsKey(s.deviceId))
                sprinklerMap.Add(s.deviceId, s);

            if (s.waterParticle != null)
                s.waterParticle.Stop();

            s.activated = false;
        }
    }

    public void UpdateSprinklers(FDSFrame frame)
    {
        if (frame == null) return;

        foreach (var kv in sprinklerMap)
        {
            string deviceId = kv.Key;
            SprinklerVisual sprinkler = kv.Value;

            if (!frame.deviceValues.TryGetValue(deviceId, out float value))
                continue;

            if (!sprinkler.activated && value >= sprinkler.activationThreshold)
            {
                sprinkler.activated = true;

                if (sprinkler.waterParticle != null)
                    sprinkler.waterParticle.Play();
            }
        }
    }

    public bool AnySprinklerActivated()
    {
        foreach (var s in sprinklers)
        {
            if (s.activated) return true;
        }
        return false;
    }
}