using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Lobby fence (Environment/Structures/DecorationsRoot/Fence, an instance of Fence.prefab):
// inspection of its layout against the road network.
public static class LobbyFenceTools
{
    const string RoadRootName = "Road Network";

    [MenuItem("tools/Environment/Inspect Lobby Fence")]
    public static string Inspect()
    {
        var sb = new StringBuilder();
        var group = FenceGroup();
        if (group == null) return Report("Fence/FENCE not found");
        var road = RoadSamples();
        sb.AppendLine($"FENCE parent: pos {group.position}, rot {group.eulerAngles}, lossy {group.lossyScale}");
        foreach (Transform t in group)
        {
            var mf = t.GetComponent<MeshFilter>();
            var lb = mf && mf.sharedMesh ? mf.sharedMesh.bounds : new Bounds();
            float clear = t.GetComponentsInChildren<Renderer>().SelectMany(r => Corners(r.bounds))
                .Select(p => NearestRoad(road, p)).DefaultIfEmpty(float.MaxValue).Min();
            sb.AppendLine($"  {t.name}: pos ({t.position.x:F2},{t.position.y:F2},{t.position.z:F2}) yaw {t.eulerAngles.y:F1} lossy ({t.lossyScale.x:F2},{t.lossyScale.y:F2},{t.lossyScale.z:F2}) " +
                          $"mesh {(mf && mf.sharedMesh ? mf.sharedMesh.name : "-")} local c({lb.center.x:F2},{lb.center.y:F2},{lb.center.z:F2}) s({lb.size.x:F2},{lb.size.y:F2},{lb.size.z:F2}) road clearance {clear:F1} m");
            foreach (Transform c in t)
            {
                var cmf = c.GetComponent<MeshFilter>();
                var cb = cmf && cmf.sharedMesh ? cmf.sharedMesh.bounds : new Bounds();
                sb.AppendLine($"      {c.name}: localPos {c.localPosition} localYaw {c.localEulerAngles.y:F1} mesh c{cb.center} s{cb.size}");
            }
        }
        return Report(sb.ToString());
    }

    // ---------------------------------------------------------------- rebuild

    const string FencePrefabPath = "Assets/Polytope Studio/Lowpoly_Demos/Environment_Free/Helpers/Fence.prefab";
    const string GateName = "PT_Medieval_Village_Gate_01";
    const float RoadClearance = 3f;     // metres between any fence part and the nearest road mesh
    const float KeepOutMargin = 2f;     // metres the fence must stay away from protected lobby objects
    const float ColliderThickness = 0.5f;
    const float ColliderExtraHeight = 0.3f;

