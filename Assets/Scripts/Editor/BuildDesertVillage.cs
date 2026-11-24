using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Populates the area around the lobby (DecorationsRoot/CountryHouse) with a desert village:
// houses lining the roads, street props, cacti/bushes/rocks. Everything goes under
// "VillageRoot", which is cleared and rebuilt on every run, so re-running is safe.
// The lobby prefab is never used here - it must stay one of a kind.
public static class BuildDesertVillage
{
    const string RootName = "VillageRoot";
    const string RoadRootName = "Road Network";
    const string LobbyAreaName = "DecorationsRoot";
    const int DefaultSeed = 1337;

    const float HouseMinRadius = 25f;       // from lobby centre
    const float HouseMaxRadius = 140f;
    const int HouseCount = 40;
    const float HouseRoadBuffer = 5f;       // metres between house footprint and road
    const float HouseSpacing = 5f;          // metres between house footprints
    const float VegMaxRadius = 260f;
    const int VegetationCount = 450;
    const float LobbyMargin = 12f;

    const string Rpg = "Assets/RPGPP_LT/Prefabs/";
    const string Sky = "Assets/Skyden_Games/Low Poly Environment/Prefabs/";
    const string Ea = "Assets/EmaceArt - raft on the desert/Prefabs/";

    // (path, max instances)
    static readonly (string path, int max)[] Houses =
    {
        (Rpg + "Buildings/Bld_closed/rpgpp_lt_building_01.prefab", 4),
        (Rpg + "Buildings/Bld_closed/rpgpp_lt_building_02.prefab", 3),
        (Rpg + "Buildings/Bld_closed/rpgpp_lt_building_03.prefab", 4),
        (Rpg + "Buildings/Bld_closed/rpgpp_lt_building_04.prefab", 4),
        (Sky + "House 1 .prefab", 4),
        (Sky + "House 2 .prefab", 4),
        (Sky + "House 3 .prefab", 4),
        (Sky + "House 4 .prefab", 4),
        (Sky + "House 5 .prefab", 4),
        (Sky + "Tent 1 .prefab", 3),
        (Sky + "Tent 2 .prefab", 3),
        (Sky + "Warehouse01.prefab", 1),
        (Sky + "Watch Tower .prefab", 1),
        (Sky + "Water Well.prefab", 2),
        (Rpg + "Exterior/Wood_path/rpgpp_lt_shed_wood_01.prefab", 1),
        (Rpg + "Exterior/Wood_path/rpgpp_lt_shed_wood_02.prefab", 1),
    };

    static readonly string[] Props =
    {
        Rpg + "Props/Containers/rpgpp_lt_barrel_01.prefab",
        Rpg + "Props/Containers/rpgpp_lt_barrel_02.prefab",
        Rpg + "Props/Containers/rpgpp_lt_crate_01.prefab",
        Rpg + "Props/Containers/rpgpp_lt_crate_02.prefab",
        Rpg + "Props/Containers/rpgpp_lt_crate_03.prefab",
        Rpg + "Props/Containers/rpgpp_lt_vase_01.prefab",
        Rpg + "Props/Containers/rpgpp_lt_vase_02.prefab",
        Rpg + "Props/Containers/rpgpp_lt_vase_03.prefab",
        Rpg + "Props/Containers/rpgpp_lt_box_wood_01.prefab",
        Rpg + "Exterior/Wood_path/rpgpp_lt_awning_standing_01a.prefab",
        Rpg + "Exterior/Wood_path/rpgpp_lt_awning_standing_01b.prefab",
        Rpg + "Props/Benches/rpgpp_lt_bench_wood_01.prefab",
        Rpg + "Props/Benches/rpgpp_lt_bench_wood_02.prefab",
        Rpg + "Props/Wood/rpgpp_lt_log_wood_01.prefab",
        Rpg + "Props/Wood/rpgpp_lt_log_wood_02a.prefab",
        Ea + "Prop/Barrel/EA04_Prop_Barrel_01a_PRE.prefab",
        Ea + "Prop/Fireplace/EA04_Prop_Fireplace_01a_PRE.prefab",
        Sky + "Cart01.prefab",
        Sky + "Basket01.prefab",
        Sky + "Basket02.prefab",
    };

