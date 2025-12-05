using UnityEngine;

public class fg_sun : MonoBehaviour
{
    [Header("Zugriff auf das Time-Objekt")]
    public time timeScript; 

    [Header("Sonnenbewegung")]
    [Tooltip("Radius des kleinen Kreises")]
    public float radius = 5f;

    [Tooltip("Höhe der Sonne (Grundhöhe)")]
    public float baseHeight = 2f;

    [Tooltip("Zusätzlicher Höhenverlauf je nach Tageszeit")]
    public float heightAmplitude = 1.5f;

    void Update()
    {
        if (timeScript == null) return;

        float t = timeScript.timeInPercent / 100f;

        float angle = t * Mathf.PI * 2f;

        float x = Mathf.Cos(angle) * radius;
        float z = Mathf.Sin(angle) * radius;


        float y = baseHeight + Mathf.Sin(angle) * heightAmplitude;

        transform.position = new Vector3(x, y, z);
        transform.LookAt(Vector3.zero); 
    }
}
