using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

// Play-mode checks for TornadoRandomizer. Reports go to the Console and Temp/TornadoRandomizerReport.txt.
public static class TornadoRandomizerTools
{
    const string ReportPath = "Temp/TornadoRandomizerReport.txt";
    const int BatchSize = 12;          // tornadoes per batch
    const int ForcedOnPlayer = 6;      // of those, how many are dropped right on a player (tests the reroll)
    const float SimSeconds = 90f;      // simulated game time to watch them for
    const float SimTimeScale = 5f;
    const int MaxSteps = 4000;

    class Sample
    {
        public TornadoRandomizer r;
        public TornadoLift lift;
        public Vector3 requested, spawned, last;
        public float nearestBefore, nearestAfter, travelled, maxFromSpawn;
        public float minSpeed = float.MaxValue, maxSpeed, sumSpeed; public int n;
        public int surges, lulls; string lastEvent = "none";
        public readonly List<float> speedTrace = new List<float>();

        public void Record()
        {
            float s = Get<float>(r, "currentSpeed");
            string e = Get<string>(r, "currentEvent");
            if (e != lastEvent) { if (e == "surge") surges++; else if (e == "lull") lulls++; lastEvent = e; }
            minSpeed = Mathf.Min(minSpeed, s); maxSpeed = Mathf.Max(maxSpeed, s); sumSpeed += s; n++;
            Vector3 p = r.transform.position;
            travelled += Flat(p - last).magnitude; last = p;
            maxFromSpawn = Mathf.Max(maxFromSpawn, Flat(p - spawned).magnitude);
            if (n % 25 == 0) speedTrace.Add(s);
        }
    }

    static List<Sample> _batch;
    static int _steps;
    static float _simEnd, _oldTimeScale;

    [MenuItem("Tools/Tornado/Randomizer Batch Test (play mode)")]
    public static string BatchTest()
    {
        if (!Application.isPlaying) return Report("Enter play mode first.");
        if (_batch != null) return Report("A batch is already running.");

        var spawner = Object.FindFirstObjectByType<TornadoSpawner>();
        var area = Object.FindFirstObjectByType<TornadoSpawnArea>();
        if (spawner == null || area == null) return Report("Need a TornadoSpawner and TornadoSpawnArea in the scene.");
        var prefab = (GameObject)new SerializedObject(spawner).FindProperty("tornadoPrefab").objectReferenceValue;
        var players = GameObject.FindGameObjectsWithTag("Player");
        if (players.Length == 0) return Report("No players spawned yet - wait for the player, then retry.");
        if (Time.timeSinceLevelLoad < 1f) return Report("Level loaded <1 s ago (reroll is skipped then) - step some frames first.");

        bool networked = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        _batch = new List<Sample>();
        for (int i = 0; i < BatchSize; i++)
        {
            Vector3 req = i < ForcedOnPlayer ? players[i % players.Length].transform.position : area.GetRandomPoint();
            var go = Object.Instantiate(prefab, req, Quaternion.identity); // TornadoRandomizer.Awake runs here
            go.name = $"RandomizerTest_{i}";
            // Skip the reveal cutscene (camera swap + 6 s hold) so the batch starts moving at once.
            var cut = go.GetComponent<TornadoRevealCutscene>();
            if (cut != null) Object.DestroyImmediate(cut);
            if (networked && go.TryGetComponent(out NetworkObject no)) no.Spawn();

            var s = new Sample { r = go.GetComponent<TornadoRandomizer>(), lift = go.GetComponent<TornadoLift>(),
                                 requested = req, spawned = go.transform.position, last = go.transform.position };
            s.nearestBefore = Nearest(req, players);
            s.nearestAfter = Nearest(s.spawned, players);
            _batch.Add(s);
        }

        _oldTimeScale = Time.timeScale;
        Time.timeScale = SimTimeScale;
        _simEnd = Time.time + SimSeconds;
        _steps = 0;
        EditorApplication.isPaused = true;
        EditorApplication.update += StepBatch;
        return Report($"Batch started: {BatchSize} tornadoes, simulating {SimSeconds}s (networked={networked}). Report -> {ReportPath}");
    }

    static void StepBatch()
    {
        if (!Application.isPlaying) { EditorApplication.update -= StepBatch; _batch = null; return; }
        foreach (var s in _batch) if (s.r != null) s.Record();
        if (Time.time < _simEnd && _steps++ < MaxSteps) { EditorApplication.Step(); return; }

        EditorApplication.update -= StepBatch;
        Time.timeScale = _oldTimeScale;
        WriteBatchReport();
        foreach (var s in _batch) if (s.r != null) Object.Destroy(s.r.gameObject);
        _batch = null;
    }