    // (path, weight)
    static readonly (string path, float weight)[] Vegetation =
    {
        (Ea + "Env/Plants/EA04_Env_Plants_Cactus_01a_PRE.prefab", 3f),
        (Ea + "Env/Plants/EA04_Env_Plants_Cactus_01b_PRE.prefab", 3f),
        (Ea + "Env/Plants/EA04_Env_Plants_Cactus_01c_PRE.prefab", 3f),
        (Ea + "Env/Plants/EA04_Env_Plants_Cactus_Dry_01a_PRE.prefab", 2f),
        (Ea + "Env/Plants/EA04_Env_Plants_Cactus_Dry_01b_PRE.prefab", 2f),
        (Ea + "Env/Plants/EA04_Env_Plants_Cactus_Dry_01c_PRE.prefab", 2f),
        (Ea + "Env/Plants/EA04_Env_Plants_Grass_Dry_01a_PRE.prefab", 3f),
        (Ea + "Env/Plants/EA04_Env_Plants_Grass_Dry_01b_PRE.prefab", 3f),
        (Ea + "Env/Plants/EA04_Env_Plants_Grass_Dry_01c_PRE.prefab", 3f),
        (Ea + "Env/Plants/EA04_Env_Plants_Tree_01a_PRE.prefab", 1f),
        (Ea + "Env/Plants/EA04_Env_Plants_Tree_01b_PRE.prefab", 0.6f),
        ("Assets/BOKI/LowPolyNature/Prefabs/models/bush_yellow_1.prefab", 2f),
        ("Assets/BOKI/LowPolyNature/Prefabs/models/bush_yellow_2.prefab", 2f),
        (Rpg + "Nature/Vegetation/Bushes/rpgpp_lt_bush_01.prefab", 1.5f),
        (Rpg + "Nature/Vegetation/Bushes/rpgpp_lt_bush_02.prefab", 1.5f),
        ("Assets/Dry_Trees/Prefab/Dry3333.prefab", 0.5f),
        ("Assets/Dry_Trees/Prefab/Dry4910.prefab", 0.5f),
        ("Assets/Dry_Trees/Prefab/Dry6524.prefab", 0.5f),
        ("Assets/Dry_Trees/Prefab/Dry8872.prefab", 0.5f),
        (Ea + "Env/Rocks/EA04_Env_Rocks_Sand_01a_PRE.prefab", 1f),
        (Ea + "Env/Rocks/EA04_Env_Rocks_Sand_01b_PRE.prefab", 1f),
        (Ea + "Env/Rocks/EA04_Env_Rocks_Sand_01c_PRE.prefab", 0.7f),
        (Ea + "Env/Rocks/EA04_Env_Rocks_Sand_01d_PRE.prefab", 1f),
        (Ea + "Env/Stone/EA04_Env_Stone_Sand_01a_PRE.prefab", 1f),
        (Ea + "Env/Stone/EA04_Env_Stone_Sand_01b_PRE.prefab", 1f),
    };

    // Grid state (1 m cells, aligned to the terrain origin).
    static int W, H;
    static Vector3 gridOrigin;
    static bool[] road;      // road surface
    static int[] roadDist;   // cells to nearest road (capped)
    static bool[] occupied;  // existing objects, lobby area, and everything placed so far
    static Terrain terrain;

    [MenuItem("tools/Environment/Build Desert Village")]
    public static string Run() => RunWithSeed(DefaultSeed);

