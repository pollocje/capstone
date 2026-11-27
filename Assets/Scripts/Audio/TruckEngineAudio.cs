using LuigiGameDev.CarController.Logic;
using UnityEngine;

/// <summary>
/// Engine sound for the truck: start-up when a driver gets in, then an idle loop that
/// crossfades into a rev loop as the (simulated) RPM rises. Throttle makes the rev
/// sound brighter/rougher (low-pass filter opens up), not just louder.
///
/// No networking needed: it reads the synced driver seat (VehicleEnterExit.HasDriver)
/// and works out speed + throttle from how the truck moves, so every player hears the
/// same thing.
///
/// Put this on the truck (same object as VehicleEnterExit is fine).
/// </summary>
public class TruckEngineAudio : MonoBehaviour
{
    [Header("References")]
    [SerializeField] VehicleEnterExit vehicle;
    // Optional: used for max speed and rolling resistance. Auto-found if left empty.
    [SerializeField] CarSettings carSettings;
    // The part of the truck that actually moves (has the Rigidbody). Auto-found if left empty.
    [SerializeField] Transform body;

    [Header("Clips")]
    [SerializeField] AudioClip startClip;
    [SerializeField] AudioClip idleClip;
    [SerializeField] AudioClip revClip;

    [Header("Volume")]
    [Range(0f, 1f)][SerializeField] float masterVolume = 0.8f;
    [Range(0f, 1f)][SerializeField] float startVolume = 0.9f;
    // Skips this many seconds at the beginning of the start-up clip (trims it without editing the file).
    [Min(0f)][SerializeField] float startOffset = 0f;
    // Distance (m) at which the engine becomes silent for other players.
    [SerializeField] float maxDistance = 60f;

    [Header("Pitch (1 = original clip)")]
    [SerializeField] float idlePitchMin = 1.0f;
    [SerializeField] float idlePitchMax = 1.5f;
    [SerializeField] float revPitchMin = 0.8f;
    [SerializeField] float revPitchMax = 1.8f;

    [Header("Gears / RPM")]
    // Number of simulated gears across the speed range. More gears = more "shifts".
    [SerializeField] int gearCount = 4;
    // RPM (0-1) right after shifting up — how far the revs drop on each shift.
    [Range(0f, 1f)][SerializeField] float rpmAfterShift = 0.45f;
    // Used if no CarSettings is found (m/s).
    [SerializeField] float fallbackMaxSpeed = 25f;

    [Header("Throttle feel")]
    // Acceleration (m/s²) that counts as full throttle.
    [SerializeField] float fullThrottleAccel = 8f;
    // Low-pass cutoff off-throttle (muffled) and on-throttle (open, aggressive).
    [SerializeField] float cutoffOffThrottle = 1800f;
    [SerializeField] float cutoffOnThrottle = 22000f;

    AudioSource _startSource, _idleSource, _revSource;
    AudioLowPassFilter _revFilter;

    [Header("Idle while driving")]
    // How much the idle quietens when the rev sound takes over (0 = idle stays full, 1 = idle fades out completely).
    [Range(0f, 1f)][SerializeField] float idleDuckWhenRevving = 0.6f;

    bool _engineOn;
    float _engineLevel;      // 0 = off, 1 = running (fades out when the driver leaves)
    float _localStartTimer;  // > 0 while we've started locally and are waiting for the seat to sync
    Vector3 _lastPos;
    float _speed, _prevSpeed, _throttle, _rpm;

    void Awake()
    {
        if (vehicle == null) vehicle = GetComponentInParent<VehicleEnterExit>();
        if (carSettings == null) carSettings = GetComponentInChildren<CarSettings>();
        if (body == null)
        {
            var rb = GetComponentInChildren<Rigidbody>();
            body = rb != null ? rb.transform : transform;
        }

        _startSource = CreateSource("Engine_Start", startClip, false);
        _idleSource = CreateSource("Engine_Idle", idleClip, true);
        _revSource = CreateSource("Engine_Rev", revClip, true);
        _revFilter = _revSource.gameObject.AddComponent<AudioLowPassFilter>();

        // Load the clips now, so the first play doesn't wait for them to load.
        Preload(startClip);
        Preload(idleClip);
        Preload(revClip);

        _lastPos = body.position;
    }

    static void Preload(AudioClip clip)
    {
        if (clip != null && clip.loadState != AudioDataLoadState.Loaded)
            clip.LoadAudioData();
    }

    AudioSource CreateSource(string name, AudioClip clip, bool loop)
    {
        var go = new GameObject(name);
        go.transform.SetParent(body != null ? body : transform, false);

        var source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = loop;
        source.playOnAwake = false;
        source.volume = 0f;
        source.spatialBlend = 1f;                     // 3D: heard from the truck
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 3f;
        source.maxDistance = maxDistance;
        source.dopplerLevel = 0f;                     // avoids warbly pitch when passing by
        return source;
    }

    void Update()
    {
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);

