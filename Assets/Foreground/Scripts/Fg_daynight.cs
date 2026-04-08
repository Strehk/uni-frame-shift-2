using UnityEngine;

/// <summary>
/// Controls the sun's orbital position, light color, and intensity based on
/// the current time of day provided by <see cref="Fg_time"/>.
/// The sun follows a 360-degree orbit: 0-180 degrees during daytime (75% of the cycle)
/// and 180-360 degrees during nighttime (25% of the cycle).
/// </summary>
public class Fg_daynight : MonoBehaviour
{
    [Header("Global Time Settings")]
    /// <summary>Reference to the <see cref="Fg_time"/> controller providing the current time.</summary>
    public Fg_time timeSettings;

    [Header("Orbit Settings")]
    /// <summary>Transform that is rotated to move the sun along its orbital path.</summary>
    public Transform sunOrbit;        // rotates around stage
    /// <summary>Tilt angle in degrees applied to the orbit for perspective adjustment.</summary>
    public float orbitTilt = 10f;     // adjustable tilt of orbit in Inspector

    [Header("Sun Visual Settings")]
    /// <summary>Transform of the visual sun sphere positioned relative to the directional light.</summary>
    public Transform sunVisual;       // sphere representing the visible sun
    /// <summary>Distance of the sun visual from the directional light along its forward axis.</summary>
    public float sunDistance = -1f;  // distance in front of the directional light

    [Header("Light Settings")]
    /// <summary>The directional light component representing the sun.</summary>
    public Light sun;                 // directional light
    /// <summary>Color gradient evaluated over the day cycle (0 = start, 1 = end) to tint the sunlight.</summary>
    public Gradient sunColor;
    /// <summary>Animation curve evaluated over the day cycle to control light intensity.</summary>
    public AnimationCurve sunIntensity;

    /// <summary>Cached normalized time value (0-1) representing current day progress.</summary>
    private float t;

    /// <summary>
    /// Each frame, calculates the sun's orbital angle from the current time,
    /// rotates the orbit transform, positions the sun visual, and applies
    /// the color gradient and intensity curve to the directional light.
    /// </summary>
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
