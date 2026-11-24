using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Round end (win/lose) and the end screen. Creates itself at startup and only acts in the
/// gameplay scene, so no scene or prefab needs it.
///
/// The host decides: every player's machine reports what it saw (research %, tornado detected,
/// tornado killed, items used) and the host combines it, runs the truck-flip countdown and ends
/// the round. Countdown and result go out to everyone with NGO named messages, so all players see
/// the same thing. Offline (single player) the same code runs locally.
///
/// Win: a detected (spawned) tornado is killed. Lose: the truck stays flipped until the countdown ends.
/// </summary>
public class RoundManager : MonoBehaviour
{
    public static RoundManager Instance { get; private set; }

    const string MsgReport = "Round.Report";   // client -> host
    const string MsgState = "Round.State";     // host -> all: countdown
    const string MsgEnd = "Round.End";         // host -> all: result
    enum Report : byte { Research, Detected, Killed, ItemUsed }

    public RoundSettings Settings { get; private set; }
    public bool InGameplay { get; private set; }
    public bool Ended => Result != null;
    public RoundResult Result { get; private set; }
    public bool CountdownActive { get; private set; }
    public float CountdownRemaining { get; private set; }
    public static bool IsNetworked => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    public static bool IsAuthority => !IsNetworked || NetworkManager.Singleton.IsServer;
    public static ulong LocalId => IsNetworked ? NetworkManager.Singleton.LocalClientId : 0;

    // Host state
    float _startTime;
    float _research;
    bool _detected, _killed;
    readonly Dictionary<ulong, RoundResult.PlayerItems> _items = new Dictionary<ulong, RoundResult.PlayerItems>();
    Transform _truck;
    Vector3 _lastTruckPos; Quaternion _lastTruckRot; bool _hasLastTruck;
    float _truckSpeed, _truckTurnRate, _flippedFor, _uprightFor, _nextStateSend;
    bool _debugFlip;

