using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(TornadoLift))]
public class TornadoRandomizer : MonoBehaviour
{
    [Header("Spawn")]
    [Tooltip("Reroll the spawner's point so the tornado doesn't appear right next to the players.")]
    public bool rerollSpawnNearPlayers = true;
    [Tooltip("Minimum distance (m, ground plane) from every player for an acceptable spawn point.")]
    public float minDistanceFromPlayers = 60f;
    [Tooltip("How many extra points to try from the TornadoSpawnArea before keeping the farthest one.")]
    [Range(1, 50)] public int spawnRerollAttempts = 15;
    [Tooltip("Tag used to find players.")]
    public string playerTag = "Player";

    [Header("Per-run personality (x = min, y = max)")]
    [Tooltip("Base movement speed (m/s), rolled once per tornado. TornadoLift's own default is 3.")]
    public Vector2 baseSpeedRange = new Vector2(2f, 5f);
    [Tooltip("Wander range (m) around the spawn point, rolled once per tornado. TornadoLift's own default is 18.")]
    public Vector2 wanderRangeRange = new Vector2(12f, 30f);

    [Header("Continuous speed drift")]
    [Tooltip("How far speed drifts from the base, as a fraction (0.3 = +/-30%).")]
    [Range(0f, 1f)] public float driftAmount = 0.3f;
    [Tooltip("How fast the drift changes. Lower = slow, rolling changes; higher = jittery.")]
    public float driftFrequency = 0.15f;

    [Header("Surges and lulls")]
    [Tooltip("Seconds between rolls for a surge/lull event.")]
    public Vector2 eventIntervalRange = new Vector2(8f, 20f);
    [Tooltip("Chance (0-1) that an event roll actually triggers something.")]
    [Range(0f, 1f)] public float eventChance = 0.6f;
    [Tooltip("Of triggered events, the chance (0-1) it's a surge rather than a lull.")]
    [Range(0f, 1f)] public float surgeVsLull = 0.5f;
    [Tooltip("Speed multiplier during a surge.")]
    public Vector2 surgeMultiplierRange = new Vector2(1.5f, 2.5f);
    [Tooltip("Speed multiplier during a lull (0 = stops completely).")]
    public Vector2 lullMultiplierRange = new Vector2(0.1f, 0.5f);
    [Tooltip("How long a surge or lull lasts (s).")]
    public Vector2 eventDurationRange = new Vector2(3f, 7f);
    [Tooltip("Seconds to blend into and out of an event.")]
    public float eventBlendTime = 1.5f;

    [Header("Safety clamp")]
    [Tooltip("Final speed is always kept inside this range (m/s).")]
    public Vector2 speedClamp = new Vector2(0f, 12f);

    [Header("Debug (read only)")]
    [SerializeField] private float rolledBaseSpeed;
    [SerializeField] private float rolledWanderRange;
    [SerializeField] private float currentSpeed;
    [SerializeField] private string currentEvent = "none";

    TornadoLift _lift;
    float _noiseSeed;
    float _eventMultiplier = 1f;   // blended value actually applied
    float _eventTarget = 1f;       // value we're blending towards
    float _eventEndsAt;
    float _nextEventRollAt;
    bool _rolled;

    static bool IsNetworked => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    static bool IsAuthority => !IsNetworked || NetworkManager.Singleton.IsServer;

    void Awake()
    {
        _lift = GetComponent<TornadoLift>();
        if (!IsAuthority) return;

        // Awake runs inside TornadoSpawner's Instantiate(), before it calls NetworkObject.Spawn()
        // and before TornadoLift.Start() records its wander centre, so moving the transform here
        // means clients receive the final spot with the spawn itself and the wander follows it.
        // Skipped for tornadoes placed in a scene by hand (they Awake during the level load).
        if (rerollSpawnNearPlayers && Time.timeSinceLevelLoad >= 1f)
            RerollSpawnPoint();

        RollPersonality();
    }

    void Update()
    {
        if (!IsAuthority || !_rolled || _lift == null || !_lift.enabled) return;

        float now = Time.time;

        // Surge/lull scheduling.
        if (_eventTarget != 1f && now >= _eventEndsAt)
        {
            _eventTarget = 1f;
            currentEvent = "none";
        }
        if (now >= _nextEventRollAt)
        {
            _nextEventRollAt = now + RandomIn(eventIntervalRange);
            if (_eventTarget == 1f && Random.value < eventChance)
            {
                bool surge = Random.value < surgeVsLull;
                _eventTarget = RandomIn(surge ? surgeMultiplierRange : lullMultiplierRange);
                _eventEndsAt = now + RandomIn(eventDurationRange);
                currentEvent = surge ? "surge" : "lull";
            }
        }

        // Blend towards the event multiplier so speed changes never snap.
        float blendRate = eventBlendTime > 0f ? Mathf.Abs(_eventTarget - 1f) / eventBlendTime + 0.01f : 1000f;
        _eventMultiplier = Mathf.MoveTowards(_eventMultiplier, _eventTarget, blendRate * Time.deltaTime);

        // Smooth drift in [-1, 1].
        float drift = Mathf.PerlinNoise(_noiseSeed, now * driftFrequency) * 2f - 1f;

        currentSpeed = rolledBaseSpeed * (1f + drift * driftAmount) * _eventMultiplier;
        currentSpeed = Mathf.Clamp(currentSpeed, speedClamp.x, speedClamp.y);
        _lift.moveSpeed = currentSpeed;
    }

    void RollPersonality()
    {
        rolledBaseSpeed = RandomIn(baseSpeedRange);
        rolledWanderRange = RandomIn(wanderRangeRange);
        _lift.moveSpeed = rolledBaseSpeed;
        _lift.wanderRange = rolledWanderRange;

        _noiseSeed = Random.Range(0f, 1000f);
        _nextEventRollAt = Time.time + RandomIn(eventIntervalRange);
        _rolled = true;
    }

    void RerollSpawnPoint()
    {
        var area = FindFirstObjectByType<TornadoSpawnArea>();
        var players = GameObject.FindGameObjectsWithTag(playerTag);
        if (area == null || players.Length == 0) return;

        Vector3 best = transform.position;
        float bestDist = NearestPlayerDistance(best, players);

        for (int i = 0; i < spawnRerollAttempts && bestDist < minDistanceFromPlayers; i++)
        {
            Vector3 candidate = area.GetRandomPoint();
            float d = NearestPlayerDistance(candidate, players);
            if (d > bestDist) { best = candidate; bestDist = d; }
        }

        transform.position = best;
    }

    static float NearestPlayerDistance(Vector3 point, GameObject[] players)
    {
        float nearest = float.MaxValue;
        foreach (var p in players)
        {
            if (p == null) continue;
            Vector3 delta = p.transform.position - point;
            delta.y = 0f;
            nearest = Mathf.Min(nearest, delta.magnitude);
        }
        return nearest;
    }

    static float RandomIn(Vector2 range) => Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));

    void OnValidate()
    {
        minDistanceFromPlayers = Mathf.Max(0f, minDistanceFromPlayers);
        driftFrequency = Mathf.Max(0f, driftFrequency);
        eventBlendTime = Mathf.Max(0f, eventBlendTime);
    }
}
