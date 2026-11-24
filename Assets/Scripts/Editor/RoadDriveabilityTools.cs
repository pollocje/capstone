using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;

// Checks how drivable the transition from each road onto the terrain is (the lip at the road edge
// and the slope of the terrain beside it), what colliders the roads carry, and the truck's clearance.
public static class RoadDriveabilityTools
{
    const string RoadRootName = "Road Network";
    static readonly float[] Offsets = { 0.3f, 1f, 2f, 3f, 4f };

    [MenuItem("tools/Roads/Inspect Road Edges")]
    public static string InspectRoadEdges()
    {
        var sb = new StringBuilder();
        var roadRoot = GameObject.Find(RoadRootName);
        var terrain = Terrain.activeTerrain;
        if (roadRoot == null || terrain == null) return Report("Need Road Network and an active Terrain.");
        float ty = terrain.transform.position.y;

        var colliderKinds = roadRoot.GetComponentsInChildren<Collider>(true)
            .GroupBy(c => $"{c.GetType().Name}{(c.enabled ? "" : " (disabled)")}{(c.isTrigger ? " trigger" : "")} on '{c.name}'")
            .Select(g => $"{g.Count()}x {g.Key}");
        sb.AppendLine("Colliders under Road Network: " + string.Join(", ", colliderKinds.Take(12)));

        var rows = new List<(string name, float lipUp, float lipDown, float slope, Vector3 at, int bad, int total)>();
        var slopeNotes = new List<string>();
        foreach (var mf in roadRoot.GetComponentsInChildren<MeshFilter>())
        {
            var mr = mf.GetComponent<MeshRenderer>();
            if (mf.sharedMesh == null || mr == null || !mr.enabled) continue;
            var mesh = mf.sharedMesh;
            var v = mesh.vertices.Select(p => mf.transform.TransformPoint(p)).ToArray();
            var tris = mesh.triangles;

            // Boundary edges belong to exactly one triangle; remember that triangle's third vertex
            // so we know which side is "outside".
            var edges = new Dictionary<(int, int), (int count, int third)>();
            for (int t = 0; t < tris.Length; t += 3)
                for (int k = 0; k < 3; k++)
                {
                    int a = tris[t + k], b = tris[t + (k + 1) % 3], c = tris[t + (k + 2) % 3];
                    var key = a < b ? (a, b) : (b, a);
                    edges[key] = edges.TryGetValue(key, out var e) ? (e.count + 1, e.third) : (1, c);
                }

            float worstUp = 0, worstDown = 0, worstSlope = 0; Vector3 worstAt = default; int bad = 0, total = 0;
            foreach (var kv in edges.Where(e => e.Value.count == 1))
            {
                Vector3 a = v[kv.Key.Item1], b = v[kv.Key.Item2], c = v[kv.Value.third];
                Vector3 mid = (a + b) * 0.5f;
                Vector3 along = Vector3.ProjectOnPlane(b - a, Vector3.up);
                if (along.sqrMagnitude < 1e-4f) continue;
                Vector3 outDir = Vector3.Cross(Vector3.up, along).normalized;
                if (Vector3.Dot(outDir, c - mid) > 0) outDir = -outDir;
                total++;

                float roadY = mid.y;
                float prevY = roadY, prevD = 0f, edgeSlope = 0f;
                float lip = roadY - (terrain.SampleHeight(mid + outDir * Offsets[0]) + ty);
                foreach (float d in Offsets)
                {
                    float y = terrain.SampleHeight(mid + outDir * d) + ty;
                    if (d > Offsets[0]) edgeSlope = Mathf.Max(edgeSlope, Mathf.Atan2(Mathf.Abs(y - prevY), d - prevD) * Mathf.Rad2Deg);
                    prevY = y; prevD = d;
                }
                if (lip > worstUp) { worstUp = lip; worstAt = mid; }
                if (-lip > worstDown) worstDown = -lip;
                if (edgeSlope > worstSlope && edgeSlope > 20f)
                    slopeNotes.Add($"    {mf.name} {edgeSlope:F0} deg at ({mid.x:F0},{mid.z:F0}) road y {roadY:F2}, terrain out 1/2/4/6/8/12/16/24 m: " +
                                   string.Join(" ", new[] { 1f, 2f, 4f, 6f, 8f, 12f, 16f, 24f }.Select(d => (terrain.SampleHeight(mid + outDir * d) + ty).ToString("F2"))));
                worstSlope = Mathf.Max(worstSlope, edgeSlope);
                if (Mathf.Abs(lip) > 0.15f || edgeSlope > 20f) bad++;
            }
            if (total > 0) rows.Add((Path(mf.gameObject), worstUp, worstDown, worstSlope, worstAt, bad, total));
        }

        sb.AppendLine("Road edge transitions (lip = road surface minus terrain 0.3 m outside; + means road stands above terrain):");
        foreach (var r in rows.OrderByDescending(r => r.bad))
            sb.AppendLine($"  {r.name}: worst lip up {r.lipUp:F2} m at ({r.at.x:F0},{r.at.y:F1},{r.at.z:F0}), terrain above road {r.lipDown:F2} m, steepest shoulder {r.slope:F0} deg, bad edges {r.bad}/{r.total}");
        sb.AppendLine("Steep shoulders (>20 deg):");
        foreach (var s in slopeNotes) sb.AppendLine(s);

        sb.AppendLine(TruckClearance());
        return Report(sb.ToString());
    }

