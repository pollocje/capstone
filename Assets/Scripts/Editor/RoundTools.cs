using UnityEditor;
using UnityEngine;

// Play-mode checks for RoundManager (win/lose, truck-flip countdown, end screen).
public static class RoundTools
{
    [MenuItem("Tools/Round/Report State")]
    public static string ReportState()
    {
        var r = RoundManager.Instance;
        if (!Application.isPlaying || r == null) return Report("Enter play mode first.");
        var ui = Object.FindFirstObjectByType<RoundUI>();
        var vee = Object.FindFirstObjectByType<VehicleEnterExit>();
        string truck = vee ? $"truck tilt {Vector3.Angle(vee.transform.up, Vector3.up):F0} deg at {vee.transform.position:F1}" : "no truck";
        string result = r.Ended ? $"ENDED {(r.Result.won ? "WON" : "LOST")} '{r.Result.reason}' time {r.Result.elapsedSeconds:F1}s research {r.Result.research:P0} " +
                                  $"detected {r.Result.tornadoDetected} killed {r.Result.tornadoKilled} score {r.Result.total} players {r.Result.players.Count}"
                                : "playing";
        return Report($"in gameplay {r.InGameplay}, authority {RoundManager.IsAuthority}, countdown {(r.CountdownActive ? r.CountdownRemaining.ToString("F1") + "s" : "off")}, {truck}\n  {result}\n  UI: {(ui ? ui.Describe() : "none")}");
    }

    [MenuItem("Tools/Round/Force Win (F5)")] public static void Win() => RoundManager.Instance?.DebugWin();
    [MenuItem("Tools/Round/Force Loss (F6)")] public static void Lose() => RoundManager.Instance?.DebugLose();
    [MenuItem("Tools/Round/Toggle Fake Flip (F7)")] public static void Flip() => RoundManager.Instance?.DebugToggleFlip();
    [MenuItem("Tools/Round/Press Start Over")] public static void StartOver() => RoundManager.Instance?.StartOver();

    // Rolls the real truck through its Rigidbody, so the actual flip detection is tested.
    [MenuItem("Tools/Round/Flip Real Truck (upside down)")] public static void FlipTruck() => SetTruckRoll(180f);
    [MenuItem("Tools/Round/Put Real Truck Back Upright")] public static void UprightTruck() => SetTruckRoll(0f);

    [MenuItem("Tools/Round/Step 600 Frames")] public static void Step600() => LiveMapTools.StepFrames(600);

    // Raises Hotbar.ItemUsed for the local player as if they had clicked with the Binoculars (testing only).
    [MenuItem("Tools/Round/Fake Item Use (Binoculars)")]
    public static void FakeItemUse()
    {
        var local = LiveMap.Instance != null ? LiveMap.Instance.LocalPlayer : null;
        var hotbar = local != null ? local.GetComponent<Hotbar>() : null;
        var item = AssetDatabase.LoadAssetAtPath<InventoryItem>("Assets/Items/Binoculars.asset");
        var field = typeof(Hotbar).GetField("ItemUsed", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var handler = field?.GetValue(null) as System.Action<Hotbar, InventoryItem>;
        if (hotbar == null || item == null || handler == null) { Report($"Can't fake: hotbar {hotbar != null}, item {item != null}, listeners {handler != null}"); return; }
        handler(hotbar, item);
        Report("Faked one Binoculars use.");
    }

    static void SetTruckRoll(float roll)
    {
        var vee = Object.FindFirstObjectByType<VehicleEnterExit>();
        if (!Application.isPlaying || vee == null) { Report("Enter play mode first (needs the truck)."); return; }
        var t = vee.transform;
        var flat = Vector3.ProjectOnPlane(t.forward, Vector3.up);
        var rot = Quaternion.AngleAxis(roll, flat) * Quaternion.LookRotation(flat, Vector3.up);
        var pos = t.position + Vector3.up * 2f;
        var rb = t.GetComponent<Rigidbody>();
        t.SetPositionAndRotation(pos, rot);
        if (rb != null) { rb.position = pos; rb.rotation = rot; rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        Physics.SyncTransforms();
        Report($"Truck rolled to {roll} deg (up {t.up:F2}).");
    }

    static string Report(string msg)
    {
        Debug.Log("[Round] " + msg);
        return msg;
    }
}