        HandleEngineOnOff(dt);
        MeasureMovement(dt);
        UpdateRpm();
        ApplyAudio(dt);
    }

    // ── Engine on/off (follows the synced driver seat) ───────────────────────

    /// <summary>
    /// Called by VehicleEnterExit on the driver's own machine the moment they get in,
    /// so they hear the start-up instantly instead of after the seat syncs.
    /// Returns how many seconds until the engine is running (the start-up sound's length, or 0).
    /// </summary>
    public float StartEngineLocally(bool playStartSound)
    {
        if (_engineOn) return 0f;
        _localStartTimer = 3f; // give the seat up to 3s to sync before giving up
        TurnOn(playStartSound);

        return playStartSound && startClip != null ? startClip.length - StartOffsetClamped() : 0f;
    }

    // Never skip past the end of the clip.
    float StartOffsetClamped() =>
        startClip != null ? Mathf.Clamp(startOffset, 0f, Mathf.Max(0f, startClip.length - 0.05f)) : 0f;

    void TurnOn(bool playStartSound)
    {
        _engineOn = true;
        _engineLevel = 1f;

        // Play the start-up now, and schedule the idle (and rev layer) to begin the
        // instant the start-up ends — no gap, no overlap.
        double loopStart = AudioSettings.dspTime;
        if (playStartSound && startClip != null)
        {
            if (startClip.loadState != AudioDataLoadState.Loaded)
                Debug.LogWarning($"[TruckEngineAudio] Start clip wasn't loaded yet ({startClip.loadState}) — this causes a delay. " +
                                 "Tick 'Preload Audio Data' and set Load Type to 'Decompress On Load' on the clip.");

            float offset = StartOffsetClamped();
            _startSource.volume = startVolume * masterVolume;
            _startSource.pitch = 1f;
            _startSource.time = offset;            // skip the beginning of the clip
            _startSource.Play();
            loopStart += startClip.length - offset;
        }

        _idleSource.Stop();
        _revSource.Stop();
        _idleSource.PlayScheduled(loopStart);
        _revSource.PlayScheduled(loopStart);
    }

    void HandleEngineOnOff(float dt)
    {
        bool hasDriver = vehicle != null && vehicle.HasDriver;

        // Started locally: stay on while waiting for the driver seat to sync.
        // If it never does (e.g. someone else got the seat first), shut off.
        if (_localStartTimer > 0f)
        {
            _localStartTimer = hasDriver ? 0f : _localStartTimer - dt;
            if (_localStartTimer > 0f) hasDriver = true;
        }

        if (hasDriver && !_engineOn)
        {
            // Another player got in the driver seat (we just saw the seat sync).
            // The engine was off (no driver), so it always starts up — even if passengers were already in.
            TurnOn(playStartSound: true);
        }
        else if (!hasDriver && _engineOn)
        {
            // Driver got out: cut the start-up (if still going) and fade the engine off.
            _engineOn = false;
            _localStartTimer = 0f;
            _startSource.Stop();
        }

        if (!_engineOn)
            _engineLevel = Mathf.MoveTowards(_engineLevel, 0f, dt * 1.5f);

        // Fully faded out: stop the loops so they cost nothing.
        if (!_engineOn && _engineLevel <= 0f)
        {
            if (_idleSource.isPlaying) _idleSource.Stop();
            if (_revSource.isPlaying) _revSource.Stop();
        }
    }

    // ── Speed + throttle, worked out from movement (same on every machine) ───

    void MeasureMovement(float dt)
    {
        Vector3 delta = body.position - _lastPos;
        _lastPos = body.position;

        float rawSpeed = delta.magnitude / dt;
        if (rawSpeed > 100f) rawSpeed = _speed; // ignore teleports/respawns

        _prevSpeed = _speed;
        _speed = Mathf.Lerp(_speed, rawSpeed, dt * 6f);

        // The truck slows down on its own (rolling resistance), so holding speed — or
        // speeding up — means the gas is pressed. Coasting/braking reads as ~0 throttle.
        float accel = (_speed - _prevSpeed) / dt;
        float rolling = carSettings != null ? carSettings.RollingResistanceStrength : 3f;

        float throttleTarget = _speed > 0.5f
            ? Mathf.Clamp01((accel + rolling) / fullThrottleAccel)
            : 0f;

        _throttle = Mathf.Lerp(_throttle, throttleTarget, dt * 5f);
    }

    // ── Simulated RPM with simple gears ──────────────────────────────────────

    void UpdateRpm()
    {
        float maxSpeed = carSettings != null ? carSettings.MaxSpeed : fallbackMaxSpeed;
        float speedNorm = Mathf.Clamp01(_speed / Mathf.Max(maxSpeed, 0.1f));

        // Split the speed range into gears. RPM climbs through each gear, then drops on the shift.
        float g = speedNorm * gearCount;
        int gear = Mathf.Min(Mathf.FloorToInt(g), gearCount - 1);
        float withinGear = g - gear;

        float targetRpm = gear == 0
            ? withinGear                                    // 1st gear: from idle upward
            : Mathf.Lerp(rpmAfterShift, 1f, withinGear);

        // Pressing the gas pushes the revs up a little extra.
        targetRpm = Mathf.Clamp01(targetRpm + _throttle * 0.1f);

        _rpm = Mathf.Lerp(_rpm, targetRpm, Time.deltaTime * 8f);
    }

    // ── Apply to the audio sources ───────────────────────────────────────────

    void ApplyAudio(float dt)
    {
        // How much of the rev loop vs the idle loop we hear.
        float revMix = Mathf.Max(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 0.45f, _rpm)), _throttle * 0.5f);

        _idleSource.pitch = Mathf.Lerp(idlePitchMin, idlePitchMax, _rpm);
        _revSource.pitch = Mathf.Lerp(revPitchMin, revPitchMax, _rpm);

        // Idle keeps playing underneath while driving; it just gets a bit quieter as the rev sound comes in.
        _idleSource.volume = (1f - revMix * idleDuckWhenRevving) * _engineLevel * masterVolume;
        // On throttle the rev loop is fuller; off throttle it drops back.
        _revSource.volume = revMix * Mathf.Lerp(0.6f, 1f, _throttle) * _engineLevel * masterVolume;

        // The key "not just volume" part: on throttle the sound opens up (bright, growly),
        // off throttle it's muffled.
        _revFilter.cutoffFrequency = Mathf.Lerp(cutoffOffThrottle, cutoffOnThrottle, _throttle);
    }
}