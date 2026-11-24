using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;

// Terrain/tree maintenance for Prototype_World: audit trees (terrain prototypes and
// scene GameObjects) for missing colliders and broken (white) materials.
public static class TerrainTreeTools
{
    static readonly string[] TreeWords = { "tree", "pine", "oak", "birch", "palm", "spruce", "fir", "maple", "conifer", "willow", "cactus" };

    [MenuItem("tools/Terrain/Audit Trees")]
    public static string Audit()
    {
        var sb = new StringBuilder();
        foreach (var t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var d = t.terrainData;
            sb.AppendLine($"TERRAIN {Path(t.transform)} pos={t.transform.position} data={AssetDatabase.GetAssetPath(d)} res={d.heightmapResolution} size={d.size} trees={d.treeInstanceCount} drawTrees={t.drawTreesAndFoliage}");
            var protos = d.treePrototypes;
            var counts = new int[protos.Length];
            foreach (var ti in d.treeInstances) if (ti.prototypeIndex < counts.Length) counts[ti.prototypeIndex]++;
            for (int i = 0; i < protos.Length; i++)
            {
                var p = protos[i].prefab;
                sb.AppendLine($"  proto[{i}] {(p ? AssetDatabase.GetAssetPath(p) : "NULL")} instances={counts[i]}");
                if (p) DescribeHierarchy(p, sb, "    ");
            }
        }

        // Scene GameObject trees, grouped by source prefab (or name if not a prefab instance).
        var groups = new Dictionary<string, List<GameObject>>();
        foreach (var go in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(x => x.gameObject))
        {
            if (!IsTreeRoot(go)) continue;
            var src = PrefabUtility.GetCorrespondingObjectFromOriginalSource(go);
            string key = src ? AssetDatabase.GetAssetPath(src) : "(no prefab) " + go.name;
            if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<GameObject>();
            list.Add(go);
        }
        sb.AppendLine($"SCENE TREE GROUPS: {groups.Count}");
        foreach (var kv in groups.OrderBy(k => k.Key))
        {
            var first = kv.Value[0];
            int withCol = kv.Value.Count(g => g.GetComponentsInChildren<Collider>(true).Length > 0);
            sb.AppendLine($"  {kv.Key} count={kv.Value.Count} withCollider={withCol} e.g. {Path(first.transform)} scale={first.transform.lossyScale}");
            DescribeHierarchy(first, sb, "    ");
        }
        return Report(sb.ToString());
    }

    // Standing Polytope forest trees ship without colliders. Logs/stumps are left alone.
    static readonly string[] ColliderlessTreePrefabs =
    {
        "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Trees/PT_Fruit_Tree_01_dead.prefab",
        "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Trees/PT_Fruit_Tree_01_dead_cut.prefab",
        "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Trees/PT_Pine_Tree_03_dead.prefab",
        "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Trees/PT_Pine_Tree_03_dead_cut.prefab",
        "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Trees/PT_Pine_Tree_03_green.prefab",
        "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Trees/PT_Pine_Tree_03_green_cut.prefab",
    };

    // Adds a trunk-only CapsuleCollider to each scene instance of the prefabs above that has no collider
    // (re-running updates the capsule this tool added). "_cut" trees are the full tree minus its stump,
    // so they reuse the full tree's trunk radius and the capsule is extended down to the ground.
    [MenuItem("tools/Terrain/Add Trunk Colliders To Trees")]
    public static string AddTrunkColliders()
    {
        var sb = new StringBuilder();
        foreach (var path in ColliderlessTreePrefabs)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var full = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace("_cut.prefab", ".prefab"));
            if (!prefab || !full) { sb.AppendLine("missing " + path); continue; }
            if (!TryMeasureTrunk(full, out var center, out float radius, out _, out _) ||
                !TryMeasureTrunk(prefab, out _, out _, out float minY, out float maxY))
            { sb.AppendLine("could not measure trunk of " + path); continue; }
            if (prefab != full) minY = Mathf.Min(minY, 0f);
            float height = Mathf.Max(maxY - minY, radius * 2f);
            center.y = minY + height * 0.5f;

