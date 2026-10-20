using System.Collections.Generic;

[System.Serializable]
public class FDSFrame
{
    public float time;

    // HRR Data
    public float hrr;

    // Device Data
    public Dictionary<string, float> deviceValues =
        new Dictionary<string, float>();

    // Temperatures
    public float tempExit;
    public float tempCenter;
}