    public static string RunWithSeed(int seed)
    {
        terrain = Terrain.activeTerrain;
        var roadRoot = GameObject.Find(RoadRootName);
        var lobbyArea = GameObject.Find(LobbyAreaName);
        var lobby = lobbyArea != null ? lobbyArea.transform.Find("CountryHouse") : null;
        if (terrain == null || roadRoot == null || lobbyArea == null || lobby == null)
            return Report($"Aborted: need an active Terrain, '{RoadRootName}' and '{LobbyAreaName}/CountryHouse'.");

        var rng = new System.Random(seed);
        var root = GetOrCreateRoot();
        var housesParent = CreateGroup(root, "Houses");
        var propsParent = CreateGroup(root, "Props");
        var vegParent = CreateGroup(root, "Vegetation");

        BuildGrid(roadRoot, lobbyArea, root);
        Bounds lobbyBounds = WorldBounds(lobby.gameObject);
        Vector3 centre = lobbyBounds.center;

        // --- Houses ---
        var remaining = new List<(GameObject prefab, int left)>();
        foreach (var (path, max) in Houses)
        {
            var p = Load(path);
            if (p == null) continue;
            Bounds pb = PrefabBounds(p);
            // Keep the lobby the biggest house around.
            if (pb.size.y >= lobbyBounds.size.y || pb.size.x * pb.size.z >= lobbyBounds.size.x * lobbyBounds.size.z) continue;
            remaining.Add((p, max));
        }

        var placedHouses = new List<(Vector3 pos, float radius)>();
        int houseAttempts = 0;
        while (placedHouses.Count < HouseCount && remaining.Count > 0 && houseAttempts < 200000)
        {
            int idx = rng.Next(remaining.Count);
            var (prefab, left) = remaining[idx];
            Bounds pb = PrefabBounds(prefab);
            float radius = Mathf.Max(pb.extents.x, pb.extents.z);

            bool placed = false;
            for (int tries = 0; tries < 1500 && !placed; tries++, houseAttempts++)
            {
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                float dist = Mathf.Lerp(HouseMinRadius, HouseMaxRadius, Mathf.Pow((float)rng.NextDouble(), 0.8f));
                var pos = centre + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * dist;

                if (!AreaFree(pos, radius + 1f, radius + HouseRoadBuffer)) continue;
                bool tooClose = false;
                foreach (var h in placedHouses)
                    if (Flat(pos - h.pos).magnitude < radius + h.radius + HouseSpacing) { tooClose = true; break; }
                if (tooClose) continue;

                // Prefer lots that front a road (so houses line the streets).
                int d = RoadDistAt(pos) - Mathf.CeilToInt(radius);
                bool frontsRoad = d >= HouseRoadBuffer && d <= 14;
                if (!frontsRoad && rng.NextDouble() > 0.03) continue;

                float yaw = FaceNearestRoad(pos, 45) + (float)(rng.NextDouble() * 30 - 15);
                var go = Place(prefab, housesParent, pos, yaw, 1f);
                EnsureColliders(go);
                placedHouses.Add((pos, radius));
                Mark(pos, radius + 1f);
                placed = true;
            }

            if (!placed || --left <= 0) remaining.RemoveAt(idx);
            else remaining[idx] = (prefab, left);
        }

        // --- Street props around each house ---
        var propPrefabs = LoadAll(Props);
        int propCount = 0;
        foreach (var h in placedHouses)
        {
            int n = 2 + rng.Next(3);
            for (int i = 0, tries = 0; i < n && tries < 40; tries++)
            {
                var prefab = propPrefabs[rng.Next(propPrefabs.Count)];
                float r = Mathf.Max(PrefabBounds(prefab).extents.x, PrefabBounds(prefab).extents.z);
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                var pos = h.pos + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (h.radius + 2f + (float)rng.NextDouble() * 3f);
                if (!AreaFree(pos, r + 0.3f, r + 1.5f)) continue;
                Place(prefab, propsParent, pos, (float)rng.NextDouble() * 360f, 1f);
                Mark(pos, r + 0.3f);
                propCount++; i++;
            }
        }

        // --- Vegetation & rocks, dense in the village, thinning into the desert ---
        var vegPrefabs = new List<(GameObject p, float w)>();
        float totalW = 0f;
        foreach (var (path, w) in Vegetation)
        {
            var p = Load(path);
            if (p == null) continue;
            vegPrefabs.Add((p, w)); totalW += w;
        }
        int vegCount = 0;
        for (int tries = 0; vegCount < VegetationCount && tries < VegetationCount * 40; tries++)
        {
            float pick = (float)rng.NextDouble() * totalW;
            GameObject prefab = vegPrefabs[vegPrefabs.Count - 1].p;
            foreach (var (p, w) in vegPrefabs) { if ((pick -= w) <= 0f) { prefab = p; break; } }

            float scale = 0.8f + (float)rng.NextDouble() * 0.5f;
            Bounds pb = PrefabBounds(prefab);
            float r = Mathf.Max(pb.extents.x, pb.extents.z) * scale;
            float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
            float dist = Mathf.Lerp(HouseMinRadius * 0.7f, VegMaxRadius, Mathf.Pow((float)rng.NextDouble(), 1.8f));
            var pos = centre + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * dist;

            if (!AreaFree(pos, r + 1f, r + 2.5f)) continue;
            Place(prefab, vegParent, pos, (float)rng.NextDouble() * 360f, scale);
            Mark(pos, r + 1f);
            vegCount++;
        }

        EditorSceneManager.MarkSceneDirty(root.scene);
        return Report($"Village built (seed {seed}) around {centre.x:F0},{centre.z:F0}: {placedHouses.Count} houses, {propCount} props, {vegCount} vegetation/rocks.");
    }

