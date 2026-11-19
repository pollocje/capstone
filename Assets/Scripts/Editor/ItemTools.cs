using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
    // Every droppable item also gets WorldItemRecovery so it can never be lost out of reach.
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
                sb.AppendLine($"  -  {label}: no drop prefab - can't be put down, so it can't be lost (intended only for the anemometer)");
                continue;
            }

            string prefabPath = AssetDatabase.GetAssetPath(item.dropPrefab);
            var issues = new List<string>();
            var root = item.dropPrefab;

            var pickup = root.GetComponent<ItemPickup>();
            bool needPickup = pickup == null;
            bool wrongItem = pickup != null && pickup.Item != item;
            bool needRecovery = root.GetComponent<WorldItemRecovery>() == null;
            bool hasNetObj = root.GetComponent<NetworkObject>() != null;
            bool registered = lists.Any(l => l.Contains(root));

            if (needPickup) issues.Add("no ItemPickup (can't be picked back up)");
            if (wrongItem) issues.Add($"ItemPickup gives '{(pickup.Item != null ? pickup.Item.name : "nothing")}' instead of this item");
            if (needRecovery) issues.Add("no WorldItemRecovery (could be lost under the map or out of reach)");
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

    // ── Firework world prefab ──────────────────────────────────────────────

    const string FireworkItemPath = "Assets/Items/FireworkFlare.asset";
    const string FireworkPrefabPath = "Assets/Prefabs/Items/Item_FireworkFlare.prefab";
    const string FireworkModelPath = "Assets/Prefabs/Items/RomanCandle.prefab";   // same model the hand shows
    const string TemplatePrefabPath = "Assets/Prefabs/Items/Item_Binoculars.prefab";
    const float FireworkLength = 0.45f;   // world length of the dropped firework (m)

    // Builds the firework's put-down/pick-up world prefab from the binoculars one (NetworkObject,
    // NetworkTransform, Rigidbody, solid + trigger collider, ItemPickup), with the firework model.
    [MenuItem("Tools/Items/Create Firework World Prefab")]
    public static string CreateFireworkPrefab()
    {
        if (Application.isPlaying) return Report("Leave play mode first.");
        var item = AssetDatabase.LoadAssetAtPath<InventoryItem>(FireworkItemPath);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(FireworkModelPath);
        if (item == null || model == null) return Report("Missing FireworkFlare item or RomanCandle model.");

        if (AssetDatabase.LoadAssetAtPath<GameObject>(FireworkPrefabPath) == null &&
            !AssetDatabase.CopyAsset(TemplatePrefabPath, FireworkPrefabPath))
            return Report("Couldn't copy the binoculars prefab.");

        var root = PrefabUtility.LoadPrefabContents(FireworkPrefabPath);
        Vector3 size;
        try
        {
            root.name = "Item_FireworkFlare";
            var oldModel = root.transform.Find("Model");
            if (oldModel != null) Object.DestroyImmediate(oldModel.gameObject);

            // Meshes only - the RomanCandle prefab also carries the launch particle emitter.
            var visual = HeldModelUtility.CopyMeshes(model.transform, root.transform);
            visual.name = "Model";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);   // lying on its side
            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            // Scale so its longest side is FireworkLength, then centre it on the root.
            Bounds b = WorldBounds(renderers, visual.transform.position);
            float longest = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (longest > 1e-5f) visual.transform.localScale *= FireworkLength / longest;
            b = WorldBounds(renderers, visual.transform.position);
            visual.transform.position -= b.center - root.transform.position;
            size = b.size;

            foreach (var box in root.GetComponents<BoxCollider>())
            {
                box.center = Vector3.zero;
                // Solid fits the model; trigger is roomier so "standing near it" finds it too.
                box.size = box.isTrigger ? Vector3.Max(size * 1.6f, Vector3.one * 0.6f) : size;
            }

            var so = new SerializedObject(root.GetComponent<ItemPickup>());
            so.FindProperty("item").objectReferenceValue = item;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, FireworkPrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FireworkPrefabPath);
        item.dropPrefab = prefab;
        EditorUtility.SetDirty(item);

        var list = AssetDatabase.FindAssets("t:NetworkPrefabsList")
            .Select(g => AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(AssetDatabase.GUIDToAssetPath(g)))
            .FirstOrDefault(l => l != null);
        if (list != null && !list.Contains(prefab)) { list.Add(new NetworkPrefab { Prefab = prefab }); EditorUtility.SetDirty(list); }
        AssetDatabase.SaveAssets();
        // Write NetworkObject's GlobalObjectIdHash (generated on load) into the file, not just memory.
        AssetDatabase.ForceReserializeAssets(new[] { FireworkPrefabPath });

        return Report($"Created {FireworkPrefabPath} (model size {size:F2}), set as FireworkFlare's dropPrefab, registered: {list != null && list.Contains(prefab)}");
    }

    static Bounds WorldBounds(Renderer[] renderers, Vector3 fallback)
    {
        if (renderers.Length == 0) return new Bounds(fallback, Vector3.zero);
        // Renderer.bounds isn't valid on unloaded prefab contents, so use mesh bounds through the transform.
        var points = renderers.OfType<MeshRenderer>()
            .Select(r => (r, f: r.GetComponent<MeshFilter>()))
            .Where(x => x.f != null && x.f.sharedMesh != null)
            .SelectMany(x =>
            {
                var mb = x.f.sharedMesh.bounds;
                return new[] { -1, 1 }.SelectMany(sx => new[] { -1, 1 }.SelectMany(sy => new[] { -1, 1 }.Select(sz =>
                    x.r.transform.TransformPoint(mb.center + Vector3.Scale(mb.extents, new Vector3(sx, sy, sz))))));
            }).ToList();
        if (points.Count == 0) return new Bounds(fallback, Vector3.zero);
        var bounds = new Bounds(points[0], Vector3.zero);
        foreach (var p in points) bounds.Encapsulate(p);
        return bounds;
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

    [MenuItem("Tools/Items/Test All Items Put Down + Pick Up (play mode)")]
    public static string TestAllItems()
    {
        if (!Application.isPlaying) return Report("Enter play mode first.");
        if (_test != null) return Report("Test already running.");
        _test = AllItemsTest();
        _wait = 0;
        EditorApplication.isPaused = true;
        EditorApplication.update += Tick;
        return Report("All-items test started...");
    }

    static IEnumerator AllItemsTest()
    {
        var log = new StringBuilder("All items put down + pick up test\n");
        bool ok = true;
        void Check(bool pass, string what) { ok &= pass; log.AppendLine($"  {(pass ? "PASS" : "FAIL")} {what}"); }

        var hotbar = Object.FindObjectsByType<Hotbar>(FindObjectsSortMode.None).FirstOrDefault(h => !h.IsSpawned || h.IsOwner);
        var grab = hotbar != null ? hotbar.GetComponent<PlayerGrabController>() : null;
        if (hotbar == null || grab == null) { Report("FAIL: need a local player with Hotbar + PlayerGrabController."); yield break; }
        log.AppendLine($"  networked: {hotbar.IsSpawned}");

        var all = AssetDatabase.FindAssets("t:InventoryItem")
            .Select(g => AssetDatabase.LoadAssetAtPath<InventoryItem>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(i => i != null).ToList();

        foreach (var item in all)
        {
            // Empty the hotbar so every item gets a free slot.
            for (int s = 0; s < hotbar.items.Length; s++) { hotbar.SelectSlot(s); hotbar.ConsumeSelected(); }
            yield return 2;
            hotbar.SelectSlot(1); hotbar.SelectSlot(0);
            Check(hotbar.AddItem(item), $"{item.name}: added to the hotbar");
            hotbar.SelectSlot(hotbar.IndexOf(item));
            yield return 2;

            if (item.itemType == ItemType.Anemometer)
            {
                Check(grab.PutDownTarget == null, $"{item.name}: E can NOT put it down (stays with its owner)");
                continue;
            }

            Check(grab.PutDownTarget == item, $"{item.name}: E would put it down");
            var before = new HashSet<ItemPickup>(Object.FindObjectsByType<ItemPickup>(FindObjectsSortMode.None));
            bool down = hotbar.DropSelected();   // what E calls (TryPutDownMap)
            foreach (var f in Wait(0.3f)) yield return f;
            var world = Object.FindObjectsByType<ItemPickup>(FindObjectsSortMode.None).FirstOrDefault(p => !before.Contains(p) && p.Item == item);
            Check(down && world != null, $"{item.name}: put down as a world pickup");
            Check(hotbar.IndexOf(item) < 0, $"{item.name}: left the hotbar");
            if (world == null) continue;

            foreach (var f in Wait(1.5f)) yield return f;   // settle + ItemPickup.armDelay
            var hit = Physics.Raycast(world.transform.position + Vector3.up * 2f, Vector3.down, out var rh, 5f, ~0, QueryTriggerInteraction.Ignore);
            Check(hit && rh.collider.GetComponentInParent<ItemPickup>() == world, $"{item.name}: has a solid collider E can aim at");
            Check(world.transform.position.y > -50f, $"{item.name}: resting in the world (y {world.transform.position.y:F1})");

            bool took = world.TryPickup(hotbar);   // what E calls when aimed at / near it
            foreach (var f in Wait(0.5f)) yield return f;   // online: claim -> host -> confirm -> despawn
            Check(took && hotbar.IndexOf(item) >= 0, $"{item.name}: picked back up into the hotbar");
            Check(world == null, $"{item.name}: world copy removed");
        }

        log.AppendLine(ok ? "RESULT: PASS" : "RESULT: FAIL");
        Report(log.ToString());
    }

    // Reproduces "items vanish at the vending machine": E both asks the machine for an item and
    // used to put the held item down (into the machine). Simulates pressing E there 4 times.
    [MenuItem("Tools/Items/Test Vending Machine + E (play mode)")]
    public static string TestVending()
    {
        if (!Application.isPlaying) return Report("Enter play mode first.");
        if (_test != null) return Report("Test already running.");
        _test = VendingTest();
        _wait = 0;
        EditorApplication.isPaused = true;
        EditorApplication.update += Tick;
        return Report("Vending test started...");
    }

    static IEnumerator VendingTest()
    {
        const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;
        var log = new StringBuilder("Vending machine + E test\n");
        bool ok = true;
        void Check(bool pass, string what) { ok &= pass; log.AppendLine($"  {(pass ? "PASS" : "FAIL")} {what}"); }

        var hotbar = Object.FindObjectsByType<Hotbar>(FindObjectsSortMode.None).FirstOrDefault(h => !h.IsSpawned || h.IsOwner);
        var grab = hotbar != null ? hotbar.GetComponent<PlayerGrabController>() : null;
        var dispenser = Object.FindFirstObjectByType<ItemDispenser>();
        if (hotbar == null || grab == null || dispenser == null || !dispenser.IsSpawned)
        { Report("FAIL: needs a hosted game (start from MainMenu) with the local player and the vending machine."); yield break; }

        for (int s = 0; s < hotbar.items.Length; s++) { hotbar.SelectSlot(s); hotbar.ConsumeSelected(); }
        hotbar.SelectSlot(0);

        // Walk the player up to the machine (CharacterController has to be off to teleport).
        var cc = grab.transform.root.GetComponentInChildren<CharacterController>(true);
        var body = cc != null ? cc.transform : grab.transform;
        if (cc != null) cc.enabled = false;
        body.position = dispenser.transform.position + dispenser.transform.forward * 1.2f;
        if (cc != null) cc.enabled = true;
        foreach (var f in Wait(1f)) yield return f;
        Check(dispenser.PlayerInRange, "player is in the vending machine's range");

        var request = typeof(ItemDispenser).GetMethod("RequestItemRpc", Priv);
        var pending = typeof(ItemDispenser).GetField("requestPending", Priv);
        var interact = typeof(PlayerGrabController).GetMethod("TryInteract", Priv);
        var worldBefore = new HashSet<ItemPickup>(Object.FindObjectsByType<ItemPickup>(FindObjectsSortMode.None));

        for (int press = 1; press <= 4 && !dispenser.IsEmpty; press++)
        {
            InventoryItem inHand = hotbar.GetSelectedItem();
            var before = hotbar.items.Where(i => i != null).ToList();
            // One E press = the machine's own key check + PlayerGrabController's Interact action.
            pending.SetValue(dispenser, true);
            request.Invoke(dispenser, new object[] { default(RpcParams) });
            interact.Invoke(grab, null);
            foreach (var f in Wait(0.5f)) yield return f;
            if (inHand != null) Check(hotbar.IndexOf(inHand) >= 0, $"press {press}: '{inHand.itemName}' (in hand) stayed in the hotbar");
            Check(before.All(i => hotbar.IndexOf(i) >= 0), $"press {press}: nothing left the hotbar");

            // Like a player looking at what they just got: hold the newest item for the next press.
            var newest = hotbar.items.FirstOrDefault(i => i != null && !before.Contains(i));
            if (newest != null) hotbar.SelectSlot(hotbar.IndexOf(newest));
        }

        var vendItems = ((List<InventoryItem>)typeof(ItemDispenser).GetField("items", Priv).GetValue(dispenser)).Where(i => i != null).ToList();
        var got = hotbar.items.Where(i => i != null).ToList();
        Check(vendItems.All(got.Contains), $"hotbar has every vending item ({string.Join(", ", vendItems.Select(i => i.itemName))}); hotbar: {string.Join(", ", got.Select(i => i.itemName))}");
        Check(got.Count == vendItems.Count, $"E at the machine didn't also grab other nearby items ({got.Count} in hotbar)");
        var dropped = Object.FindObjectsByType<ItemPickup>(FindObjectsSortMode.None).Where(p => !worldBefore.Contains(p)).ToList();
        Check(dropped.Count == 0, $"nothing was put down at the machine ({dropped.Count} dropped)");

        // Items placed in the scene must not be "rescued" just for sitting where they were placed.
        foreach (var f in Wait(8f)) yield return f;
        foreach (var r in Object.FindObjectsByType<WorldItemRecovery>(FindObjectsSortMode.None))
            Check(r.Recoveries == 0, $"scene item '{r.name}' left alone by WorldItemRecovery (recoveries {r.Recoveries}, pos {r.transform.position:F1})");

        log.AppendLine(ok ? "RESULT: PASS" : "RESULT: FAIL");
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
