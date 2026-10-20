using UnityEngine;

public class AlarmManager : MonoBehaviour
{
    public AudioSource alarmAudio;
    public Light[] emergencyLights;
    public float blinkSpeed = 4f;

    private bool alarmOn = false;

    public void SetAlarm(bool active)
    {
        if (active && !alarmOn)
        {
            alarmOn = true;
            if (alarmAudio != null && !alarmAudio.isPlaying)
                alarmAudio.Play();
        }
        else if (!active && alarmOn)
        {
            alarmOn = false;
            if (alarmAudio != null && alarmAudio.isPlaying)
                alarmAudio.Stop();

            SetLights(false);
        }
    }

    void Update()
    {
        if (!alarmOn || emergencyLights == null)
            return;

        bool on = Mathf.Sin(Time.time * blinkSpeed) > 0f;
        SetLights(on);
    }

    private void SetLights(bool state)
    {
        if (emergencyLights == null) return;
        foreach (var l in emergencyLights)
        {
            if (l != null)
                l.enabled = state;
        }
    }
}