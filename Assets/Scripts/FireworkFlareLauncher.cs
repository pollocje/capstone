using UnityEngine;
using Unity.Netcode;
/// <summary>
/// Equip-side component for the Firework flare item. Lives alongside Hotbar/Binoculars on
/// the player. Spawns and throws a FireworkFlareProjectile on use, then consumes the slot —
/// the flare is a single-use throwable, not a reusable launcher.
/// </summary>
public class FireworkFlareLauncher : NetworkBehaviour, IEquippable
{
    [Header("Dependencies")]
    public GameObject flarePrefab;
    public Transform throwOrigin; // defaults to Camera.main if unset
    public GameObject heldFlareVisual; // optional in-hand model shown while equipped

    [Header("Throw Settings")]
    public float throwForce = 18f;
    public float upwardArc = 0.15f; // 0-1, blends aim direction toward world up

    [SerializeField] AudioSource flareSource;
    [SerializeField] AudioClip flareSound;

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
        if (flarePrefab == null || throwOrigin == null)
            return;

        Vector3 spawnPos = throwOrigin.position + throwOrigin.forward * 0.5f;
        Quaternion spawnRot = Quaternion.LookRotation(throwOrigin.forward, Vector3.up);
        Vector3 throwDir = Vector3.Slerp(throwOrigin.forward, Vector3.up, upwardArc).normalized;
        Vector3 velocity = throwDir * throwForce;

        // Singleplayer is never network-spawned (same convention as PlayerEmote/VehicleEnterExit) -
        // spawn and play locally instead of going through the server, which wouldn't be listening.
        if (IsSpawned)
        {
            PlayFlareSoundRpc();
            ThrowRequestRpc(spawnPos, spawnRot, velocity);
        }
        else
        {
            PlayFlareSound();
            GameObject flare = Instantiate(flarePrefab, spawnPos, spawnRot);
            Collider playerCollider = GetComponentInParent<Collider>();
            flare.GetComponent<FireworkFlareProjectile>().InitializeOffline(velocity, playerCollider);
        }
    }

    // The flare needs to exist for every client, so only the server can actually create it - same
    // reasoning as PlayerSpawnManager spawning players. We just ask for it and let our own Rigidbody
    // velocity (applied in FireworkFlareProjectile.OnNetworkSpawn once we're handed ownership) fly it.
    [Rpc(SendTo.Server)]
    void ThrowRequestRpc(Vector3 spawnPos, Quaternion spawnRot, Vector3 velocity, RpcParams rpcParams = default)
    {
        GameObject flare = Instantiate(flarePrefab, spawnPos, spawnRot);
        NetworkObject flareNetObj = flare.GetComponent<NetworkObject>();
        FireworkFlareProjectile projectile = flare.GetComponent<FireworkFlareProjectile>();

        // Set before Spawn() so it's part of the initial state every client (including the new
        // owner) receives, rather than a post-spawn write that would need its own permission check.
        projectile.InitialVelocity.Value = velocity;

        flareNetObj.SpawnWithOwnership(rpcParams.Receive.SenderClientId, destroyWithScene: true);
    }

    [Rpc(SendTo.Everyone)]
    void PlayFlareSoundRpc() => PlayFlareSound();

    void PlayFlareSound() => flareSource.PlayOneShot(flareSound);
}
