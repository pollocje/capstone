using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

// Droppable-item checks. Validate/Fix work in edit mode; the test runs in play mode.
// Reports go to the Console and Temp/ItemToolsReport.txt.
public static class ItemTools
{
    const string ReportPath = "Temp/ItemToolsReport.txt";

    // ── Validate / fix (edit mode) ─────────────────────────────────────────

    [MenuItem("Tools/Items/Validate Droppable Items")]
    public static string Validate() => Run(fix: false);

    [MenuItem("Tools/Items/Fix Droppable Items")]
    public static string Fix() => Run(fix: true);

    // Every InventoryItem with a dropPrefab must be able to come back: its drop prefab needs an
    // ItemPickup pointing at that item, a NetworkObject that's registered (otherwise online drops
    // fail and the item is gone), a Rigidbody and a solid collider (so E's raycast can hit it).
    // Droppable-type items (the barrel) are needed to win, so they also get WorldItemRecovery.
    static string Run(bool fix)
    {
        if (Application.isPlaying) return Report("Leave play mode first.");

        var lists = AssetDatabase.FindAssets("t:NetworkPrefabsList")
            .Select(g => AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(l => l != null).ToList();

        var sb = new StringBuilder(fix ? "Fix Droppable Items\n" : "Validate Droppable Items\n");
        int problems = 0, fixedCount = 0;

        foreach (var guid in AssetDatabase.FindAssets("t:InventoryItem"))
        {
            string itemPath = AssetDatabase.GUIDToAssetPath(guid);
            var item = AssetDatabase.LoadAssetAtPath<InventoryItem>(itemPath);
            if (item == null) continue;
            string label = $"{Path.GetFileNameWithoutExtension(itemPath)} ({item.itemType})";

            if (item.dropPrefab == null)
            {
                sb.AppendLine($"  -  {label}: no drop prefab - can't be dropped, so it can't be lost (intended for the anemometer and consumables)");
                continue;
            }

            string prefabPath = AssetDatabase.GetAssetPath(item.dropPrefab);
            var issues = new List<string>();
            var root = item.dropPrefab;

            var pickup = root.GetComponent<ItemPickup>();
            bool needPickup = pickup == null;
            bool wrongItem = pickup != null && pickup.Item != item;
            bool needRecovery = item.itemType == ItemType.Droppable && root.GetComponent<WorldItemRecovery>() == null;
            bool hasNetObj = root.GetComponent<NetworkObject>() != null;
            bool registered = lists.Any(l => l.Contains(root));

            if (needPickup) issues.Add("no ItemPickup (can't be picked back up)");
            if (wrongItem) issues.Add($"ItemPickup gives '{(pickup.Item != null ? pickup.Item.name : "nothing")}' instead of this item");
            if (needRecovery) issues.Add("no WorldItemRecovery (key item could be lost out of reach)");
            if (!hasNetObj) issues.Add("no NetworkObject (other players won't see it dropped) - fix by hand");
            else if (!registered) issues.Add("not in a NetworkPrefabsList (online drop is lost)");
            if (root.GetComponent<NetworkTransform>() == null) issues.Add("no NetworkTransform (others won't see it move) - fix by hand");
            if (root.GetComponent<Rigidbody>() == null) issues.Add("no Rigidbody - fix by hand");
            if (!root.GetComponentsInChildren<Collider>(true).Any(c => !c.isTrigger))
                issues.Add("no solid collider (E can't aim at it) - fix by hand");

            if (issues.Count == 0) { sb.AppendLine($"  OK {label} -> {prefabPath}"); continue; }

            problems += issues.Count;
            sb.AppendLine($"  !! {label} -> {prefabPath}");
            foreach (var i in issues) sb.AppendLine($"       - {i}");

            if (!fix) continue;

            if (needPickup || wrongItem || needRecovery)
            {
                var contents = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    var p = contents.GetComponent<ItemPickup>() ?? contents.AddComponent<ItemPickup>();
                    var so = new SerializedObject(p);
                    so.FindProperty("item").objectReferenceValue = item;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    if (needRecovery) contents.AddComponent<WorldItemRecovery>();
                    PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                sb.AppendLine("       fixed: ItemPickup / item / WorldItemRecovery");
                fixedCount++;
            }
            if (hasNetObj && !registered && lists.Count > 0)
            {
                lists[0].Add(new NetworkPrefab { Prefab = root });
                EditorUtility.SetDirty(lists[0]);
                sb.AppendLine($"       fixed: registered in {AssetDatabase.GetAssetPath(lists[0])}");
                fixedCount++;
            }
        }

        if (fix) AssetDatabase.SaveAssets();
        sb.AppendLine(problems == 0 ? "RESULT: all droppable items OK" : $"RESULT: {problems} problem(s){(fix ? $", {fixedCount} fix(es) applied - run Validate again" : " - run Tools/Items/Fix Droppable Items")}");
        return Report(sb.ToString());
    }

    // ── Play-mode test: drop, re-pickup, recovery ──────────────────────────

    [MenuItem("Tools/Items/Test Barrel Drop + Re-pickup (play mode)")]
    public static string TestBarrel()
    {
        if (!Application.isPlaying) return Report("Enter play mode first.");
        if (_test != null) return Report("Test already running.");
        _test = BarrelTest();
        _wait = 0;
        EditorApplication.isPaused = true;
        EditorApplication.update += Tick;
        return Report("Barrel test started...");
    }

    static IEnumerator _test;
    static int _wait;

    static void Tick()
    {
        if (!Application.isPlaying) { Stop(); return; }
        if (_wait > 0) { _wait--; EditorApplication.Step(); return; }
        if (!_test.MoveNext()) Stop();
        else if (_test.Current is int frames) _wait = frames;
    }

    static void Stop() { EditorApplication.update -= Tick; _test = null; }

    static IEnumerator BarrelTest()
    {
        var log = new StringBuilder("Barrel drop + re-pickup test\n");
        bool ok = true;
        void Check(bool pass, string what) { ok &= pass; log.AppendLine($"  {(pass ? "PASS" : "FAIL")} {what}"); }

        var barrel = AssetDatabase.LoadAssetAtPath<InventoryItem>("Assets/Items/Barrel.asset");
        var hotbar = Object.FindObjectsByType<Hotbar>(FindObjectsSortMode.None)
            .FirstOrDefault(h => !h.IsSpawned || h.IsOwner);
        if (barrel == null || hotbar == null || hotbar.items == null)
        {
            Report("FAIL: need Assets/Items/Barrel.asset and a spawned local player with a Hotbar.");
            yield break;
        }
        log.AppendLine($"  networked: {hotbar.IsSpawned}");

        for (int round = 1; round <= 3; round++)
        {
            // Give + select + drop (same calls as the dispenser and left click).
            if (hotbar.IndexOf(barrel) < 0 && !hotbar.AddItem(barrel)) { Check(false, "hotbar had room for the barrel"); break; }
            hotbar.SelectSlot(hotbar.IndexOf(barrel));
            var before = new HashSet<ItemPickup>(Object.FindObjectsByType<ItemPickup>(FindObjectsSortMode.None));
            hotbar.DropSelected();
            yield return 10;

            var dropped = Object.FindObjectsByType<ItemPickup>(FindObjectsSortMode.None)
                .FirstOrDefault(p => !before.Contains(p) && p.Item == barrel);
            Check(dropped != null, $"round {round}: drop spawned a world barrel with ItemPickup");
            Check(hotbar.IndexOf(barrel) < 0, $"round {round}: barrel left the hotbar");
            if (dropped == null) break;
            Check(dropped.CompareTag("Extinguish"), $"round {round}: world barrel still tagged Extinguish (can kill the tornado)");

            if (round == 2)
            {
                // Lost under the ground -> recovered.
                var rec = dropped.GetComponent<WorldItemRecovery>();
                Check(rec != null, "dropped barrel has WorldItemRecovery");
                if (rec != null)
                {
                    foreach (var f in Wait(2.5f)) yield return f; // let it settle so a safe spot is recorded
                    Vector3 settled = dropped.transform.position;
                    Teleport(dropped.gameObject, settled + Vector3.down * 30f);
                    rec.CheckNow();
                    yield return 10;
                    Check(rec.Recoveries == 1, $"under-ground barrel recovered (recoveries {rec.Recoveries})");
                    Check(Vector3.Distance(dropped.transform.position, settled) < 3f, $"put back near its last safe spot ({Vector3.Distance(dropped.transform.position, settled):F1}m away)");

                    // Off the map -> recovered.
                    foreach (var f in Wait(1.5f)) yield return f;
                    Teleport(dropped.gameObject, settled + new Vector3(100000f, 0f, 0f));
                    rec.CheckNow();
                    yield return 10;
                    Check(rec.Recoveries == 2, $"off-map barrel recovered (recoveries {rec.Recoveries})");

                    // Resting high up (roof/tree) -> recovered after stuckHighSeconds.
                    foreach (var f in Wait(1.5f)) yield return f;
                    var rb = dropped.GetComponent<Rigidbody>();
                    rb.isKinematic = true; // stands in for "lodged on a roof"
                    Teleport(dropped.gameObject, settled + Vector3.up * (rec.maxRestHeightAboveGround + 6f));
                    foreach (var f in Wait(rec.stuckHighSeconds + 1.5f)) yield return f;
                    rb.isKinematic = false;
                    Check(rec.Recoveries == 3, $"barrel stuck up high recovered (recoveries {rec.Recoveries})");
                }
            }
            else
            {
                foreach (var f in Wait(1.5f)) yield return f; // past ItemPickup.armDelay
            }

            // Pick it back up (what pressing E calls).
            Check(dropped.IsArmed, $"round {round}: pickup armed after delay");
            bool took = dropped.TryPickup(hotbar);
            yield return 10;
            Check(took, $"round {round}: TryPickup accepted");
            Check(hotbar.IndexOf(barrel) >= 0, $"round {round}: barrel back in the hotbar");
            Check(dropped == null, $"round {round}: world barrel removed after pickup");
        }

        log.AppendLine(ok ? "RESULT: PASS" : "RESULT: FAIL");
        Report(log.ToString());
    }

    [MenuItem("Tools/Items/Test Re-picked Barrel Kills Tornado (play mode)")]
    public static string TestKill()
    {
        if (!Application.isPlaying) return Report("Enter play mode first.");
        if (_test != null) return Report("Test already running.");
        _test = KillTest();
        _wait = 0;
        EditorApplication.isPaused = true;
        EditorApplication.update += Tick;
        return Report("Kill test started...");
    }

    static IEnumerator KillTest()
    {
        var log = new StringBuilder("Re-picked barrel kills tornado test\n");
        var barrel = AssetDatabase.LoadAssetAtPath<InventoryItem>("Assets/Items/Barrel.asset");
        var hotbar = Object.FindObjectsByType<Hotbar>(FindObjectsSortMode.None).FirstOrDefault(h => !h.IsSpawned || h.IsOwner);
        var spawner = Object.FindFirstObjectByType<TornadoSpawner>();
        if (barrel == null || hotbar == null || spawner == null) { Report("FAIL: need barrel asset, local Hotbar and TornadoSpawner."); yield break; }

        bool killed = false;
        System.Action<TornadoLift> onKilled = _ => killed = true;
        TornadoLift.Killed += onKilled;
        try
        {
            var existing = new HashSet<TornadoLift>(Object.FindObjectsByType<TornadoLift>(FindObjectsSortMode.None));
            spawner.SpawnTornado();
            yield return 5;
            var tornado = Object.FindObjectsByType<TornadoLift>(FindObjectsSortMode.None).FirstOrDefault(t => !existing.Contains(t));
            if (tornado == null) { Report("FAIL: spawner didn't create a tornado."); yield break; }
            foreach (var f in Wait(7f)) yield return f; // past the reveal cutscene (if any) so the tornado runs normally

            // Drop, pick back up, drop again: the barrel that reaches the tornado is a re-picked one.
            if (hotbar.IndexOf(barrel) < 0) hotbar.AddItem(barrel);
            hotbar.SelectSlot(hotbar.IndexOf(barrel));
            hotbar.DropSelected();
            yield return 5;
            var first = Object.FindObjectsByType<ItemPickup>(FindObjectsSortMode.None).First(p => p.Item == barrel);
            foreach (var f in Wait(1.5f)) yield return f;
            bool repicked = first.TryPickup(hotbar);
            yield return 5;
            log.AppendLine($"  {(repicked ? "PASS" : "FAIL")} barrel picked back up before the attempt");

            hotbar.SelectSlot(hotbar.IndexOf(barrel));
            hotbar.DropSelected();
            yield return 5;
            var dropped = Object.FindObjectsByType<ItemPickup>(FindObjectsSortMode.None).First(p => p.Item == barrel);
            var rec = dropped.GetComponent<WorldItemRecovery>();
            Teleport(dropped.gameObject, tornado.transform.position + Vector3.up * 1f);

            float end = Time.time + 10f;
            while (!killed && Time.time < end) yield return 1;

            log.AppendLine($"  {(killed ? "PASS" : "FAIL")} tornado destroyed by the re-picked barrel");
            log.AppendLine($"  {(rec != null && rec.Recoveries == 0 ? "PASS" : "FAIL")} recovery didn't pull the barrel out of the tornado (recoveries {rec?.Recoveries})");
            log.AppendLine(killed && repicked && rec != null && rec.Recoveries == 0 ? "RESULT: PASS" : "RESULT: FAIL");
            if (dropped != null) Object.Destroy(dropped.gameObject);
        }
        finally { TornadoLift.Killed -= onKilled; }
        Report(log.ToString());
    }

    // Steps one frame at a time until this much game time has passed.
    static IEnumerable<int> Wait(float seconds)
    {
        float end = Time.time + seconds;
        while (Time.time < end) yield return 1;
    }

    static void Teleport(GameObject go, Vector3 pos)
    {
        var nt = go.GetComponent<NetworkTransform>();
        if (nt != null && nt.IsSpawned) nt.Teleport(pos, go.transform.rotation, go.transform.localScale);
        else go.transform.position = pos;
        var rb = go.GetComponent<Rigidbody>();
        if (rb != null) { rb.position = pos; if (!rb.isKinematic) rb.linearVelocity = Vector3.zero; }
    }

    static string Report(string msg)
    {
        Directory.CreateDirectory("Temp");
        File.WriteAllText(ReportPath, msg);
        Debug.Log("[ItemTools] " + msg);
        return msg;
    }
}
