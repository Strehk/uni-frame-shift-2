using UnityEngine;
using System;

/// <summary>
/// Rotates a turntable based on absolute UTC time rather than in-game time,
/// ensuring that all viewers see synchronized rotation regardless of when the
/// application was started. The rotation angle is set (not accumulated) each frame.
/// </summary>
public class Fg_turntablerotation : MonoBehaviour
{
    [Tooltip("Umdrehungen pro Stunde")]
    /// <summary>Number of full rotations the turntable completes per real-time hour.</summary>
    public float rotationsPerHour = 1f;

    [Tooltip("1 = Uhrzeigersinn, -1 = Gegen den Uhrzeigersinn")]
    /// <summary>Rotation direction multiplier: 1 for clockwise, -1 for counter-clockwise.</summary>
    public int rotationDirection = 1;

    /// <summary>
    /// Computes the absolute rotation angle from UTC epoch time and sets the
    /// transform's Y-axis rotation directly, ensuring frame-independent synchronization.
    /// </summary>
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
