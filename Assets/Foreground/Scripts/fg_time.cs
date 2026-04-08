using UnityEngine;

/// <summary>
/// Master time controller that simulates a configurable day/night cycle.
/// Tracks elapsed time and exposes the current time as both a 24-hour value
/// and a percentage (0-100) for use by other scripts such as
/// <see cref="Fg_daynight"/>, <see cref="Fg_rotate"/>, and <see cref="NightOpacityFadeSimple"/>.
/// </summary>
public class Fg_time : MonoBehaviour
{
    [Header("Einstellungen")]
    [Tooltip("Dauer eines vollständigen Tages in Minuten.")]
    /// <summary>Duration of one full in-game day in real-time minutes.</summary>
    [SerializeField] private float dayDurationInMinutes = 20f;

    [Tooltip("Startzeit in Stunden (0-24).")]
    /// <summary>The hour at which the day cycle begins (0-24 range).</summary>
    [SerializeField] public  float startTimeInHours = 6f;

    /// <summary>Current time of day in 24-hour format (0.0 - 24.0).</summary>
    [HideInInspector] public float timeIn24hFormat { get; private set; } = 6f;

    /// <summary>Current day progress as a percentage (0.0 - 100.0).</summary>
    [HideInInspector] public float timeInPercent { get; private set; } = 0f;

    /// <summary>Total number of real seconds in a 24-hour day (86400).</summary>
    private const float SECONDS_PER_DAY = 86400f; // 24 * 60 * 60

    /// <summary>Accumulated elapsed time in seconds since the cycle started.</summary>
    private float elapsedTimeInSeconds = 0f;

    /// <summary>
    /// Initializes the elapsed time based on <see cref="startTimeInHours"/>
    /// so the cycle begins at the configured hour.
    /// </summary>
    void Start()
    {
        elapsedTimeInSeconds = startTimeInHours * 3600f / SECONDS_PER_DAY * (dayDurationInMinutes * 60f);
    }

    /// <summary>
    /// Advances the elapsed time each frame and resets it when a full day
    /// cycle has completed. Calls <see cref="UpdateTime"/> to recalculate
    /// the public time values.
    /// </summary>
    void Update()
    {

        elapsedTimeInSeconds += Time.deltaTime;

        float secondsPerDay = dayDurationInMinutes * 60f;


        if (elapsedTimeInSeconds >= secondsPerDay)
        {
            elapsedTimeInSeconds = 0f;
        }

        UpdateTime();
    }

    /// <summary>
    /// Recalculates <see cref="timeInPercent"/> and <see cref="timeIn24hFormat"/>
    /// from the current elapsed time.
    /// </summary>
    private void UpdateTime()
    {

        float secondsPerDay = dayDurationInMinutes * 60f;

        float dayProgress = elapsedTimeInSeconds / secondsPerDay;

        timeInPercent = dayProgress * 100f;

        timeIn24hFormat = dayProgress * 24f;
    }
}
