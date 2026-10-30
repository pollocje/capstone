using System.Collections.Generic;
using Unity.Netcode;

/// <summary>Everything the end screen shows. Built by the host and sent to every player as-is.</summary>
public class RoundResult
{
    public bool won;
    public float elapsedSeconds;
    public float research;        // 0..1, furthest any player got
    public bool tornadoDetected;
    public bool tornadoKilled;
    public string reason = "";

    public class PlayerItems
    {
        public ulong clientId;
        public Dictionary<string, int> uses = new Dictionary<string, int>();
        public int Total { get { int t = 0; foreach (var v in uses.Values) t += v; return t; } }
    }
    public List<PlayerItems> players = new List<PlayerItems>();

    // Score parts, so the screen can show the breakdown.
    public int researchScore, detectedScore, killedScore, winScore, speedScore, itemScore, total;

    public void ComputeScore(RoundSettings s)
    {
        researchScore = UnityEngine.Mathf.RoundToInt(s.researchPoints * research);
        detectedScore = tornadoDetected ? s.tornadoDetectedPoints : 0;
        killedScore = tornadoKilled ? s.tornadoKilledPoints : 0;
        winScore = won ? s.winBonus : 0;
        speedScore = won ? UnityEngine.Mathf.RoundToInt(UnityEngine.Mathf.Max(0f, s.targetTimeSeconds - elapsedSeconds) * s.pointsPerSecondUnderTarget) : 0;
        int uses = 0; foreach (var p in players) uses += p.Total;
        itemScore = uses * s.pointsPerItemUsed;
        total = UnityEngine.Mathf.Max(0, researchScore + detectedScore + killedScore + winScore + speedScore + itemScore);
    }

    public void Write(FastBufferWriter w)
    {
        w.WriteValueSafe(won); w.WriteValueSafe(elapsedSeconds); w.WriteValueSafe(research);
        w.WriteValueSafe(tornadoDetected); w.WriteValueSafe(tornadoKilled); w.WriteValueSafe(reason);
        w.WriteValueSafe(players.Count);
        foreach (var p in players)
        {
            w.WriteValueSafe(p.clientId); w.WriteValueSafe(p.uses.Count);
            foreach (var kv in p.uses) { w.WriteValueSafe(kv.Key); w.WriteValueSafe(kv.Value); }
        }
        foreach (var v in new[] { researchScore, detectedScore, killedScore, winScore, speedScore, itemScore, total }) w.WriteValueSafe(v);
    }

    public static RoundResult Read(FastBufferReader r)
    {
        var res = new RoundResult();
        r.ReadValueSafe(out res.won); r.ReadValueSafe(out res.elapsedSeconds); r.ReadValueSafe(out res.research);
        r.ReadValueSafe(out res.tornadoDetected); r.ReadValueSafe(out res.tornadoKilled); r.ReadValueSafe(out res.reason);
        r.ReadValueSafe(out int count);
        for (int i = 0; i < count; i++)
        {
            var p = new PlayerItems();
            r.ReadValueSafe(out p.clientId); r.ReadValueSafe(out int n);
            for (int j = 0; j < n; j++) { r.ReadValueSafe(out string k); r.ReadValueSafe(out int v); p.uses[k] = v; }
            res.players.Add(p);
        }
        r.ReadValueSafe(out res.researchScore); r.ReadValueSafe(out res.detectedScore); r.ReadValueSafe(out res.killedScore);
        r.ReadValueSafe(out res.winScore); r.ReadValueSafe(out res.speedScore); r.ReadValueSafe(out res.itemScore); r.ReadValueSafe(out res.total);
        return res;
    }
}
