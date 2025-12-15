using UnityEngine;

public class RotateAnchor : MonoBehaviour
{
    public float degreesPerSecond = 20f;

    void Update()
    {
        transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.Self);
    }
}
