using UnityEngine;
using System;

public class fg_turntablerotation : MonoBehaviour
{
    [Tooltip("Umdrehungen pro Stunde")]
    public float rotationsPerHour = 1f;

    [Tooltip("1 = Uhrzeigersinn, -1 = Gegen den Uhrzeigersinn")]
    public int rotationDirection = 1;

    void Update()
    {
        // Absolute globale Zeit in Sekunden (UTC, unabhängig vom Spielstart)
        double globalSeconds = DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;

        // Fortschritt der Rotation (0..1) innerhalb einer Stunde
        double hourFraction = (globalSeconds * rotationsPerHour) % 3600 / 3600.0;

        // Winkel berechnen
        float angle = (float)(hourFraction * 360.0) * rotationDirection;

        // Rotation SETZEN (nicht addieren!)
        transform.rotation = Quaternion.Euler(0f, angle, 0f);
    }
}
