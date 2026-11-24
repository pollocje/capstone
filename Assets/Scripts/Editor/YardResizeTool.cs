using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Shrinks the lobby yard (DecorationsRoot/Fence around CountryHouse) about the house.
// Fence segments and the gate move inward and get shorter along their length by the same factor,
// so the joints still meet; yard props, plants and spawn points move inward unscaled and are
// nudged away from the new fence line. Everything keeps its height above the terrain.
// Changes are scene overrides on the Fence instance (Fence.prefab is not touched) and are undoable.
public static class YardResizeTool
{
    const float Factor = 0.62f;
    const float PropClearance = 1.5f;   // metres between a moved object's footprint and the fence line
    const float KeepOutMargin = 2f;     // house / truck / spawn points must stay this far inside
    const float AlreadyShrunkSpan = 60f; // original yard is ~74 m across; below this it was already resized
    const float TreeFootprint = 6f;     // wider footprints are canopies: only the trunk point is kept clear

    // Objects moved without scaling: parent path, name, approximate world xz before the resize.
    static readonly (string parent, string name, float x, float z)[] Movers =
    {
        ("Environment/Structures/DecorationsRoot", "Doghouse", 317.3f, 596.6f),
        ("Environment/Structures/DecorationsRoot", "PT_Fruit_Tree_01_dead", 318.0f, 583.4f),
        ("Environment/Structures/DecorationsRoot", "PT_Fruit_Tree_01_dead_cut", 318.3f, 582.7f),
        ("Environment/Structures/DecorationsRoot", "PT_Pine_Tree_03_logs", 318.3f, 586.9f),
        ("Environment/Structures/DecorationsRoot", "PT_Pine_Tree_03_stump", 317.6f, 585.0f),
        ("Environment/Structures/DecorationsRoot", "PT_Poppy_02", 378.8f, 610.0f),
        ("Environment/Structures/DecorationsRoot", "tree_normal_yellow", 378.2f, 565.7f),
        ("Environment/Structures/Props", "EA04_Animal_Bird_01a_PRE", 381.3f, 601.9f),
        ("Environment/Structures/Props", "EA04_Prop_Oar_01a_PRE", 374.2f, 595.9f),
        ("Environment/Structures/Props", "EA04_Prop_Pole_01a_PRE", 325.6f, 593.9f),
        ("Environment/Structures/Props", "EA04_Prop_Skull_01a_PRE", 379.8f, 603.4f),
        ("Environment/Structures/Props", "EA04_Prop_cauldron_01a_PRE", 321.1f, 586.1f),
        ("Environment/Structures/Props", "EA04_Prop_washing_Comp_PRE (1)", 371.6f, 603.1f),
        ("Environment/Structures/Props", "EA04_Wehicule_Raft_01a_PRE", 324.8f, 583.3f),
        ("Environment/Vegetation", "EA04_Env_Plants_Cactus_Dry_01a_PRE (3)", 324.6f, 605.0f),
        ("Environment/Vegetation", "EA04_Env_Plants_Grass_01c_PRE", 380.0f, 606.5f),
        ("Environment/Vegetation", "EA04_Env_Plants_Tree_01a_PRE (2)", 382.1f, 572.7f),
        ("Environment/Vegetation", "EA04_Env_Tree_Beam_01c_PRE", 319.0f, 588.0f),
        ("Environment/Vegetation", "PT_Grass_02", 380.1f, 608.6f),
        ("Environment/Vegetation", "PT_Grass_02 (1)", 380.2f, 608.0f),
        ("Environment/Vegetation", "PT_Grass_02 (2)", 379.4f, 608.6f),
        ("Environment/Vegetation", "PT_Grass_02 (3)", 379.5f, 608.1f),
        ("Environment/Vegetation", "PT_Grass_02 (4)", 380.3f, 607.5f),
        ("Environment/Vegetation", "PT_Grass_02 (5)", 379.7f, 607.6f),
        ("Gameplay/SpawnRoot", "SpawnPoint1", 323.2f, 593.7f),
        ("Gameplay/SpawnRoot", "SpawnPoint2", 370.8f, 602.4f),
        ("Gameplay/SpawnRoot", "SpawnPoint3", 374.8f, 602.4f),
        ("Gameplay/SpawnRoot", "SpawnPoint4", 378.8f, 602.4f),
    };

