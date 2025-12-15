using UnityEngine;

public class Fg_time : MonoBehaviour
{
    [Header("Einstellungen")]
    [Tooltip("Dauer eines vollständigen Tages in Minuten.")]
    [SerializeField] private float dayDurationInMinutes = 20f;

    [Tooltip("Startzeit in Stunden (0-24).")]
    [SerializeField] public  float startTimeInHours = 6f;

    [HideInInspector] public float timeIn24hFormat { get; private set; } = 6f;

    [HideInInspector] public float timeInPercent { get; private set; } = 0f;

    private const float SECONDS_PER_DAY = 86400f; // 24 * 60 * 60

    private float elapsedTimeInSeconds = 0f;

    void Start()
    {
        elapsedTimeInSeconds = startTimeInHours * 3600f / SECONDS_PER_DAY * (dayDurationInMinutes * 60f);
    }

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

    private void UpdateTime()
    {

        float secondsPerDay = dayDurationInMinutes * 60f;

        float dayProgress = elapsedTimeInSeconds / secondsPerDay;

        timeInPercent = dayProgress * 100f;

        timeIn24hFormat = dayProgress * 24f;
    }
}
