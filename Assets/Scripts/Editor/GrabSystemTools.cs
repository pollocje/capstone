using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

/// <summary>
/// Setup + self-test for the physics grab system.
/// Tools/Grab System/Setup Player Map View — adds MapItem + a MapOverlay (RT_Map picture) to the player prefab.
/// Tools/Grab System/Run Play Mode Self-Test — in play mode, grabs the map, opens the view, throws it and logs [GrabTest] results.
/// </summary>
public static class GrabSystemTools
{
    const string PlayerPrefabPath = "Assets/Prefabs/MainPlayer Variant.prefab";
    const string MapTexturePath = "Assets/Prefabs/Items/RT_Map.renderTexture";

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
    static Grabbable _map;
    static MapItem _mapItem;
    static Vector3 _throwFrom;

    [MenuItem("Tools/Grab System/Run Play Mode Self-Test")]
    public static void RunPlayModeSelfTest()
    {
        if (!Application.isPlaying) { Log("FAIL — enter play mode first."); return; }

        _grab = Object.FindFirstObjectByType<PlayerGrabController>();
        _mapItem = Object.FindFirstObjectByType<MapItem>();
        foreach (var g in Object.FindObjectsByType<Grabbable>(FindObjectsSortMode.None))
            if (g.GetComponent<MapGrabbable>() != null) _map = g;

        Log($"player grab controller: {(_grab != null ? "found" : "MISSING")}, map: {(_map != null ? _map.name : "MISSING")}, MapItem on player: {(_mapItem != null ? "found" : "MISSING")}");
        if (_grab == null || _map == null) return;

        var input = _grab.GetComponentInParent<PlayerInput>();
        foreach (var name in new[] { "Interact", "Throw", "Attack" })
            Log($"action '{name}': {(input != null && input.actions.FindAction(name) != null ? "OK" : "MISSING")}");

        _steps.Clear();
        _clock = EditorApplication.timeSinceStartup;

        // 1. Put the map in front of the camera and press E.
        Then(0.0, () =>
        {
            var cam = Camera.main != null ? Camera.main.transform : _grab.transform;
            _map.Body.position = cam.position + cam.forward * 1.2f;
            _map.Body.linearVelocity = Vector3.zero;
            _map.transform.position = _map.Body.position;
            Physics.SyncTransforms();
            Press(Keyboard.current, new KeyboardState(Key.E));
        });
        Then(0.1, () => Press(Keyboard.current, new KeyboardState()));
        Then(0.3, () =>
        {
            if (_grab.Held == _map) { Log("PASS — E key grabbed the map."); return; }
            Log("E key didn't reach the game (Game view not focused?) — grabbing through the same code path directly.");
            Invoke(_grab, "TryGrab");
            Log(_grab.Held == _map ? "PASS — raycast grab works." : "FAIL — raycast didn't grab the map.");
        });

        // 2. Carried: should hang near the HoldPoint, dynamic, colliding.
        Then(1.5, () =>
        {
            if (_grab.Held != _map) return;
            var hold = _grab.transform.root.GetComponentInChildren<Camera>().transform.Find("HoldPoint");
            float d = hold != null ? Vector3.Distance(_map.Body.worldCenterOfMass, hold.position) : -1f;
            bool joint = _map.GetComponent<ConfigurableJoint>() != null;
            Log($"{(d >= 0 && d < 1.0f && joint && !_map.Body.isKinematic ? "PASS" : "FAIL")} — carrying: distance to HoldPoint {d:0.00} m, joint {joint}, kinematic {_map.Body.isKinematic}");
        });

        // 3. Left click opens the map view and steadies the map (left open ~7 s so it can be looked at).
        Then(1.6, () => Press(Mouse.current, new MouseState().WithButton(MouseButton.Left, true)));
        Then(1.9, () =>
        {
            if (_mapItem == null) return;
            if (!_mapItem.IsUsing)
            {
                Log("Left click didn't reach the game (Game view not focused?) — opening the view directly.");
                _mapItem.OnUseDown();
            }
            var ui = new SerializedObject(_mapItem).FindProperty("mapUI").objectReferenceValue as GameObject;
            Log($"{(_mapItem.IsUsing && ui != null && ui.activeInHierarchy && _map.SuppressSwing ? "PASS" : "FAIL")} — map view: open {_mapItem.IsUsing}, overlay visible {(ui != null && ui.activeInHierarchy)}, steady hold {_map.SuppressSwing}");
        });
        Then(9.0, () =>
        {
            Press(Mouse.current, new MouseState());
            if (_mapItem != null && _mapItem.IsUsing) _mapItem.OnUseUp();
        });
        Then(9.2, () => Log($"{(_mapItem == null || !_mapItem.IsUsing ? "PASS" : "FAIL")} — map view closed on release"));

        // 4. Throw: hold right mouse ~0.6 s, release.
        Then(9.4, () => Press(Mouse.current, new MouseState().WithButton(MouseButton.Right, true)));
        Then(10.0, () => Press(Mouse.current, new MouseState()));
        Then(10.2, () =>
        {
            if (_grab.Held == _map)
            {
                Log("Right mouse didn't reach the game (Game view not focused?) — throwing directly.");
                Invoke(_grab, "Throw", 0.6f);
            }
            _throwFrom = _map.Body.position;
        });
        // The impulse lands on the next physics step; an unfocused editor ticks slowly, so wait.
        Then(13.0, () =>
        {
            Log($"after throw: moved {Vector3.Distance(_throwFrom, _map.Body.position):0.00} m, sleeping {_map.Body.IsSleeping()}, joint left {_map.GetComponent<ConfigurableJoint>() != null}");
            Log($"{(_grab.Held == null && _map.Body.linearVelocity.magnitude > 1f ? "PASS" : "FAIL")} — thrown: speed {_map.Body.linearVelocity.magnitude:0.0} m/s, still held {_grab.Held != null}");
        });
        Then(13.1, () => Log("Self-test finished."));

        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

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

    static void Press<TState>(InputDevice device, TState state) where TState : struct, IInputStateTypeInfo
    {
        if (device != null) InputSystem.QueueStateEvent(device, state);
    }

    static void Invoke(object target, string method, params object[] args)
    {
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
              ?.Invoke(target, args);
    }

    static void Log(string msg) => Debug.Log("[GrabTest] " + msg);
}
