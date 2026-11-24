using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Freeform polygon spawn zone. Add empty child GameObjects as corner points — their order in the
/// Hierarchy is the polygon's edge order — and drag them around in the Scene view to outline any
/// shape. No code changes needed to reshape it, just move/add/remove the child points.
///
/// To carve out a no-spawn area (e.g. the middle of the map), add an empty GameObject to
/// `exclusionZones`, give it its own child corner points the same way, and position them around
/// the area to avoid. A candidate spawn point is rerolled if it lands inside any exclusion zone.
/// </summary>
public class TornadoSpawnArea : MonoBehaviour
{
    [Tooltip("No-spawn areas inside the main outline. Each entry is its own container with its own " +
             "child corner points, set up the same way as this object.")]
    public List<Transform> exclusionZones = new List<Transform>();

    [Tooltip("Height above the ground to start the downward raycast that finds spawn height.")]
    public float raycastHeight = 200f;

    [Tooltip("Layers considered ground when snapping a spawn point to terrain height.")]
    public LayerMask groundMask = ~0;

    [Tooltip("Y position used if the ground raycast doesn't hit anything.")]
    public float fallbackHeight = 0f;

    /// <summary>Picks a random point inside the polygon (XZ) and outside any exclusion zone, with Y
    /// snapped to the ground below it.</summary>
    public Vector3 GetRandomPoint()
    {
        Vector3[] points = GetPointsFromChildren(transform);
        if (points.Length < 3)
        {
            Debug.LogWarning("TornadoSpawnArea needs at least 3 child points to form an area.", this);
            return transform.position;
        }

        Vector3 xz = RandomPointInArea(points);
        return new Vector3(xz.x, SnapToGround(xz), xz.z);
    }

    static Vector3[] GetPointsFromChildren(Transform container)
    {
        var points = new Vector3[container.childCount];
        for (int i = 0; i < container.childCount; i++)
            points[i] = container.GetChild(i).position;
        return points;
    }

    // Rejection sampling: pick a random point in the polygon's bounding box, keep it if it's
    // inside the polygon and outside every exclusion zone, try again otherwise.
    Vector3 RandomPointInArea(Vector3[] points)
    {
        float minX = points[0].x, maxX = points[0].x;
        float minZ = points[0].z, maxZ = points[0].z;
        foreach (var p in points)
        {
            minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
            minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z);
        }

        for (int attempt = 0; attempt < 50; attempt++)
        {
            var candidate = new Vector3(Random.Range(minX, maxX), 0f, Random.Range(minZ, maxZ));
            if (IsInside(points, candidate) && !IsInsideAnyExclusion(candidate))
                return candidate;
        }

        // Exclusion zones cover most of the area, or it's a thin/concave shape — centroid is a
        // safe (if imperfect) fallback rather than spawning nowhere.
        Debug.LogWarning("TornadoSpawnArea: couldn't find a spot outside exclusion zones after 50 tries " +
                          "— falling back to the area's centroid.", this);
        return Centroid(points);
    }

    bool IsInsideAnyExclusion(Vector3 point)
    {
        foreach (var zone in exclusionZones)
        {
            if (zone == null) continue;

            Vector3[] zonePoints = GetPointsFromChildren(zone);
            if (zonePoints.Length >= 3 && IsInside(zonePoints, point))
                return true;
        }
        return false;
    }

    // Standard even-odd ray casting point-in-polygon test (PNPOLY), using X/Z in place of X/Y.
    static bool IsInside(Vector3[] points, Vector3 point)
    {
        bool inside = false;
        for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
        {
            bool straddles = (points[i].z > point.z) != (points[j].z > point.z);
            if (!straddles) continue;

            float edgeX = points[i].x + (points[j].x - points[i].x)
                        * (point.z - points[i].z) / (points[j].z - points[i].z);
            if (point.x < edgeX) inside = !inside;
        }
        return inside;
    }

    static Vector3 Centroid(Vector3[] points)
    {
        Vector3 sum = Vector3.zero;
        foreach (var p in points) sum += p;
        return sum / points.Length;
    }

    float SnapToGround(Vector3 xzPoint)
    {
        Vector3 origin = new Vector3(xzPoint.x, raycastHeight, xzPoint.z);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, raycastHeight * 2f, groundMask))
            return hit.point.y;

        return fallbackHeight;
    }

    void OnDrawGizmos()
    {
        DrawOutline(transform, new Color(1f, 0.5f, 0f, 0.8f));

        foreach (var zone in exclusionZones)
        {
            if (zone != null) DrawOutline(zone, new Color(1f, 0.1f, 0.1f, 0.8f));
        }
    }

    static void DrawOutline(Transform container, Color color)
    {
        Vector3[] points = GetPointsFromChildren(container);
        if (points.Length < 2) return;

        Gizmos.color = color;
        for (int i = 0; i < points.Length; i++)
        {
            Vector3 a = points[i];
            Vector3 b = points[(i + 1) % points.Length];
            Gizmos.DrawLine(a, b);
            Gizmos.DrawSphere(a, 0.5f);
        }
    }
}
