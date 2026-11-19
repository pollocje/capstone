using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

/// <summary>
/// Keeps a dropped world item reachable, so a key item (the barrel that kills the tornado) can't
/// be lost for the rest of the round. Put it on the item's drop prefab next to ItemPickup.
///
/// While the item rests on the ground it remembers that spot. If it then ends up somewhere no
/// player can get it - under the terrain, off the map, or resting high up (flung onto a roof or
/// tree by the tornado) - it's put back at the last safe spot (or where it was dropped).
///
/// Server-only, like the rest of the item's physics (its NetworkTransform is server-authoritative);
/// clients just see the synced move. Runs locally in singleplayer.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class WorldItemRecovery : MonoBehaviour
{
    [Header("Lost checks")]
    [Tooltip("Recover if the item is this far below the terrain surface (m).")]
    public float maxDepthBelowGround = 2f;
    [Tooltip("Recover below this world height no matter what (m) - catches falling out of the world.")]
    public float killY = -100f;
    [Tooltip("Recover if the item leaves every terrain (off the edge of the map).")]
    public bool recoverOffMap = true;
    [Tooltip("Recover if it comes to rest more than this high above the terrain (roof, tree). 0 = off.")]
    public float maxRestHeightAboveGround = 4f;
    [Tooltip("Seconds it must sit up high before counting as stuck (so it can still roll/fall off).")]
    public float stuckHighSeconds = 5f;

    [Header("Safe spot tracking")]
    [Tooltip("Below this speed (m/s) the item counts as resting.")]
    public float restSpeed = 0.3f;
    [Tooltip("Seconds between checks.")]
    public float checkInterval = 0.5f;
    [Tooltip("How high above the safe spot it's put back (m), so it drops onto the ground.")]
    public float recoveryLift = 1f;

    Rigidbody _rb;
    NetworkTransform _netTransform;
    Vector3 _safePos;
    Quaternion _safeRot;
    float _nextCheck, _highSince = -1f;

    /// <summary>How many times this item has been recovered (for tests/debugging).</summary>
    public int Recoveries { get; private set; }

    static bool IsAuthority =>
        NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _netTransform = GetComponent<NetworkTransform>();
        // Fallback until it has rested somewhere: where it was dropped (right in front of the player).
        _safePos = transform.position;
        _safeRot = transform.rotation;
    }

    void FixedUpdate()
    {
        if (!IsAuthority || Time.time < _nextCheck) return;
        _nextCheck = Time.time + checkInterval;

        // A scene without terrain has no "ground" or "map" to compare against - only killY applies.
        if (Terrain.activeTerrains.Length == 0)
        {
            if (_rb.position.y < killY) Recover();
            return;
        }

        Vector3 p = _rb.position;
        bool onMap = TryGroundHeight(p, out float ground);
        bool resting = _rb.linearVelocity.magnitude < restSpeed;
        float aboveGround = onMap ? p.y - ground : 0f;

        if (p.y < killY || (onMap && aboveGround < -maxDepthBelowGround) || (recoverOffMap && !onMap))
        {
            Recover();
            return;
        }

        bool high = maxRestHeightAboveGround > 0f && resting && aboveGround > maxRestHeightAboveGround;
        if (high)
        {
            if (_highSince < 0f) _highSince = Time.time;
            if (Time.time - _highSince >= stuckHighSeconds) Recover();
            return;
        }
        _highSince = -1f;

        if (resting && onMap && aboveGround >= -0.5f)
        {
            _safePos = p;
            _safeRot = _rb.rotation;
        }
    }

    void Recover()
    {
        Vector3 target = _safePos + Vector3.up * recoveryLift;
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;

        // Teleport (not a normal move) so clients snap instead of interpolating through the ground.
        if (_netTransform != null && _netTransform.IsSpawned)
            _netTransform.Teleport(target, _safeRot, transform.localScale);
        else
        {
            transform.SetPositionAndRotation(target, _safeRot);
            _rb.position = target;
            _rb.rotation = _safeRot;
        }

        _highSince = -1f;
        Recoveries++;
        Debug.Log($"WorldItemRecovery: '{name}' was out of reach - moved back to {target:F1}.", this);
    }

    /// <summary>Terrain surface height under p, false if p isn't over any terrain.</summary>
    static bool TryGroundHeight(Vector3 p, out float height)
    {
        foreach (var t in Terrain.activeTerrains)
        {
            Vector3 o = t.GetPosition(), size = t.terrainData.size;
            if (p.x < o.x || p.z < o.z || p.x > o.x + size.x || p.z > o.z + size.z) continue;
            height = t.SampleHeight(p) + o.y;
            return true;
        }
        height = 0f;
        return false;
    }

    /// <summary>Test hook: runs the next check on the next physics step instead of waiting.</summary>
    public void CheckNow() => _nextCheck = 0f;
}
