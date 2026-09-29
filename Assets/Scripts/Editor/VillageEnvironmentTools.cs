using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using EasyRoads3Dv3;

// Environment passes for the desert village around the lobby (DecorationsRoot/CountryHouse):
// grass under the village, house colliders, and keeping roads out of the lobby footprint.
public static class VillageEnvironmentTools
{
    const string LobbyPath = "DecorationsRoot/CountryHouse";
    const string RoadRootName = "Road Network";

    [MenuItem("tools/Environment/Inspect Village Environment")]
    public static string Inspect()
    {
        var sb = new StringBuilder();
        var terrain = Terrain.activeTerrain;
        var data = terrain.terrainData;
        sb.AppendLine($"Terrain '{terrain.name}' pos {terrain.transform.position} size {data.size} alphamap {data.alphamapResolution}");
        var layers = data.terrainLayers;
        var weights = new float[layers.Length];
        var maps = data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight);
        for (int z = 0; z < data.alphamapHeight; z += 4)
        for (int x = 0; x < data.alphamapWidth; x += 4)
        for (int l = 0; l < layers.Length; l++) weights[l] += maps[z, x, l];
        float total = weights.Sum();
        for (int l = 0; l < layers.Length; l++)
            sb.AppendLine($"  layer {l}: {(layers[l] ? AssetDatabase.GetAssetPath(layers[l]) : "null")} ({100f * weights[l] / total:F1}%)");

        var lobby = GameObject.Find(LobbyPath);
        var lb = WorldBounds(lobby);
        sb.AppendLine($"Lobby bounds {lb.center} size {lb.size}");

        var roadRoot = GameObject.Find(RoadRootName);
        foreach (var mf in roadRoot.GetComponentsInChildren<MeshFilter>())
        {
            var mr = mf.GetComponent<MeshRenderer>();
            if (mf.sharedMesh == null || mr == null || !mr.enabled) continue;
            int inside = 0;
            var m = mf.transform.localToWorldMatrix;
            foreach (var v in mf.sharedMesh.vertices)
            {
                var w = m.MultiplyPoint3x4(v);
                if (w.x > lb.min.x && w.x < lb.max.x && w.z > lb.min.z && w.z < lb.max.z) inside++;
            }
            if (inside > 0) sb.AppendLine($"  road mesh '{mf.name}' has {inside} verts inside lobby footprint");
        }

        var net = new ERRoadNetwork();
        foreach (var road in net.GetRoads())
        {
            var pts = road.GetMarkerPositions();
            float best = float.MaxValue;
            foreach (var p in pts) best = Mathf.Min(best, Flat(p - lb.center).magnitude);
            if (best < 80f)
                sb.AppendLine($"  ERRoad '{road.GetName()}' markers {pts.Length}, nearest marker {best:F1} m: " +
                              string.Join(" ", pts.Select(p => $"({p.x:F0},{p.y:F1},{p.z:F0})")));
        }

        sb.AppendLine("Buildings without a solid collider:");
        foreach (var go in CandidateBuildings())
            if (!HasSolidCollider(go)) sb.AppendLine($"  {Path(go)} at {go.transform.position}");

