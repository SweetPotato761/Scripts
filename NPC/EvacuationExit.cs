using UnityEngine;

/// <summary>
/// Represents one possible evacuation exit.
/// Attach this script to every ExitPoint object.
/// </summary>
public class EvacuationExit : MonoBehaviour
{
    [Header("Exit Status")]
    [SerializeField] private bool isAvailable = true;

    [Header("Current Hazard Values")]
    [Min(0f)]
    [SerializeField] private float smokeLevel;

    [Min(0f)]
    [SerializeField] private float fireRisk;

    [Header("Unsafe Limits")]
    [Min(0f)]
    [SerializeField] private float maximumSafeSmoke = 3f;

    [Min(0f)]
    [SerializeField] private float maximumSafeFireRisk = 1f;

    [Header("Route Cost Penalties")]
    [Min(0f)]
    [SerializeField] private float smokeCostMultiplier = 200f;

    [Min(0f)]
    [SerializeField] private float fireCostMultiplier = 500f;

    public bool IsAvailable
    {
        get
        {
            return isAvailable
                && smokeLevel < maximumSafeSmoke
                && fireRisk < maximumSafeFireRisk;
        }
    }

    public float HazardPenalty
    {
        get
        {
            return smokeLevel * smokeCostMultiplier
                 + fireRisk * fireCostMultiplier;
        }
    }

    public float SmokeLevel => smokeLevel;
    public float FireRisk => fireRisk;

    public void SetAvailable(bool value)
    {
        isAvailable = value;
    }

    public void SetSmokeLevel(float value)
    {
        smokeLevel = Mathf.Max(0f, value);
    }

    public void SetFireRisk(float value)
    {
        fireRisk = Mathf.Max(0f, value);
    }
}