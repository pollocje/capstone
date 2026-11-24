using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

// One-click wiring for the Blender binoculars model (Assets/Models/Binoculars.fbx):
// materials, world pickup prefab, network prefab registration, item asset, and the player's held model.
// Safe to re-run; each step updates what's already there instead of duplicating it.
public static class BinocularsSetup
{
    const string FbxPath = "Assets/Models/Binoculars.fbx";
    const string MaterialFolder = "Assets/Prefabs/Items/Materials";
    const string PickupPath = "Assets/Prefabs/Items/Item_Binoculars.prefab";
    const string ItemPath = "Assets/Items/Binoculars.asset";
    const string PlayerPrefabPath = "Assets/Prefabs/PlayerPrefab2.prefab";
    const string NetworkPrefabsPath = "Assets/DefaultNetworkPrefabs.asset";

    // Material slot names come from the Blender file; colors match the Blender previews.
    static readonly (string slot, Color color, float smoothness)[] Slots =
    {
        ("Body",   new Color(0.95f, 0.55f, 0.30f), 0.2f),
        ("Rubber", new Color(0.20f, 0.22f, 0.30f), 0.1f),
        ("Glass",  new Color(0.55f, 0.85f, 1.00f), 0.85f),
        ("Accent", new Color(1.00f, 0.85f, 0.40f), 0.2f),
    };

    [MenuItem("Tools/Items/Set Up Binoculars")]
    public static void Run()
    {
        GameObject model = ConfigureModel();
        if (model == null) return;

        GameObject pickup = BuildPickupPrefab(model);
        RegisterNetworkPrefab(pickup);
        ConfigureItem(pickup);
        AssignHeldModel(model);

        AssetDatabase.SaveAssets();
        Report();
    }

    [MenuItem("Tools/Items/Report Binoculars Setup")]
    public static void Report()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        var pickup = AssetDatabase.LoadAssetAtPath<GameObject>(PickupPath);
        var item = AssetDatabase.LoadAssetAtPath<InventoryItem>(ItemPath);
        var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsPath);
        var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        var binoculars = player != null ? player.GetComponentInChildren<Binoculars>(true) : null;
        var held = binoculars != null ? new SerializedObject(binoculars).FindProperty("heldModel").objectReferenceValue : null;

        string bounds = model != null ? LocalBounds(model).size.ToString("F3") : "-";
        string mats = model != null
            ? string.Join(", ", model.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Select(m => m != null ? m.name : "null"))
            : "-";
        Debug.Log($"[BinocularsSetup] model {(model != null)} size {bounds} materials [{mats}]\n" +
                  $"  pickup {(pickup != null)}, registered {(list != null && pickup != null && list.Contains(pickup))}\n" +
                  $"  item name '{item?.itemName}' dropPrefab {(item != null && item.dropPrefab == pickup && pickup != null)}\n" +
                  $"  player Binoculars {(binoculars != null)} heldModel {(held != null ? held.name : "none")}");
    }

    // ── Steps ────────────────────────────────────────────────────────────────

    static GameObject ConfigureModel()
    {
        var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"[BinocularsSetup] {FbxPath} not found. Copy the FBX from Blender_WIP first.");
            return null;
        }

        importer.importAnimation = false;
        importer.animationType = ModelImporterAnimationType.None;
        importer.importCameras = false;
        importer.importLights = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        foreach (var (slot, color, smoothness) in Slots)
        {
            string path = $"{MaterialFolder}/Binoculars_{slot}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);

            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), slot), mat);
        }

        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
    }

    // Same layout as Item_Map: solid collider for aiming/physics, roomier trigger for "standing near".
    static GameObject BuildPickupPrefab(GameObject model)
    {
        var root = new GameObject("Item_Binoculars");
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
        instance.name = "Model";

        Bounds b = LocalBounds(instance);

        var trigger = root.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = b.center;
        trigger.size = b.size + new Vector3(0.6f, 0.4f, 0.6f);

        var rb = root.AddComponent<Rigidbody>();
        rb.mass = 0.3f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        var pickup = root.AddComponent<ItemPickup>();   // RequireComponent adds the NetworkObject
        var so = new SerializedObject(pickup);
        so.FindProperty("item").objectReferenceValue = AssetDatabase.LoadAssetAtPath<InventoryItem>(ItemPath);
        so.FindProperty("pickupOnTouch").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();

        var solid = root.AddComponent<BoxCollider>();
        solid.center = b.center;
        solid.size = b.size;

        root.AddComponent<NetworkTransform>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PickupPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void RegisterNetworkPrefab(GameObject pickup)
    {
        var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsPath);
        if (list == null)
        {
            Debug.LogWarning($"[BinocularsSetup] {NetworkPrefabsPath} not found; register {PickupPath} by hand.");
            return;
        }
        if (!list.Contains(pickup))
        {
            list.Add(new NetworkPrefab { Prefab = pickup });
            EditorUtility.SetDirty(list);
        }
    }

    static void ConfigureItem(GameObject pickup)
    {
        var item = AssetDatabase.LoadAssetAtPath<InventoryItem>(ItemPath);
        if (item == null) return;
        item.itemName = "Binoculars";
        item.dropPrefab = pickup;
        EditorUtility.SetDirty(item);
    }

    // Set on the base player prefab so MainPlayer Variant inherits it.
    static void AssignHeldModel(GameObject model)
    {
        var contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            var binoculars = contents.GetComponentInChildren<Binoculars>(true);
            if (binoculars == null)
            {
                Debug.LogWarning($"[BinocularsSetup] No Binoculars component in {PlayerPrefabPath}.");
                return;
            }
            var so = new SerializedObject(binoculars);
            so.FindProperty("heldModel").objectReferenceValue = model;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    static Bounds LocalBounds(GameObject go)
    {
        var filters = go.GetComponentsInChildren<MeshFilter>();
        var root = go.transform;
        Bounds? total = null;
        foreach (var f in filters)
        {
            if (f.sharedMesh == null) continue;
            Bounds mb = f.sharedMesh.bounds;
            var toRoot = root.worldToLocalMatrix * f.transform.localToWorldMatrix;
            foreach (var corner in Corners(mb))
            {
                Vector3 p = toRoot.MultiplyPoint3x4(corner);
                if (total == null) total = new Bounds(p, Vector3.zero);
                else { var t = total.Value; t.Encapsulate(p); total = t; }
            }
        }
        // Expressed in the parent's space so it lines up with colliders on the parent.
        Bounds local = total ?? new Bounds(Vector3.zero, Vector3.one * 0.2f);
        local.center = root.localPosition + root.localRotation * Vector3.Scale(local.center, root.localScale);
        local.size = Vector3.Scale(local.size, root.localScale);
        return local;
    }

    static Vector3[] Corners(Bounds b)
    {
        Vector3 c = b.center, e = b.extents;
        return new[]
        {
            c + new Vector3(-e.x, -e.y, -e.z), c + new Vector3(e.x, -e.y, -e.z),
            c + new Vector3(-e.x, e.y, -e.z),  c + new Vector3(e.x, e.y, -e.z),
            c + new Vector3(-e.x, -e.y, e.z),  c + new Vector3(e.x, -e.y, e.z),
            c + new Vector3(-e.x, e.y, e.z),   c + new Vector3(e.x, e.y, e.z),
        };
    }
}