    // Pulls each side of the lobby fence straight inward until it clears the roads, re-lays the
    // segments evenly along the new sides, moves the gate with its side and folds its leaves flat
    // against the inside of the fence, and swaps every MeshCollider for a thick BoxCollider.
    // Computed in the scene (roads/terrain), written into Fence.prefab in FENCE-local space.
    [MenuItem("tools/Environment/Rebuild Lobby Fence Clear Of Roads")]
    public static string Rebuild()
    {
        var sb = new StringBuilder();
        var group = FenceGroup();
        if (group == null) return Report("Fence/FENCE not found");
        var terrain = Terrain.activeTerrain;
        var road = RoadSamples();
        var gate = group.Find(GateName);

        // 1) Current segments as line pieces in world xz.
        var segs = new List<Transform>();
        var pieces = new List<(Vector2 a, Vector2 b)>();
        foreach (Transform t in group)
        {
            if (t == gate) continue;
            var mf = t.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            segs.Add(t);
            var (a, b) = Ends(t, mf.sharedMesh.bounds);
            pieces.Add((a, b));
        }
        if (segs.Count < 4) return Report("Too few fence segments found");
        float segLen = pieces.Select(p => Vector2.Distance(p.a, p.b)).OrderBy(x => x).ElementAt(pieces.Count / 2);

        // 2) Corner polygon = hull of all segment ends, simplified to its real corners.
        var hull = Hull(pieces.SelectMany(p => new[] { p.a, p.b }).ToList());
        var corners = Simplify(hull, 20f);
        if (gate != null) corners = Simplify(Hull(hull.Concat(GateEnds(gate)).ToList()), 20f);
        sb.AppendLine($"Old corners: {string.Join(" ", corners.Select(c => $"({c.x:F1},{c.y:F1})"))}");
        int n = corners.Count;

        // Which side the gate is on, and where along it.
        int gateEdge = -1; float gateT = 0f, gateWidth = 0f; Vector2 gateOldDir = default;
        if (gate != null)
        {
            var ge = GateEnds(gate);
            var gc = (ge[0] + ge[1]) * 0.5f;
            gateWidth = Vector2.Distance(ge[0], ge[1]);
            gateEdge = Enumerable.Range(0, n).OrderBy(i => DistToSegment(gc, corners[i], corners[(i + 1) % n])).First();
            var e0 = corners[gateEdge]; var e1 = corners[(gateEdge + 1) % n];
            gateT = Vector2.Dot(gc - e0, e1 - e0) / (e1 - e0).sqrMagnitude;
            gateOldDir = (e1 - e0).normalized;
        }

        // 3) Offset every side inward until it (and the fence thickness) clears the roads.
        var keepOut = KeepOutPoints();
        var centroid = corners.Aggregate(Vector2.zero, (s, c) => s + c) / n;
        var offsets = new float[n];
        var newCorners = corners;
        for (int iter = 0; iter < 400; iter++)
        {
            newCorners = OffsetPolygon(corners, offsets, centroid);
            bool moved = false;
            for (int i = 0; i < n; i++)
            {
                var a = newCorners[i]; var b = newCorners[(i + 1) % n];
                float len = Vector2.Distance(a, b);
                bool tooClose = false;
                for (float s = 0; s <= len && !tooClose; s += 0.5f)
                    tooClose = NearestRoad(road, ToV3(Vector2.Lerp(a, b, s / len))) < RoadClearance;
                if (!tooClose) continue;
                var trial = (float[])offsets.Clone(); trial[i] += 0.25f;
                var trialPoly = OffsetPolygon(corners, trial, centroid);
                if (keepOut.Any(p => !InsideWithMargin(trialPoly, p, KeepOutMargin)))
                {
                    if (iter == 0 || offsets[i] == 0f) sb.AppendLine($"  side {i} can't move further in without crowding the lobby");
                    continue;
                }
                offsets[i] = trial[i]; moved = true;
            }
            if (!moved) break;
        }
        newCorners = OffsetPolygon(corners, offsets, centroid);
        sb.AppendLine($"Side offsets (m inward): {string.Join(", ", offsets.Select(o => o.ToString("F2")))}");
        sb.AppendLine($"New corners: {string.Join(" ", newCorners.Select(c => $"({c.x:F1},{c.y:F1})"))}");

        // 4) New piece layout: fill every side with whole segments, leaving the gate opening.
        var layout = new List<(Vector2 mid, Vector2 dir, float len)>();
        for (int i = 0; i < n; i++)
        {
            var a = newCorners[i]; var b = newCorners[(i + 1) % n];
            var spans = new List<(float from, float to)>();
            float len = Vector2.Distance(a, b);
            if (i == gateEdge)
            {
                float c = gateT * len;
                spans.Add((0f, c - gateWidth * 0.5f));
                spans.Add((c + gateWidth * 0.5f, len));
            }
            else spans.Add((0f, len));
            var dir = (b - a) / len;
            foreach (var (from, to) in spans)
            {
                float spanLen = to - from;
                if (spanLen < segLen * 0.4f) continue;
                int count = Mathf.Max(1, Mathf.RoundToInt(spanLen / segLen));
                float pieceLen = spanLen / count;
                for (int k = 0; k < count; k++)
                    layout.Add((a + dir * (from + pieceLen * (k + 0.5f)), dir, pieceLen));
            }
        }
        sb.AppendLine($"Segments: {segs.Count} before, {layout.Count} after (nominal length {segLen:F2} m)");

        // 5) Convert to FENCE-local poses (FENCE has the same local frame in scene and prefab).
        var poses = new List<(Vector3 pos, Quaternion rot, Vector3 scale, int source)>();
        for (int i = 0; i < layout.Count; i++)
        {
            var src = segs[i % segs.Count];
            var mb = src.GetComponent<MeshFilter>().sharedMesh.bounds;
            var (longAx, _, _) = Axes(src, mb);
            var (mid, dir, len) = layout[i];
            var d3 = new Vector3(dir.x, 0, dir.y);
            // Yaw only: turn the piece's current long axis onto the side direction (keeps import tilt).
            var cur = Flat(src.rotation * Unit(longAx));
            if (Vector3.Dot(cur, d3) < 0) cur = -cur;
            var rot = Quaternion.FromToRotation(cur, d3) * src.rotation;
            var (pa, pb) = Ends(src, mb);
            var localScale = src.localScale;
            localScale[longAx] *= len / Vector2.Distance(pa, pb);
            // Pivot so the mesh bounds centre lands on the segment midpoint; keep height above ground.
            var worldScale = Vector3.Scale(localScale, src.parent.lossyScale);
            var centerOffset = rot * Vector3.Scale(mb.center, worldScale);
            centerOffset.y = 0;
            float groundOffset = src.position.y - Ground(terrain, src.position);
            var pivot = new Vector3(mid.x, 0, mid.y) - centerOffset;
            pivot.y = Ground(terrain, pivot) + groundOffset;
            poses.Add((group.InverseTransformPoint(pivot), Quaternion.Inverse(group.rotation) * rot, localScale, i % segs.Count));
        }

        // Gate pose (FENCE-local) and leaf fold, from its new place on the moved side.
        Vector3 gateLocalPos = default; Quaternion gateLocalRot = default;
        if (gate != null)
        {
            var e0 = newCorners[gateEdge]; var e1 = newCorners[(gateEdge + 1) % n];
            var newDir = (e1 - e0).normalized;
            var newCenter = Vector2.Lerp(e0, e1, gateT);
            var ge = GateEnds(gate); var oldCenter = (ge[0] + ge[1]) * 0.5f;
            var turn = Quaternion.Euler(0, -Vector2.SignedAngle(gateOldDir, newDir), 0);
            var rootOffset = new Vector2(gate.position.x, gate.position.z) - oldCenter;
            var ro = turn * new Vector3(rootOffset.x, 0, rootOffset.y);
            var newRootXZ = newCenter + new Vector2(ro.x, ro.z);
            float gOff = gate.position.y - Ground(terrain, gate.position);
            var worldPos = new Vector3(newRootXZ.x, Ground(terrain, ToV3(newRootXZ)) + gOff, newRootXZ.y);
            gateLocalPos = group.InverseTransformPoint(worldPos);
            gateLocalRot = Quaternion.Inverse(group.rotation) * (turn * gate.rotation);
        }

        // 6) Write into the prefab.
        var root = PrefabUtility.LoadPrefabContents(FencePrefabPath);
        try
        {
            var pg = root.transform.Find("FENCE");
            // Scene instance and prefab keep the same child order under FENCE.
            var pSegs = segs.Select(s => pg.GetChild(s.GetSiblingIndex())).ToList();
            var used = new List<Transform>();
            for (int i = 0; i < poses.Count; i++)
            {
                var (pos, rot, scale, source) = poses[i];
                Transform t = i < pSegs.Count ? pSegs[i] : Object.Instantiate(pSegs[source].gameObject, pg).transform;
                if (i >= pSegs.Count) t.name = pSegs[source].name.Split(' ')[0] + $" ({100 + i})";
                t.localPosition = pos; t.localRotation = rot; t.localScale = scale;
                used.Add(t);
                SwapToBox(t);
            }
            for (int i = poses.Count; i < pSegs.Count; i++) Object.DestroyImmediate(pSegs[i].gameObject);
            sb.AppendLine($"  removed {Mathf.Max(0, pSegs.Count - poses.Count)} surplus segment(s)");

            var pGate = pg.Find(GateName);
            if (pGate != null)
            {
                pGate.localPosition = gateLocalPos;
                pGate.localRotation = gateLocalRot;
                sb.AppendLine(FoldLeaves(pGate));
                foreach (var mf in pGate.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.name.Contains("poles")) PostBoxes(mf.transform); else SwapToBox(mf.transform);
            }
            PrefabUtility.SaveAsPrefabAsset(root, FencePrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        // 7) Verify in the scene.
        group = FenceGroup();
        float minClear = group.GetComponentsInChildren<Renderer>().SelectMany(r => Corners(r.bounds))
            .Select(p => NearestRoad(road, p)).Min();
        int boxes = group.GetComponentsInChildren<BoxCollider>(true).Count(b => !b.isTrigger);
        int meshCols = group.GetComponentsInChildren<MeshCollider>(true).Length;
        sb.AppendLine($"Result: closest fence part to a road {minClear:F2} m; {boxes} solid BoxColliders, {meshCols} MeshColliders left");
        foreach (var p in KeepOutPoints())
            if (!InsideWithMargin(OffsetPolygon(corners, offsets, centroid), p, 0f)) sb.AppendLine($"  !! protected point {p} ended up outside the fence");
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        return Report(sb.ToString());
    }

    // Every fence/gate BoxCollider blocks a ray from both sides, and a truck-sized box fits through the gate.
    [MenuItem("tools/Environment/Verify Lobby Fence")]
    public static string Verify()
    {
        var sb = new StringBuilder();
        var group = FenceGroup();
        Physics.SyncTransforms();
        var boxes = group.GetComponentsInChildren<BoxCollider>(true).Where(b => !b.isTrigger).ToList();
        int blocked = 0;
        foreach (var b in boxes)
        {
            var (_, thin, _) = Axes(b.transform, new Bounds(b.center, b.size));
            var normal = Flat(b.transform.rotation * Unit(thin));
            var c = b.bounds.center;
            if (b.Raycast(new Ray(c - normal * 4f, normal), out _, 8f) && b.Raycast(new Ray(c + normal * 4f, -normal), out _, 8f)) blocked++;
            else sb.AppendLine($"  !! ray passed through {b.name}");
        }
        sb.AppendLine($"Solid BoxColliders blocking rays: {blocked}/{boxes.Count}");

        var gate = group.Find(GateName);
        var poles = gate ? gate.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(m => m.name.Contains("poles")) : null;
        if (poles)
        {
            var along = Flat(poles.transform.rotation * Unit(Axes(poles.transform, poles.sharedMesh.bounds).longAx));
            var center = poles.GetComponent<Renderer>().bounds.center;
            center.y = Ground(Terrain.activeTerrain, center) + 1.2f;
            // Truck body is ~3.3 m wide x 1.6 m tall; test a 3.5 x 1.5 m box straddling the fence line.
            var rot = Quaternion.LookRotation(Vector3.Cross(along, Vector3.up), Vector3.up);
            var hits = Physics.OverlapBox(center, new Vector3(1.75f, 0.7f, 2f), rot)
                .Where(h => h.transform.IsChildOf(group) && !h.isTrigger).Select(h => h.name).ToList();
            sb.AppendLine(hits.Count == 0 ? "Gate opening: clear for a truck-sized box" : "Gate opening blocked by: " + string.Join(", ", hits));
        }
        return Report(sb.ToString());
    }

    // Leaves swing on a hinge at one end: turn each so it lies along the fence, pointing away from
    // the opening, just inside the fence line.
    static string FoldLeaves(Transform gate)
    {
        var sb = new StringBuilder();
        var poles = gate.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(m => m.name.Contains("poles"));
        Vector3 openingCenter = poles ? poles.GetComponent<Renderer>().bounds.center : gate.position;
        Vector3 along = Flat(gate.right);
        if (poles) along = Flat(poles.transform.rotation * Unit(Axes(poles.transform, poles.sharedMesh.bounds).longAx));
        var inward = Vector3.Cross(along, Vector3.up); // one side; flipped below towards the fence centroid
        var fence = gate.parent;
        var fenceCenter = fence.GetComponentsInChildren<Renderer>().Select(r => r.bounds.center).Aggregate(Vector3.zero, (s, c) => s + c)
                          / Mathf.Max(1, fence.GetComponentsInChildren<Renderer>().Length);
        if (Vector3.Dot(inward, fenceCenter - openingCenter) < 0) inward = -inward;

        foreach (var mf in gate.GetComponentsInChildren<MeshFilter>(true).Where(m => m.name.EndsWith("_left") || m.name.EndsWith("_right")))
        {
            var t = mf.transform;
            var mb = mf.sharedMesh.bounds;
            int longAx = Axes(t, mb).longAx;
            // Hinge = pivot (Polytope "mobile" parts pivot on their hinge). Leaf should point away from the opening.
            var hinge = t.position;
            var away = Vector3.Dot(hinge - openingCenter, along) >= 0 ? along : -along;
            // Which way does the mesh extend from its pivot along its long axis?
            float sign = mb.center[longAx] < 0 ? -1f : 1f;
            var current = Flat(t.rotation * (Unit(longAx) * sign));
            t.rotation = Quaternion.FromToRotation(current, away) * t.rotation;
            t.position = hinge + inward * (ColliderThickness * 0.5f + 0.05f);
            sb.AppendLine($"  folded {t.name} flat along the fence");
        }
        return sb.ToString();
    }

    const float PostWidthLocal = 0.35f; // gate post width in mesh units (mesh is scaled 3x in the scene)

    // The gate "poles" mesh is two posts plus a high crossbar, so one bounding box would seal the
    // opening. Give it one box per post instead; the crossbar is far above any vehicle.
    [MenuItem("tools/Environment/Fix Lobby Gate Post Colliders")]
    public static string FixGatePostColliders()
    {
        var root = PrefabUtility.LoadPrefabContents(FencePrefabPath);
        try
        {
            var poles = root.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(m => m.name.Contains("poles"));
            if (poles == null) return Report("Gate poles not found in Fence.prefab");
            PostBoxes(poles.transform);
            PrefabUtility.SaveAsPrefabAsset(root, FencePrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        return Report("Gate poles: replaced the opening-wide box with one box per post.\n" + Verify());
    }

    static void PostBoxes(Transform poles)
    {
        var mf = poles.GetComponent<MeshFilter>();
        foreach (var c in poles.GetComponents<Collider>()) Object.DestroyImmediate(c, true);
        var b = mf.sharedMesh.bounds;
        var (longAx, thin, up) = Axes(poles, b);
        var ls = poles.lossyScale;
        foreach (float side in new[] { -1f, 1f })
        {
            var box = poles.gameObject.AddComponent<BoxCollider>();
            var size = b.size;
            size[longAx] = PostWidthLocal;
            size[thin] = Mathf.Max(size[thin], ColliderThickness / Mathf.Max(0.001f, Mathf.Abs(ls[thin])));
            var center = b.center;
            center[longAx] = b.center[longAx] + side * (b.extents[longAx] - PostWidthLocal * 0.5f);
            box.center = center;
            box.size = size;
            box.isTrigger = false;
        }
    }

    static void SwapToBox(Transform t)
    {
        var mf = t.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;
        foreach (var mc in t.GetComponents<MeshCollider>()) Object.DestroyImmediate(mc, true);
        var box = t.GetComponent<BoxCollider>();
        if (box == null) box = t.gameObject.AddComponent<BoxCollider>();
        var b = mf.sharedMesh.bounds;
        var (_, thin, up) = Axes(t, b);
        var ls = t.lossyScale;
        var size = b.size;
        // Thin axis gets a minimum real-world thickness so fast vehicles can't slip through.
        size[thin] = Mathf.Max(size[thin], ColliderThickness / Mathf.Max(0.001f, Mathf.Abs(ls[thin])));
        // A little taller than the mesh, grown towards world up only.
        float extra = ColliderExtraHeight / Mathf.Max(0.001f, Mathf.Abs(ls[up]));
        size[up] += extra;
        float upSign = (t.rotation * Unit(up)).y < 0 ? -1f : 1f;
        box.center = b.center + Unit(up) * (extra * 0.5f * upSign);
        box.size = size;
        box.isTrigger = false;
    }

    // Lobby things the fence must keep enclosing: the lobby house footprint, spawn points, the truck.
    static List<Vector2> KeepOutPoints()
    {
        var pts = new List<Vector2>();
        var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var lobby = all.FirstOrDefault(t => t.name == "CountryHouse");
        if (lobby)
        {
            var rs = lobby.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                pts.Add(new Vector2(b.min.x, b.min.z)); pts.Add(new Vector2(b.max.x, b.min.z));
                pts.Add(new Vector2(b.min.x, b.max.z)); pts.Add(new Vector2(b.max.x, b.max.z));
            }
        }
        foreach (var t in all.Where(t => t.name.StartsWith("SpawnPoint") || t.name == "TruckRootNEW"))
            pts.Add(new Vector2(t.position.x, t.position.z));
        return pts;
    }

    static (Vector2, Vector2) Ends(Transform t, Bounds mb)
    {
        var (longAx, _, _) = Axes(t, mb);
        var ax = Unit(longAx) * mb.extents[longAx];
        var a = t.TransformPoint(mb.center - ax); var b = t.TransformPoint(mb.center + ax);
        return (new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }

    // Local mesh axes by their world role: longest horizontal, the other horizontal, most vertical.
    static (int longAx, int thinAx, int upAx) Axes(Transform t, Bounds mb)
    {
        int up = Enumerable.Range(0, 3).OrderByDescending(i => Mathf.Abs((t.rotation * Unit(i)).y)).First();
        var hor = Enumerable.Range(0, 3).Where(i => i != up)
            .OrderByDescending(i => mb.size[i] * Mathf.Abs(t.lossyScale[i])).ToArray();
        return (hor[0], hor[1], up);
    }

    static Vector3 Unit(int axis) => axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0;
        return v.normalized;
    }

    // The two ends of the gate opening: extremes of the gate's poles mesh (or whole gate) along its long axis.
    static Vector2[] GateEnds(Transform gate)
    {
        var poles = gate.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(m => m.name.Contains("poles"));
        if (poles && poles.sharedMesh)
        {
            var (a, b) = Ends(poles.transform, poles.sharedMesh.bounds);
            return new[] { a, b };
        }
        var rs = gate.GetComponentsInChildren<Renderer>();
        var bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds);
        return bb.size.x >= bb.size.z
            ? new[] { new Vector2(bb.min.x, bb.center.z), new Vector2(bb.max.x, bb.center.z) }
            : new[] { new Vector2(bb.center.x, bb.min.z), new Vector2(bb.center.x, bb.max.z) };
    }

    static List<Vector2> Hull(List<Vector2> pts)
    {
        var p = pts.Distinct().OrderBy(v => v.x).ThenBy(v => v.y).ToList();
        if (p.Count < 3) return p;
        float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
        var h = new List<Vector2>();
        foreach (var v in p) { while (h.Count >= 2 && Cross(h[h.Count - 2], h[h.Count - 1], v) <= 0) h.RemoveAt(h.Count - 1); h.Add(v); }
        int lower = h.Count + 1;
        for (int i = p.Count - 2; i >= 0; i--) { var v = p[i]; while (h.Count >= lower && Cross(h[h.Count - 2], h[h.Count - 1], v) <= 0) h.RemoveAt(h.Count - 1); h.Add(v); }
        h.RemoveAt(h.Count - 1);
        return h; // counter-clockwise
    }

    // Drop hull vertices where the outline turns by less than minTurn degrees (keeps the real corners).
    static List<Vector2> Simplify(List<Vector2> hull, float minTurn)
    {
        var pts = new List<Vector2>(hull);
        bool changed = true;
        while (changed && pts.Count > 3)
        {
            changed = false;
            for (int i = 0; i < pts.Count && pts.Count > 3; i++)
            {
                var prev = pts[(i - 1 + pts.Count) % pts.Count]; var cur = pts[i]; var next = pts[(i + 1) % pts.Count];
                if (Mathf.Abs(Vector2.SignedAngle(cur - prev, next - cur)) < minTurn) { pts.RemoveAt(i); changed = true; break; }
            }
        }
        return pts;
    }

    static List<Vector2> OffsetPolygon(List<Vector2> corners, float[] offsets, Vector2 centroid)
    {
        int n = corners.Count;
        var lines = new (Vector2 p, Vector2 d)[n];
        for (int i = 0; i < n; i++)
        {
            var a = corners[i]; var b = corners[(i + 1) % n];
            var d = (b - a).normalized;
            var normal = new Vector2(-d.y, d.x);
            if (Vector2.Dot(normal, centroid - a) < 0) normal = -normal;
            lines[i] = (a + normal * offsets[i], d);
        }
        var result = new List<Vector2>();
        for (int i = 0; i < n; i++)
        {
            var l1 = lines[(i - 1 + n) % n]; var l2 = lines[i];
            float den = l1.d.x * l2.d.y - l1.d.y * l2.d.x;
            if (Mathf.Abs(den) < 1e-6f) { result.Add(l2.p); continue; }
            var diff = l2.p - l1.p;
            float s = (diff.x * l2.d.y - diff.y * l2.d.x) / den;
            result.Add(l1.p + l1.d * s);
        }
        return result;
    }

    static bool InsideWithMargin(List<Vector2> poly, Vector2 p, float margin)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) inside = !inside;
        if (!inside) return false;
        for (int i = 0; i < poly.Count; i++)
            if (DistToSegment(p, poly[i], poly[(i + 1) % poly.Count]) < margin) return false;
        return true;
    }

