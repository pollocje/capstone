using UnityEditor;
using UnityEngine;

// Play-mode checks for the live map (Assets/Scripts/Map). Run in each Multiplayer Play Mode window.
public static class LiveMapTools
{
    [MenuItem("Tools/Live Map/Self-Test (play mode)")]
    public static string SelfTest()
    {
        if (!Application.isPlaying) return Report("Enter play mode first.");
        var map = LiveMap.Instance;
        if (map == null) return Report("No LiveMap in the scene (it should be on Cameras/MapCamera).");
        return Report(map.Describe());
    }

    [MenuItem("Tools/Live Map/Fake Tornado Detected (spawn now)")]
    public static string FakeDetected()
    {
        if (!Application.isPlaying || LiveMap.Instance == null) return Report("Enter play mode first.");
        LiveMap.Instance.DebugSpawnTornado();
        return Report("Asked TornadoSpawner to spawn the tornado.");
    }

    [MenuItem("Tools/Live Map/Toggle Fake Detection (no cutscene)")]
    public static string ToggleFakeDetection()
    {
        if (!Application.isPlaying || LiveMap.Instance == null) return Report("Enter play mode first.");
        LiveMap.Instance.DebugToggleExisting();
        return Report($"Fake detection: {LiveMap.Instance.treatExistingTornadoesAsDetected}");
    }

    // Equips the map on the local player without opening it: the corner minimap (testing only).
    [MenuItem("Tools/Live Map/Equip Map (test)")]
    public static string EquipMap()
    {
        if (!Application.isPlaying || LiveMap.Instance == null) return Report("Enter play mode first.");
        var local = LiveMap.Instance.LocalPlayer;
        if (local == null) return Report("No local player yet.");
        if (!local.IsEquipped) local.OnEquip();
        if (local.IsUsing) local.ToggleView();
        return Report($"Map equipped on {local.transform.parent?.name}, view open {local.IsUsing}");
    }

    // Equips the map on the local player and opens the view, skipping the E pickup (testing only).
    [MenuItem("Tools/Live Map/Open Map View (test)")]
    public static string OpenMapView()
    {
        if (!Application.isPlaying || LiveMap.Instance == null) return Report("Enter play mode first.");
        var local = LiveMap.Instance.LocalPlayer;
        if (local == null) return Report("No local player yet.");
        if (!local.IsEquipped) local.OnEquip();
        if (!local.IsUsing) local.ToggleView();
        return Report($"Map view open on {local.transform.parent?.name}: {local.IsUsing}");
    }

    // Play mode barely ticks while the Editor window is in the background; this pauses and
    // advances it frame by frame from editor updates instead.
    [MenuItem("Tools/Live Map/Step 60 Frames")]
    public static void Step60() => StepFrames(60);

    static int _framesLeft;

    public static void StepFrames(int frames)
    {
        if (!Application.isPlaying) { Report("Enter play mode first."); return; }
        EditorApplication.isPaused = true;
        if (_framesLeft <= 0) EditorApplication.update += StepOnce;
        _framesLeft = frames;
    }

    static void StepOnce()
    {
        if (!Application.isPlaying || _framesLeft-- <= 0)
        {
            EditorApplication.update -= StepOnce;
            _framesLeft = 0;
            return;
        }
        EditorApplication.Step();
    }

    static string Report(string msg)
    {
        Debug.Log("[LiveMap] " + msg);
        return msg;
    }
}
