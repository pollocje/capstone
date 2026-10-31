using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Play-mode checks for the vehicle exit spot (SafeGround) and the fall safety net (PlayerFallSafety).
public static class PlayerSafetyTools
{
    // Rolls/pitches the truck through a few poses and reports where the old ExitPoint was
    // versus the spot SafeGround picks. The truck is put back afterwards.
    [MenuItem("Tools/Player Safety/Test Exit Spots (flipped truck)")]
    public static string TestExitSpots()
    {
        if (!Application.isPlaying) return Report("Enter play mode first.");
        var vee = Object.FindFirstObjectByType<VehicleEnterExit>();
        if (vee == null) return Report("No VehicleEnterExit in the scene.");
        var truck = vee.transform;
        var exitPoint = truck.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "ExitPoint");
        var rb = truck.GetComponent<Rigidbody>();
        var pos = truck.position; var rot = truck.rotation;

        var sb = new StringBuilder("Exit spots (old ExitPoint -> SafeGround):\n");
        foreach (var (label, euler) in new[] { ("upright", Vector3.zero), ("on its right side", new Vector3(0, 0, -90)),
                                               ("on its left side", new Vector3(0, 0, 90)), ("upside down", new Vector3(0, 0, 180)),
                                               ("nose down", new Vector3(90, 0, 0)) })
        {
            truck.SetPositionAndRotation(pos + Vector3.up * (euler == Vector3.zero ? 0f : 1.2f), rot * Quaternion.Euler(euler));
            Physics.SyncTransforms();
            bool ok = SafeGround.TryFindExit(truck, 0.5f, 1.8f, out var spot);
            float ground = Terrain.activeTerrain ? Terrain.activeTerrain.SampleHeight(spot) + Terrain.activeTerrain.transform.position.y : 0f;
            sb.AppendLine($"  {label}: ExitPoint {(exitPoint ? exitPoint.position.ToString("F1") : "-")} -> {(ok ? $"{spot:F1} (terrain {ground:F1}, fits {SafeGround.Fits(spot, truck, 0.5f, 1.8f)})" : "NO SPOT FOUND")}");
        }
        truck.SetPositionAndRotation(pos, rot);
        if (rb != null) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        Physics.SyncTransforms();
        return Report(sb.ToString());
    }

    // Drops the local player 60 m under the terrain; PlayerFallSafety should put them back next frame.
    [MenuItem("Tools/Player Safety/Drop Local Player Below Map")]
    public static string DropBelowMap()
    {
        if (!Application.isPlaying) return Report("Enter play mode first.");
        var local = LiveMap.Instance != null ? LiveMap.Instance.LocalPlayer : null;
        var cc = local != null ? local.GetComponent<CharacterController>() : Object.FindFirstObjectByType<CharacterController>();
        if (cc == null) return Report("No player found.");
        var from = cc.transform.position;
        cc.enabled = false;
        cc.transform.position = from + Vector3.down * 60f;
        Physics.SyncTransforms();
        cc.enabled = true;
        return Report($"Moved {cc.transform.root.name} from {from:F1} to {cc.transform.position:F1}; step a few frames and check for the PlayerFallSafety warning.");
    }

    // Real enter -> flip -> exit path through VehicleEnterExit's own (private) methods.
    // Run step 1, step some frames, step 2, step some frames, then Report.
    [MenuItem("Tools/Player Safety/Flip Test 1 - Enter Truck And Flip It")]
    public static string FlipTestEnter()
    {
        if (!Application.isPlaying) return Report("Enter play mode first.");
        var vee = Object.FindFirstObjectByType<VehicleEnterExit>();
        var local = LiveMap.Instance != null ? LiveMap.Instance.LocalPlayer : null;
        if (vee == null || local == null) return Report("Need the truck and a local player.");
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        typeof(VehicleEnterExit).GetField("playerRoot", flags).SetValue(vee, local.transform.root.gameObject);
        typeof(VehicleEnterExit).GetMethod("EnterVehicle", flags).Invoke(vee, new object[] { 0 }); // seat 0 = Driver
        var t = vee.transform;
        var pos = t.position + Vector3.up * 2.5f;
        var rot = Quaternion.AngleAxis(FlipRoll, t.forward) * t.rotation;
        t.SetPositionAndRotation(pos, rot);
        var rb = t.GetComponent<Rigidbody>();
        if (rb != null) { rb.position = pos; rb.rotation = rot; rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        Physics.SyncTransforms();
        return Report($"Entered and rolled the truck {FlipRoll} deg: up {t.up:F2}");
    }

    static float FlipRoll = 180f;
    [MenuItem("Tools/Player Safety/Flip Test - Use Side Roll (90)")] static void Roll90() => FlipRoll = 90f;
    [MenuItem("Tools/Player Safety/Flip Test - Use Upside Down (180)")] static void Roll180() => FlipRoll = 180f;

    [MenuItem("Tools/Player Safety/Flip Test 2 - Exit Truck")]
    public static string FlipTestExit()
    {
        if (!Application.isPlaying) return Report("Enter play mode first.");
        var vee = Object.FindFirstObjectByType<VehicleEnterExit>();
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        typeof(VehicleEnterExit).GetMethod("ExitVehicle", flags).Invoke(vee, null);
        return Report("Exited.\n" + ReportPosition());
    }

    [MenuItem("Tools/Player Safety/Report Local Player Position")]
    public static string ReportPosition()
    {
        var local = LiveMap.Instance != null ? LiveMap.Instance.LocalPlayer : null;
        if (local == null) return Report("No local player.");
        var cc = local.GetComponent<CharacterController>();
        var p = local.transform.position;
        float ground = Terrain.activeTerrain ? Terrain.activeTerrain.SampleHeight(p) + Terrain.activeTerrain.transform.position.y : 0f;
        var truck = Object.FindFirstObjectByType<VehicleEnterExit>();
        return Report($"{local.transform.root.name} feet {p:F2}, terrain {ground:F2}, rotation {local.transform.eulerAngles:F0} (up {local.transform.up:F2}), " +
                      $"CC enabled {cc.enabled} grounded {cc.isGrounded}, CC centre in world {local.transform.TransformPoint(cc.center):F2}" +
                      (truck ? $"; truck at {truck.transform.position:F1} up {truck.transform.up:F2}" : ""));
    }

    static string Report(string msg)
    {
        Debug.Log("[PlayerSafety] " + msg);
        return msg;
    }
}
