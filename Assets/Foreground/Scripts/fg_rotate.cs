using UnityEngine;

public class Fg_rotate : MonoBehaviour
{
    [Header("Zugriff auf das Time-Objekt")]
    public Fg_time timeScript; // Ziehe das Objekt mit dem 'time'-Script hier rein

    [Header("Dreh-Einstellungen")]
    [Tooltip("Wie oft soll sich das Objekt pro Tag rotieren?")]
    public float rotationsPerDay = 1f;

    [Tooltip("Rotationsachse")]
    public Vector3 rotationAxis = Vector3.up;


    void Update()
    {
        if (timeScript == null) return;

        float t = timeScript.timeInPercent / 100f;

        float totalRotation = t * 360f * rotationsPerDay;

        transform.localRotation = Quaternion.AngleAxis(totalRotation, rotationAxis);
    }
}
