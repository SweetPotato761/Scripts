using UnityEngine;

[System.Serializable]
public class HazardCell
{
    [Header("Grid Position")]
    public int x;
    public int z;

    [Header("World Position")]
    public Vector3 worldPosition;

    [Header("Smoke")]
    [Range(0f, 1f)]
    public float smokeDensity;

    [Header("Temperature")]
    public float temperature = 27f;

    [Header("Visibility")]
    [Range(0f, 1f)]
    public float visibility = 1f;

    [Header("Flags")]
    public bool isObstacle;
    public bool containsFire;

    public HazardCell(
        int gridX,
        int gridZ,
        Vector3 position)
    {
        x = gridX;
        z = gridZ;
        worldPosition = position;

        smokeDensity = 0f;
        temperature = 27f;
        visibility = 1f;

        isObstacle = false;
        containsFire = false;
    }

    public void SetSmokeDensity(float value)
    {
        smokeDensity = Mathf.Clamp01(value);

        // More smoke = lower visibility
        visibility = 1f - smokeDensity;
    }

    public void AddSmoke(float amount)
    {
        SetSmokeDensity(smokeDensity + amount);
    }

    public void RemoveSmoke(float amount)
    {
        SetSmokeDensity(smokeDensity - amount);
    }

    public void SetTemperature(float value)
    {
        temperature = Mathf.Max(0f, value);
    }

    public void AddTemperature(float amount)
    {
        temperature += amount;
    }

    public void CoolDown(float amount)
    {
        temperature = Mathf.Max(27f, temperature - amount);
    }

    public void ResetCell(float ambientTemperature = 27f)
    {
        smokeDensity = 0f;
        visibility = 1f;
        temperature = ambientTemperature;
        containsFire = false;
    }

    public float GetHazardCost()
    {
        float smokeCost = smokeDensity * 10f;

        float heatCost =
            Mathf.Max(
                0f,
                temperature - 40f
            ) * 0.1f;

        return smokeCost + heatCost;
    }

    public bool IsDangerous()
    {
        return smokeDensity > 0.5f ||
               temperature > 60f;
    }

    public override string ToString()
    {
        return
            $"Cell ({x},{z}) | " +
            $"Smoke: {smokeDensity:F2} | " +
            $"Temp: {temperature:F1}°C";
    }
}