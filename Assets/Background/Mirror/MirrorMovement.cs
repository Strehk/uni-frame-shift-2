using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Positions and orients a hidden camera behind a mirror plane to create a
/// real-time reflection effect. The camera tracks the player's head position
/// and renders the scene to a RenderTexture displayed on the mirror surface,
/// extending the viewer's perspective beyond the Tilt Five field of view.
/// </summary>
public class MirrorMovement : MonoBehaviour
{

    /// <summary>Transform of the player's head (or Tilt Five glasses) to reflect.</summary>
    public Transform playerTarget;
    /// <summary>Transform of the mirror plane used as the reflection reference.</summary>
    public Transform mirror;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    /// <summary>
    /// Each frame, calculates the reflected position of the player relative to the
    /// mirror plane using inverse/forward transform operations, then orients the
    /// camera to look back through the mirror at the reflected viewpoint.
    /// </summary>
    void Update()
    {
        Vector3 localPlayer = mirror.InverseTransformPoint(playerTarget.position);
        transform.position = mirror.TransformPoint(new Vector3(localPlayer.x, localPlayer.y, -localPlayer.z));

        Vector3 lookAtMirror = mirror.TransformPoint(new Vector3(-localPlayer.x, localPlayer.y, localPlayer.z));
        transform.LookAt(lookAtMirror);
    }
}
