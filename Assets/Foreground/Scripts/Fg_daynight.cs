using UnityEngine;

public class Fg_daynight : MonoBehaviour
{
    [Header("Global Time Settings")]
    public Fg_time timeSettings;

    [Header("Orbit Settings")]
    public Transform sunOrbit;        // rotates around stage
    public float orbitTilt = 10f;     // adjustable tilt of orbit in Inspector

    [Header("Sun Visual Settings")]
    public Transform sunVisual;       // sphere representing the visible sun
    public float sunDistance = -1f;  // distance in front of the directional light

    [Header("Light Settings")]
    public Light sun;                 // directional light
    public Gradient sunColor;
    public AnimationCurve sunIntensity;

    private float t;

    void Update()
    {
        if (timeSettings == null) return;

        t = timeSettings.timeInPercent / 100f;

        float angle;

        if (t < 0.75f)
        {
            // Tag 0..45s → 0°..180°
            float dayT = t / 0.75f;          // normiere 0..1
            angle = dayT * 180f;             // 0° bis 180°
        }
        else
        {
            // Nacht 45..60s → 180°..360°
            float nightT = (t - 0.75f) / 0.25f; // 0..1
            angle = 180f + nightT * 180f;       // 180° bis 360°
        }

        // Rotate Orbit (controls sun path)
        sunOrbit.localRotation = Quaternion.Euler(angle, orbitTilt, 0f);

        // Keep directional light pointing downward
        sun.transform.localRotation = Quaternion.identity;

        // Keep sunVisual always at a set distance in front of the light
        sunVisual.localPosition = new Vector3(0, 0, sunDistance);

        // Apply light color + intensity
        sun.color = sunColor.Evaluate(t);
        sun.intensity = sunIntensity.Evaluate(t);
    }
}
