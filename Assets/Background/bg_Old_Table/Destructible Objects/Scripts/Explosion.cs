using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Explosion : MonoBehaviour
{
    public float radius;
    public float power;
    public float delay;

    void Start()
    {
        StartCoroutine(Explode());
    }

    private IEnumerator Explode()
    {
        yield return new WaitForSeconds(delay);
        Vector3 explosionPos = transform.position;
        Collider[] colliders = Physics.OverlapSphere(explosionPos, radius);
        foreach (Collider hit in colliders)
        {            
            if(hit.TryGetComponent(out DestructibleObject destructibleObject)){
                destructibleObject.Break();
            }
        }

        colliders = Physics.OverlapSphere(explosionPos, radius);
        foreach (Collider hit in colliders)
        {            
            if(hit.TryGetComponent(out Rigidbody rigidbody)){
                rigidbody.AddExplosionForce(power, transform.position, radius, 1.0f);
            }
        }
    }
}
