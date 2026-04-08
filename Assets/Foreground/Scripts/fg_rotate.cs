using UnityEngine;

/// <summary>
/// Rotates an object based on the in-game time progress provided by <see cref="Fg_time"/>.
/// Supports a configurable number of full rotations per day and an arbitrary rotation axis.
/// Used for time-synced elements such as the central cross anchor.
/// </summary>
public class Fg_rotate : MonoBehaviour
{
    [Header("Zugriff auf das Time-Objekt")]
    /// <summary>Reference to the <see cref="Fg_time"/> script providing day progress.</summary>
    public Fg_time timeScript; // Ziehe das Objekt mit dem 'time'-Script hier rein

    [Header("Dreh-Einstellungen")]
    [Tooltip("Wie oft soll sich das Objekt pro Tag rotieren?")]
    /// <summary>Number of complete rotations the object performs per in-game day.</summary>
    public float rotationsPerDay = 1f;

    [Tooltip("Rotationsachse")]
    /// <summary>The axis around which the object rotates (default: Y-up).</summary>
    public Vector3 rotationAxis = Vector3.up;


    /// <summary>
    /// Sets the object's local rotation each frame based on the current day
    /// progress percentage from <see cref="Fg_time.timeInPercent"/>.
    /// </summary>
    void Update()
    {
        if (timeScript == null) return;

        float t = timeScript.timeInPercent / 100f;

        float totalRotation = t * 360f * rotationsPerDay;

        transform.localRotation = Quaternion.AngleAxis(totalRotation, rotationAxis);
    }
}
