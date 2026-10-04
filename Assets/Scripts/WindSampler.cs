using System.Collections.Generic;
using UnityEngine;

public static class WindSampler
{
    static readonly List<WindZone> zones = new List<WindZone>();
    static bool cached = false;

    public static void Refresh()
    {
        cached = false;
    }

    public static bool TryGetWindAt(Vector3 worldPosition, out float speed, out Vector2 directionXZ)
    {
        EnsureCached();

        speed = 0f;
        directionXZ = Vector2.zero;

        foreach (WindZone zone in zones)
        {
            if (zone == null) continue;

            float zoneSpeed;
            Vector2 zoneDirection;

            if (zone.mode == WindZoneMode.Directional)
            {
                zoneSpeed = zone.windMain;
                Vector3 forward = zone.transform.forward;
                zoneDirection = new Vector2(forward.x, forward.z).normalized;
            }
            else
            {
                float distance = Vector3.Distance(zone.transform.position, worldPosition);
                if (distance > zone.radius) continue;

                float falloff = 1f - (distance / zone.radius);
                zoneSpeed = zone.windMain * falloff;

                Vector3 outward = worldPosition - zone.transform.position;
                Vector2 flatOutward = new Vector2(outward.x, outward.z);
                zoneDirection = flatOutward.sqrMagnitude > 0.0001f ? flatOutward.normalized : Vector2.right;
            }

            if (zoneSpeed > speed)
            {
                speed = zoneSpeed;
                directionXZ = zoneDirection;
            }
        }

        return speed > 0f;
    }

    static void EnsureCached()
    {
        if (cached) return;

        zones.Clear();
        zones.AddRange(Object.FindObjectsByType<WindZone>(FindObjectsSortMode.None));
        cached = true;
    }
}
