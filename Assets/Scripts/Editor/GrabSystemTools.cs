using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Setup + self-test for the physics grab system.
/// Tools/Grab System/Setup Player Map View — adds MapItem + a MapOverlay (RT_Map picture) to the player prefab.
/// Tools/Grab System/Run Map Item Self-Test — in play mode, picks up the map with E, equips it, toggles the view and logs [MapTest] results.
/// </summary>
public static class GrabSystemTools
{
    const string PlayerPrefabPath = "Assets/Prefabs/MainPlayer Variant.prefab";
    const string MapTexturePath = "Assets/Prefabs/Items/RT_Map.renderTexture";
    const string MapPrefabPath = "Assets/Prefabs/Items/Item_Map.prefab";
    const string MapAssetPath = "Assets/Items/Map.asset";
    const string MapIconPath = "Assets/Images/Icon_Map.png";
    const string MapIconMaterialPath = "Assets/Prefabs/Items/Mat_MapIcon.mat";

    // Size of the map card on the floor (m), and the part of the 256px icon the parchment covers.
    const float CardSize = 0.6f;
    static readonly Vector2 ParchmentFraction = new Vector2(220f / 256f, 150f / 256f);

    // ── Setup ────────────────────────────────────────────────────────────────

    /// <summary>
    /// One-click fix for the map item: icon sprite + hotbar icon, the floor model (a flat card
    /// showing Icon_Map), and the player components (E pickup, prompt, MapItem + MapOverlay).
    /// Safe to run again — everything is created only if missing.
    /// </summary>
    [MenuItem("Tools/Grab System/Setup Map Item (Full)")]
    public static void SetupMapItemFull()
    {
        var sprite = ImportIconSprite();
        var material = CreateIconMaterial(sprite != null ? sprite.texture : null);

        var mapAsset = AssetDatabase.LoadAssetAtPath<InventoryItem>(MapAssetPath);
        if (mapAsset != null && sprite != null)
        {
            mapAsset.icon = sprite;
            EditorUtility.SetDirty(mapAsset);
            AssetDatabase.SaveAssetIfDirty(mapAsset);
        }

        RebuildWorldMapPrefab(material);
        SetupPlayerMapView();
        RevertSceneMapOverrides();
        Debug.Log($"[GrabSetup] Map item set up (icon: {(sprite != null ? "ok" : "MISSING")}, Map.asset: {(mapAsset != null ? "ok" : "MISSING")}).");
    }

