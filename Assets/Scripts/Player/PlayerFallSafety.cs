using System.Collections.Generic;
using System.Linq;
using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// Safety net: if the player this machine controls ends up under the terrain (or below the map),
/// put them back on the last ground they stood on. Creates itself when the game starts, so no
/// scene or prefab needs it. Each machine only moves its own player; the capsule's NetworkTransform
/// is owner-authoritative, so Teleport() syncs the jump to everyone else without a slide.
/// </summary>
public class PlayerFallSafety : MonoBehaviour
{
    const float BelowTerrain = 3f;     // this far under the terrain surface counts as fallen through
    const float AbsoluteFloor = -50f;  // anywhere (also off the terrain), below this counts too
    const float SafeSampleEvery = 0.25f;
    const float TerrainTop = 100f;     // start of the ray when searching from scratch (above any hill)

    class Tracked
    {
        public CharacterController cc;
        public Vector3 lastSafe;
        public bool hasSafe;
        public float nextSample;
    }

    readonly List<Tracked> _players = new List<Tracked>();
    float _nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindFirstObjectByType<PlayerFallSafety>() != null) return;
        var go = new GameObject(nameof(PlayerFallSafety));
        DontDestroyOnLoad(go);
        go.AddComponent<PlayerFallSafety>();
    }

    void Update()
    {
        if (Time.time >= _nextScan) Scan();

        foreach (var p in _players)
        {
            // Disabled while seated in a vehicle (VehicleEnterExit) - nothing to rescue then.
            if (p.cc == null || !p.cc.enabled) continue;
            // A tilted player (e.g. left upside down by a vehicle) has its capsule off to the side of or
            // below the feet and sinks into the ground: stand them back up first.
            if (p.cc.transform.up.y < 0.99f) StandUpright(p);

            var pos = p.cc.transform.position;
            bool onTerrain = TryTerrainHeight(pos, out float ground);

            if ((onTerrain && pos.y < ground - BelowTerrain) || pos.y < AbsoluteFloor)
            {
                Rescue(p, pos);
                continue;
            }

            if (Time.time >= p.nextSample && p.cc.isGrounded && (!onTerrain || pos.y > ground - 0.5f))
            {
                p.lastSafe = pos;
                p.hasSafe = true;
                p.nextSample = Time.time + SafeSampleEvery;
            }
        }
    }

    // Owned players only: online that's this client's player, offline every (the one) player.
    void Scan()
    {
        _nextScan = Time.time + 1f;
        _players.RemoveAll(p => p.cc == null);
        foreach (var fpc in FindObjectsByType<StarterAssets.FirstPersonController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var cc = fpc.GetComponent<CharacterController>();
            if (cc == null || _players.Any(p => p.cc == cc)) continue;
            if (PlayerSpawnManager.IsNetworked)
            {
                var setup = fpc.GetComponentInParent<PlayerNetworkSetup>();
                if (setup == null || !setup.IsSpawned || !setup.IsOwner) continue;
            }
            _players.Add(new Tracked { cc = cc });
        }
    }

    void Rescue(Tracked p, Vector3 from)
    {
        float r = p.cc.radius, h = p.cc.height;
        var body = p.cc.transform;
        Vector3 target;
        bool found =
            // Short ray at the last standing spot (stays under roofs), then from high over where they fell.
            (p.hasSafe && SafeGround.TryGroundAt(p.lastSafe, p.lastSafe.y, body, r, h, out target, 1.5f)) ||
            SafeGround.TryGroundAt(from, TerrainTop, body, r, h, out target) ||
            SafeGround.TryNearestSpawn(from, r, h, out target);
        if (!found)
        {
            if (!p.hasSafe) return;
            target = p.lastSafe;
        }

        Debug.LogWarning($"PlayerFallSafety: {body.root.name} fell to {from:F1}, moved back to {target:F1}.", body);
        p.cc.enabled = false;
        var nt = body.GetComponent<NetworkTransform>();
        var upright = SafeGround.Upright(body);
        if (nt != null && nt.IsSpawned && nt.CanCommitToTransform)
        {
            var local = nt.InLocalSpace && body.parent != null ? body.parent.InverseTransformPoint(target) : target;
            var rot = nt.InLocalSpace && body.parent != null ? Quaternion.Inverse(body.parent.rotation) * upright : upright;
            nt.Teleport(local, rot, body.localScale);
        }
        else body.SetPositionAndRotation(target, upright);
        Physics.SyncTransforms();
        p.cc.enabled = true;
    }

    // Keep the heading, drop any pitch/roll. NetworkTransform syncs the rotation to everyone else.
    static void StandUpright(Tracked p)
    {
        var body = p.cc.transform;
        Debug.LogWarning($"PlayerFallSafety: {body.root.name} was tilted (up {body.up:F2}), stood upright.", body);
        p.cc.enabled = false;
        body.rotation = SafeGround.Upright(body);
        Physics.SyncTransforms();
        p.cc.enabled = true;
    }

    static bool TryTerrainHeight(Vector3 pos, out float height)
    {
        foreach (var t in Terrain.activeTerrains)
        {
            var o = t.transform.position;
            var size = t.terrainData.size;
            if (pos.x < o.x || pos.z < o.z || pos.x > o.x + size.x || pos.z > o.z + size.z) continue;
            height = t.SampleHeight(pos) + o.y;
            return true;
        }
        height = 0f;
        return false;
    }
}
