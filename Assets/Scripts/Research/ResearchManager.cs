using System.Collections.Generic;
using Microlight.MicroBar;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

/// <summary>Persistent HUD bar tracking total cloud research progress - shared across every
/// connected player. A reading/scan/pickup anywhere reports a gain to the host, which adds it to
/// one pooled total and broadcasts the new value to everyone. Same NGO named-message pattern as
/// RoundManager (not NetworkVariable/RPCs) since there's no NetworkObject to hang those off of -
/// ResearchManager still lives on the player prefab, not a spawned network object.</summary>
public class ResearchManager : NetworkBehaviour
{
    public static ResearchManager Instance { get; private set; }

    const string MsgGain = "Research.Gain";     // client -> host
    const string MsgCredit = "Research.Credit"; // host -> all

    /// <summary>What earned the research. Anemometer readings have no world object to dedupe
    /// against (sourceIndex stays -1); Scan/Pickup identify a specific scene object so the same
    /// cloud or coin can't be credited twice.</summary>
    public enum Source : byte { Anemometer, Scan, Pickup }

    [SerializeField] private MicroBar researchBar;
    [SerializeField] private float maxResearch = 100f;

    [Tooltip("Fires once, the moment the research bar reaches its max value.")]
    public UnityEvent onResearchComplete;

    /// <summary>Fires on every machine (host included) once a Scan/Pickup source is confirmed
    /// consumed, so the matching world object can apply its own visual (hide the coin, mark the
    /// cloud scanned) even on machines that never looked at it.</summary>
    public event System.Action<Source, int> onSourceConsumed;

    static bool IsNetworked => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    static bool IsAuthority => !IsNetworked || NetworkManager.Singleton.IsServer;

    // Mirrored on every machine (not just the host) from ApplyGain/OnCreditMessage, so a client
    // can answer "has this already been consumed?" immediately instead of only via the event.
    readonly HashSet<int> _consumedScans = new HashSet<int>();
    readonly HashSet<int> _consumedPickups = new HashSet<int>();

    private bool _completed;
    NetworkManager _registeredOn;

    public bool IsComplete => _completed;

    /// <summary>How full the research bar is, 0 to 1.</summary>
    public float Progress => researchBar != null && researchBar.MaxValue > 0f ? Mathf.Clamp01(researchBar.CurrentValue / researchBar.MaxValue) : 0f;

    // Every spawned player copy (the local owner AND every remote ghost) carries this component,
    // so this has to decide "is this actually mine?" somewhere ownership is guaranteed correct.
    // Awake()/Start() both run too early for that - Unity's own Start dispatch can beat NGO's
    // OnNetworkSpawn on the ghost's side depending on spawn order, which let a ghost's copy claim
    // the singleton and destroy the real, locally-owned one before it ever initialized. NGO
    // guarantees IsOwner is already correct by the time OnNetworkSpawn runs, same as
    // PlayerNetworkSetup already relies on for the camera - so a non-owned copy just disables
    // itself here, unconditionally, instead of racing for the claim at all.
    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        Instance = this;

        if (researchBar == null)
        {
            Debug.LogError("ResearchManager: researchBar is not assigned.", this);
            return;
        }