            int added = 0, updated = 0, skipped = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var go = t.gameObject;
                if (PrefabUtility.GetOutermostPrefabInstanceRoot(go) != go) continue;
                if (PrefabUtility.GetCorrespondingObjectFromOriginalSource(go) != prefab) continue;
                var cols = go.GetComponentsInChildren<Collider>(true);
                var cap = cols.Length == 1 ? cols[0] as CapsuleCollider : null;
                if (cap && cap.gameObject == go) { Undo.RecordObject(cap, "Trunk Collider"); updated++; }
                else if (cols.Length > 0) { skipped++; continue; }
                else { cap = Undo.AddComponent<CapsuleCollider>(go); added++; }
                cap.direction = 1;
                cap.center = center;
                cap.radius = radius;
                cap.height = height;
            }
            sb.AppendLine($"{System.IO.Path.GetFileNameWithoutExtension(path)}: r={radius:F2} h={height:F2} c={center} added={added} updated={updated} otherCollider={skipped}");
        }
        MarkSceneDirty();
        return Report(sb.ToString());
    }

    // Trunk axis/radius from LOD0 trunk-material vertices in the prefab root's local space.
    // Radius is measured near the base (0.3-1.5 m up), height runs to the top of the trunk submesh.
    static bool TryMeasureTrunk(GameObject prefab, out Vector3 center, out float radius, out float minY, out float maxY)
    {
        center = Vector3.zero; radius = 0f; minY = maxY = 0f;
        var mf = prefab.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.sharedMesh && !f.name.Contains("LOD1") && !f.name.Contains("LOD2"));
        if (!mf) return false;
        var mesh = mf.sharedMesh;
        var mats = mf.GetComponent<MeshRenderer>().sharedMaterials;
        var toRoot = prefab.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
        var verts = mesh.vertices;

        var pts = new List<Vector3>();
        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            var mat = s < mats.Length ? mats[s] : null;
            if (mat && mat.name.ToLowerInvariant().Contains("leaves")) continue; // canopy
            foreach (int i in mesh.GetIndices(s)) pts.Add(toRoot.MultiplyPoint3x4(verts[i]));
        }
        if (pts.Count == 0) return false;

        float bottom = pts.Min(p => p.y);
        minY = bottom; maxY = pts.Max(p => p.y);
        var band = pts.Where(p => p.y >= bottom + 0.3f && p.y <= bottom + 1.5f).ToList();
        if (band.Count < 3) band = pts.Where(p => p.y <= bottom + 1.5f).ToList();
        float cx = band.Average(p => p.x), cz = band.Average(p => p.z);
        // 75th percentile distance from the axis: ignores roots/branch stubs flaring out at the base.
        var d = band.Select(p => new Vector2(p.x - cx, p.z - cz).magnitude).OrderBy(v => v).ToList();
        radius = Mathf.Clamp(d[(int)(d.Count * 0.75f)], 0.1f, 0.6f);
        center = new Vector3(cx, (minY + maxY) * 0.5f, cz);
        return true;
    }

    // ---- Terrain bumps ----

    const float BumpHeight = 5f;          // metres, max raise (taller than the truck so hills read from ground level)
    const float BumpLargeScale = 120f;    // metres per noise cell (broad hills)
    const float BumpSmallScale = 60f;     // metres per noise cell (secondary swells; keep large or it looks jiggly)
    const float FlatMargin = 6f;          // metres kept perfectly flat around protected areas
    const float BlendDistance = 45f;      // metres over which bumps fade in
    const float SpawnRadius = 20f;        // flat radius around spawn points and vehicles

    // Raises gentle Perlin bumps on open ground. Roads, spawn points, vehicles and every
    // non-vegetation object (buildings, props, rocks, water) are kept flat with a soft blend.
    // Vegetation is moved with the ground so it doesn't float or sink.
    // `baseTerrainPath` (optional): a TerrainData asset holding the pre-bump heights; the bumps are
    // rebuilt on top of it instead of stacking on the current heightmap.
    [MenuItem("tools/Terrain/Add Gentle Bumps")]
    public static string AddGentleBumps() => AddGentleBumps(null);

    public static string AddGentleBumps(string baseTerrainPath)
    {
        var terrain = Terrain.activeTerrain;
        if (!terrain) return Report("No active terrain.");
        var data = terrain.terrainData;
        int res = data.heightmapResolution;
        Vector3 size = data.size, origin = terrain.transform.position;
        float cell = size.x / (res - 1);

        // 1. Protected samples.
        var prot = new bool[res, res];
        int roadSamples = 0;
        var roadRoot = GameObject.Find("Road Network");
        if (roadRoot)
            foreach (var mf in roadRoot.GetComponentsInChildren<MeshFilter>())
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mf.sharedMesh && mr && mr.enabled) roadSamples += RasterizeXZ(mf, origin, size, res, prot);
            }

        var vegRoots = FindVegetationRoots();
        var vegSet = new HashSet<Transform>(vegRoots);
        int protectedObjects = 0;
        var skippedHuge = new List<string>();
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!r.enabled || r is ParticleSystemRenderer) continue;
            if (IsUnder(r.transform, vegSet) || (roadRoot && r.transform.IsChildOf(roadRoot.transform))) continue;
            var b = r.bounds;
            if (b.size.x > 300f || b.size.z > 300f) { skippedHuge.Add(Path(r.transform)); continue; }
            MarkRect(prot, origin, cell, res, b.min.x, b.min.z, b.max.x, b.max.z);
            protectedObjects++;
        }
        foreach (var name in new[] { "SpawnRoot", "Vehicles" })
        {
            var root = GameObject.Find(name);
            if (!root) continue;
            foreach (Transform c in root.transform)
            {
                var p = c.position;
                MarkRect(prot, origin, cell, res, p.x - SpawnRadius, p.z - SpawnRadius, p.x + SpawnRadius, p.z + SpawnRadius);
            }
        }

        // 2. Distance (metres) from each sample to the nearest protected sample (chamfer transform).
        var dist = ChamferDistance(prot, res, cell);

        // 3. Apply noise.
        Undo.RegisterCompleteObjectUndo(data, "Add Gentle Bumps");
        var before = vegRoots.Select(t => terrain.SampleHeight(t.position)).ToArray();
        float[,] h = data.GetHeights(0, 0, res, res);
        if (!string.IsNullOrEmpty(baseTerrainPath))
        {
            var baseData = AssetDatabase.LoadAssetAtPath<TerrainData>(baseTerrainPath);
            if (!baseData || baseData.heightmapResolution != res) return Report("Base terrain missing or resolution differs: " + baseTerrainPath);
            h = baseData.GetHeights(0, 0, res, res);
        }
        float maxAdd = 0f, maxSlope = 0f; int changed = 0;
        var added = new float[res, res];
        for (int z = 0; z < res; z++)
        for (int x = 0; x < res; x++)
        {
            float w = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(FlatMargin, FlatMargin + BlendDistance, dist[z, x]));
            if (w <= 0f) continue;
            float wx = origin.x + x * cell, wz = origin.z + z * cell;
            float n = 0.8f * Mathf.PerlinNoise(wx / BumpLargeScale + 31.7f, wz / BumpLargeScale + 12.3f)
                    + 0.2f * Mathf.PerlinNoise(wx / BumpSmallScale + 7.1f, wz / BumpSmallScale + 53.9f);
            float add = BumpHeight * w * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.8f, n));
            if (add <= 0f) continue;
            added[z, x] = add;
            h[z, x] = Mathf.Min(1f, h[z, x] + add / size.y);
            maxAdd = Mathf.Max(maxAdd, add); changed++;
        }
        // Steepest slope the bumps themselves introduce (degrees).
        for (int z = 1; z < res; z++)
        for (int x = 1; x < res; x++)
            maxSlope = Mathf.Max(maxSlope, Mathf.Abs(added[z, x] - added[z, x - 1]) / cell, Mathf.Abs(added[z, x] - added[z - 1, x]) / cell);
        maxSlope = Mathf.Atan(maxSlope) * Mathf.Rad2Deg;
        data.SetHeights(0, 0, h);
        data.SyncHeightmap();
        EditorUtility.SetDirty(data);
        // Pixel error 5 lets terrain LOD visibly morph the hills as the camera moves ("jiggly"); 1 keeps them stable.
        Undo.RecordObject(terrain, "Add Gentle Bumps");
        terrain.heightmapPixelError = 1f;
        EditorUtility.SetDirty(terrain);

        // 4. Keep vegetation on the ground.
        int moved = 0;
        for (int i = 0; i < vegRoots.Count; i++)
        {
            float dy = terrain.SampleHeight(vegRoots[i].position) - before[i];
            if (Mathf.Abs(dy) < 0.005f) continue;
            Undo.RecordObject(vegRoots[i], "Add Gentle Bumps");
            vegRoots[i].position += new Vector3(0f, dy, 0f);
            moved++;
        }
        MarkSceneDirty();
        return Report($"Raised {changed}/{res * res} samples, max +{maxAdd:F2} m, steepest added slope {maxSlope:F1} deg. Protected: {roadSamples} road samples, {protectedObjects} objects, spawn/vehicles r={SpawnRadius} m. Re-grounded {moved}/{vegRoots.Count} vegetation objects."
                      + (skippedHuge.Count > 0 ? "\nNot protected (too large): " + string.Join(", ", skippedHuge) : ""));
    }

    // ---- Map edge ----

    const float BoundsInset = 3f;       // walls sit this far inside the terrain edge
    const float BoundsThickness = 10f;
    const float BoundsHeight = 400f;    // tall enough that tornado-lifted players stay inside

    // Invisible walls around the terrain so players and vehicles can't leave the map.
    // Re-running replaces the existing walls.
    [MenuItem("tools/Terrain/Create Map Bounds")]
    public static string CreateMapBounds()
    {
        var terrain = Terrain.activeTerrain;
        if (!terrain) return Report("No active terrain.");
        var size = terrain.terrainData.size;
        var o = terrain.transform.position;
        var parent = terrain.transform.parent;

        var old = parent ? parent.Find("MapBounds") : GameObject.Find("MapBounds")?.transform;
        if (old) Undo.DestroyObjectImmediate(old.gameObject);

        var root = new GameObject("MapBounds");
        Undo.RegisterCreatedObjectUndo(root, "Create Map Bounds");
        if (parent) root.transform.SetParent(parent, false);
        root.transform.position = Vector3.zero;

        float cy = o.y - 20f + BoundsHeight * 0.5f;
        float half = BoundsThickness * 0.5f;
        var c = new Vector3(o.x + size.x * 0.5f, cy, o.z + size.z * 0.5f);
        var walls = new (string name, Vector3 pos, Vector3 scale)[]
        {
            ("Wall_West",  new Vector3(o.x + BoundsInset - half, cy, c.z),          new Vector3(BoundsThickness, BoundsHeight, size.z + 2 * BoundsThickness)),
            ("Wall_East",  new Vector3(o.x + size.x - BoundsInset + half, cy, c.z), new Vector3(BoundsThickness, BoundsHeight, size.z + 2 * BoundsThickness)),
            ("Wall_South", new Vector3(c.x, cy, o.z + BoundsInset - half),          new Vector3(size.x + 2 * BoundsThickness, BoundsHeight, BoundsThickness)),
            ("Wall_North", new Vector3(c.x, cy, o.z + size.z - BoundsInset + half), new Vector3(size.x + 2 * BoundsThickness, BoundsHeight, BoundsThickness)),
        };
        foreach (var w in walls)
        {
            var go = new GameObject(w.name);
            go.transform.SetParent(root.transform, false);
            go.transform.position = w.pos;
            go.isStatic = true;
            go.AddComponent<BoxCollider>().size = w.scale;
        }
        root.isStatic = true;
        MarkSceneDirty();
        return Report($"Created {Path(root.transform)} with 4 invisible walls {BoundsInset} m inside the terrain edge, {BoundsHeight} m tall.");
    }

    // Diagnostic: how the loaded terrain differs from another TerrainData asset, plus trunk-collider count.
    public static string CompareTerrain(string otherPath)
    {
        var data = Terrain.activeTerrain.terrainData;
        var other = AssetDatabase.LoadAssetAtPath<TerrainData>(otherPath);
        int res = data.heightmapResolution;
        var a = data.GetHeights(0, 0, res, res);
        var b = other.GetHeights(0, 0, res, res);
        float maxDiff = 0f; int diff = 0;
        for (int z = 0; z < res; z++) for (int x = 0; x < res; x++)
        {
            float d = Mathf.Abs(a[z, x] - b[z, x]) * data.size.y;
            if (d > 0.01f) diff++;
            maxDiff = Mathf.Max(maxDiff, d);
        }
        int caps = Object.FindObjectsByType<CapsuleCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None).Count(c => c.name.StartsWith("PT_"));
        return $"samples differing >1cm: {diff}, max diff {maxDiff:F2} m; PT trunk capsules in scene: {caps}";
    }

    // Diagnostic: small-scale roughness = height minus its local (2r+1)^2 average, on low-slope ground.
    public static string MeasureRoughness(string terrainPath)
    {
        var data = string.IsNullOrEmpty(terrainPath) ? Terrain.activeTerrain.terrainData : AssetDatabase.LoadAssetAtPath<TerrainData>(terrainPath);
        int res = data.heightmapResolution; float sy = data.size.y, cell = data.size.x / (res - 1);
        var h = data.GetHeights(0, 0, res, res);
        var sb = new StringBuilder();
        foreach (int r in new[] { 1, 3 })
        {
            var devs = new List<float>();
            for (int z = r; z < res - r; z += 2)
            for (int x = r; x < res - r; x += 2)
            {
                float sum = 0; int n = 0;
                for (int dz = -r; dz <= r; dz++) for (int dx = -r; dx <= r; dx++) { sum += h[z + dz, x + dx]; n++; }
                float slope = Mathf.Abs(h[z, x + r] - h[z, x - r]) * sy / (2 * r * cell);
                if (slope > 0.15f) continue; // skip hills/mountains
                devs.Add(Mathf.Abs(h[z, x] - sum / n) * sy);
            }
            devs.Sort();
            sb.Append($"r={r} ({(2 * r + 1) * cell:F0} m window): median {devs[devs.Count / 2] * 100:F1} cm, p95 {devs[(int)(devs.Count * 0.95f)] * 100:F1} cm, max {devs[devs.Count - 1] * 100:F0} cm; ");
        }
        // Also report a heightmap value histogram step to detect quantization stairs.
        sb.Append($"cell={cell:F2} m, 16-bit step={sy / 65535f * 100:F2} cm");
        return sb.ToString();
    }

    // Lists scene objects (outermost prefab instance, or plain renderer object) with no collider anywhere
    // in their hierarchy, grouped by prefab/mesh, largest first.
    public static string AuditMissingColliders()
    {
        var groups = new Dictionary<string, List<GameObject>>();
        foreach (var go in FindColliderlessObjects())
        {
            var src = PrefabUtility.GetCorrespondingObjectFromOriginalSource(go);
            string key = src ? AssetDatabase.GetAssetPath(src) : "(scene) " + go.name.Split(' ')[0];
            if (!groups.TryGetValue(key, out var l)) groups[key] = l = new List<GameObject>();
            l.Add(go);
        }
        var sb = new StringBuilder();
        foreach (var kv in groups.OrderByDescending(k => RendererBounds(k.Value[0]).size.magnitude))
        {
            var b = RendererBounds(kv.Value[0]);
            sb.AppendLine($"{kv.Key} x{kv.Value.Count} size={b.size} e.g. {Path(kv.Value[0].transform)}");
        }
        return sb.ToString();
    }

    static readonly string[] NoColliderWords = { "water", "waterfall", "grass", "poppy", "flower" };

    // Gives every solid-looking object without a collider one fitted to its shape:
    // stumps/trees get a trunk capsule, awnings a mesh collider (walkable underneath), everything else a box.
    [MenuItem("tools/Terrain/Add Missing Colliders")]
    public static string AddMissingColliders()
    {
        var counts = new SortedDictionary<string, int>();
        foreach (var go in FindColliderlessObjects().ToList())
        {
            string n = go.name.ToLowerInvariant();
            if (NoColliderWords.Any(w => n.Contains(w))) continue;
            string kind;
            if (n.Contains("stump") || n.StartsWith("dry"))
            {
                if (!TryMeasureTrunk(go, out var center, out float radius, out float minY, out float maxY)) continue;
                var cap = Undo.AddComponent<CapsuleCollider>(go);
                cap.direction = 1; cap.radius = radius;
                cap.height = Mathf.Max(maxY - minY, radius * 2f);
                cap.center = center;
                kind = "capsule";
            }
            else if (n.Contains("awning"))
            {
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                    if (mf.sharedMesh) Undo.AddComponent<MeshCollider>(mf.gameObject).sharedMesh = mf.sharedMesh;
                kind = "mesh";
            }
            else
            {
                if (!TryLocalBounds(go, out var b)) continue;
                var box = Undo.AddComponent<BoxCollider>(go);
                box.center = b.center; box.size = b.size;
                kind = "box";
            }
            string key = $"{go.name.Split(' ')[0]} ({kind})";
            counts[key] = counts.TryGetValue(key, out int c) ? c + 1 : 1;
        }
        MarkSceneDirty();
        return Report("Added colliders:\n" + string.Join("\n", counts.Select(kv => $"  {kv.Key} x{kv.Value}")));
    }

    // Bounds of the LOD0 (or only) meshes in the object's local space.
    static bool TryLocalBounds(GameObject go, out Bounds result)
    {
        result = default; bool any = false;
        var toLocal = go.transform.worldToLocalMatrix;
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
        {
            if (!mf.sharedMesh || mf.name.Contains("LOD1") || mf.name.Contains("LOD2") || mf.name.Contains("LOD3")) continue;
            var mb = mf.sharedMesh.bounds;
            var m = toLocal * mf.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                var p = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                if (!any) { result = new Bounds(p, Vector3.zero); any = true; } else result.Encapsulate(p);
            }
        }
        return any;
    }

    static IEnumerable<GameObject> FindColliderlessObjects()
    {
        var road = GameObject.Find("Road Network");
        var seen = new HashSet<GameObject>();
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!(r is MeshRenderer) || !r.enabled) continue;
            if (road && r.transform.IsChildOf(road.transform)) continue;
            if (r.GetComponentInParent<Rigidbody>()) continue; // vehicles / physics props manage their own
            var root = PrefabUtility.GetOutermostPrefabInstanceRoot(r.gameObject) ?? r.gameObject;
            // Plain (non-prefab) multi-part models: climb while the parent also renders.
            if (!PrefabUtility.IsPartOfPrefabInstance(root))
                while (root.transform.parent && root.transform.parent.GetComponent<MeshRenderer>()) root = root.transform.parent.gameObject;
            if (!seen.Add(root)) continue;
            if (root.GetComponentsInChildren<Collider>(true).Any(IsWorkingSolid)) continue;
            yield return root;
        }
    }

    // A collider that actually blocks: enabled, active, not a trigger, and a MeshCollider has a mesh.
    static bool IsWorkingSolid(Collider c) =>
        c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger && !(c is MeshCollider m && m.sharedMesh == null);

    static Bounds RendererBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    // Diagnostic: top-down PNG of (current - base) height, white = +maxM metres. North is up.
    public static string HeightDiffImage(string basePath, float maxM, string outPath)
    {
        var data = Terrain.activeTerrain.terrainData;
        var other = AssetDatabase.LoadAssetAtPath<TerrainData>(basePath);
        int res = data.heightmapResolution;
        var a = data.GetHeights(0, 0, res, res); var b = other.GetHeights(0, 0, res, res);
        var tex = new Texture2D(res, res, TextureFormat.RGB24, false);
        for (int z = 0; z < res; z++) for (int x = 0; x < res; x++)
        {
            float d = (a[z, x] - b[z, x]) * data.size.y / maxM;
            tex.SetPixel(x, z, d < 0 ? Color.red : new Color(d, d, d));
        }
        tex.Apply();
        System.IO.File.WriteAllBytes(outPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        return "wrote " + outPath;
    }

    // Diagnostic: colliders and rigidbodies under a scene object (MCP can't read colliders safely).
    public static string DescribeColliders(string path)
    {
        var go = GameObject.Find(path);
        if (!go) return "not found: " + path;
        var sb = new StringBuilder();
        foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true))
            sb.AppendLine($"Rigidbody on {rb.name}: kinematic={rb.isKinematic} mode={rb.collisionDetectionMode} mass={rb.mass} interp={rb.interpolation}");
        foreach (var c in go.GetComponentsInChildren<Collider>(true))
        {
            string extra = c is BoxCollider b ? $" size={b.size} center={b.center}" : c is MeshCollider m ? $" mesh={(m.sharedMesh ? m.sharedMesh.name : "NONE")} convex={m.convex}" : "";
            sb.AppendLine($"{c.GetType().Name} on {Path(c.transform)} enabled={c.enabled} active={c.gameObject.activeInHierarchy} trigger={c.isTrigger} layer={LayerMask.LayerToName(c.gameObject.layer)} scale={c.transform.lossyScale}{extra}");
        }
        return sb.ToString();
    }

    // Outermost prefab instances / renderer objects under any "Vegetation" group.
    static List<Transform> FindVegetationRoots()
    {
        var result = new List<Transform>();
        var seen = new HashSet<Transform>();
        foreach (var group in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.name == "Vegetation"))
            foreach (var r in group.GetComponentsInChildren<Renderer>(true))
            {
                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(r.gameObject);
                var t = root ? root.transform : r.transform;
                // Walk up to the direct child of a plain grouping object (no renderer, not a prefab).
                while (t.parent && t.parent != group && !PrefabUtility.IsPartOfPrefabInstance(t.parent) && t.parent.GetComponent<Renderer>() && t.parent.GetComponent<Terrain>() == null)
                    t = t.parent;
                if (seen.Add(t)) result.Add(t);
            }
        // Drop nested entries whose ancestor is already in the list.
        return result.Where(t => !IsUnder(t.parent, seen)).ToList();
    }

    static bool IsUnder(Transform t, HashSet<Transform> set)
    {
        for (; t != null; t = t.parent) if (set.Contains(t)) return true;
        return false;
    }

    static void MarkRect(bool[,] prot, Vector3 origin, float cell, int res, float x0, float z0, float x1, float z1)
    {
        int gx0 = Mathf.Max(0, Mathf.FloorToInt((x0 - origin.x) / cell)), gx1 = Mathf.Min(res - 1, Mathf.CeilToInt((x1 - origin.x) / cell));
        int gz0 = Mathf.Max(0, Mathf.FloorToInt((z0 - origin.z) / cell)), gz1 = Mathf.Min(res - 1, Mathf.CeilToInt((z1 - origin.z) / cell));
        for (int z = gz0; z <= gz1; z++)
        for (int x = gx0; x <= gx1; x++) prot[z, x] = true;
    }

    static float[,] ChamferDistance(bool[,] prot, int res, float cell)
    {
        const float Inf = 1e9f;
        float d1 = cell, d2 = cell * 1.41421356f;
        var d = new float[res, res];
        for (int z = 0; z < res; z++) for (int x = 0; x < res; x++) d[z, x] = prot[z, x] ? 0f : Inf;
        for (int z = 0; z < res; z++)
        for (int x = 0; x < res; x++)
        {
            float v = d[z, x];
            if (x > 0) v = Mathf.Min(v, d[z, x - 1] + d1);
            if (z > 0) v = Mathf.Min(v, d[z - 1, x] + d1);
            if (x > 0 && z > 0) v = Mathf.Min(v, d[z - 1, x - 1] + d2);
            if (x < res - 1 && z > 0) v = Mathf.Min(v, d[z - 1, x + 1] + d2);
            d[z, x] = v;
        }
        for (int z = res - 1; z >= 0; z--)
        for (int x = res - 1; x >= 0; x--)
        {
            float v = d[z, x];
            if (x < res - 1) v = Mathf.Min(v, d[z, x + 1] + d1);
            if (z < res - 1) v = Mathf.Min(v, d[z + 1, x] + d1);
            if (x < res - 1 && z < res - 1) v = Mathf.Min(v, d[z + 1, x + 1] + d2);
            if (x > 0 && z < res - 1) v = Mathf.Min(v, d[z + 1, x - 1] + d2);
            d[z, x] = v;
        }
        return d;
    }

    // Marks heightmap samples covered by the mesh's triangles (XZ only).
    static int RasterizeXZ(MeshFilter mf, Vector3 origin, Vector3 size, int res, bool[,] prot)
    {
        var mesh = mf.sharedMesh;
        var verts = mesh.vertices;
        var tris = mesh.triangles;
        var m = mf.transform.localToWorldMatrix;
        for (int i = 0; i < verts.Length; i++) verts[i] = m.MultiplyPoint3x4(verts[i]);
        float toGrid = (res - 1) / size.x, toGridZ = (res - 1) / size.z;
        int marked = 0;
        for (int t = 0; t < tris.Length; t += 3)
        {
            Vector3 a = verts[tris[t]], b = verts[tris[t + 1]], c = verts[tris[t + 2]];
            float den = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
            if (Mathf.Abs(den) < 1e-9f) continue;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, Mathf.Min(b.x, c.x)) - origin.x) * toGrid));
            int x1 = Mathf.Min(res - 1, Mathf.CeilToInt((Mathf.Max(a.x, Mathf.Max(b.x, c.x)) - origin.x) * toGrid));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.z, Mathf.Min(b.z, c.z)) - origin.z) * toGridZ));
            int z1 = Mathf.Min(res - 1, Mathf.CeilToInt((Mathf.Max(a.z, Mathf.Max(b.z, c.z)) - origin.z) * toGridZ));
            for (int gz = z0; gz <= z1; gz++)
            for (int gx = x0; gx <= x1; gx++)
            {
                if (prot[gz, gx]) continue;
                // Conservative: mark any sample within one cell of the triangle's footprint.
                float x = origin.x + gx / toGrid, z = origin.z + gz / toGridZ;
                float l1 = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / den;
                float l2 = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / den;
                float l3 = 1f - l1 - l2;
                if (l1 < -0.25f || l2 < -0.25f || l3 < -0.25f) continue;
                prot[gz, gx] = true; marked++;
            }
        }
        return marked;
    }

    static void MarkSceneDirty()
    {
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    // The Dry_Trees bark material lost its texture links (renders plain white in URP); re-link them.
    [MenuItem("tools/Terrain/Fix White Dry Trees")]
    public static string FixDryTreeBark()
    {
        const string Dir = "Assets/Dry_Trees/Materials/";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(Dir + "Bark.mat");
        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "Bark.tif");
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "Normal.tif");
        if (!mat || !albedo || !normal) return Report("Dry_Trees bark material or textures not found.");
        Undo.RecordObject(mat, "Fix Dry Tree Bark");
        mat.SetTexture("_BaseMap", albedo);
        mat.SetTexture("_MainTex", albedo);
        mat.SetTexture("_BumpMap", normal);
        mat.EnableKeyword("_NORMALMAP");
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssetIfDirty(mat);
        return Report("Bark.mat: " + DescribeMaterial(mat));
    }

    // Lists every scene material that is plain white with no base texture, and the objects using it.
    public static string AuditWhiteMaterials()
    {
        var sb = new StringBuilder();
        var byMat = new Dictionary<Material, List<Renderer>>();
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            foreach (var m in r.sharedMaterials)
            {
                if (m == null || !IsBlankWhite(m)) continue;
                if (!byMat.TryGetValue(m, out var list)) byMat[m] = list = new List<Renderer>();
                list.Add(r);
            }
        foreach (var kv in byMat.OrderByDescending(k => k.Value.Count))
        {
            sb.AppendLine($"{DescribeMaterial(kv.Key)} renderers={kv.Value.Count}");
            foreach (var g in kv.Value.GroupBy(r => { var s = PrefabUtility.GetCorrespondingObjectFromOriginalSource(r.gameObject); return s ? AssetDatabase.GetAssetPath(s) : "(no prefab)"; }))
                sb.AppendLine($"  {g.Key} x{g.Count()} e.g. {Path(g.First().transform)} @ {g.First().bounds.center}");
        }
        return Report(sb.ToString());
    }

    static bool IsBlankWhite(Material m)
    {
        bool hasTexProp = m.HasProperty("_BaseMap") || m.HasProperty("_MainTex");
        if (!hasTexProp) return false;
        var tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.GetTexture("_MainTex");
        if (tex != null) return false;
        var c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
        return c.r > 0.85f && c.g > 0.85f && c.b > 0.85f;
    }

    // A tree "root": matches a tree word and no ancestor also matches (so we don't count LOD children).
    static bool IsTreeRoot(GameObject go)
    {
        if (!IsTreeName(go.name) || go.GetComponentsInChildren<Renderer>(true).Length == 0) return false;
        for (var p = go.transform.parent; p != null; p = p.parent)
            if (IsTreeName(p.name) && p.GetComponentsInChildren<Renderer>(true).Length > 0 && p.GetComponent<Terrain>() == null && !IsGroupName(p.name)) return false;
        return true;
    }

    static bool IsGroupName(string n)
    {
        n = n.ToLowerInvariant();
        return n.EndsWith("s") && (n.EndsWith("trees") || n.EndsWith("palms") || n.EndsWith("pines")) || n.Contains("root") || n.Contains("forest") || n.Contains("group");
    }

    static bool IsTreeName(string n)
    {
        n = n.ToLowerInvariant();
        return TreeWords.Any(w => n.Contains(w));
    }

    static void DescribeHierarchy(GameObject root, StringBuilder sb, string indent)
    {
        foreach (var c in root.GetComponentsInChildren<Collider>(true))
            sb.AppendLine($"{indent}collider {c.GetType().Name} on {c.name} enabled={c.enabled} trigger={c.isTrigger}");
        var lod = root.GetComponent<LODGroup>();
        if (lod) sb.AppendLine($"{indent}LODGroup lods={lod.lodCount}");
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            var mesh = r is MeshRenderer ? r.GetComponent<MeshFilter>()?.sharedMesh : (r as SkinnedMeshRenderer)?.sharedMesh;
            sb.AppendLine($"{indent}renderer {r.GetType().Name} on {r.name} mesh={(mesh ? mesh.name : "none")} bounds={r.bounds.size}");
            foreach (var m in r.sharedMaterials) sb.AppendLine($"{indent}  mat {DescribeMaterial(m)}");
        }
    }

    static string DescribeMaterial(Material m)
    {
        if (m == null) return "NULL";
        var s = m.shader;
        string tex = m.HasProperty("_BaseMap") ? Name(m.GetTexture("_BaseMap")) : m.HasProperty("_MainTex") ? Name(m.GetTexture("_MainTex")) : "-";
        string col = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").ToString() : m.HasProperty("_Color") ? m.GetColor("_Color").ToString() : "-";
        return $"{m.name} [{AssetDatabase.GetAssetPath(m)}] shader={(s ? s.name : "NULL")} supported={(s && s.isSupported)} tex={tex} color={col}";
    }

    // Player's-eye view: camera `eye` metres above the terrain at (x,z), looking along `yaw` degrees.
    public static string SnapshotGround(float x, float z, float yaw, float eye, string outPath)
    {
        var t = Terrain.activeTerrain;
        float y = t.SampleHeight(new Vector3(x, 0, z)) + t.transform.position.y + eye;
        var dir = Quaternion.Euler(8f, yaw, 0f) * Vector3.forward;
        var p = new Vector3(x, y, z) + dir * 10f;
        float dist = 10f;
        // Reuse Snapshot's camera by aiming from (x,y,z) at a point 10 m ahead.
        var go = new GameObject("TreeSnapshotCam") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var cam = go.AddComponent<Camera>();
            cam.farClipPlane = 3000f; cam.fieldOfView = 60f;
            go.transform.position = new Vector3(x, y, z);
            go.transform.LookAt(p);
            return Render(cam, outPath) + $" (eye y={y:F2}, look {dist} m ahead)";
        }
        finally { Object.DestroyImmediate(go); }
    }

    static string Render(Camera cam, string outPath)
    {
        var rt = RenderTexture.GetTemporary(1280, 720, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);
        System.IO.File.WriteAllBytes(outPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        return "wrote " + outPath;
    }

    // Renders a view of (x,y,z) from `dist` metres away (south-east, slightly above) to a PNG.
    // Uses a temporary hidden camera, so the scene is not modified.
    public static string Snapshot(float x, float y, float z, float dist, string outPath)
    {
        var go = new GameObject("TreeSnapshotCam") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var cam = go.AddComponent<Camera>();
            cam.farClipPlane = 3000f;
            var target = new Vector3(x, y, z);
            go.transform.position = target + new Vector3(dist * 0.6f, dist * 0.45f, -dist * 0.66f);
            go.transform.LookAt(target);
            var rt = RenderTexture.GetTemporary(1280, 720, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            System.IO.File.WriteAllBytes(outPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return "wrote " + outPath;
        }
        finally { Object.DestroyImmediate(go); }
    }

    static string Name(Object o) => o ? o.name : "none";

    static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;

    static string Report(string msg)
    {
        Debug.Log("[TerrainTreeTools] " + msg);
        return msg;
    }
}