    // ---------------------------------------------------------------- desert scatter

    const string ScatterRootName = "DesertScatterRoot";
    const float ScatterMinRadius = 230f;  // from the lobby; inside this is the village
    const int ScatterCount = 650;
    const float MaxSlope = 25f;

    static readonly (string path, float weight, float minScale, float maxScale)[] Scatter =
    {
        (Ea + "Env/Plants/EA04_Env_Plants_Cactus_01a_PRE.prefab", 3f, 0.6f, 2.2f),
        (Ea + "Env/Plants/EA04_Env_Plants_Cactus_01b_PRE.prefab", 3f, 0.6f, 2.2f),
        (Ea + "Env/Plants/EA04_Env_Plants_Cactus_01c_PRE.prefab", 3f, 0.6f, 2.2f),
        (Ea + "Env/Plants/EA04_Env_Plants_Cactus_Dry_01a_PRE.prefab", 2f, 0.6f, 2f),
        (Ea + "Env/Plants/EA04_Env_Plants_Cactus_Dry_01b_PRE.prefab", 2f, 0.6f, 2f),
        (Ea + "Env/Plants/EA04_Env_Plants_Cactus_Dry_01c_PRE.prefab", 2f, 0.6f, 2f),
        ("Assets/Dry_Trees/Prefab/Dry3333.prefab", 1f, 0.5f, 1.6f),
        ("Assets/Dry_Trees/Prefab/Dry4195.prefab", 1f, 0.5f, 1.6f),
        ("Assets/Dry_Trees/Prefab/Dry4910.prefab", 1f, 0.5f, 1.6f),
        ("Assets/Dry_Trees/Prefab/Dry5818.prefab", 1f, 0.5f, 1.6f),
        ("Assets/Dry_Trees/Prefab/Dry6524.prefab", 1f, 0.5f, 1.6f),
        ("Assets/Dry_Trees/Prefab/Dry7509.prefab", 1f, 0.5f, 1.6f),
        ("Assets/Dry_Trees/Prefab/Dry8872.prefab", 1f, 0.5f, 1.6f),
        ("Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Trees/PT_Fruit_Tree_01_dead.prefab", 1f, 0.7f, 1.5f),
        ("Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Trees/PT_Pine_Tree_03_dead.prefab", 0.6f, 0.6f, 1.3f),
        ("Assets/BOKI/LowPolyNature/Prefabs/models/tree_normal_naked.prefab", 0.8f, 0.6f, 1.4f),
        ("Assets/BOKI/LowPolyNature/Prefabs/models/tree_hero_naked.prefab", 0.5f, 0.6f, 1.3f),
        (Ea + "Env/Trunk/EA04_Env_Tree_Trunk_01a_PRE.prefab", 0.6f, 0.7f, 1.5f),
        (Ea + "Env/Trunk/EA04_Env_Tree_Trunk_01c_PRE.prefab", 0.6f, 0.7f, 1.5f),
        (Ea + "Env/Plants/EA04_Env_Plants_Grass_Dry_01a_PRE.prefab", 1.5f, 0.8f, 1.6f),
        (Ea + "Env/Plants/EA04_Env_Plants_Grass_Dry_01c_PRE.prefab", 1.5f, 0.8f, 1.6f),
    };

    // Cacti and dead trees of varying sizes across the open desert outside the village.
    // Rebuilds only DesertScatterRoot, so it can be re-run without touching the village.
    [MenuItem("tools/Environment/Scatter Desert Props")]
    public static string RunDesertScatter() => RunDesertScatterWithSeed(DefaultSeed + 1);

