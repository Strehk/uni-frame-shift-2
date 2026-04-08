using UnityEngine;

/// <summary>
/// Simple utility that continuously rotates a GameObject around its local Y-axis
/// at a configurable speed in degrees per second.
/// </summary>
public class RotateAnchor : MonoBehaviour
{
    /// <summary>Rotation speed in degrees per second around the Y-axis.</summary>
    public float degreesPerSecond = 20f;

    /// <summary>
    /// Applies incremental Y-axis rotation each frame, scaled by delta time
    /// for frame-rate independent rotation.
    /// </summary>
    void Update()
    {
        transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.Self);
    }
}