    static Sprite ImportIconSprite()
    {
        var importer = AssetImporter.GetAtPath(MapIconPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"[GrabSetup] {MapIconPath} not found.");
            return null;
        }
        if (importer.textureType != TextureImporterType.Sprite || !importer.alphaIsTransparency)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(MapIconPath);
    }

    static Material CreateIconMaterial(Texture texture)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MapIconMaterialPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, MapIconMaterialPath);
        }

        // Alpha-clipped so only the parchment shows, and double-sided so it reads from any angle.
        mat.SetTexture("_BaseMap", texture);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_AlphaClip", 1f);
        mat.SetFloat("_Cutoff", 0.5f);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        mat.SetFloat("_Smoothness", 0.1f);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssetIfDirty(mat);
        return mat;
    }

    /// <summary>Item_Map becomes an unscaled root (colliders, Rigidbody, ItemPickup) with a flat Icon_Map card on top.</summary>
    static void RebuildWorldMapPrefab(Material material)
    {
        var root = PrefabUtility.LoadPrefabContents(MapPrefabPath);
        try
        {
            root.transform.localScale = Vector3.one;

            // The old grey cube body is replaced by the card.
            var oldRenderer = root.GetComponent<MeshRenderer>();
            if (oldRenderer != null) Object.DestroyImmediate(oldRenderer);
            var oldFilter = root.GetComponent<MeshFilter>();
            if (oldFilter != null) Object.DestroyImmediate(oldFilter);

            Vector3 solidSize = new Vector3(CardSize * ParchmentFraction.x, 0.03f, CardSize * ParchmentFraction.y);
            BoxCollider solid = null, trigger = null;
            foreach (var box in root.GetComponents<BoxCollider>())
            {
                if (box.isTrigger) trigger = box; else solid = box;
            }
            if (solid == null) solid = root.AddComponent<BoxCollider>();
            if (trigger == null) { trigger = root.AddComponent<BoxCollider>(); trigger.isTrigger = true; }
            solid.size = solidSize;
            solid.center = Vector3.zero;
            // Generous trigger so "standing near it" (PlayerGrabController.pickupRadius) finds it.
            trigger.size = new Vector3(1.2f, 0.6f, 1.2f);
            trigger.center = Vector3.zero;

            var display = root.transform.Find("Map_Display");
            if (display == null)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(quad.GetComponent<Collider>());
                quad.name = "Map_Display";
                display = quad.transform;
                display.SetParent(root.transform, false);
            }
            display.localPosition = new Vector3(0f, solidSize.y * 0.5f + 0.002f, 0f);
            display.localRotation = Quaternion.Euler(90f, 0f, 0f);   // lying flat, face up
            display.localScale = new Vector3(CardSize, CardSize, 1f);
            var renderer = display.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // a flat card only casts a blocky rectangle
            renderer.receiveShadows = true;

            PrefabUtility.SaveAsPrefabAsset(root, MapPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>Clears old scale/collider/material overrides on Item_Map instances in the open scenes, keeping their placement.</summary>
    static void RevertSceneMapOverrides()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MapPrefabPath);
        foreach (var pickup in Object.FindObjectsByType<ItemPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(pickup.gameObject);
            if (instanceRoot == null || PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot) != prefab) continue;

            var t = instanceRoot.transform;
            Vector3 pos = t.localPosition;
            Quaternion rot = t.localRotation;
            string name = instanceRoot.name;

            PrefabUtility.RevertPrefabInstance(instanceRoot, InteractionMode.AutomatedAction);

            t.localPosition = pos;
            t.localRotation = rot;
            instanceRoot.name = name;
            EditorUtility.SetDirty(instanceRoot);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(instanceRoot.scene);
            Debug.Log($"[GrabSetup] Reverted overrides on scene map '{name}' at {pos}.");
        }
    }

    [MenuItem("Tools/Grab System/Setup Player Map View")]
    public static void SetupPlayerMapView()
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            var capsule = root.transform.Find("PlayerCapsule");
            var canvas = root.transform.Find("HUD Canvas");
            if (capsule == null || canvas == null)
            {
                Debug.LogError("[GrabSetup] PlayerCapsule or HUD Canvas not found on the player prefab.");
                return;
            }

            // E to pick up + the on-screen "Press E" / "Press Tab" hint live next to the map.
            if (capsule.GetComponent<PlayerGrabController>() == null) capsule.gameObject.AddComponent<PlayerGrabController>();
            var mapItem = capsule.GetComponent<MapItem>();
            if (mapItem == null) mapItem = capsule.gameObject.AddComponent<MapItem>();
            if (capsule.GetComponent<InteractPrompt>() == null) capsule.gameObject.AddComponent<InteractPrompt>();

            // Dimmed full-screen backdrop with the live map picture centred, kept square.
            var overlay = canvas.Find("MapOverlay");
            if (overlay == null)
            {
                var go = new GameObject("MapOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                overlay = go.transform;
                overlay.SetParent(canvas, false);
            }
            Stretch((RectTransform)overlay, 0f);
            var backdrop = overlay.GetComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.75f);
            backdrop.raycastTarget = false;

            var image = overlay.Find("MapImage");
            if (image == null)
            {
                var go = new GameObject("MapImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(AspectRatioFitter));
                image = go.transform;
                image.SetParent(overlay, false);
            }
            Stretch((RectTransform)image, 40f);
            var raw = image.GetComponent<RawImage>();
            raw.texture = AssetDatabase.LoadAssetAtPath<Texture>(MapTexturePath);
            raw.raycastTarget = false;
            var fitter = image.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1f;

            overlay.gameObject.SetActive(false);

            var so = new SerializedObject(mapItem);
            so.FindProperty("mapUI").objectReferenceValue = overlay.gameObject;
            so.FindProperty("heldModel").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(MapPrefabPath);
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Debug.Log($"[GrabSetup] Player prefab updated: MapItem on PlayerCapsule, MapOverlay under HUD Canvas (texture: {(raw.texture != null ? raw.texture.name : "MISSING")}).");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void Stretch(RectTransform rt, float margin)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(margin, margin);
        rt.offsetMax = new Vector2(-margin, -margin);
        rt.localScale = Vector3.one;
    }

    // ── Play mode self-test ──────────────────────────────────────────────────

    static readonly Queue<(double at, System.Action step)> _steps = new Queue<(double, System.Action)>();
    static double _clock;
    static PlayerGrabController _grab;
    static Hotbar _hotbar;
    static MapItem _mapItem;
    static ItemPickup _worldMap;
    static InventoryItem _mapAsset;
    static Vector3 _heldPos;

    [MenuItem("Tools/Grab System/Run Map Item Self-Test")]
    public static void RunMapItemSelfTest()
    {
        if (!Application.isPlaying) { Log("FAIL — enter play mode first."); return; }

        _grab = Object.FindFirstObjectByType<PlayerGrabController>();
        _hotbar = Object.FindFirstObjectByType<Hotbar>();
        _mapItem = Object.FindFirstObjectByType<MapItem>();
        _mapAsset = AssetDatabase.LoadAssetAtPath<InventoryItem>("Assets/Items/Map.asset");
        _worldMap = null;
        foreach (var p in Object.FindObjectsByType<ItemPickup>(FindObjectsSortMode.None))
            if (new SerializedObject(p).FindProperty("item").objectReferenceValue == _mapAsset) _worldMap = p;

        Log($"grab controller: {Found(_grab)}, hotbar: {Found(_hotbar)}, MapItem: {Found(_mapItem)}, world map: {Found(_worldMap)}, Map.asset: {Found(_mapAsset)}");
        if (_grab == null || _hotbar == null || _mapItem == null || _worldMap == null) return;

        _steps.Clear();
        _clock = EditorApplication.timeSinceStartup;

        // 1. Put the map just in front of the player's feet, wait out ItemPickup's arm delay,
        //    then press E (same code path as the Interact action).
        Then(0.1, () =>
        {
            var player = _grab.transform;
            var rb = _worldMap.GetComponent<Rigidbody>();
            Vector3 pos = player.position + player.forward * 0.8f + Vector3.up * 0.1f;
            if (rb != null) { rb.isKinematic = true; rb.position = pos; }
            _worldMap.transform.position = pos;
            Physics.SyncTransforms();
        });
        Then(1.3, () =>
        {
            if (!_worldMap.IsArmed) Log("note — map not armed yet (game time is behind; keep the Game view focused).");
            Invoke(_grab, "TryInteract");
        });

        // 2. In an empty hotbar slot, world object gone, nothing physically held.
        int slot = -1;
        Then(1.4, () =>
        {
            slot = _hotbar.IndexOf(_mapAsset);
            Log($"{(slot >= 0 && _worldMap == null && _grab.Held == null ? "PASS" : "FAIL")} — E pickup: hotbar slot {slot + 1}, world map removed {_worldMap == null}, physically held {_grab.Held != null}");
        });

        // 3. Number key equips it: model shown in hand with no physics on it.
        Then(1.5, () => { if (slot >= 0) _hotbar.SelectSlot(slot); });
        Then(1.7, () =>
        {
            var held = _mapItem.HeldVisual;
            bool physics = held != null && (held.GetComponentInChildren<Rigidbody>(true) != null || held.GetComponentInChildren<Joint>(true) != null || held.GetComponentInChildren<Collider>(true) != null);
            Log($"{(_mapItem.IsEquipped && held != null && held.activeInHierarchy && !physics ? "PASS" : "FAIL")} — equipped {_mapItem.IsEquipped}, in hand {(held != null && held.activeInHierarchy)}, physics components {physics}");
            if (held != null) _heldPos = held.transform.localPosition;
        });

        // 4. Steady: camera-local pose unchanged after a second.
        Then(2.7, () =>
        {
            var held = _mapItem.HeldVisual;
            float drift = held != null ? Vector3.Distance(held.transform.localPosition, _heldPos) : -1f;
            Log($"{(drift == 0f ? "PASS" : "FAIL")} — held steady: local drift {drift:0.0000} m");
        });

        // 5. Tab opens / closes the overlay (left open ~4 s so it can be looked at).
        Then(2.8, () => _mapItem.ToggleView());
        Then(2.9, () => Log($"{(_mapItem.IsUsing && OverlayVisible() ? "PASS" : "FAIL")} — Tab opens map view: overlay visible {OverlayVisible()}"));
        Then(7.0, () => _mapItem.ToggleView());
        Then(7.1, () => Log($"{(!_mapItem.IsUsing && !OverlayVisible() ? "PASS" : "FAIL")} — Tab closes map view"));

        // 6. Unequipping closes the view and hides the model.
        Then(7.2, () => { _mapItem.ToggleView(); _hotbar.SelectSlot(slot == 0 ? 1 : 0); });
        Then(7.4, () =>
        {
            var held = _mapItem.HeldVisual;
            Log($"{(!_mapItem.IsUsing && !OverlayVisible() && (held == null || !held.activeSelf) ? "PASS" : "FAIL")} — unequip closes view and hides model");
            Log("Self-test finished.");
        });

        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    static bool OverlayVisible()
    {
        var ui = new SerializedObject(_mapItem).FindProperty("mapUI").objectReferenceValue as GameObject;
        return ui != null && ui.activeInHierarchy;
    }

    static string Found(Object o) => o != null ? "found" : "MISSING";

    static void Then(double at, System.Action step) => _steps.Enqueue((at, step));

    static void Tick()
    {
        if (!Application.isPlaying) { _steps.Clear(); EditorApplication.update -= Tick; return; }
        double t = EditorApplication.timeSinceStartup - _clock;
        while (_steps.Count > 0 && _steps.Peek().at <= t)
        {
            try { _steps.Dequeue().step(); }
            catch (System.Exception e) { Log("FAIL — exception: " + e.Message); }
        }
        if (_steps.Count == 0) EditorApplication.update -= Tick;
    }

    static void Invoke(object target, string method, params object[] args)
    {
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
              ?.Invoke(target, args);
    }

    static void Log(string msg) => Debug.Log("[MapTest] " + msg);
}