    public static string RunDesertScatterWithSeed(int seed)
    {
        terrain = Terrain.activeTerrain;
        var roadRoot = GameObject.Find(RoadRootName);
        var lobbyArea = GameObject.Find(LobbyAreaName);
        var lobby = lobbyArea != null ? lobbyArea.transform.Find("CountryHouse") : null;
        if (terrain == null || roadRoot == null || lobby == null)
            return Report($"Aborted: need an active Terrain, '{RoadRootName}' and '{LobbyAreaName}/CountryHouse'.");

        var rng = new System.Random(seed);
        var root = GetOrCreateRoot(ScatterRootName);
        BuildGrid(roadRoot, lobbyArea, root);
        Vector3 centre = WorldBounds(lobby.gameObject).center;
        var data = terrain.terrainData;

        var prefabs = new List<(GameObject p, float w, float min, float max)>();
        float totalW = 0f;
        foreach (var (path, w, min, max) in Scatter)
        {
            var p = Load(path);
            if (p == null) continue;
            prefabs.Add((p, w, min, max)); totalW += w;
        }

        // Water planes are too big to be blocked as obstacles, so test the ground against them instead.
        var water = new List<Bounds>();
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (r.name.Contains("Water") || r.name.Contains("Lake")) water.Add(r.bounds);

        int placed = 0;
        for (int tries = 0; placed < ScatterCount && tries < ScatterCount * 30; tries++)
        {
            float pick = (float)rng.NextDouble() * totalW;
            var entry = prefabs[prefabs.Count - 1];
            foreach (var e in prefabs) { if ((pick -= e.w) <= 0f) { entry = e; break; } }

            // Uniform over the map, skipping the village disc.
            var pos = gridOrigin + new Vector3((float)rng.NextDouble() * (W - 20) + 10, 0f, (float)rng.NextDouble() * (H - 20) + 10);
            if (Flat(pos - centre).magnitude < ScatterMinRadius) continue;
            float nx = (pos.x - gridOrigin.x) / data.size.x, nz = (pos.z - gridOrigin.z) / data.size.z;
            if (data.GetSteepness(nx, nz) > MaxSlope) continue;
            float groundY = terrain.SampleHeight(pos) + terrain.transform.position.y;
            bool underwater = false;
            foreach (var wb in water)
                if (pos.x > wb.min.x && pos.x < wb.max.x && pos.z > wb.min.z && pos.z < wb.max.z && groundY < wb.max.y + 0.3f) { underwater = true; break; }
            if (underwater) continue;

            // Skew towards smaller sizes, with the occasional big one.
            float scale = Mathf.Lerp(entry.min, entry.max, Mathf.Pow((float)rng.NextDouble(), 1.6f));
            Bounds pb = PrefabBounds(entry.p);
            float r = Mathf.Max(pb.extents.x, pb.extents.z) * scale;
            if (!AreaFree(pos, r + 1.5f, r + 3f)) continue;

            Place(entry.p, root.transform, pos, (float)rng.NextDouble() * 360f, scale);
            Mark(pos, r + 1.5f);
            placed++;
        }

        EditorSceneManager.MarkSceneDirty(root.scene);
        return Report($"Desert scatter (seed {seed}): {placed} cacti/dead trees outside {ScatterMinRadius:F0} m of the lobby.");
    }

    // ---------------------------------------------------------------- grid