    static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
        return Vector2.Distance(p, a + ab * t);
    }

    static float Ground(Terrain terrain, Vector3 p) =>
        terrain ? terrain.SampleHeight(p) + terrain.transform.position.y : 0f;

    static Vector3 ToV3(Vector2 v) => new Vector3(v.x, 0, v.y);

    // ---------------------------------------------------------------- helpers

    internal static Transform FenceGroup()
    {
        var fence = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(t => t.name == "Fence" && t.Find("FENCE") != null);
        return fence ? fence.Find("FENCE") : null;
    }

    // Road surface sample points (xz), 1 m apart along every triangle edge of every visible road mesh.
    internal static List<Vector2> RoadSamples()
    {
        var pts = new List<Vector2>();
        var roadRoot = GameObject.Find(RoadRootName);
        if (roadRoot == null) return pts;
        foreach (var mf in roadRoot.GetComponentsInChildren<MeshFilter>())
        {
            var mr = mf.GetComponent<MeshRenderer>();
            if (mf.sharedMesh == null || mr == null || !mr.enabled) continue;
            var v = mf.sharedMesh.vertices.Select(p => mf.transform.TransformPoint(p)).ToArray();
            var tris = mf.sharedMesh.triangles;
            for (int i = 0; i < tris.Length; i += 3)
                for (int k = 0; k < 3; k++)
                {
                    Vector3 a = v[tris[i + k]], b = v[tris[i + (k + 1) % 3]];
                    int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b)));
                    for (int s = 0; s <= n; s++) { var p = Vector3.Lerp(a, b, s / (float)n); pts.Add(new Vector2(p.x, p.z)); }
                }
        }
        return pts;
    }

    internal static float NearestRoad(List<Vector2> road, Vector3 p)
    {
        var q = new Vector2(p.x, p.z);
        float best = float.MaxValue;
        foreach (var r in road) { float d = (r - q).sqrMagnitude; if (d < best) best = d; }
        return Mathf.Sqrt(best);
    }

    internal static IEnumerable<Vector3> Corners(Bounds b)
    {
        for (int i = 0; i < 8; i++)
            yield return b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
    }

    static string Report(string msg)
    {
        Debug.Log("[LobbyFenceTools] " + msg);
        return msg;
    }
}
