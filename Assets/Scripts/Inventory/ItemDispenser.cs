using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Walk up, press E, receive a random item from a shared pool. Each item in the list can
/// only be handed out once across all players.
///
/// The server owns the pool: clients ask for an item, the server picks and removes one,
/// then tells only the requesting client which item it got. Items are sent as indices into
/// the `items` list because ScriptableObjects can't be sent over the network. The list is
/// serialized into the scene, so it's the same on every machine.
///
/// SETUP:
/// 1. Object needs a NetworkObject and a trigger collider (sized for the "walk up" range)
/// 2. Drag InventoryItem assets into `items`, one entry per item available
/// 3. Optionally assign prompt objects ("Press E" / "Empty")
/// </summary>
public class ItemDispenser : NetworkBehaviour
{
    [Header("Items")]
    [Tooltip("Every item this dispenser can give out. Each entry is handed out once.")]
    [SerializeField] private List<InventoryItem> items = new List<InventoryItem>();

    [Header("UI")]
    [SerializeField] private GameObject usePromptUI;
    [SerializeField] private GameObject emptyPromptUI;
    [SerializeField] AudioSource coinSource;
    [SerializeField] AudioClip coinSound;

    // Server-only: indices into `items` that haven't been handed out yet
    private readonly List<int> _remaining = new List<int>();

    // Synced so every client can show "Press E" vs "Empty" without asking the server
    private readonly NetworkVariable<int> _remainingCount = new NetworkVariable<int>();

    private bool playerInRange;
    private Hotbar playerHotbar;
    private bool requestPending;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            _remaining.Clear();
            for (int i = 0; i < items.Count; i++)
                if (items[i] != null) _remaining.Add(i);

            _remainingCount.Value = _remaining.Count;
        }

        _remainingCount.OnValueChanged += OnRemainingCountChanged;
        RefreshPrompt();
    }

    public override void OnNetworkDespawn()
    {
        _remainingCount.OnValueChanged -= OnRemainingCountChanged;
    }

    private void Start()
    {
        if (usePromptUI != null) usePromptUI.SetActive(false);
        if (emptyPromptUI != null) emptyPromptUI.SetActive(false);
    }

    // ── Range detection (local player only) ─────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // In multiplayer, ignore players we don't own
        var netObj = other.transform.root.GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned && !netObj.IsOwner) return;

        playerHotbar = other.transform.root.GetComponentInChildren<Hotbar>(true);
        playerInRange = true;
        RefreshPrompt();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        var netObj = other.transform.root.GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned && !netObj.IsOwner) return;

        playerInRange = false;
        RefreshPrompt();
    }

    private void Update()
    {
        if (!playerInRange || requestPending || !IsSpawned) return;
        if (!Input.GetKeyDown(KeyCode.E)) return;

        if (playerHotbar == null || _remainingCount.Value <= 0) return;

        // Don't pull an item out of the shared pool if we have nowhere to put it
        if (!playerHotbar.HasFreeSlot()) return;

        requestPending = true;
        RequestItemRpc();
    }

    // ── Networking ───────────────────────────────────────────────────────────

    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void RequestItemRpc(RpcParams rpcParams = default)
    {
        //AUDIO QUEUE - SEBASTION 
        coinSource.PlayOneShot(coinSound);
        //Plays every time vending machine is used 

        ulong sender = rpcParams.Receive.SenderClientId;

        if (_remaining.Count == 0)
        {
            DenyItemRpc(RpcTarget.Single(sender, RpcTargetUse.Temp));
            return;
        }

        int pick = Random.Range(0, _remaining.Count);
        int itemIndex = _remaining[pick];
        _remaining.RemoveAt(pick);
        _remainingCount.Value = _remaining.Count;

        GiveItemRpc(itemIndex, RpcTarget.Single(sender, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void GiveItemRpc(int itemIndex, RpcParams rpcParams)
    {
        requestPending = false;

        if (itemIndex < 0 || itemIndex >= items.Count) return;

        // Hotbar could have filled while the request was in flight — put the item back
        if (playerHotbar == null || !playerHotbar.AddItem(items[itemIndex]))
            ReturnItemRpc(itemIndex);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void DenyItemRpc(RpcParams rpcParams)
    {
        requestPending = false;
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void ReturnItemRpc(int itemIndex)
    {
        // Only accept real items that are actually checked out
        if (itemIndex < 0 || itemIndex >= items.Count || items[itemIndex] == null) return;
        if (_remaining.Contains(itemIndex)) return;

        _remaining.Add(itemIndex);
        _remainingCount.Value = _remaining.Count;
    }

    // ── UI ───────────────────────────────────────────────────────────────────

    private void OnRemainingCountChanged(int previous, int current)
    {
        RefreshPrompt();
    }

    private void RefreshPrompt()
    {
        bool empty = _remainingCount.Value <= 0;

        if (usePromptUI != null) usePromptUI.SetActive(playerInRange && !empty);
        if (emptyPromptUI != null) emptyPromptUI.SetActive(playerInRange && empty);
    }
}