    static void BuildGrid(GameObject roadRoot, GameObject lobbyArea, GameObject excludeRoot)
    {
        gridOrigin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        W = Mathf.CeilToInt(size.x); H = Mathf.CeilToInt(size.z);
        road = new bool[W * H];
        occupied = new bool[W * H];

        foreach (var mf in roadRoot.GetComponentsInChildren<MeshFilter>())
        {
            var mr = mf.GetComponent<MeshRenderer>();
            if (mf.sharedMesh == null || mr == null || !mr.enabled) continue;
            RasterizeRoad(mf);
        }
        ComputeRoadDistance(60);

        // Lobby garden.
        var lb = WorldBounds(lobbyArea);
        MarkRect(lb.min.x - LobbyMargin, lb.min.z - LobbyMargin, lb.max.x + LobbyMargin, lb.max.z + LobbyMargin);

        // Every other renderer already in the scene (houses, props, trucks, spawn markers...).
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (r is ParticleSystemRenderer || !r.enabled) continue;
            var t = r.transform;
            if (t.IsChildOf(roadRoot.transform) || t.IsChildOf(excludeRoot.transform)) continue;
            var b = r.bounds;
            if (b.size.x > 120f || b.size.z > 120f) continue; // big helpers/volumes, not obstacles
            MarkRect(b.min.x - 1f, b.min.z - 1f, b.max.x + 1f, b.max.z + 1f);
        }
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (t.name.StartsWith("SpawnPoint")) Mark(t.position, 6f);
    }

    static void RasterizeRoad(MeshFilter mf)
    {
        var verts = mf.sharedMesh.vertices;
        var tris = mf.sharedMesh.triangles;
        var m = mf.transform.localToWorldMatrix;
        for (int i = 0; i < verts.Length; i++) verts[i] = m.MultiplyPoint3x4(verts[i]) - gridOrigin;

        for (int t = 0; t < tris.Length; t += 3)
        {
            Vector3 a = verts[tris[t]], b = verts[tris[t + 1]], c = verts[tris[t + 2]];
            float den = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
            if (Mathf.Abs(den) < 1e-9f) continue;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
            int x1 = Mathf.Min(W - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
            int z0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.z, Mathf.Min(b.z, c.z))));
            int z1 = Mathf.Min(H - 1, Mathf.CeilToInt(Mathf.Max(a.z, Mathf.Max(b.z, c.z))));
            for (int gz = z0; gz <= z1; gz++)
            for (int gx = x0; gx <= x1; gx++)
            {
                float x = gx + 0.5f, z = gz + 0.5f;
                float l1 = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / den;
                float l2 = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / den;
                // Small tolerance so thin triangles still mark the cells they cross.
                if (l1 < -0.1f || l2 < -0.1f || 1f - l1 - l2 < -0.1f) continue;
                road[gz * W + gx] = true;
            }
        }
    }

    static void ComputeRoadDistance(int cap)
    {
        roadDist = new int[W * H];
        var queue = new Queue<int>();
        for (int i = 0; i < road.Length; i++)
        {
            if (road[i]) { roadDist[i] = 0; queue.Enqueue(i); }
            else roadDist[i] = cap;
        }
        while (queue.Count > 0)
        {
            int i = queue.Dequeue();
            int d = roadDist[i] + 1;
            if (d >= cap) continue;
            int x = i % W, z = i / W;
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, nz = z + dz;
                if (nx < 0 || nz < 0 || nx >= W || nz >= H) continue;
                int n = nz * W + nx;
                if (roadDist[n] > d) { roadDist[n] = d; queue.Enqueue(n); }
            }
        }
    }

    // Free if no occupied cell within occRadius and no road cell within roadRadius.
    static bool AreaFree(Vector3 pos, float occRadius, float roadRadius)
    {
        int cx = Mathf.FloorToInt(pos.x - gridOrigin.x), cz = Mathf.FloorToInt(pos.z - gridOrigin.z);
        int reach = Mathf.CeilToInt(Mathf.Max(occRadius, roadRadius));
        if (cx - reach < 0 || cz - reach < 0 || cx + reach >= W || cz + reach >= H) return false;
        if (roadDist[cz * W + cx] < roadRadius - 1f) return false;
        float occ2 = occRadius * occRadius;
        for (int dz = -reach; dz <= reach; dz++)
        for (int dx = -reach; dx <= reach; dx++)
        {
            int i = (cz + dz) * W + cx + dx;
            float d2 = dx * dx + dz * dz;
            if (d2 <= occ2 && occupied[i]) return false;
            if (road[i] && d2 <= roadRadius * roadRadius) return false;
        }
        return true;
    }

    static int RoadDistAt(Vector3 pos)
    {
        int x = Mathf.FloorToInt(pos.x - gridOrigin.x), z = Mathf.FloorToInt(pos.z - gridOrigin.z);
        return roadDist[Mathf.Clamp(z, 0, H - 1) * W + Mathf.Clamp(x, 0, W - 1)];
    }

    static float FaceNearestRoad(Vector3 pos, int reach)
    {
        int cx = Mathf.FloorToInt(pos.x - gridOrigin.x), cz = Mathf.FloorToInt(pos.z - gridOrigin.z);
        float best = float.MaxValue; Vector2 dir = Vector2.up;
        for (int dz = -reach; dz <= reach; dz++)
        for (int dx = -reach; dx <= reach; dx++)
        {
            int x = cx + dx, z = cz + dz;
            if (x < 0 || z < 0 || x >= W || z >= H || !road[z * W + x]) continue;
            float d2 = dx * dx + dz * dz;
            if (d2 < best) { best = d2; dir = new Vector2(dx, dz); }
        }
        return Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
    }

    static void Mark(Vector3 pos, float radius)
    {
        int cx = Mathf.FloorToInt(pos.x - gridOrigin.x), cz = Mathf.FloorToInt(pos.z - gridOrigin.z);
        int reach = Mathf.CeilToInt(radius);
        for (int dz = -reach; dz <= reach; dz++)
        for (int dx = -reach; dx <= reach; dx++)
        {
            int x = cx + dx, z = cz + dz;
            if (x < 0 || z < 0 || x >= W || z >= H || dx * dx + dz * dz > radius * radius) continue;
            occupied[z * W + x] = true;
        }
    }

    static void MarkRect(float minX, float minZ, float maxX, float maxZ)
    {
        int x0 = Mathf.Max(0, Mathf.FloorToInt(minX - gridOrigin.x)), x1 = Mathf.Min(W - 1, Mathf.CeilToInt(maxX - gridOrigin.x));
        int z0 = Mathf.Max(0, Mathf.FloorToInt(minZ - gridOrigin.z)), z1 = Mathf.Min(H - 1, Mathf.CeilToInt(maxZ - gridOrigin.z));
        for (int z = z0; z <= z1; z++)
        for (int x = x0; x <= x1; x++)
            occupied[z * W + x] = true;
    }

    // ---------------------------------------------------------------- objects

    static GameObject GetOrCreateRoot(string name = RootName)
    {
        var root = GameObject.Find(name);
        if (root == null)
        {
            root = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(root, "Build Desert Village");
        }
        for (int i = root.transform.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(root.transform.GetChild(i).gameObject);
        return root;
    }

    static Transform CreateGroup(GameObject root, string name)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Build Desert Village");
        go.transform.SetParent(root.transform, false);
        return go.transform;
    }

    static GameObject Place(GameObject prefab, Transform parent, Vector3 pos, float yaw, float scale)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y;
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * prefab.transform.rotation;
        go.transform.localScale = prefab.transform.localScale * scale;
        Undo.RegisterCreatedObjectUndo(go, "Build Desert Village");
        return go;
    }

    static void EnsureColliders(GameObject go)
    {
        if (go.GetComponentInChildren<Collider>() != null) return;
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            if (mf.sharedMesh != null) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
    }

    static readonly Dictionary<GameObject, Bounds> boundsCache = new Dictionary<GameObject, Bounds>();

    // Bounds of the prefab relative to its root position (unrotated).
    static Bounds PrefabBounds(GameObject prefab)
    {
        if (boundsCache.TryGetValue(prefab, out var cached)) return cached;
        var b = new Bounds(prefab.transform.position, Vector3.zero);
        bool any = false;
        foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            var mb = TransformBounds(mf.sharedMesh.bounds, mf.transform.localToWorldMatrix);
            if (!any) { b = mb; any = true; } else b.Encapsulate(mb);
        }
        foreach (var smr in prefab.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (smr.sharedMesh == null) continue;
            var mb = TransformBounds(smr.sharedMesh.bounds, smr.transform.localToWorldMatrix);
            if (!any) { b = mb; any = true; } else b.Encapsulate(mb);
        }
        if (!any) b = new Bounds(prefab.transform.position, Vector3.one);
        b.center -= prefab.transform.position;
        boundsCache[prefab] = b;
        return b;
    }

    static Bounds TransformBounds(Bounds local, Matrix4x4 m)
    {
        var c = m.MultiplyPoint3x4(local.center);
        var e = local.extents;
        var ax = m.MultiplyVector(new Vector3(e.x, 0, 0));
        var ay = m.MultiplyVector(new Vector3(0, e.y, 0));
        var az = m.MultiplyVector(new Vector3(0, 0, e.z));
        var ext = new Vector3(
            Mathf.Abs(ax.x) + Mathf.Abs(ay.x) + Mathf.Abs(az.x),
            Mathf.Abs(ax.y) + Mathf.Abs(ay.y) + Mathf.Abs(az.y),
            Mathf.Abs(ax.z) + Mathf.Abs(ay.z) + Mathf.Abs(az.z));
        return new Bounds(c, ext * 2f);
    }

    static Bounds WorldBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static GameObject Load(string path)
    {
        var p = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (p == null) Debug.LogWarning("[BuildDesertVillage] Missing prefab: " + path);
        return p;
    }

    static List<GameObject> LoadAll(string[] paths)
    {
        var list = new List<GameObject>();
        foreach (var path in paths) { var p = Load(path); if (p != null) list.Add(p); }
        return list;
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    static string Report(string msg)
    {
        Debug.Log("[BuildDesertVillage] " + msg);
        return msg;
    }
}
