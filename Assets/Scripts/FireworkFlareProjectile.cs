using UnityEngine;
using Unity.Netcode;

/// <summary>
/// The thrown flare itself. Purely physics-driven — TornadoLift.OnTriggerStay already pulls,
/// swirls and lifts any Rigidbody it touches, so no tornado-side code is needed to get the
/// "picked up and spun around" behavior once this flies into the funnel.
///
/// Spawned server-side by FireworkFlareLauncher.ThrowRequestRpc, owned by whoever threw it -
/// the prefab's NetworkTransform is Owner-authoritative, so only the thrower's own physics
/// simulation needs to be correct; everyone else just receives the synced transform.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NetworkObject))]
public class FireworkFlareProjectile : NetworkBehaviour
{
    [Header("Settings")]
    public float lifetime = 30f;
    public float spinSpeed = 180f; // degrees/sec, cosmetic tumble while in flight

    // Set by the server (ThrowRequestRpc) before Spawn(), so it rides along in the initial
    // spawn payload every client - including the eventual owner - receives.
    public NetworkVariable<Vector3> InitialVelocity = new NetworkVariable<Vector3>();

    private Rigidbody _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            _rb.linearVelocity = InitialVelocity.Value;

            // Don't let the flare immediately collide with the thrower's own capsule - only
            // the owner's physics matters here (Owner-authoritative NetworkTransform), so this
            // only needs to run on the owner's own local simulation.
            NetworkObject localPlayer = NetworkManager.Singleton.LocalClient?.PlayerObject;
            Collider playerCollider = localPlayer != null ? localPlayer.GetComponentInChildren<Collider>() : null;
            IgnoreCollisionWithThrower(playerCollider);
        }

        // Only the server despawns - clients destroying a spawned NetworkObject directly isn't
        // allowed, and the despawn message handles removing it everywhere once the server does.
        if (IsServer)
            Invoke(nameof(DespawnSelf), lifetime);
    }

    // Singleplayer is never network-spawned (same convention as PlayerEmote/VehicleEnterExit):
    // called directly by FireworkFlareLauncher right after a plain Instantiate() when this
    // client isn't networked at all, since OnNetworkSpawn above never fires in that case.
    public void InitializeOffline(Vector3 velocity, Collider throwerCollider)
    {
        _rb.linearVelocity = velocity;
        IgnoreCollisionWithThrower(throwerCollider);
        Destroy(gameObject, lifetime);
    }

    private void IgnoreCollisionWithThrower(Collider throwerCollider)
    {
        Collider flareCollider = GetComponent<Collider>();
        if (throwerCollider != null && flareCollider != null)
            Physics.IgnoreCollision(flareCollider, throwerCollider);
    }

    private void DespawnSelf()
    {
        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn();
    }

    private void Update()
    {
        transform.Rotate(Vector3.right * spinSpeed * Time.deltaTime, Space.Self);
    }
}
