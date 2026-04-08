using UnityEngine;

/// <summary>
/// Controls the alpha transparency of a material to simulate a night ceiling veil
/// that fades between translucent during the day and opaque at night.
/// Uses <see cref="Fg_time"/> for the current hour, with configurable night range
/// and smooth fade transitions around dawn and dusk. Supports both URP Lit (_BaseColor)
/// and Standard shader (_Color) properties.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class NightOpacityFadeSimple : MonoBehaviour
{
    [Header("Referenzen")]
    /// <summary>Reference to the <see cref="Fg_time"/> controller providing the current hour.</summary>
    public Fg_time timeController;

    [Header("Opacity")]
    /// <summary>Alpha value during daytime (0 = fully transparent, 1 = fully opaque).</summary>
    [Range(0f, 1f)] public float dayOpacity = 0.2f;
    /// <summary>Alpha value during nighttime (0 = fully transparent, 1 = fully opaque).</summary>
    [Range(0f, 1f)] public float nightOpacity = 1f;

    [Header("Nacht-Zeiten (0-24)")]
    [Tooltip("Start der Nacht (z.B. 24 oder 0 für Mitternacht)")]
    /// <summary>Hour at which night begins (0-24 range). Supports ranges crossing midnight.</summary>
    public float nightStart = 18f; // wird intern wie 0 behandelt

    [Tooltip("Ende der Nacht (z.B. 6)")]
    /// <summary>Hour at which night ends (0-24 range).</summary>
    public float nightEnd = 2f;

    [Header("Fade")]
    [Tooltip("Übergangsdauer in Stunden an den Rändern (z.B. 2 = 2 Stunden Fade)")]
    /// <summary>Duration in hours of the smooth fade transition at night boundaries.</summary>
    [Min(0f)] public float fadeHours = 2f;

    /// <summary>Cached Renderer component on this GameObject.</summary>
    private Renderer rend;
    /// <summary>Material instance (per-object copy) used to modify alpha without affecting shared materials.</summary>
    private Material mat;
    /// <summary>Shader property ID for URP Lit base color.</summary>
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP Lit
    /// <summary>Shader property ID for Standard shader fallback color.</summary>
    private static readonly int ColorId     = Shader.PropertyToID("_Color");     // Fallback

    /// <summary>
    /// Caches the Renderer component and creates a material instance for this object.
    /// </summary>
    void Start()
    {
        rend = GetComponent<Renderer>();
        mat = rend.material; // Instanz pro Objekt
    }

    /// <summary>
    /// Each frame, reads the current hour from <see cref="timeController"/>,
    /// computes the night factor via <see cref="NightFactor"/>, and applies
    /// the interpolated alpha value to the material.
    /// </summary>
    void Update()
    {
        if (timeController == null || mat == null) return;

        float hour = NormalizeHour(timeController.timeIn24hFormat);

        float f = NightFactor(hour, nightStart, nightEnd, fadeHours); // 0..1
        float alpha = Mathf.Lerp(dayOpacity, nightOpacity, f);

        SetAlpha(alpha);
    }

    /// <summary>
    /// Computes a night factor between 0 (full day) and 1 (full night) with
    /// smooth fade transitions of the specified duration at the night boundaries.
    /// </summary>
    /// <param name="hour">Current hour (0-24, normalized).</param>
    /// <param name="start">Hour when night begins.</param>
    /// <param name="end">Hour when night ends.</param>
    /// <param name="fade">Transition duration in hours at each boundary.</param>
    /// <returns>A value from 0.0 (day) to 1.0 (night) with smooth fade at edges.</returns>
    // 0 = Tag, 1 = Nacht, weich gefadet an Start/Ende
    private float NightFactor(float hour, float start, float end, float fade)
    {
        start = NormalizeHour(start);
        end   = NormalizeHour(end);

        bool inNight = InRange(hour, start, end);

        if (fade <= 0f)
            return inNight ? 1f : 0f;

        if (inNight)
        {
            // Fade-Out Richtung nightEnd
            float toEnd = ForwardDistance(hour, end); // 0..24
            if (toEnd < fade)
                return Smooth01(toEnd / fade); // bei end => 0, weiter weg => 1

            return 1f;
        }
        else
        {
            // Fade-In kurz vor nightStart
            float toStart = ForwardDistance(hour, start);
            if (toStart < fade)
                return 1f - Smooth01(toStart / fade); // bei start => 1, weiter weg => 0

            return 0f;
        }
    }

    /// <summary>
    /// Sets the alpha channel of the material's color property.
    /// Tries URP _BaseColor first, then falls back to Standard _Color.
    /// </summary>
    /// <param name="a">Alpha value to apply (0-1).</param>
    private void SetAlpha(float a)
    {
        if (mat.HasProperty(BaseColorId))
        {
            var c = mat.GetColor(BaseColorId);
            c.a = a;
            mat.SetColor(BaseColorId, c);
            return;
        }

        if (mat.HasProperty(ColorId))
        {
            var c = mat.GetColor(ColorId);
            c.a = a;
            mat.SetColor(ColorId, c);
        }
    }


    /// <summary>
    /// Wraps an hour value into the 0-24 range, treating 24 as midnight (0).
    /// </summary>
    /// <param name="h">Hour value to normalize.</param>
    /// <returns>The hour clamped to the 0-24 range.</returns>
    private float NormalizeHour(float h)
    {
        h %= 24f;
        if (h < 0f) h += 24f;
        // 24 wird zu 0 (Mitternacht)
        return h;
    }

    /// <summary>
    /// Checks whether an hour falls within a circular range on the 24-hour clock.
    /// Supports ranges that cross midnight (e.g., 22:00 to 06:00).
    /// </summary>
    /// <param name="h">Hour to test.</param>
    /// <param name="start">Start of the range.</param>
    /// <param name="end">End of the range.</param>
    /// <returns>True if <paramref name="h"/> lies within the range from <paramref name="start"/> to <paramref name="end"/>.</returns>
    // Range: start -> end, unterstützt über Mitternacht (z.B. 22 -> 6)
    private bool InRange(float h, float start, float end)
    {
        if (Mathf.Approximately(start, end)) return false;

        if (start < end) return h >= start && h < end;
        return (h >= start) || (h < end);
    }

    /// <summary>
    /// Calculates the forward (clockwise) distance in hours from <paramref name="a"/>
    /// to <paramref name="b"/> on the 24-hour clock.
    /// </summary>
    /// <param name="a">Starting hour.</param>
    /// <param name="b">Target hour.</param>
    /// <returns>Distance in hours (0-24) going forward from a to b.</returns>
    // Distanz von a nach b vorwärts auf der Uhr (0..24)
    private float ForwardDistance(float a, float b)
    {
        float d = NormalizeHour(b) - NormalizeHour(a);
        if (d < 0f) d += 24f;
        return d;
    }

    /// <summary>
    /// Applies a smooth Hermite interpolation (SmoothStep) to a value clamped between 0 and 1.
    /// </summary>
    /// <param name="t">Input value.</param>
    /// <returns>Smoothly interpolated value between 0 and 1.</returns>
    private float Smooth01(float t) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
}
