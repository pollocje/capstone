using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Prototype_World housekeeping: fixes the fence MeshColliders that lost their mesh (legacy
// fileID 4300000 / deleted Fence_04 FBX), audits the scene for the same bug, sorts the scene
// root into top-level groups and checks the truck body colliders.
// Only touches sharedMesh/isTrigger/convex on colliders: reading other Collider properties
// through the MCP reflector on a mesh-less MeshCollider crashed the editor (PhysX GeometryHolder).
public static class SceneCleanupTools
{
    const string FencePrefabPath = "Assets/Polytope Studio/Lowpoly_Demos/Environment_Free/Helpers/Fence.prefab";
    const string FenceSceneName = "Fence";

    // ---------------------------------------------------------------- audit

    [MenuItem("tools/Scene Cleanup/Audit Mesh Colliders")]
    public static string AuditMeshColliders()
    {
        var sb = new StringBuilder();
        var broken = AllInScene<MeshCollider>().Where(mc => mc.sharedMesh == null).ToList();
        sb.AppendLine($"Scene MeshColliders with no mesh: {broken.Count}");
        foreach (var group in broken.GroupBy(mc => GroupKey(mc.gameObject)).OrderBy(g => g.Key))
        {
            sb.AppendLine($"  [{group.Key}] {group.Count()}");
            foreach (var mc in group)
            {
                var mf = mc.GetComponent<MeshFilter>();
                string src = PrefabUtility.IsPartOfPrefabInstance(mc)
                    ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(mc) : "scene object";
                sb.AppendLine($"    {Path(mc.gameObject)}  (MeshFilter: {(mf && mf.sharedMesh ? mf.sharedMesh.name : "none")}, source: {src})");
            }
        }

        // Prefab assets used by the scene that carry the same bug (fixing those fixes every copy).
        var prefabPaths = AllInScene<Transform>()
            .Where(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))
            .Select(t => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject))
            .Where(p => !string.IsNullOrEmpty(p)).Distinct();
        var brokenPrefabs = new List<string>();
        foreach (var p in prefabPaths)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (asset == null) continue;
            int n = asset.GetComponentsInChildren<MeshCollider>(true).Count(mc => mc.sharedMesh == null);
            if (n > 0) brokenPrefabs.Add($"  {p}: {n}");
        }
        sb.AppendLine($"Prefab assets (used in scene) with mesh-less MeshColliders: {brokenPrefabs.Count}");
        foreach (var l in brokenPrefabs) sb.AppendLine(l);

        // Fence/gate meshes that have no collider at all.
        var fence = FindInScene(FenceSceneName);
        if (fence != null)
        {
            var bare = fence.GetComponentsInChildren<MeshFilter>(true)
                .Where(mf => mf.GetComponent<Collider>() == null).Select(mf => Path(mf.gameObject)).ToList();
            sb.AppendLine($"Fence meshes without any collider: {bare.Count}");
            foreach (var l in bare) sb.AppendLine("  " + l);
            var box = fence.GetComponent<BoxCollider>();
            if (box) sb.AppendLine($"Fence root BoxCollider: center {box.center}, size {box.size}, trigger {box.isTrigger}");
        }
        return Report(sb.ToString());
    }

    // ---------------------------------------------------------------- fence fix

    [MenuItem("tools/Scene Cleanup/Fix Fence Colliders")]
    public static string FixFenceColliders()
    {
        var sb = new StringBuilder();

        // 1) Source prefab, so every placed copy inherits working colliders.
        var root = PrefabUtility.LoadPrefabContents(FencePrefabPath);
        try
        {
            sb.AppendLine("Fence.prefab:");
            FixCollidersUnder(root.transform, sb, inPrefab: true);
            PrefabUtility.SaveAsPrefabAsset(root, FencePrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        // 2) Scene instance: drop overrides that would mask the prefab fix, remove the stray box.
        var fence = FindInScene(FenceSceneName);
        if (fence != null)
        {
            int reverted = 0;
            foreach (var mc in fence.GetComponentsInChildren<MeshCollider>(true))
            {
                if (!PrefabUtility.IsPartOfPrefabInstance(mc)) continue;
                var so = new SerializedObject(mc);
                foreach (var name in new[] { "m_Mesh", "m_IsTrigger", "m_Convex" })
                {
                    var prop = so.FindProperty(name);
                    if (prop != null && prop.prefabOverride)
                    {
                        PrefabUtility.RevertPropertyOverride(prop, InteractionMode.UserAction);
                        reverted++;
                    }
                }
            }
            sb.AppendLine($"Scene Fence: reverted {reverted} collider override(s).");

            var box = fence.GetComponent<BoxCollider>();
            if (box)
            {
                sb.AppendLine($"Scene Fence: removed root BoxCollider (center {box.center}, size {box.size}) - one box can't follow a perimeter fence.");
                if (PrefabUtility.IsAddedComponentOverride(box))
                    PrefabUtility.RevertAddedComponent(box, InteractionMode.UserAction);
                else
                    Undo.DestroyObjectImmediate(box);
            }

            sb.AppendLine("Scene Fence (anything still broken after the prefab fix):");
            FixCollidersUnder(fence.transform, sb, inPrefab: false);
        }

        // 3) Loose fence segments placed straight into the scene (e.g. under TerrainRoot).
        var loose = AllInScene<MeshFilter>()
            .Where(mf => mf.name.StartsWith("PT_Medieval_Village_") && (fence == null || !mf.transform.IsChildOf(fence.transform)))
            .Select(mf => mf.transform).ToList();
        sb.AppendLine($"Loose fence/gate pieces elsewhere: {loose.Count}");
        foreach (var t in loose) FixOne(t.gameObject, sb, inPrefab: false);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        return Report(sb.ToString());
    }

    const string GatePrefabPath = "Assets/Polytope Studio/Lowpoly_Village/Prefabs/Modular/Fence/PT_Modular_Gate_Wood_01.prefab";

    // Gate_01 in Fence.prefab is a nested prefab whose source asset is gone: its parts have no mesh
    // (invisible, nothing to collide with) and Unity won't edit a missing-prefab instance. Swap it for
    // the current Polytope gate prefab (same FBX, ships with MeshColliders), keeping every transform.
    [MenuItem("tools/Scene Cleanup/Replace Broken Gate")]
    public static string ReplaceBrokenGate()
    {
        var sb = new StringBuilder();
        // part name in the broken gate -> part name in PT_Modular_Gate_Wood_01
        var partMap = new Dictionary<string, string>
        {
            { "PT_Medieval_Village_Gate_01_fixed", "PT_Modular_Gate_Wood_01_poles" },
            { "PT_Medieval_Village_Gate_01_left_moblie", "PT_Modular_Gate_Wood_01_left" },
            { "PT_Medieval_Village_Gate_01_right_mobile", "PT_Modular_Gate_Wood_01_right" },
        };

        // Poses relative to FENCE, read from the scene copy (the prefab stage can't load the gate's parts).
        var fence = FindInScene(FenceSceneName);
        var sceneGroup = fence ? fence.transform.Find("FENCE") : null;
        var sceneGate = sceneGroup ? sceneGroup.Find("PT_Medieval_Village_Gate_01") : null;
        if (sceneGate == null) return Report("Broken gate not found under scene Fence/FENCE (already replaced?)");
        var gatePose = RelativePose(sceneGroup, sceneGate);
        var partPoses = partMap.Keys.Select(n => sceneGate.Find(n)).Where(p => p != null)
            .ToDictionary(p => partMap[p.name], p => RelativePose(sceneGate, p));

        var root = PrefabUtility.LoadPrefabContents(FencePrefabPath);
        try
        {
            var group = root.transform.Find("FENCE");
            var holder = group.Find("GateColliders"); // empty leftover from an earlier attempt
            if (holder != null) Object.DestroyImmediate(holder.gameObject);
            var broken = group.Find("PT_Medieval_Village_Gate_01");
            int sibling = broken ? broken.GetSiblingIndex() : group.childCount;
            if (broken != null) Object.DestroyImmediate(broken.gameObject);

            var gate = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(GatePrefabPath), group);
            gate.name = "PT_Medieval_Village_Gate_01";
            gate.transform.SetSiblingIndex(sibling);
            ApplyPose(gate.transform, gatePose);
            foreach (Transform part in gate.transform)
                if (partPoses.TryGetValue(part.name, out var pose)) { ApplyPose(part, pose); sb.AppendLine($"  {part.name}: pose copied"); }
            int cols = gate.GetComponentsInChildren<MeshCollider>(true).Count(mc => mc.sharedMesh != null && !mc.isTrigger);
            sb.AppendLine($"  solid MeshColliders on new gate: {cols}");
            PrefabUtility.SaveAsPrefabAsset(root, FencePrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        return Report("Replaced broken Gate_01 in Fence.prefab with " + GatePrefabPath + ":\n" + sb);
    }

    static (Vector3 pos, Quaternion rot, Vector3 scale) RelativePose(Transform parent, Transform t) =>
        (parent.InverseTransformPoint(t.position), Quaternion.Inverse(parent.rotation) * t.rotation,
         Vector3.Scale(t.lossyScale, new Vector3(1f / parent.lossyScale.x, 1f / parent.lossyScale.y, 1f / parent.lossyScale.z)));

    static void ApplyPose(Transform t, (Vector3 pos, Quaternion rot, Vector3 scale) pose)
    {
        t.localPosition = pose.pos;
        t.localRotation = pose.rot;
        t.localScale = pose.scale;
    }

    static void FixCollidersUnder(Transform root, StringBuilder sb, bool inPrefab)
    {
        // Colliders on pure grouping objects (no mesh, e.g. Gate_01 itself) collide with nothing.
        foreach (var mc in root.GetComponentsInChildren<MeshCollider>(true).ToList())
        {
            if (mc.sharedMesh != null || mc.GetComponent<MeshFilter>() != null) continue;
            sb.AppendLine($"  {Path(mc.gameObject)}: removed mesh-less MeshCollider (no MeshFilter on this object)");
            if (inPrefab) Object.DestroyImmediate(mc, true);
            else if (PrefabUtility.IsPartOfPrefabInstance(mc)) continue; // prefab fix already handles it
            else Undo.DestroyObjectImmediate(mc);
        }
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            FixOne(mf.gameObject, sb, inPrefab);
    }

    static void FixOne(GameObject go, StringBuilder sb, bool inPrefab)
    {
        var mf = go.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;
        var mc = go.GetComponent<MeshCollider>();
        if (mc == null)
        {
            if (go.GetComponent<Collider>() != null) return; // has some other solid collider
            mc = inPrefab ? go.AddComponent<MeshCollider>() : Undo.AddComponent<MeshCollider>(go);
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = false;
            mc.isTrigger = false;
            sb.AppendLine($"  {Path(go)}: added MeshCollider ({mf.sharedMesh.name})");
            return;
        }
        if (mc.sharedMesh != null && !mc.isTrigger) return;
        if (!inPrefab) Undo.RecordObject(mc, "Fix fence collider");
        string what = mc.sharedMesh == null ? $"assigned mesh {mf.sharedMesh.name}" : "cleared Is Trigger";
        if (mc.sharedMesh == null) mc.sharedMesh = mf.sharedMesh;
        mc.isTrigger = false;
        mc.convex = false;
        if (!inPrefab) PrefabUtility.RecordPrefabInstancePropertyModifications(mc);
        sb.AppendLine($"  {Path(go)}: {what}");
    }

    // ---------------------------------------------------------------- hierarchy

    // First match wins; matched against the trimmed root object name.
    static readonly (string group, Regex pattern)[] RootRules =
    {
        ("Lighting", new Regex(@"^Directional Light$")),
        ("UI", new Regex(@"^(Canvas|Panel|EventSystem)$")),
        ("Cameras", new Regex(@"^MapCamera$")),
        ("Vehicles", new Regex(@"^(TruckRoot|TruckRootNEW)$|^Car \d")),
        ("Gameplay", new Regex(@"^(Tornado|PlayerSpawnManager|GameManager|Item_Map)$|^SpawnPoint")),
        ("VFX", new Regex(@"^Waterfall_")),
        ("Environment/Structures", new Regex(@"^(DecorationsRoot|VillageRoot|Warehouse01|Mill|Water Well|wall_2)$|^rpgpp_lt_building")),
        ("Environment/Structures/Props", new Regex(@"^Cart01$|^EA04_Prop_|^EA04_Wooden_|^EA04_Wehicule_|^Cucumber_bus_|^EA04_Animal_")),
        ("Environment/Vegetation", new Regex(@"^(Forest|CornField|DesertScatterRoot)$|^PT_Grass_|^EA04_Env_Plants_|^EA04_Env_Tree_|^EA_04_Trunk|^PT_Generic_Shrub|^tree_|^Dry\d")),
        ("Environment/Terrain", new Regex(@"^PT_River_Rock|^EA04_Env_Rocks_|^EA04_Env_Terrain_|^rock_|^Rock |^Lake$")),
    };

    static readonly string[] Groups =
    {
        "Environment/Terrain", "Environment/Vegetation", "Environment/Structures",
        "Vehicles", "Gameplay", "VFX", "Lighting", "UI", "Cameras",
    };

    [MenuItem("tools/Scene Cleanup/Organize Hierarchy")]
    public static string OrganizeHierarchy()
    {
        var moved = new StringBuilder();
        var renamed = new StringBuilder();
        var scene = EditorSceneManager.GetActiveScene();

        // Existing WorldRoot sub-roots first, so LightingRoot/VFXRoot become the Lighting/VFX groups.
        var worldRoot = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "WorldRoot");
        if (worldRoot != null)
        {
            Promote(worldRoot, "LightingRoot", "Lighting", renamed);
            Promote(worldRoot, "VFXRoot", "VFX", renamed);
            var terrainRoot = worldRoot.transform.Find("TerrainRoot");
            if (terrainRoot != null)
            {
                foreach (Transform c in terrainRoot.Cast<Transform>().ToList())
                {
                    if (c.name.StartsWith("PT_Medieval_Village_")) Move(c.gameObject, "Environment/Structures/Fence_North", moved);
                    else Move(c.gameObject, "Environment/Terrain", moved);
                }
            }
            MoveChild(worldRoot, "RoadRoot", "Environment/Terrain", moved);
            MoveChild(worldRoot, "WaterfallRoot", "Environment/Terrain", moved);
            MoveChild(worldRoot, "SpawnRoot", "Gameplay", moved);
        }

        foreach (var g in Groups) Ensure(g);

        foreach (var go in scene.GetRootGameObjects())
        {
            if (Groups.Any(g => g.Split('/')[0] == go.name) || go == worldRoot) continue;
            string clean = Regex.Replace(go.name.Trim(), @"\s{2,}", " ");
            var rule = RootRules.FirstOrDefault(r => r.pattern.IsMatch(clean));
            if (rule.group == null) { moved.AppendLine($"  (left at root, no rule) {go.name}"); continue; }
            Move(go, rule.group, moved);
        }

        // Renames: trailing/double spaces, leftover defaults.
        foreach (var t in AllInScene<Transform>().Where(t => PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject) || !PrefabUtility.IsPartOfPrefabInstance(t)))
        {
            string clean = Regex.Replace(t.name.Trim(), @"\s{2,}", " ");
            if (clean != t.name) Rename(t.gameObject, clean, renamed);
        }
        var car = FindInScene("Car 2 (1)");
        if (car != null && FindInScene("Car 4") == null) Rename(car, "Car 4", renamed);
        var dry = FindInScene("Dry8872");
        if (dry != null) Rename(dry, "DryTree_8872", renamed);
        var tornado = FindInScene("Tornado");
        var ps = tornado ? tornado.transform.Find("Particle System") : null;
        if (ps != null)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(ps)) renamed.AppendLine("  (skipped, inside prefab instance) Tornado/Particle System");
            else Rename(ps.gameObject, "TornadoParticles", renamed);
        }

        // Drop now-empty grouping objects (Transform only, no children).
        var removed = new StringBuilder();
        if (worldRoot != null)
        {
            foreach (Transform c in worldRoot.transform.Cast<Transform>().ToList()) RemoveIfEmpty(c.gameObject, removed);
            RemoveIfEmpty(worldRoot, removed);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        return Report("Moved:\n" + moved + "Renamed:\n" + renamed + "Removed empty groups:\n" + removed);
    }

    // WorldRoot is an instance of Assets/Scenes/Prototype/WorldRoot.prefab (only used in this scene), so its
    // own children can't be reparented. Unpack it (the .prefab file stays), then finish the grouping.
    [MenuItem("tools/Scene Cleanup/Unpack WorldRoot")]
    public static string UnpackWorldRoot()
    {
        var sb = new StringBuilder();
        var worldRoot = EditorSceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "WorldRoot");
        if (worldRoot == null) return Report("No WorldRoot at scene root");
        if (PrefabUtility.IsOutermostPrefabInstanceRoot(worldRoot))
        {
            PrefabUtility.UnpackPrefabInstance(worldRoot, PrefabUnpackMode.OutermostRoot, InteractionMode.UserAction);
            sb.AppendLine("  unpacked WorldRoot prefab instance");
        }
        MoveChild(worldRoot, "RoadRoot", "Environment/Terrain", sb);
        MoveChild(worldRoot, "SpawnRoot", "Gameplay", sb);
        foreach (Transform c in worldRoot.transform.Cast<Transform>().ToList()) RemoveIfEmpty(c.gameObject, sb);
        if (worldRoot.transform.childCount == 0) { sb.AppendLine("  removed empty WorldRoot"); Undo.DestroyObjectImmediate(worldRoot); }
        else sb.AppendLine($"  WorldRoot still has {worldRoot.transform.childCount} child(ren), kept");
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        return Report(sb.ToString());
    }

    static void Promote(GameObject worldRoot, string child, string newName, StringBuilder renamed)
    {
        var t = worldRoot.transform.Find(child);
        if (t == null || EditorSceneManager.GetActiveScene().GetRootGameObjects().Any(g => g.name == newName)) return;
        Undo.SetTransformParent(t, null, "Organize hierarchy");
        t.SetAsLastSibling();
        Rename(t.gameObject, newName, renamed);
    }

    static void MoveChild(GameObject parent, string child, string group, StringBuilder log)
    {
        var t = parent.transform.Find(child);
        if (t != null) Move(t.gameObject, group, log);
    }

    static void Move(GameObject go, string group, StringBuilder log)
    {
        var parent = Ensure(group);
        string from = Path(go);
        Undo.SetTransformParent(go.transform, parent, true, "Organize hierarchy"); // keeps world position
        log.AppendLine($"  {from} -> {group}");
    }

    static Transform Ensure(string groupPath)
    {
        Transform parent = null;
        foreach (var part in groupPath.Split('/'))
        {
            Transform found = parent == null
                ? EditorSceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == part)?.transform
                : parent.Find(part);
            if (found == null)
            {
                var go = new GameObject(part);
                Undo.RegisterCreatedObjectUndo(go, "Organize hierarchy");
                if (parent != null) go.transform.SetParent(parent, false);
                found = go.transform;
            }
            parent = found;
        }
        return parent;
    }

    static void Rename(GameObject go, string name, StringBuilder log)
    {
        Undo.RecordObject(go, "Rename");
        log.AppendLine($"  '{go.name}' -> '{name}'");
        go.name = name;
    }

    static void RemoveIfEmpty(GameObject go, StringBuilder log)
    {
        if (go.transform.childCount > 0 || go.GetComponents<Component>().Length > 1) return;
        log.AppendLine("  " + Path(go));
        Undo.DestroyObjectImmediate(go);
    }

    // ---------------------------------------------------------------- vehicles

    [MenuItem("tools/Scene Cleanup/Check Vehicle Colliders")]
    public static string CheckVehicleColliders()
    {
        var sb = new StringBuilder();
        foreach (var name in new[] { "TruckRootNEW", "TruckRoot" })
        {
            var truck = FindInScene(name);
            if (truck == null) { sb.AppendLine($"{name}: not in scene"); continue; }
            bool inUse = truck.GetComponent("TruckNewTEST") != null;
            sb.AppendLine($"{name}{(inUse ? " (in use: has TruckNewTEST)" : "")}, active {truck.activeInHierarchy}");

            int wheels = truck.GetComponentsInChildren<WheelCollider>(true).Length;
            var bodyCols = truck.GetComponentsInChildren<Collider>(true).Where(c => !(c is WheelCollider)).ToList();
            sb.AppendLine($"  WheelColliders: {wheels}; body colliders: {bodyCols.Count}");

            var bodyBounds = BodyBounds(truck);
            sb.AppendLine($"  body mesh (local): center {bodyBounds.center}, size {bodyBounds.size}");

            foreach (var c in bodyCols)
            {
                string kind = c.GetType().Name;
                string shape = c is BoxCollider b ? $"center {b.center}, size {b.size}"
                             : c is CapsuleCollider cc ? $"center {cc.center}, r {cc.radius}, h {cc.height}" : "";
                sb.AppendLine($"  {kind} on {Path(c.gameObject)}: {shape}, enabled {c.enabled}, trigger {c.isTrigger}");
            }

            // The root box is VehicleEnterExit's enter/exit trigger zone, not the body; leave it a trigger.
            var body = truck.transform.Find(BodyColliderName);
            sb.AppendLine(body != null
                ? $"  OK: solid {BodyColliderName} separate from the WheelColliders and the enter/exit trigger"
                : $"  !! no solid body collider (only seat boxes / bump stop) - run Add Truck Body Collider");
        }
        return Report(sb.ToString());
    }

    const string BodyColliderName = "BodyCollider";

    // Solid box around the truck body (tyres excluded) on its own child, so the chassis can't clip
    // through static colliders. Bottom sits 10 cm above the lowest body mesh to stay clear of the
    // ground under suspension travel; the WheelColliders still carry the truck.
    [MenuItem("tools/Scene Cleanup/Add Truck Body Collider")]
    public static string AddTruckBodyCollider()
    {
        var sb = new StringBuilder();
        var truck = FindInScene("TruckRootNEW");
        if (truck == null) return Report("TruckRootNEW not in scene");

        var trigger = truck.GetComponent<BoxCollider>();
        if (trigger != null && !trigger.isTrigger)
        {
            Undo.RecordObject(trigger, "Restore enter/exit trigger");
            trigger.isTrigger = true;
            sb.AppendLine("  restored root BoxCollider to Is Trigger (VehicleEnterExit zone)");
        }

        var b = BodyBounds(truck);
        float bottom = b.min.y + 0.1f;
        var existing = truck.transform.Find(BodyColliderName);
        var go = existing != null ? existing.gameObject : new GameObject(BodyColliderName);
        if (existing == null)
        {
            Undo.RegisterCreatedObjectUndo(go, "Add truck body collider");
            go.transform.SetParent(truck.transform, false);
        }
        go.layer = truck.layer;
        var box = go.GetComponent<BoxCollider>();
        if (box == null) box = Undo.AddComponent<BoxCollider>(go);
        box.isTrigger = false;
        box.center = new Vector3(b.center.x, (bottom + b.max.y) * 0.5f, b.center.z);
        box.size = new Vector3(b.size.x, b.max.y - bottom, b.size.z);
        sb.AppendLine($"  {Path(go)}: BoxCollider center {box.center}, size {box.size}");

        string prefab = PrefabUtility.IsPartOfPrefabInstance(truck) ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(truck) : null;
        sb.AppendLine(prefab != null ? $"  scene truck is an instance of {prefab} (applied there)" : "  scene truck is not a prefab instance (scene only)");
        if (prefab != null)
        {
            if (existing == null) PrefabUtility.ApplyAddedGameObject(go, prefab, InteractionMode.UserAction);
            else PrefabUtility.ApplyObjectOverride(box, prefab, InteractionMode.UserAction);
            if (trigger != null) PrefabUtility.ApplyObjectOverride(trigger, prefab, InteractionMode.UserAction);
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        return Report("Truck body collider:\n" + sb);
    }

    // Body extents in truck-local space, tyres and effects excluded (the wheels are the WheelColliders' job).
    static Bounds BodyBounds(GameObject truck)
    {
        var t = truck.transform;
        var chassis = t.Find("PickUpTruck");
        if (chassis == null) chassis = t;
        var body = new Bounds();
        bool any = false;
        foreach (var r in truck.GetComponentsInChildren<Renderer>(true))
        {
            if (r.name.Contains("Tire") || r is ParticleSystemRenderer || !r.transform.IsChildOf(chassis)) continue;
            foreach (var corner in Corners(r.bounds))
            {
                var p = t.InverseTransformPoint(corner);
                if (!any) { body = new Bounds(p, Vector3.zero); any = true; } else body.Encapsulate(p);
            }
        }
        return body;
    }

    // ---------------------------------------------------------------- verification

    // Casts a ray at every fence/gate collider from 3 m out; each should report a hit.
    [MenuItem("tools/Scene Cleanup/Verify Fence Raycasts")]
    public static string VerifyFenceRaycasts()
    {
        var cols = AllInScene<MeshCollider>().Where(mc => mc.name.StartsWith("PT_Medieval_Village_")).ToList();
        var misses = new List<string>();
        foreach (var mc in cols)
        {
            var c = mc.bounds.center;
            bool hit = false;
            foreach (var dir in new[] { mc.transform.forward, mc.transform.right })
                hit |= mc.Raycast(new Ray(c - dir * 3f, dir), out _, 6f) || mc.Raycast(new Ray(c + dir * 3f, -dir), out _, 6f);
            if (!hit) misses.Add(Path(mc.gameObject));
        }
        return Report($"Fence/gate colliders hit: {cols.Count - misses.Count}/{cols.Count}" +
                      (misses.Count > 0 ? "\nMissed:\n  " + string.Join("\n  ", misses) : ""));
    }

    // ---------------------------------------------------------------- helpers

    static IEnumerable<T> AllInScene<T>() where T : Component =>
        EditorSceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true));

    static GameObject FindInScene(string name) =>
        AllInScene<Transform>().FirstOrDefault(t => t.name == name)?.gameObject;

    // Nearest ancestor that reads as a meaningful group (the child of a *Root / group object).
    static string GroupKey(GameObject go)
    {
        var t = go.transform;
        while (t.parent != null && t.parent.parent != null) t = t.parent;
        return t.name;
    }

    static IEnumerable<Vector3> Corners(Bounds b)
    {
        for (int i = 0; i < 8; i++)
            yield return b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
    }

    static string Path(GameObject go) =>
        go.transform.parent == null ? go.name : Path(go.transform.parent.gameObject) + "/" + go.name;

    static string Report(string msg)
    {
        Debug.Log("[SceneCleanupTools] " + msg);
        return msg;
    }
}
