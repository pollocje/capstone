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

    // ── Setup ────────────────────────────────────────────────────────────────

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

            var mapItem = capsule.GetComponent<MapItem>();
            if (mapItem == null) mapItem = capsule.gameObject.AddComponent<MapItem>();

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

        // 1. Put the map in front of the camera and press E (same code path as the Interact action).
        Then(1.2, () =>
        {
            var cam = Camera.main != null ? Camera.main.transform : _grab.transform;
            var rb = _worldMap.GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic = true; rb.position = cam.position + cam.forward * 1.2f; }
            _worldMap.transform.position = cam.position + cam.forward * 1.2f;
            Physics.SyncTransforms();
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