    // Local (every machine) tracking
    float _reportedResearch, _nextResearchPoll;
    bool _reportedDetected, _reportedKilled;
    readonly HashSet<TornadoLift> _sceneTornadoes = new HashSet<TornadoLift>();
    TornadoSpawner _spawner;
    NetworkManager _registeredOn;
    RoundUI _ui;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Instance != null) return;
        var go = new GameObject(nameof(RoundManager));
        DontDestroyOnLoad(go);
        go.AddComponent<RoundManager>();
    }

    void Awake()
    {
        Instance = this;
        Settings = RoundSettings.Load();
        SceneManager.sceneLoaded += OnSceneLoaded;
        Hotbar.ItemUsed += OnItemUsed;
        TornadoLift.Killed += OnTornadoKilled;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Hotbar.ItemUsed -= OnItemUsed;
        TornadoLift.Killed -= OnTornadoKilled;
        Unregister();
        if (Instance == this) Instance = null;
    }

    // A fresh round every time the gameplay scene (re)loads - including "Start over".
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        InGameplay = scene.name == Settings.gameplayScene;
        Result = null;
        CountdownActive = false; CountdownRemaining = 0f;
        _startTime = Time.time;
        _research = 0f; _detected = _killed = false; _items.Clear();
        _truck = null; _hasLastTruck = false; _flippedFor = _uprightFor = 0f; _debugFlip = false;
        _reportedResearch = 0f; _reportedDetected = _reportedKilled = false;
        _sceneTornadoes.Clear();
        if (_spawner != null) _spawner.onTornadoSpawned.RemoveListener(OnTornadoSpawned);
        _spawner = null;
        if (!InGameplay) return;

        foreach (var t in FindObjectsByType<TornadoLift>(FindObjectsInactive.Include, FindObjectsSortMode.None)) _sceneTornadoes.Add(t);
        _spawner = FindFirstObjectByType<TornadoSpawner>();
        if (_spawner != null) _spawner.onTornadoSpawned.AddListener(OnTornadoSpawned);
        _ui = RoundUI.Create(this, scene);
    }

    void Update()
    {
        Register();
        if (!InGameplay) return;

        if (Time.time >= _nextResearchPoll) PollResearch();
        if (IsAuthority && !Ended)
        {
            HandleDebugKeys();
            UpdateFlip(Time.deltaTime);
            if (IsNetworked && Time.time >= _nextStateSend) { _nextStateSend = Time.time + 0.2f; SendState(); }
        }
        // Clients tick the countdown between the host's updates (5 per second) so it counts smoothly.
        else if (!IsAuthority && CountdownActive) CountdownRemaining = Mathf.Max(0f, CountdownRemaining - Time.deltaTime);
        if (_ui != null) _ui.Refresh();
    }

    // ── Local reports (every machine) ────────────────────────────────────────

    void PollResearch()
    {
        _nextResearchPoll = Time.time + 0.5f;
        var rm = ResearchManager.Instance;
        if (rm == null || rm.Progress <= _reportedResearch + 0.001f) return;
        _reportedResearch = rm.Progress;
        SendReport(Report.Research, _reportedResearch, null);
    }

    void OnTornadoSpawned()
    {
        if (_reportedDetected) return;
        _reportedDetected = true;
        SendReport(Report.Detected, 0f, null);
    }

    void OnTornadoKilled(TornadoLift t)
    {
        // The tornado placed in the scene doesn't count - only the one spawned when research finished.
        if (!InGameplay || _reportedKilled || _sceneTornadoes.Contains(t)) return;
        _reportedKilled = true;
        SendReport(Report.Killed, 0f, null);
    }

    void OnItemUsed(Hotbar hotbar, InventoryItem item)
    {
        if (!InGameplay || Ended || item == null || !IsLocalPlayer(hotbar)) return;
        SendReport(Report.ItemUsed, 0f, string.IsNullOrEmpty(item.itemName) || item.itemName == "Item" ? item.name : item.itemName);
    }

    static bool IsLocalPlayer(Component c)
    {
        if (!IsNetworked) return true;
        var setup = c.GetComponentInParent<PlayerNetworkSetup>();
        return setup != null && setup.IsSpawned && setup.IsOwner;
    }

    void SendReport(Report kind, float value, string text)
    {
        if (IsAuthority) { ApplyReport(LocalId, kind, value, text); return; }
        using var w = new FastBufferWriter(256, Allocator.Temp, 4096);
        w.WriteValueSafe((byte)kind); w.WriteValueSafe(value); w.WriteValueSafe(text ?? "");
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(MsgReport, NetworkManager.ServerClientId, w, NetworkDelivery.ReliableSequenced);
    }

    // ── Host ─────────────────────────────────────────────────────────────────

    void ApplyReport(ulong from, Report kind, float value, string text)
    {
        if (Ended) return;
        switch (kind)
        {
            case Report.Research: _research = Mathf.Max(_research, Mathf.Clamp01(value)); break;
            case Report.Detected: _detected = true; break;
            case Report.Killed:
                _killed = true; _detected = true;
                EndRound(true, "The tornado was destroyed.");
                break;
            case Report.ItemUsed:
                if (!_items.TryGetValue(from, out var p)) _items[from] = p = new RoundResult.PlayerItems { clientId = from };
                p.uses[text] = p.uses.TryGetValue(text, out int n) ? n + 1 : 1;
                break;
        }
    }

    void UpdateFlip(float dt)
    {
        if (_truck == null)
        {
            var vee = FindFirstObjectByType<VehicleEnterExit>();
            _truck = vee != null ? vee.transform : null;
            _hasLastTruck = false;
            if (_truck == null && !_debugFlip) return;
        }

        float tilt = 0f;
        if (_truck != null && dt > 0f)
        {
            // Speed from the position change: on the host a client-driven truck is kinematic, so its
            // Rigidbody velocity reads zero.
            var pos = _truck.position; var rot = _truck.rotation;
            if (_hasLastTruck)
            {
                float k = 1f - Mathf.Exp(-dt / 0.3f);
                _truckSpeed = Mathf.Lerp(_truckSpeed, (pos - _lastTruckPos).magnitude / dt, k);
                _truckTurnRate = Mathf.Lerp(_truckTurnRate, Quaternion.Angle(rot, _lastTruckRot) / dt, k);
            }
            _lastTruckPos = pos; _lastTruckRot = rot; _hasLastTruck = true;
            tilt = Vector3.Angle(_truck.up, Vector3.up);
        }

        bool still = _truckSpeed < Settings.maxStillSpeed && _truckTurnRate < Settings.maxStillTurnRate;
        bool flipped = _debugFlip || (tilt > Settings.flippedAngle && still);
        bool upright = !_debugFlip && tilt < Settings.uprightAngle;

        if (!CountdownActive)
        {
            _flippedFor = flipped ? _flippedFor + dt : 0f;
            if (_flippedFor >= Settings.flipConfirmSeconds)
            {
                CountdownActive = true;
                CountdownRemaining = Settings.flipCountdownSeconds;
                _uprightFor = 0f;
                Debug.Log($"RoundManager: truck flipped (tilt {tilt:F0} deg) - countdown {Settings.flipCountdownSeconds:F0} s.");
                SendState();
            }
            return;
        }

        _uprightFor = upright ? _uprightFor + dt : 0f;
        if (_uprightFor >= Settings.uprightConfirmSeconds)
        {
            CountdownActive = false; _flippedFor = 0f;
            Debug.Log("RoundManager: truck back on its wheels - countdown cancelled.");
            SendState();
            return;
        }
        CountdownRemaining = Mathf.Max(0f, CountdownRemaining - dt);
        if (CountdownRemaining <= 0f && !upright)
            EndRound(false, "The truck stayed flipped.");
    }

    void EndRound(bool won, string reason)
    {
        if (Ended) return;
        CountdownActive = false;
        var r = new RoundResult
        {
            won = won, reason = reason, elapsedSeconds = Time.time - _startTime,
            research = _research, tornadoDetected = _detected, tornadoKilled = _killed,
        };
        // Every connected player gets a row, even with no item uses.
        var ids = IsNetworked ? NetworkManager.Singleton.ConnectedClientsIds.ToList() : new List<ulong> { 0 };
        foreach (var id in _items.Keys) if (!ids.Contains(id)) ids.Add(id);
        foreach (var id in ids.OrderBy(i => i))
            r.players.Add(_items.TryGetValue(id, out var p) ? p : new RoundResult.PlayerItems { clientId = id });
        r.ComputeScore(Settings);
        Debug.Log($"RoundManager: round over - {(won ? "WON" : "LOST")} ({reason}) after {r.elapsedSeconds:F0} s, score {r.total}.");

        Result = r;
        if (IsNetworked)
        {
            using var w = new FastBufferWriter(1024, Allocator.Temp, 65536);
            r.Write(w);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll(MsgEnd, w, NetworkDelivery.ReliableSequenced);
        }
        OnEnded();
    }

    void SendState(ulong? only = null)
    {
        if (!IsNetworked) return;
        using var w = new FastBufferWriter(32, Allocator.Temp);
        w.WriteValueSafe(CountdownActive); w.WriteValueSafe(CountdownRemaining);
        var cmm = NetworkManager.Singleton.CustomMessagingManager;
        if (only.HasValue) cmm.SendNamedMessage(MsgState, only.Value, w, NetworkDelivery.ReliableSequenced);
        else cmm.SendNamedMessageToAll(MsgState, w, NetworkDelivery.UnreliableSequenced);
    }

    // Late joiners get the current countdown, or the result if the round is already over.
    void OnClientConnected(ulong id)
    {
        if (!NetworkManager.Singleton.IsServer || id == NetworkManager.ServerClientId || !InGameplay) return;
        SendState(id);
        if (Ended)
        {
            using var w = new FastBufferWriter(1024, Allocator.Temp, 65536);
            Result.Write(w);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(MsgEnd, id, w, NetworkDelivery.ReliableSequenced);
        }
    }

    void HandleDebugKeys()
    {
        if (!Debug.isDebugBuild) return;
        if (Input.GetKeyDown(Settings.debugWinKey)) DebugWin();
        if (Input.GetKeyDown(Settings.debugLoseKey)) DebugLose();
        if (Input.GetKeyDown(Settings.debugFlipKey)) DebugToggleFlip();
    }

    public void DebugWin() { if (IsAuthority) { _detected = true; _killed = true; EndRound(true, "Debug: forced win."); } }
    public void DebugLose() { if (IsAuthority) EndRound(false, "Debug: forced loss."); }
    public void DebugToggleFlip()
    {
        if (!IsAuthority) return;
        _debugFlip = !_debugFlip;
        Debug.Log($"RoundManager: debug flip {(_debugFlip ? "ON (countdown starts)" : "OFF (truck 'back upright')")}");
    }

    // ── End screen buttons ───────────────────────────────────────────────────

    /// <summary>Host (or single player) only: reload the gameplay scene for everyone.</summary>
    public void StartOver()
    {
        if (!IsAuthority) return;
        if (IsNetworked) NetworkManager.Singleton.SceneManager.LoadScene(Settings.gameplayScene, LoadSceneMode.Single);
        else SceneManager.LoadScene(Settings.gameplayScene);
    }

    /// <summary>Leave the session (like LobbyMenu.ExitLobby) and go to the main menu. A leaving host ends the session for everyone.</summary>
    public void GoToMainMenu()
    {
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        if (SessionManager.Instance != null) SessionManager.Instance.LeaveSession();
        else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) NetworkManager.Singleton.Shutdown();
        SceneManager.LoadScene(Settings.mainMenuScene);
    }

    void OnEnded()
    {
        LockLocalPlayer();
        if (_ui != null) _ui.ShowEnd(Result);
    }

    // Stop the local player from walking, looking, driving or using items behind the end screen.
    void LockLocalPlayer()
    {
        foreach (var fpc in FindObjectsByType<StarterAssets.FirstPersonController>(FindObjectsSortMode.None))
        {
            if (!IsLocalPlayer(fpc)) continue;
            fpc.enabled = false;
            var inputs = fpc.GetComponent<StarterAssets.StarterAssetsInputs>();
            if (inputs != null) { inputs.cursorLocked = false; inputs.cursorInputForLook = false; inputs.enabled = false; }
            var hotbar = fpc.GetComponent<Hotbar>();
            if (hotbar != null) hotbar.enabled = false;
        }
        foreach (var car in FindObjectsByType<PreconfiguredCarPhysics>(FindObjectsSortMode.None)) car.SetInputEnabled(false);
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }

    // ── Network plumbing ─────────────────────────────────────────────────────

    void Register()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening || nm.CustomMessagingManager == null) { if (nm != _registeredOn) Unregister(); return; }
        if (_registeredOn == nm) return;
        Unregister();
        _registeredOn = nm;
        var cmm = nm.CustomMessagingManager;
        if (nm.IsServer)
        {
            cmm.RegisterNamedMessageHandler(MsgReport, OnReportMessage);
            nm.OnClientConnectedCallback += OnClientConnected;
        }
        else
        {
            cmm.RegisterNamedMessageHandler(MsgState, OnStateMessage);
            cmm.RegisterNamedMessageHandler(MsgEnd, OnEndMessage);
        }
        nm.OnClientStopped += OnClientStopped;
    }

    void Unregister()
    {
        var nm = _registeredOn;
        _registeredOn = null;
        if (nm == null) return;
        nm.OnClientConnectedCallback -= OnClientConnected;
        nm.OnClientStopped -= OnClientStopped;
        var cmm = nm.CustomMessagingManager;
        if (cmm == null) return;
        cmm.UnregisterNamedMessageHandler(MsgReport);
        cmm.UnregisterNamedMessageHandler(MsgState);
        cmm.UnregisterNamedMessageHandler(MsgEnd);
    }

    void OnReportMessage(ulong sender, FastBufferReader r)
    {
        r.ReadValueSafe(out byte kind); r.ReadValueSafe(out float value); r.ReadValueSafe(out string text);
        if (InGameplay) ApplyReport(sender, (Report)kind, value, text);
    }

    void OnStateMessage(ulong sender, FastBufferReader r)
    {
        r.ReadValueSafe(out bool active); r.ReadValueSafe(out float remaining);
        if (Ended) return;
        CountdownActive = active; CountdownRemaining = remaining;
    }

    void OnEndMessage(ulong sender, FastBufferReader r)
    {
        var res = RoundResult.Read(r);
        if (!InGameplay || Ended) return;
        Result = res; CountdownActive = false;
        OnEnded();
    }

    // The host left (or the connection dropped) while we were playing: back to the main menu.
    void OnClientStopped(bool wasHost)
    {
        Unregister();
        if (!wasHost && InGameplay && SceneManager.GetActiveScene().name == Settings.gameplayScene)
        {
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            SceneManager.LoadScene(Settings.mainMenuScene);
        }
    }
}
