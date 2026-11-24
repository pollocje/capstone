using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Finds a spot where a standing player fits on walkable ground. Used by VehicleEnterExit when
/// getting out (the truck may be on its side or upside down) and by PlayerFallSafety.
/// Positions are feet positions (CharacterController transform on PlayerCapsule).
/// </summary>
public static class SafeGround
{
    const float ProbeAbove = 30f;    // rays start this far above the highest thing we care about
    const float MinUpNormal = 0.6f;  // ~53 deg: steeper than this isn't somewhere to stand
    const float Clearance = 1.0f;    // gap between the vehicle's body and an exit spot (m)

    /// <summary>
    /// Exit spots around a vehicle, laid out from its flattened heading so its roll/pitch doesn't
    /// matter: right, left, behind, in front, then the diagonals, then a wider ring.
    /// </summary>
    public static bool TryFindExit(Transform vehicle, float radius, float height, out Vector3 pos)
    {
        var bounds = SolidBounds(vehicle);
        var forward = Vector3.ProjectOnPlane(vehicle.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.ProjectOnPlane(vehicle.up, Vector3.up); // nose straight up/down
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        forward.Normalize();
        var right = Vector3.Cross(Vector3.up, forward);

        float reach = Mathf.Max(bounds.extents.x, bounds.extents.z) + radius + Clearance;
        var dirs = new[]
        {
            right, -right, -forward, forward,
            (right - forward).normalized, (-right - forward).normalized, (right + forward).normalized, (-right + forward).normalized,
        };
        foreach (float r in new[] { reach, reach + 3f })
            foreach (var d in dirs)
                if (TryGroundAt(bounds.center + d * r, bounds.max.y, vehicle, radius, height, out pos))
                    return true;

        pos = default;
        return false;
    }

    /// <summary>
    /// Ground under (x, z) with room for the player, ignoring triggers and anything under <paramref name="ignore"/>.
    /// The ray starts <paramref name="probeAbove"/> over <paramref name="topY"/>; keep it short to stay under a roof.
    /// </summary>
    public static bool TryGroundAt(Vector3 at, float topY, Transform ignore, float radius, float height, out Vector3 pos, float probeAbove = ProbeAbove)
    {
        var origin = new Vector3(at.x, Mathf.Max(topY, at.y) + probeAbove, at.z);
        var hits = Physics.RaycastAll(origin, Vector3.down, probeAbove + 600f, ~0, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits.OrderBy(h => h.distance))
        {
            if (ignore != null && hit.collider.transform.IsChildOf(ignore)) continue;
            if (hit.normal.y < MinUpNormal) break; // first real surface is a wall/slope: not this spot
            pos = hit.point + Vector3.up * 0.05f;
            if (Fits(pos, ignore, radius, height)) return true;
            break;
        }
        pos = default;
        return false;
    }

    /// <summary>True when a standing capsule at these feet overlaps nothing solid (except <paramref name="ignore"/>).</summary>
    public static bool Fits(Vector3 feet, Transform ignore, float radius, float height)
    {
        var bottom = feet + Vector3.up * (radius + 0.1f);
        var top = feet + Vector3.up * Mathf.Max(radius + 0.1f, height - radius);
        return Physics.OverlapCapsule(bottom, top, radius * 0.95f, ~0, QueryTriggerInteraction.Ignore)
            .All(c => ignore != null && c.transform.IsChildOf(ignore));
    }

    /// <summary>Yaw-only rotation facing the way <paramref name="t"/> faces on the ground plane (upright, never tilted).</summary>
    public static Quaternion Upright(Transform t)
    {
        var f = Vector3.ProjectOnPlane(t.forward, Vector3.up);
        if (f.sqrMagnitude < 0.01f) f = Vector3.ProjectOnPlane(t.up, Vector3.up);
        return f.sqrMagnitude < 0.01f ? Quaternion.identity : Quaternion.LookRotation(f.normalized, Vector3.up);
    }

    /// <summary>Nearest SpawnPoint, snapped to the ground when possible. Last-resort fallback.</summary>
    public static bool TryNearestSpawn(Vector3 from, float radius, float height, out Vector3 pos)
    {
        var points = Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None)
            .OrderBy(p => (p.transform.position - from).sqrMagnitude);
        foreach (var p in points)
        {
            if (TryGroundAt(p.transform.position, p.transform.position.y, null, radius, height, out pos)) return true;
        }
        var first = points.FirstOrDefault();
        pos = first != null ? first.transform.position : default;
        return first != null;
    }

    static Bounds SolidBounds(Transform root)
    {
        var cols = root.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger).ToList();
        if (cols.Count == 0) return new Bounds(root.position, Vector3.one * 2f);
        var b = cols[0].bounds;
        foreach (var c in cols) b.Encapsulate(c.bounds);
        return b;
    }
}