        return Report(sb.ToString());
    }

    const float ClearanceMargin = 14f; // also keeps the road outside the garden fence

    // Pushes road markers that sit inside the lobby's keep-out circle out to its edge,
    // so the spline bends around the house instead of running through it.
    [MenuItem("tools/Environment/Reroute Roads Around Lobby")]
    public static string RerouteRoadsAroundLobby()
    {
        var lobby = GameObject.Find(LobbyPath);
        if (lobby == null) return Report($"Reroute aborted: '{LobbyPath}' not found.");
        var lb = WorldBounds(lobby);
        var centre = Flat(lb.center);
        var net = new ERRoadNetwork();
        var sb = new StringBuilder();

        foreach (var road in net.GetRoads())
        {
            float clearance = Mathf.Max(lb.extents.x, lb.extents.z) + road.GetWidth() * 0.5f + ClearanceMargin;
            var pts = road.GetMarkerPositions();
            bool changed = false;
            // End markers are attached to crossings; leave them where they are.
            for (int i = 1; i < pts.Length - 1; i++)
            {
                var d = Flat(pts[i]) - centre;
                if (d.magnitude >= clearance) continue;
                var dir = d.sqrMagnitude > 0.01f ? d.normalized : Vector3.back;
                var p = centre + dir * clearance;
                p.y = pts[i].y;
                sb.AppendLine($"  {road.GetName()} marker {i}: ({pts[i].x:F1},{pts[i].z:F1}) -> ({p.x:F1},{p.z:F1})");
                road.SetMarkerPosition(i, p);
                changed = true;
            }
            if (changed) road.Refresh();
        }

        EditorSceneManager.MarkSceneDirty(lobby.scene);
        int inside = RoadVertsInside(lb, 1f);
        return Report((sb.Length > 0 ? "Moved markers:\n" + sb : "No markers needed moving.\n") +
                      $"Road vertices left inside lobby footprint (+1 m): {inside}");
    }

    // Static buildings only need a (non-convex) MeshCollider; the truck's Rigidbody does the rest.
    [MenuItem("tools/Environment/Add Missing House Colliders")]
    public static string AddMissingHouseColliders()
    {
        var sb = new StringBuilder();
        foreach (var go in CandidateBuildings())
        {
            if (HasSolidCollider(go) || go.name.StartsWith("Waterfall") || go.name.StartsWith("Dry")) continue;
            int added = 0;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var mc = Undo.AddComponent<MeshCollider>(mf.gameObject);
                mc.sharedMesh = mf.sharedMesh;
                added++;
            }
            sb.AppendLine($"  {Path(go)}: {added} MeshCollider(s)");
        }
        if (sb.Length > 0) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        return Report(sb.Length > 0 ? "Added colliders:\n" + sb : "Every building already has a collider.");
    }

    const float GrassFullRadius = 22f;   // full grass within this distance of a house
    const float GrassFadeRadius = 55f;   // back to sand at this distance
    const float LobbyGrassRadius = 120f; // extra grass disc around the village centre
    const float LobbyGrassFade = 60f;

    // Paints the terrain's grass layer under and around the village, fading to sand with a noisy edge.
    // Only the sand weight is converted, so road/gravel painting is preserved.
    [MenuItem("tools/Environment/Paint Village Grass")]
    public static string PaintVillageGrass()
    {
        var terrain = Terrain.activeTerrain;
        var data = terrain.terrainData;
        var layers = data.terrainLayers;
        int sand = System.Array.FindIndex(layers, l => l != null && l.name.StartsWith("Sand"));
        int grass = System.Array.FindIndex(layers, l => l != null && l.name == "Grass");
        var lobby = GameObject.Find(LobbyPath);
        if (sand < 0 || grass < 0 || lobby == null)
            return Report("Paint aborted: need 'Sand' and 'Grass' terrain layers and the lobby.");

        var houses = CandidateBuildings()
            .Where(g => g.name.StartsWith("rpgpp") || g.name.StartsWith("House") || g.name.StartsWith("Tent") ||
                        g.transform.parent != null)
            .Select(g => Flat(WorldBounds(g).center)).ToList();
        var centre = Flat(WorldBounds(lobby).center);

        int res = data.alphamapResolution;
        Vector3 size = data.size, origin = terrain.transform.position;
        Undo.RegisterCompleteObjectUndo(data, "Paint Village Grass");
        var maps = data.GetAlphamaps(0, 0, res, res);
        int painted = 0;
        for (int z = 0; z < res; z++)
        for (int x = 0; x < res; x++)
        {
            var p = new Vector3(origin.x + (x + 0.5f) / res * size.x, 0f, origin.z + (z + 0.5f) / res * size.z);
            // Noise wobbles the edge so the grass doesn't form perfect circles.
            float n = (Mathf.PerlinNoise(p.x * 0.02f, p.z * 0.02f) - 0.5f) * 30f +
                      (Mathf.PerlinNoise(p.x * 0.09f + 50f, p.z * 0.09f + 50f) - 0.5f) * 10f;
            float g = 1f - Mathf.InverseLerp(LobbyGrassRadius, LobbyGrassRadius + LobbyGrassFade, Flat(p - centre).magnitude + n);
            foreach (var h in houses)
            {
                if (g >= 1f) break;
                float d = Flat(p - h).magnitude + n * 0.6f;
                g = Mathf.Max(g, 1f - Mathf.InverseLerp(GrassFullRadius, GrassFadeRadius, d));
            }
            g = Mathf.SmoothStep(0f, 1f, g);
            if (g <= 0f) continue;
            float moved = maps[z, x, sand] * g;
            if (moved <= 0f) continue;
            maps[z, x, sand] -= moved;
            maps[z, x, grass] += moved;
            painted++;
        }
        data.SetAlphamaps(0, 0, maps);
        EditorUtility.SetDirty(data);
        EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
        return Report($"Painted grass on {painted} alphamap samples around {houses.Count} buildings.");
    }

    // ---------------------------------------------------------------- helpers

    static int RoadVertsInside(Bounds b, float margin)
    {
        int inside = 0;
        foreach (var mf in GameObject.Find(RoadRootName).GetComponentsInChildren<MeshFilter>())
        {
            var mr = mf.GetComponent<MeshRenderer>();
            if (mf.sharedMesh == null || mr == null || !mr.enabled) continue;
            var m = mf.transform.localToWorldMatrix;
            foreach (var v in mf.sharedMesh.vertices)
            {
                var w = m.MultiplyPoint3x4(v);
                if (w.x > b.min.x - margin && w.x < b.max.x + margin && w.z > b.min.z - margin && w.z < b.max.z + margin) inside++;
            }
        }
        return inside;
    }

    static IEnumerable<GameObject> CandidateBuildings()
    {
        var houses = GameObject.Find("VillageRoot/Houses");
        var list = new List<GameObject>();
        if (houses != null) foreach (Transform t in houses.transform) list.Add(t.gameObject);
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            var b = WorldBounds(root);
            // Loose building-sized root objects (houses, warehouse, mill...).
            if (root.GetComponentInChildren<Renderer>() != null && b.size.y > 2.5f &&
                b.size.x > 3f && b.size.z > 3f && b.size.x < 40f && b.size.z < 40f &&
                root.GetComponentInChildren<Rigidbody>() == null && root.GetComponentInChildren<ParticleSystem>() == null)
                list.Add(root);
        }
        var lobby = GameObject.Find(LobbyPath);
        if (lobby != null) list.Add(lobby);
        return list;
    }

    static bool HasSolidCollider(GameObject go) =>
        go.GetComponentsInChildren<Collider>().Any(c => c.enabled && !c.isTrigger);

    static Bounds WorldBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static string Path(GameObject go) =>
        go.transform.parent == null ? go.name : Path(go.transform.parent.gameObject) + "/" + go.name;

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    static string Report(string msg)
    {
        Debug.Log("[VillageEnvironmentTools] " + msg);
        return msg;
    }
}