    [MenuItem("tools/Environment/Yard Resize/Preview")]
    public static string Preview() => Run(false);

    [MenuItem("tools/Environment/Yard Resize/Apply")]
    public static string Apply() => Run(true);

    static string Run(bool apply)
    {
        var sb = new StringBuilder();
        sb.AppendLine(apply ? $"Yard resize x{Factor} (APPLY)" : $"Yard resize x{Factor} (preview, nothing changed)");
        var group = LobbyFenceTools.FenceGroup();
        var house = FindByPath("Environment/Structures/DecorationsRoot/CountryHouse");
        var terrain = Terrain.activeTerrain;
        if (group == null || house == null || terrain == null) return Report("Fence/FENCE, CountryHouse or the terrain not found");
        var gate = group.Find(LobbyFenceTools.GateName);
        var pivot = new Vector2(house.position.x, house.position.z);

        // Current fence outline.
        var segs = new List<Transform>();
        var ends = new List<Vector2>();
        foreach (Transform t in group)
        {
            if (t == gate) continue;
            var mf = t.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            segs.Add(t);
            var (a, b) = LobbyFenceTools.Ends(t, mf.sharedMesh.bounds);
            ends.Add(a); ends.Add(b);
        }
        if (gate != null) ends.AddRange(LobbyFenceTools.GateEnds(gate));
        var oldPoly = LobbyFenceTools.Simplify(LobbyFenceTools.Hull(ends), 20f);
        float spanX = oldPoly.Max(c => c.x) - oldPoly.Min(c => c.x), spanZ = oldPoly.Max(c => c.y) - oldPoly.Min(c => c.y);
        sb.AppendLine($"Fence: {segs.Count} segments{(gate ? " + gate" : "")}, outline {spanX:F1} x {spanZ:F1} m, pivot (house) ({pivot.x:F1},{pivot.y:F1})");
        if (Mathf.Max(spanX, spanZ) < AlreadyShrunkSpan)
            return Report(sb.AppendLine($"Yard is already under {AlreadyShrunkSpan} m across - looks resized already, nothing done.").ToString());

        Vector2 Shrink(Vector2 p) => pivot + (p - pivot) * Factor;
        var newPoly = oldPoly.Select(Shrink).ToList();
        var centroid = newPoly.Aggregate(Vector2.zero, (s, c) => s + c) / newPoly.Count;
        sb.AppendLine($"New outline {spanX * Factor:F1} x {spanZ * Factor:F1} m, corners {string.Join(" ", newPoly.Select(c => $"({c.x:F1},{c.y:F1})"))}");

        var moves = new List<(Transform t, Vector3 pos, Vector3 localScale)>();

        // Fence segments: position about the house, length along the mesh's long axis.
        foreach (var t in segs)
        {
            var mb = t.GetComponent<MeshFilter>().sharedMesh.bounds;
            var (longAx, _, _) = LobbyFenceTools.Axes(t, mb);
            var scale = t.localScale;
            scale[longAx] *= Factor;
            moves.Add((t, Grounded(terrain, t.position, Shrink(Flat2(t.position))), scale));
        }

        // Gate: shorten along whichever of its own axes runs along the opening.
        if (gate != null)
        {
            var ge = LobbyFenceTools.GateEnds(gate);
            var along = (ge[1] - ge[0]).normalized;
            int axis = Enumerable.Range(0, 3).OrderByDescending(i => Mathf.Abs(Vector2.Dot(Flat2(gate.rotation * Unit(i)).normalized, along))).First();
            float align = Mathf.Abs(Vector2.Dot(Flat2(gate.rotation * Unit(axis)).normalized, along));
            if (align < 0.95f)
                return Report(sb.AppendLine($"Gate axes don't line up with the opening (best {align:F2}); not scaling it, nothing done.").ToString());
            var scale = gate.localScale;
            scale[axis] *= Factor;
            moves.Add((gate, Grounded(terrain, gate.position, Shrink(Flat2(gate.position))), scale));
            sb.AppendLine($"Gate: opening {Vector2.Distance(ge[0], ge[1]):F1} m -> {Vector2.Distance(ge[0], ge[1]) * Factor:F1} m (local axis {axis})");
        }

        // Props, plants and spawn points: move unscaled, keep clear of the new fence line.
        var footprints = new List<(string name, Rect r)>();
        sb.AppendLine("Moved without scaling:");
        foreach (var (parentPath, name, x, z) in Movers)
        {
            var t = FindNear(parentPath, name, new Vector2(x, z));
            if (t == null) { sb.AppendLine($"  !! {parentPath}/{name} not found near ({x},{z}) - skipped"); continue; }
            var offsets = Footprint(t);
            var target = Shrink(Flat2(t.position));
            float nudged = 0f;
            var toCenter = (centroid - target).normalized;
            while (nudged < 20f && offsets.Any(o => !LobbyFenceTools.InsideWithMargin(newPoly, target + o, PropClearance)))
            {
                target += toCenter * 0.25f; nudged += 0.25f;
            }
            bool ok = offsets.All(o => LobbyFenceTools.InsideWithMargin(newPoly, target + o, PropClearance));
            var pos = Grounded(terrain, t.position, target);
            moves.Add((t, pos, t.localScale));
            if (offsets.Count > 1) footprints.Add((t.name, RectOf(offsets.Select(o => target + o))));
            sb.AppendLine($"  {name}: ({t.position.x:F1},{t.position.z:F1}) -> ({pos.x:F1},{pos.z:F1})" +
                          (nudged > 0 ? $", nudged {nudged:F2} m in from the fence" : "") + (ok ? "" : "  !! still within clearance of the fence"));
        }

        // House footprint and what has to stay inside the yard.
        var houseRect = RectOf(house.GetComponentsInChildren<Renderer>().SelectMany(r => LobbyFenceTools.Corners(r.bounds)).Select(Flat2));
        sb.AppendLine($"House footprint x {houseRect.xMin:F1}..{houseRect.xMax:F1}, z {houseRect.yMin:F1}..{houseRect.yMax:F1}");
        foreach (var c in new[] { houseRect.min, houseRect.max, new Vector2(houseRect.xMin, houseRect.yMax), new Vector2(houseRect.xMax, houseRect.yMin) })
            if (!LobbyFenceTools.InsideWithMargin(newPoly, c, KeepOutMargin)) sb.AppendLine($"  !! house corner ({c.x:F1},{c.y:F1}) is within {KeepOutMargin} m of / outside the new fence");
        foreach (var (name, r) in footprints)
            if (r.Overlaps(houseRect)) sb.AppendLine($"  !! {name} footprint overlaps the house");

        var truck = FindByPath("Vehicles/TruckRootNEW");
        if (truck != null)
        {
            var tp = Flat2(truck.position);
            var truckRect = RectOf(truck.GetComponentsInChildren<Renderer>().SelectMany(r => LobbyFenceTools.Corners(r.bounds)).Select(Flat2));
            bool inside = LobbyFenceTools.InsideWithMargin(newPoly, tp, KeepOutMargin);
            sb.AppendLine($"Truck at ({tp.x:F1},{tp.y:F1}), body {truckRect.width:F1} x {truckRect.height:F1} m (axis-aligned), {(inside ? "inside" : "!! NOT inside")} the new yard");
            foreach (var (name, r) in footprints)
                if (r.Overlaps(truckRect)) sb.AppendLine($"  !! {name} would sit on the truck");

            // Largest clear circle (fence, house, props) - the room the truck has to turn.
            var obstacles = footprints.Select(f => f.r).Append(houseRect).ToList();
            float Clear(Vector2 p)
            {
                if (!LobbyFenceTools.InsideWithMargin(newPoly, p, 0f)) return 0f;
                float d = float.MaxValue;
                for (int i = 0; i < newPoly.Count; i++) d = Mathf.Min(d, LobbyFenceTools.DistToSegment(p, newPoly[i], newPoly[(i + 1) % newPoly.Count]));
                foreach (var r in obstacles) d = Mathf.Min(d, DistToRect(p, r));
                return d;
            }
            Vector2 best = tp; float bestR = 0f;
            var bb = RectOf(newPoly);
            for (float x = bb.xMin; x <= bb.xMax; x += 0.5f)
                for (float z = bb.yMin; z <= bb.yMax; z += 0.5f)
                {
                    float r = Clear(new Vector2(x, z));
                    if (r > bestR) { bestR = r; best = new Vector2(x, z); }
                }
            sb.AppendLine($"Largest clear turning circle: {bestR * 2f:F1} m across at ({best.x:F1},{best.y:F1}); clear radius around the parked truck {Clear(tp):F1} m");
        }

        if (!apply) return Report(sb.ToString());

        Undo.SetCurrentGroupName("Shrink lobby yard");
        foreach (var (t, pos, scale) in moves)
        {
            Undo.RecordObject(t, "Shrink lobby yard");
            t.position = pos;
            t.localScale = scale;
            if (PrefabUtility.IsPartOfPrefabInstance(t)) PrefabUtility.RecordPrefabInstancePropertyModifications(t);
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        var road = LobbyFenceTools.RoadSamples();
        if (road.Count > 0)
        {
            float minClear = group.GetComponentsInChildren<Renderer>().SelectMany(r => LobbyFenceTools.Corners(r.bounds))
                .Select(p => LobbyFenceTools.NearestRoad(road, p)).Min();
            sb.AppendLine($"Closest fence part to a road: {minClear:F2} m");
        }
        sb.AppendLine($"Applied {moves.Count} transform changes (Ctrl+Z undoes them). Gate / collider check:");
        sb.Append(LobbyFenceTools.Verify());
        return Report(sb.ToString());
    }

    // Same height above the terrain at the new spot as at the old one.
    static Vector3 Grounded(Terrain terrain, Vector3 old, Vector2 xz)
    {
        float above = old.y - LobbyFenceTools.Ground(terrain, old);
        var p = new Vector3(xz.x, 0f, xz.y);
        p.y = LobbyFenceTools.Ground(terrain, p) + above;
        return p;
    }

    // Footprint corners relative to the pivot (xz). Canopies and empties use just the pivot point.
    static List<Vector2> Footprint(Transform t)
    {
        var rs = t.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new List<Vector2> { Vector2.zero };
        var rect = RectOf(rs.SelectMany(r => LobbyFenceTools.Corners(r.bounds)).Select(Flat2));
        if (rect.width > TreeFootprint || rect.height > TreeFootprint) return new List<Vector2> { Vector2.zero };
        var p = Flat2(t.position);
        return new List<Vector2> { rect.min - p, rect.max - p, new Vector2(rect.xMin, rect.yMax) - p, new Vector2(rect.xMax, rect.yMin) - p };
    }

    static Transform FindByPath(string path)
    {
        var parts = path.Split('/');
        var root = EditorSceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == parts[0]);
        return root == null ? null : parts.Length == 1 ? root.transform : root.transform.Find(string.Join("/", parts.Skip(1)));
    }

    // Names repeat under Vegetation, so pick the child with that name closest to where it was.
    static Transform FindNear(string parentPath, string name, Vector2 expected)
    {
        var parent = FindByPath(parentPath);
        if (parent == null) return null;
        return parent.Cast<Transform>().Where(c => c.name == name)
            .Select(c => (c, d: Vector2.Distance(Flat2(c.position), expected)))
            .Where(x => x.d < 2f).OrderBy(x => x.d).Select(x => x.c).FirstOrDefault();
    }

    static Rect RectOf(IEnumerable<Vector2> pts)
    {
        var l = pts.ToList();
        float x0 = l.Min(p => p.x), x1 = l.Max(p => p.x), z0 = l.Min(p => p.y), z1 = l.Max(p => p.y);
        return Rect.MinMaxRect(x0, z0, x1, z1);
    }

    static float DistToRect(Vector2 p, Rect r)
    {
        float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax), dz = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    static Vector2 Flat2(Vector3 v) => new Vector2(v.x, v.z);

    static Vector3 Unit(int axis) => axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;

    static string Report(string msg)
    {
        Debug.Log("[YardResizeTool] " + msg);
        return msg;
    }
}
