using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Implement on any component of a Grabbable's GameObject to make the held item stop
/// swinging while some condition is true (e.g. an item that should hold still while it is being used).
/// </summary>
public interface IGrabSwingSuppressor
{
    bool SuppressSwing { get; }
}

/// <summary>
/// Marks a physics item as something the player can physically grab and carry with
/// PlayerGrabController (separate from ItemPickup's walk-into-it hotbar pickup).
/// Put this on the GameObject that holds the Rigidbody. The item needs at least one
/// non-trigger collider so it can bump into the world while held.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Grabbable : MonoBehaviour
{
    [Tooltip("Attach the joint where the player's ray hit the item, so it hangs and swings from that point. Off: attach at the center.")]
    [SerializeField] private bool grabAtHitPoint = true;

    [Tooltip("Turn off this object's trigger colliders (e.g. ItemPickup's) while held, so carrying it doesn't put it straight into the hotbar.")]
    [SerializeField] private bool disableTriggersWhileHeld = true;

    public event Action<PlayerGrabController> Grabbed;
    public event Action<PlayerGrabController> Released;

    public Rigidbody Body { get; private set; }
    public PlayerGrabController Holder { get; private set; }
    public bool IsHeld => Holder != null;
    public bool GrabAtHitPoint => grabAtHitPoint;

    private readonly List<Collider> _solidColliders = new List<Collider>();
    private readonly List<Collider> _triggerColliders = new List<Collider>();
    private IGrabSwingSuppressor[] _suppressors;

    public IReadOnlyList<Collider> SolidColliders => _solidColliders;

    /// <summary>True while any IGrabSwingSuppressor on this object asks for a steady hold.</summary>
    public bool SuppressSwing
    {
        get
        {
            foreach (var s in _suppressors)
                if (s.SuppressSwing) return true;
            return false;
        }
    }

    void Awake()
    {
        Body = GetComponent<Rigidbody>();
        _suppressors = GetComponents<IGrabSwingSuppressor>();

        foreach (var col in GetComponentsInChildren<Collider>(true))
        {
            if (col.attachedRigidbody != Body) continue;
            (col.isTrigger ? _triggerColliders : _solidColliders).Add(col);
        }

        if (_solidColliders.Count == 0)
            Debug.LogWarning($"Grabbable '{name}' has no non-trigger collider — it can't collide with the world while held.", this);
    }

    // Called by PlayerGrabController only.
    // NETWORK: Holder should become a server-owned NetworkVariable (holder's client id) so two
    // players can't grab the same item, and so late joiners know who is carrying it.
    internal void NotifyGrabbed(PlayerGrabController holder)
    {
        Holder = holder;
        if (disableTriggersWhileHeld)
            foreach (var col in _triggerColliders) col.enabled = false;
        Grabbed?.Invoke(holder);
    }

    internal void NotifyReleased(PlayerGrabController holder)
    {
        if (Holder != holder) return;
        Holder = null;
        if (disableTriggersWhileHeld)
            foreach (var col in _triggerColliders) if (col != null) col.enabled = true;
        Released?.Invoke(holder);
    }

    void OnDisable()
    {
        // Item despawned/destroyed while carried — let the holder clean up its joint.
        if (Holder != null) Holder.Release();
    }
}