    static void WriteBatchReport()
    {
        var r = _batch[0].r;
        var sb = new StringBuilder();
        sb.AppendLine($"TornadoRandomizer batch: {_batch.Count} tornadoes, {_steps} steps, simulated to t={Time.time:F1}");
        sb.AppendLine($"Ranges: base {r.baseSpeedRange}, wander {r.wanderRangeRange}, clamp {r.speedClamp}, minDist {r.minDistanceFromPlayers}");
        sb.AppendLine(" #  nearPlayer req->final   base  wander  speed min/avg/max   surges lulls  travelled  maxFromSpawn  liftSpeed==current");
        bool ok = true;
        var bases = new List<float>();
        int totalSurges = 0, totalLulls = 0;
        foreach (var (s, i) in _batch.Select((s, i) => (s, i)))
        {
            float b = Get<float>(s.r, "rolledBaseSpeed"), w = Get<float>(s.r, "rolledWanderRange");
            bases.Add(b); totalSurges += s.surges; totalLulls += s.lulls;
            bool synced = Mathf.Approximately(s.lift.moveSpeed, Get<float>(s.r, "currentSpeed"));
            sb.AppendLine($"{i,2}  {s.nearestBefore,6:F1} -> {s.nearestAfter,6:F1}   {b,5:F2}  {w,5:F1}   {s.minSpeed,4:F2}/{s.sumSpeed / Mathf.Max(1, s.n),4:F2}/{s.maxSpeed,4:F2}   {s.surges,6} {s.lulls,5}  {s.travelled,8:F1}m  {s.maxFromSpawn,8:F1}m   {synced}");
            sb.AppendLine($"     speed trace: {string.Join(" ", s.speedTrace.Select(v => v.ToString("F1")))}");

            if (b < r.baseSpeedRange.x || b > r.baseSpeedRange.y) { ok = false; sb.AppendLine("     FAIL base speed out of range"); }
            if (w < r.wanderRangeRange.x || w > r.wanderRangeRange.y) { ok = false; sb.AppendLine("     FAIL wander range out of range"); }
            if (s.minSpeed < r.speedClamp.x - 1e-3f || s.maxSpeed > r.speedClamp.y + 1e-3f) { ok = false; sb.AppendLine("     FAIL speed outside clamp"); }
            if (s.maxSpeed - s.minSpeed < 0.05f) { ok = false; sb.AppendLine("     FAIL speed never varied"); }
            if (s.travelled < 1f) { ok = false; sb.AppendLine("     FAIL tornado didn't move"); }
            if (!synced) { ok = false; sb.AppendLine("     FAIL TornadoLift.moveSpeed not driven by randomizer"); }
            if (s.nearestAfter < s.nearestBefore - 0.01f) { ok = false; sb.AppendLine("     FAIL reroll moved it closer to players"); }
            if (s.nearestBefore < r.minDistanceFromPlayers && s.nearestAfter < r.minDistanceFromPlayers)
                sb.AppendLine("     WARN no spot found beyond minDistanceFromPlayers - kept the farthest candidate");
        }
        int distinct = bases.Select(v => Mathf.Round(v * 100f)).Distinct().Count();
        sb.AppendLine($"Distinct base speeds: {distinct}/{bases.Count}; spread {bases.Min():F2}-{bases.Max():F2}");
        sb.AppendLine($"Events across batch: {totalSurges} surges, {totalLulls} lulls");
        if (distinct < bases.Count / 2) { ok = false; sb.AppendLine("FAIL base speeds not varied between tornadoes"); }
        if (totalSurges == 0 || totalLulls == 0) { ok = false; sb.AppendLine("FAIL expected both surges and lulls over the batch"); }
        sb.AppendLine(ok ? "RESULT: PASS" : "RESULT: FAIL");
        Report(sb.ToString());
    }

    [MenuItem("Tools/Tornado/Report Live Tornadoes")]
    public static string ReportLive()
    {
        if (!Application.isPlaying) return Report("Enter play mode first.");
        var players = GameObject.FindGameObjectsWithTag("Player");
        var sb = new StringBuilder($"t={Time.time:F1}, players={players.Length}\n");
        foreach (var r in Object.FindObjectsByType<TornadoRandomizer>(FindObjectsSortMode.None))
        {
            var lift = r.GetComponent<TornadoLift>();
            sb.AppendLine($"{r.name}: pos {r.transform.position:F0}, nearest player {Nearest(r.transform.position, players):F1}m, " +
                          $"base {Get<float>(r, "rolledBaseSpeed"):F2}, wander {Get<float>(r, "rolledWanderRange"):F1}, " +
                          $"current {Get<float>(r, "currentSpeed"):F2} ({Get<string>(r, "currentEvent")}), " +
                          $"lift.enabled {lift.enabled}, lift.moveSpeed {lift.moveSpeed:F2}, lift.wanderRange {lift.wanderRange:F1}");
        }
        return Report(sb.ToString());
    }

    [MenuItem("Tools/Tornado/Step 300 Frames")]
    public static void Step300() => LiveMapTools.StepFrames(300);

    static float Nearest(Vector3 p, GameObject[] players) =>
        players.Where(x => x != null).Select(x => Flat(x.transform.position - p).magnitude).DefaultIfEmpty(float.MaxValue).Min();

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    static T Get<T>(object o, string field) =>
        (T)o.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);

    static string Report(string msg)
    {
        Directory.CreateDirectory("Temp");
        File.WriteAllText(ReportPath, msg);
        Debug.Log("[TornadoRandomizerTools] " + msg);
        return msg;
    }
}