    [MenuItem("tools/Roads/Inspect Crossings")]
    public static string InspectCrossings()
    {
        var sb = new StringBuilder();
        var roadRoot = GameObject.Find(RoadRootName);
        foreach (var mf in roadRoot.GetComponentsInChildren<MeshFilter>().Where(m => m.name.Contains("Crossing")))
        {
            var mr = mf.GetComponent<MeshRenderer>();
            var mesh = mf.sharedMesh;
            if (mesh == null || mr == null || !mr.enabled) continue;
            var mc = mf.GetComponent<MeshCollider>();
            sb.AppendLine($"{Path(mf.gameObject)} at {mf.transform.position}: submeshes {mesh.subMeshCount}, collider {(mc ? (mc.sharedMesh == mesh ? "same mesh" : mc.sharedMesh ? mc.sharedMesh.name : "none") : "-")}");
            var v = mesh.vertices;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var tris = mesh.GetTriangles(s);
                var ys = tris.Select(i => mf.transform.TransformPoint(v[i]).y).ToArray();
                string mat = s < mr.sharedMaterials.Length && mr.sharedMaterials[s] ? mr.sharedMaterials[s].name : "-";
                sb.AppendLine($"  submesh {s} '{mat}': {tris.Length / 3} tris, y {(ys.Length > 0 ? ys.Min() : 0):F2}..{(ys.Length > 0 ? ys.Max() : 0):F2}");
            }
        }
        return Report(sb.ToString());
    }

    const string CrossingMeshFolder = "Assets/Scenes/Prototype/CrossingMeshes";

    // Four X crossings carry a raised "sidewalk" submesh (white 0.25 m corner blocks) that is part of
    // their MeshCollider and snags vehicles. Replace each crossing's mesh (render + collider) with a
    // copy that keeps only the flat road surface. Note: rebuilding the road network in EasyRoads3D
    // regenerates the crossings with sidewalks, so disable sidewalks on those crossings there too.
    [MenuItem("tools/Roads/Remove Crossing Sidewalk Blocks")]
    public static string RemoveCrossingSidewalks()
    {
        var sb = new StringBuilder();
        var roadRoot = GameObject.Find(RoadRootName);
        if (!AssetDatabase.IsValidFolder(CrossingMeshFolder))
            AssetDatabase.CreateFolder("Assets/Scenes/Prototype", "CrossingMeshes");
        foreach (var mf in roadRoot.GetComponentsInChildren<MeshFilter>(true).Where(m => m.name.Contains("Crossing")))
        {
            var mr = mf.GetComponent<MeshRenderer>();
            var mesh = mf.sharedMesh;
            if (mesh == null || mr == null) continue;
            var mats = mr.sharedMaterials;
            var keep = Enumerable.Range(0, mesh.subMeshCount)
                .Where(s => s >= mats.Length || mats[s] == null || !mats[s].name.ToLower().Contains("sidewalk")).ToList();
            if (keep.Count == mesh.subMeshCount) continue;

            var surface = Object.Instantiate(mesh);
            var tris = keep.Select(s => mesh.GetTriangles(s)).ToList();
            surface.subMeshCount = keep.Count;
            for (int i = 0; i < keep.Count; i++) surface.SetTriangles(tris[i], i);
            surface.RecalculateBounds();
            var p = mf.transform.position;
            surface.name = $"{mf.name}_{p.x:F0}_{p.z:F0}_surface";
            string path = AssetDatabase.GenerateUniqueAssetPath($"{CrossingMeshFolder}/{surface.name}.asset");
            AssetDatabase.CreateAsset(surface, path);

            Undo.RecordObjects(new Object[] { mf, mr }, "Remove crossing sidewalks");
            mf.sharedMesh = surface;
            mr.sharedMaterials = keep.Select(s => s < mats.Length ? mats[s] : null).ToArray();
            var mc = mf.GetComponent<MeshCollider>();
            if (mc != null) { Undo.RecordObject(mc, "Remove crossing sidewalks"); mc.sharedMesh = surface; }
            sb.AppendLine($"  {Path(mf.gameObject)} at ({p.x:F0},{p.z:F0}): removed {mesh.subMeshCount - keep.Count} sidewalk submesh(es) -> {path}");
        }
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(roadRoot.scene);
        return Report(sb.Length > 0 ? "Crossings without sidewalk blocks:\n" + sb : "No crossings with sidewalk blocks found.");
    }

    const float Reach = 6f;               // metres beside a road edge that get reshaped
    const float RampSlope = 6f;             // degrees: terrain rising to meet a road edge that stands above it
    const float BankSlope = 14f;            // degrees: max slope of low banks beside a road
    const float LowBankLimit = 1.5f;        // only banks up to this height above the road are reshaped (not hills)
    const float BelowEdge = 0.03f;          // ramps stop this far under the road edge so terrain never pokes through
    const float RampLimit = 0.6f;           // only ramp up to curb-like steps, never under bridges/raised roads

    // Makes it easy to drive back onto the road: terrain beside raised road edges (e.g. the curbs of
    // the X crossings) is ramped up to the edge, and low steep banks beside roads are eased to a
    // gentle slope. Samples under the road itself and real hills/mountains are left alone.
    [MenuItem("tools/Roads/Smooth Road Edges For Driving")]
    public static string SmoothRoadEdges()
    {
        var sb = new StringBuilder();
        var roadRoot = GameObject.Find(RoadRootName);
        var terrain = Terrain.activeTerrain;
        if (roadRoot == null || terrain == null) return Report("Need Road Network and an active Terrain.");
        var data = terrain.terrainData;
        int res = data.heightmapResolution;
        Vector3 size = data.size, origin = terrain.transform.position;
        float cellX = size.x / (res - 1), cellZ = size.z / (res - 1);

        // Road boundary edges (world) and the heightmap samples covered by road surface.
        var edges = new List<(Vector3 a, Vector3 b)>();
        var covered = new HashSet<int>();
        foreach (var mf in roadRoot.GetComponentsInChildren<MeshFilter>())
        {
            var mr = mf.GetComponent<MeshRenderer>();
            if (mf.sharedMesh == null || mr == null || !mr.enabled) continue;
            var v = mf.sharedMesh.vertices.Select(p => mf.transform.TransformPoint(p)).ToArray();
            var tris = mf.sharedMesh.triangles;
            var count = new Dictionary<(int, int), int>();
            for (int t = 0; t < tris.Length; t += 3)
            {
                for (int k = 0; k < 3; k++)
                {
                    int a = tris[t + k], b = tris[t + (k + 1) % 3];
                    var key = a < b ? (a, b) : (b, a);
                    count[key] = count.TryGetValue(key, out int c) ? c + 1 : 1;
                }
                MarkCovered(v[tris[t]], v[tris[t + 1]], v[tris[t + 2]], origin, cellX, cellZ, res, covered);
            }
            foreach (var kv in count.Where(kv => kv.Value == 1)) edges.Add((v[kv.Key.Item1], v[kv.Key.Item2]));
        }

        // Nearest road edge (distance, edge height) for every uncovered sample within Reach.
        var nearest = new Dictionary<int, (float d, float y)>();
        foreach (var (a, b) in edges)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, b.x) - Reach - origin.x) / cellX));
            int x1 = Mathf.Min(res - 1, Mathf.CeilToInt((Mathf.Max(a.x, b.x) + Reach - origin.x) / cellX));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.z, b.z) - Reach - origin.z) / cellZ));
            int z1 = Mathf.Min(res - 1, Mathf.CeilToInt((Mathf.Max(a.z, b.z) + Reach - origin.z) / cellZ));
            var ab = new Vector2(b.x - a.x, b.z - a.z);
            for (int gz = z0; gz <= z1; gz++)
            for (int gx = x0; gx <= x1; gx++)
            {
                int key = gz * res + gx;
                if (covered.Contains(key)) continue;
                var p = new Vector2(origin.x + gx * cellX - a.x, origin.z + gz * cellZ - a.z);
                float t = Mathf.Clamp01(Vector2.Dot(p, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                float d = (p - ab * t).magnitude;
                if (d > Reach) continue;
                float y = Mathf.Lerp(a.y, b.y, t);
                // A curb's outer face has its bottom edge right under its top edge: treat edges that
                // are (almost) equally near as one and ramp up to the highest of them.
                if (!nearest.TryGetValue(key, out var cur)) nearest[key] = (d, y);
                else if (Mathf.Abs(d - cur.d) < 0.25f) nearest[key] = (Mathf.Min(d, cur.d), Mathf.Max(y, cur.y));
                else if (d < cur.d) nearest[key] = (d, y);
            }
        }

        Undo.RegisterCompleteObjectUndo(data, "Smooth Road Edges");
        float[,] heights = data.GetHeights(0, 0, res, res);
        float tanRamp = Mathf.Tan(RampSlope * Mathf.Deg2Rad), tanBank = Mathf.Tan(BankSlope * Mathf.Deg2Rad);
        int raised = 0, lowered = 0; float maxRaise = 0, maxLower = 0;
        var changed = new Dictionary<int, float>();
        foreach (var kv in nearest)
        {
            int gx = kv.Key % res, gz = kv.Key / res;
            float h = origin.y + heights[gz, gx] * size.y;
            var (d, edgeY) = kv.Value;
            float nh = h;
            float above = h - edgeY;
            if (above > 0 && above <= LowBankLimit) nh = Mathf.Min(nh, edgeY + d * tanBank);
            if (edgeY - h <= RampLimit) nh = Mathf.Max(nh, edgeY - BelowEdge - d * tanRamp);
            if (Mathf.Abs(nh - h) < 0.005f) continue;
            if (nh > h) { raised++; maxRaise = Mathf.Max(maxRaise, nh - h); } else { lowered++; maxLower = Mathf.Max(maxLower, h - nh); }
            heights[gz, gx] = Mathf.Clamp01((nh - origin.y) / size.y);
            changed[kv.Key] = nh - h;
        }
        data.SetHeights(0, 0, heights);
        data.SyncHeightmap();
        EditorUtility.SetDirty(data);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
        sb.AppendLine($"Raised {raised} samples (max {maxRaise:F2} m) into ramps, lowered {lowered} samples (max {maxLower:F2} m) on low banks.");

        // Placed objects whose ground moved noticeably.
        var moved = new List<string>();
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject) && t.GetComponent<Renderer>() == null) continue;
            if (t.IsChildOf(roadRoot.transform)) continue;
            int gx = Mathf.RoundToInt((t.position.x - origin.x) / cellX), gz = Mathf.RoundToInt((t.position.z - origin.z) / cellZ);
            if (changed.TryGetValue(gz * res + gx, out float dh) && Mathf.Abs(dh) > 0.1f) moved.Add($"  {Path(t.gameObject)}: ground {dh:+0.00;-0.00} m");
        }
        sb.AppendLine($"Objects standing on reshaped ground (>10 cm): {moved.Count}");
        foreach (var m in moved.Take(40)) sb.AppendLine(m);
        return Report(sb.ToString());
    }

    // Undoes consecutive "Smooth Road Edges" passes at the top of the undo stack, and nothing else.
    [MenuItem("tools/Roads/Undo Road Edge Smoothing")]
    public static string UndoSmoothing()
    {
        var getRecords = typeof(Undo).GetMethod("GetRecords",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static, null,
            new[] { typeof(List<string>), typeof(List<string>) }, null);
        if (getRecords == null) return Report("Undo.GetRecords not available; nothing undone.");
        int undone = 0;
        for (int i = 0; i < 10; i++)
        {
            var undo = new List<string>(); var redo = new List<string>();
            getRecords.Invoke(null, new object[] { undo, redo });
            string top = undo.Count > 0 ? undo[undo.Count - 1] : "(empty)";
            if (top != "Smooth Road Edges") return Report($"Undid {undone} smoothing pass(es); top of undo stack is now '{top}'.");
            Undo.PerformUndo();
            undone++;
        }
        return Report($"Undid {undone} smoothing pass(es).");
    }

    // Copies the heightmap from another TerrainData asset (e.g. a temporary copy of the saved terrain
    // file) into the active terrain.
    public static string RestoreHeightsFrom(string assetPath)
    {
        var src = AssetDatabase.LoadAssetAtPath<TerrainData>(assetPath);
        var terrain = Terrain.activeTerrain;
        if (src == null || terrain == null) return Report($"Can't restore: source {(src ? "ok" : "missing")}, terrain {(terrain ? "ok" : "missing")}");
        var data = terrain.terrainData;
        if (src.heightmapResolution != data.heightmapResolution) return Report("Resolution mismatch; nothing restored.");
        int res = data.heightmapResolution;
        Undo.RegisterCompleteObjectUndo(data, "Restore Terrain Heights");
        data.SetHeights(0, 0, src.GetHeights(0, 0, res, res));
        data.SyncHeightmap();
        EditorUtility.SetDirty(data);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
        return Report($"Restored {res}x{res} heights from {assetPath}.");
    }

    static void MarkCovered(Vector3 a, Vector3 b, Vector3 c, Vector3 origin, float cellX, float cellZ, int res, HashSet<int> covered)
    {
        float den = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
        if (Mathf.Abs(den) < 1e-9f) return;
        int x0 = Mathf.Max(0, Mathf.CeilToInt((Mathf.Min(a.x, Mathf.Min(b.x, c.x)) - origin.x) / cellX));
        int x1 = Mathf.Min(res - 1, Mathf.FloorToInt((Mathf.Max(a.x, Mathf.Max(b.x, c.x)) - origin.x) / cellX));
        int z0 = Mathf.Max(0, Mathf.CeilToInt((Mathf.Min(a.z, Mathf.Min(b.z, c.z)) - origin.z) / cellZ));
        int z1 = Mathf.Min(res - 1, Mathf.FloorToInt((Mathf.Max(a.z, Mathf.Max(b.z, c.z)) - origin.z) / cellZ));
        for (int gz = z0; gz <= z1; gz++)
        for (int gx = x0; gx <= x1; gx++)
        {
            float x = origin.x + gx * cellX, z = origin.z + gz * cellZ;
            float l1 = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / den;
            float l2 = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / den;
            if (l1 >= -1e-5f && l2 >= -1e-5f && 1f - l1 - l2 >= -1e-5f) covered.Add(gz * res + gx);
        }
    }

    static string TruckClearance()
    {
        var truck = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(t => t.name == "TruckRootNEW");
        if (truck == null) return "TruckRootNEW not found";
        var sb = new StringBuilder("TruckRootNEW clearance (truck-local y):\n");
        float groundY = float.MaxValue;
        foreach (var w in truck.GetComponentsInChildren<WheelCollider>(true))
        {
            float centerY = truck.InverseTransformPoint(w.transform.TransformPoint(w.center)).y;
            // At rest the wheel hangs up to suspensionDistance * (1 - targetPosition) below the center.
            float bottom = centerY - w.suspensionDistance * (1f - w.suspensionSpring.targetPosition) - w.radius;
            groundY = Mathf.Min(groundY, bottom);
            sb.AppendLine($"  {w.name}: center y {centerY:F2}, radius {w.radius:F2}, suspension {w.suspensionDistance:F2} (target {w.suspensionSpring.targetPosition:F2}), rest ground ~{bottom:F2}");
        }
        foreach (var c in truck.GetComponentsInChildren<Collider>(true).Where(c => !(c is WheelCollider) && !c.isTrigger && c.enabled))
        {
            var b = c.bounds;
            float minY = truck.InverseTransformPoint(new Vector3(b.center.x, b.min.y, b.center.z)).y;
            sb.AppendLine($"  {c.GetType().Name} '{c.name}': bottom y {minY:F2} -> clearance ~{minY - groundY:F2} m");
        }
        return sb.ToString();
    }

    static string Path(GameObject go) =>
        go.transform.parent == null ? go.name : Path(go.transform.parent.gameObject) + "/" + go.name;

    static string Report(string msg)
    {
        Debug.Log("[RoadDriveabilityTools] " + msg);
        return msg;
    }
}