        researchBar.Initialize(maxResearch);
        researchBar.UpdateBar(0f, true);
    }

    void Update() => Register();

    void OnDestroy()
    {
        Unregister();
        if (Instance == this) Instance = null;
    }

    /// <summary>True once this exact Scan/Pickup source has already been credited - lets a
    /// newly-subscribed world object catch up immediately instead of waiting on the next event.</summary>
    public bool IsConsumed(Source source, int sourceIndex) =>
        sourceIndex >= 0 && ConsumedSetFor(source).Contains(sourceIndex);

    /// <summary>Call from any source (anemometer reading, binocular scan, pickup). `sourceIndex`
    /// is only needed for world objects that must not be credited twice (Scan, Pickup) - leave it
    /// -1 for a reading with no object to dedupe against.</summary>
    public void AddResearch(float amount, Source source = Source.Anemometer, int sourceIndex = -1)
    {
        if (researchBar == null || _completed) return;

        if (IsAuthority) ApplyGain(source, amount, sourceIndex);
        else SendGain(source, amount, sourceIndex);
    }

    // ── Host ─────────────────────────────────────────────────────────────────

    void ApplyGain(Source source, float amount, int sourceIndex)
    {
        if (_completed) return;

        // Already credited by whoever else reported it first - ignore, don't double count.
        if (sourceIndex >= 0 && !ConsumedSetFor(source).Add(sourceIndex)) return;

        researchBar.UpdateBar(researchBar.CurrentValue + amount, UpdateAnim.Heal);
        bool justCompleted = !_completed && researchBar.CurrentValue >= researchBar.MaxValue;
        if (justCompleted) _completed = true;

        BroadcastCredit(source, sourceIndex);
        if (sourceIndex >= 0) onSourceConsumed?.Invoke(source, sourceIndex);
        if (justCompleted) onResearchComplete?.Invoke();
    }

    HashSet<int> ConsumedSetFor(Source source) => source == Source.Pickup ? _consumedPickups : _consumedScans;

    void BroadcastCredit(Source source, int sourceIndex)
    {
        if (!IsNetworked) return;
        using var w = new FastBufferWriter(32, Allocator.Temp);
        w.WriteValueSafe(researchBar.CurrentValue);
        w.WriteValueSafe(_completed);
        w.WriteValueSafe((byte)source);
        w.WriteValueSafe(sourceIndex);
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll(MsgCredit, w, NetworkDelivery.ReliableSequenced);
    }

    // Late joiners: catch them up on the current total plus every already-consumed Scan/Pickup,
    // so their bar and world objects start in sync instead of empty/unscanned.
    void OnClientConnected(ulong id)
    {
        if (!NetworkManager.Singleton.IsServer || id == NetworkManager.ServerClientId) return;

        SendCreditTo(id, Source.Anemometer, -1);
        foreach (int i in _consumedScans) SendCreditTo(id, Source.Scan, i);
        foreach (int i in _consumedPickups) SendCreditTo(id, Source.Pickup, i);
    }

    void SendCreditTo(ulong id, Source source, int sourceIndex)
    {
        using var w = new FastBufferWriter(32, Allocator.Temp);
        w.WriteValueSafe(researchBar.CurrentValue);
        w.WriteValueSafe(_completed);
        w.WriteValueSafe((byte)source);
        w.WriteValueSafe(sourceIndex);
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(MsgCredit, id, w, NetworkDelivery.ReliableSequenced);
    }

    // ── Client ───────────────────────────────────────────────────────────────

    void SendGain(Source source, float amount, int sourceIndex)
    {
        using var w = new FastBufferWriter(32, Allocator.Temp);
        w.WriteValueSafe((byte)source);
        w.WriteValueSafe(amount);
        w.WriteValueSafe(sourceIndex);
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(MsgGain, NetworkManager.ServerClientId, w, NetworkDelivery.ReliableSequenced);
    }

    void OnCreditMessage(ulong sender, FastBufferReader r)
    {
        r.ReadValueSafe(out float newTotal);
        r.ReadValueSafe(out bool completed);
        r.ReadValueSafe(out byte source);
        r.ReadValueSafe(out int sourceIndex);

        if (sourceIndex >= 0) ConsumedSetFor((Source)source).Add(sourceIndex);

        researchBar.UpdateBar(newTotal, UpdateAnim.Heal);
        bool justCompleted = completed && !_completed;
        _completed = completed;

        if (sourceIndex >= 0) onSourceConsumed?.Invoke((Source)source, sourceIndex);
        if (justCompleted) onResearchComplete?.Invoke();
    }

    // ── Network plumbing (same registration pattern as RoundManager) ───────

    void Register()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening || nm.CustomMessagingManager == null) { if (nm != _registeredOn) Unregister(); return; }
        if (_registeredOn == nm) return;
        Unregister();
        _registeredOn = nm;
        var cmm = nm.CustomMessagingManager;
        if (nm.IsServer)
        {
            cmm.RegisterNamedMessageHandler(MsgGain, OnGainMessage);
            nm.OnClientConnectedCallback += OnClientConnected;
        }
        else
        {
            cmm.RegisterNamedMessageHandler(MsgCredit, OnCreditMessage);
        }
    }

    void Unregister()
    {
        var nm = _registeredOn;
        _registeredOn = null;
        if (nm == null) return;
        nm.OnClientConnectedCallback -= OnClientConnected;
        var cmm = nm.CustomMessagingManager;
        if (cmm == null) return;
        cmm.UnregisterNamedMessageHandler(MsgGain);
        cmm.UnregisterNamedMessageHandler(MsgCredit);
    }

    void OnGainMessage(ulong sender, FastBufferReader r)
    {
        r.ReadValueSafe(out byte source);
        r.ReadValueSafe(out float amount);
        r.ReadValueSafe(out int sourceIndex);
        ApplyGain((Source)source, amount, sourceIndex);
    }
}
