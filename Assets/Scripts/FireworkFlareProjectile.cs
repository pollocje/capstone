using UnityEngine;

/// <summary>
/// The thrown flare itself. Purely physics-driven — TornadoLift.OnTriggerStay already pulls,
/// swirls and lifts any Rigidbody it touches, so no tornado-side code is needed to get the
/// "picked up and spun around" behavior once this flies into the funnel.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class FireworkFlareProjectile : MonoBehaviour
{
    [Header("Settings")]
    public float lifetime = 30f;
    public float spinSpeed = 180f; // degrees/sec, cosmetic tumble while in flight

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        transform.Rotate(Vector3.right * spinSpeed * Time.deltaTime, Space.Self);
    }
}
