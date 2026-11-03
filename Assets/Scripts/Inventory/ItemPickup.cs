using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Put this on a world item prefab. Pressing Interact (E) while looking at or standing near it
/// adds the item to the player's hotbar (PlayerGrabController calls TryPickup).
/// With pickupOnTouch on, walking into it does the same — that needs a second collider on the
/// object with Is Trigger checked.
///
/// Online, "claiming" it is a request to the server, not an immediate local grant - two players
/// reaching it in the same few ticks would otherwise each add it to their own hotbar and destroy
/// their own local copy, duplicating the item. The server decides who actually got there first via
/// _claimedBy, and only that client's OnClaimedChanged hands it their item; every client (winner
/// included) just sees the world object disappear once the server despawns it.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class ItemPickup : NetworkBehaviour
{
    [SerializeField] private InventoryItem item;
    [Tooltip("Seconds before this can be picked up, so a dropped item isn't grabbed instantly.")]
    [SerializeField] private float armDelay = 1f;
    [Tooltip("On: walking into the trigger picks it up. Off: only Interact (E) picks it up.")]
    [SerializeField] private bool pickupOnTouch = true;

    // 0 = unclaimed, otherwise (clientId + 1) - same convention VehicleEnterExit's seat
    // occupants use, so client 0 (the host) is still distinguishable from "unclaimed".
    private readonly NetworkVariable<ulong> _claimedBy = new NetworkVariable<ulong>(0);

    private float _spawnTime;

    public InventoryItem Item => item;
    public bool IsArmed => Time.time - _spawnTime >= armDelay;

    void Start() => _spawnTime = Time.time;

    public override void OnNetworkSpawn()
    {
        _claimedBy.OnValueChanged += OnClaimedChanged;

        // Late joiner: the claim already happened before we existed, so OnValueChanged above
        // won't fire for it - the NetworkVariable's initial sync already carries the value.
        if (_claimedBy.Value != 0) OnClaimedChanged(0, _claimedBy.Value);
    }

    public override void OnNetworkDespawn() => _claimedBy.OnValueChanged -= OnClaimedChanged;

    void OnTriggerEnter(Collider other)
    {
        if (!pickupOnTouch) return;
        if (!other.CompareTag("Player")) return;

        Hotbar hotbar = other.GetComponentInParent<Hotbar>();
        if (hotbar == null) hotbar = other.GetComponentInChildren<Hotbar>();
        TryPickup(hotbar);
    }

    /// <summary>Puts the item in the first empty hotbar slot and removes it from the world.</summary>
    public bool TryPickup(Hotbar hotbar)
    {
        if (item == null || hotbar == null) return false;
        if (!IsArmed) return false;
        if (!hotbar.HasFreeSlot()) return false;

        // Singleplayer is never network-spawned (same convention as Hotbar/VehicleEnterExit/
        // FireworkFlareLauncher) - grant and remove locally, no server round-trip to wait on.
        if (IsSpawned)
        {
            if (_claimedBy.Value != 0) return false; // someone already claimed it
            ClaimRpc();
        }
        else
        {
            if (!hotbar.AddItem(item)) return false;
            Destroy(gameObject);
        }
        return true;
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    void ClaimRpc(RpcParams rpcParams = default)
    {
        if (_claimedBy.Value != 0) return; // someone else's claim already landed first
        _claimedBy.Value = rpcParams.Receive.SenderClientId + 1;
    }

    void OnClaimedChanged(ulong previous, ulong current)
    {
        if (current == 0) return;

        // Only the winning client actually receives the item - everyone else (whoever lost the
        // race, plus every other spectator) just sees the world object disappear below.
        if (current - 1 == NetworkManager.Singleton.LocalClientId)
        {
            NetworkObject localPlayer = NetworkManager.Singleton.LocalClient?.PlayerObject;
            Hotbar hotbar = localPlayer != null ? localPlayer.GetComponentInChildren<Hotbar>() : null;
            hotbar?.AddItem(item);
        }

        // Only the server despawns - clients can't Destroy() a spawned NetworkObject directly,
        // and the despawn message removes it everywhere once the server does.
        if (IsServer && NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn();
    }
}
