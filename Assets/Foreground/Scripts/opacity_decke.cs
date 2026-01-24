using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class NightOpacityFadeSimple : MonoBehaviour
{
    [Header("Referenzen")]
    public Fg_time timeController;

    [Header("Opacity")]
    [Range(0f, 1f)] public float dayOpacity = 0.2f;
    [Range(0f, 1f)] public float nightOpacity = 1f;

    [Header("Nacht-Zeiten (0-24)")]
    [Tooltip("Start der Nacht (z.B. 24 oder 0 für Mitternacht)")]
    public float nightStart = 18f; // wird intern wie 0 behandelt

    [Tooltip("Ende der Nacht (z.B. 6)")]
    public float nightEnd = 2f;

    [Header("Fade")]
    [Tooltip("Übergangsdauer in Stunden an den Rändern (z.B. 2 = 2 Stunden Fade)")]
    [Min(0f)] public float fadeHours = 2f;

    private Renderer rend;
    private Material mat;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP Lit
    private static readonly int ColorId     = Shader.PropertyToID("_Color");     // Fallback

    void Start()
    {
        rend = GetComponent<Renderer>();
        mat = rend.material; // Instanz pro Objekt
    }

    void Update()
    {
        if (timeController == null || mat == null) return;

        float hour = NormalizeHour(timeController.timeIn24hFormat);

        float f = NightFactor(hour, nightStart, nightEnd, fadeHours); // 0..1
        float alpha = Mathf.Lerp(dayOpacity, nightOpacity, f);

        SetAlpha(alpha);
    }

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


    private float NormalizeHour(float h)
    {
        h %= 24f;
        if (h < 0f) h += 24f;
        // 24 wird zu 0 (Mitternacht)
        return h;
    }

    // Range: start -> end, unterstützt über Mitternacht (z.B. 22 -> 6)
    private bool InRange(float h, float start, float end)
    {
        if (Mathf.Approximately(start, end)) return false;

        if (start < end) return h >= start && h < end;
        return (h >= start) || (h < end);
    }

    // Distanz von a nach b vorwärts auf der Uhr (0..24)
    private float ForwardDistance(float a, float b)
    {
        float d = NormalizeHour(b) - NormalizeHour(a);
        if (d < 0f) d += 24f;
        return d;
    }

    private float Smooth01(float t) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
}
