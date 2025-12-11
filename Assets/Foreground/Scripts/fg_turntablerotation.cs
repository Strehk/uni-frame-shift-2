using UnityEngine;

public class fg_turntablerotation : MonoBehaviour
{
    [Tooltip("Umdrehungen pro Stunde")]
    public float rotationsPerHour = 1f;

    [Tooltip("1 = Uhrzeigersinn, -1 = Gegen den Uhrzeigersinn")]
    public int rotationDirection = 1;

    float degreesPerSecond;

    void Start()
    {
        degreesPerSecond = (360f * rotationsPerHour) / 3600f;
    }

    void Update()
    {
        // Erzwungene Rotation um die globale Y-Achse
        float rotation = degreesPerSecond * rotationDirection * Time.deltaTime;
        transform.Rotate(0f, rotation, 0f, Space.World);
    }
}
