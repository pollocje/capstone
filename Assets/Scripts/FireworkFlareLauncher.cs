using UnityEngine;

/// <summary>
/// Equip-side component for the Firework flare item. Lives alongside Hotbar/Binoculars on
/// the player. Spawns and throws a FireworkFlareProjectile on use, then consumes the slot —
/// the flare is a single-use throwable, not a reusable launcher.
/// </summary>
public class FireworkFlareLauncher : MonoBehaviour, IEquippable
{
    [Header("Dependencies")]
    public GameObject flarePrefab;
    public Transform throwOrigin;      // defaults to Camera.main if unset
    public GameObject heldFlareVisual; // optional in-hand model shown while equipped

    [Header("Throw Settings")]
    public float throwForce = 18f;
    public float upwardArc = 0.15f;    // 0-1, blends aim direction toward world up

    private Hotbar _hotbar;

    void Start()
    {
        _hotbar = GetComponent<Hotbar>();

        if (throwOrigin == null && Camera.main != null)
            throwOrigin = Camera.main.transform;

        if (heldFlareVisual != null)
            heldFlareVisual.SetActive(false);
    }

    public void OnEquip()
    {
        if (heldFlareVisual != null)
            heldFlareVisual.SetActive(true);
    }

    public void OnUnequip()
    {
        if (heldFlareVisual != null)
            heldFlareVisual.SetActive(false);
    }

    public void OnUseDown()
    {
        Throw();
        _hotbar?.ConsumeSelected();
    }

    public void OnUseUp() { }

    void Throw()
    {
        if (flarePrefab == null || throwOrigin == null) return;

        Vector3 spawnPos = throwOrigin.position + throwOrigin.forward * 0.5f;
        Quaternion spawnRot = Quaternion.LookRotation(throwOrigin.forward, Vector3.up);

        GameObject flare = Instantiate(flarePrefab, spawnPos, spawnRot);

        Rigidbody flareRb = flare.GetComponent<Rigidbody>();
        if (flareRb != null)
        {
            Vector3 throwDir = Vector3.Slerp(throwOrigin.forward, Vector3.up, upwardArc).normalized;
            flareRb.linearVelocity = throwDir * throwForce;
        }

        Collider playerCollider = GetComponentInParent<Collider>();
        Collider flareCollider = flare.GetComponent<Collider>();
        if (playerCollider != null && flareCollider != null)
            Physics.IgnoreCollision(flareCollider, playerCollider);
    }
}
