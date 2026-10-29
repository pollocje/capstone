using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// Handheld-GPS map. Lives on the scene's MapCamera (which renders RT_Map, shown by the player's
/// HUD Canvas/MapOverlay/MapImage when Tab is pressed). Every client drives its own copy: the
/// camera hovers over the local player and turns with their heading, so "forward" is always up.
/// Icons (you, teammates, the detected tornado) are UI images on top of MapImage, see LiveMapIcons.
/// Nothing here is networked; it only reads positions that are already synced or local.
/// </summary>
[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(1000)] // after FirstPersonController has turned the player this frame
public class LiveMap : MonoBehaviour
{
    public static LiveMap Instance { get; private set; }

    [Header("View")]
    [Tooltip("Metres from the player to the edge of the map (half its width). 40 = an 80 m square around you.")]
    [Range(10f, 250f)] public float viewRadius = 40f;
    [Tooltip("How far above the player the camera hovers (m). Keep it above roofs and treetops.")]
    [SerializeField] float heightAbovePlayer = 80f;
    [Tooltip("Only render the map while it is on screen. Saves a full scene render every frame.")]
    [SerializeField] bool renderOnlyWhileOpen = true;

    [Header("Minimap")]
    [Tooltip("While the map is the selected hotbar item, show it as a small corner minimap; Tab still opens the full-screen map. Untick for the old behaviour (map only on Tab).")]
    public bool cornerMinimapWhenEquipped = true;
    [Tooltip("Minimap size as a fraction of the screen height.")]
    [Range(0.15f, 0.5f)] public float minimapSize = 0.3f;
    [Tooltip("Gap between the minimap and the top-right screen corner (canvas pixels).")]
    public float minimapMargin = 20f;

    [Header("Compass (after the tornado is detected)")]
    [Tooltip("How far from the tornado the compass sends you (m), behind it and off to the side of its path.")]
    public float approachStandoff = 40f;
    [Tooltip("Within this distance, being ahead of the tornado (inside a 45 degree cone of its heading) counts as 'in its path'.")]
    public float inPathRange = 400f;
    [Tooltip("Seconds of smoothing on the tornado's heading (it wanders, so its direction changes often).")]
    public float headingSmoothing = 2f;

    [Header("Icons")]
    public Color playerColor = Color.white;
    public Color teammateColor = new Color(0.25f, 0.65f, 1f);
    public Color tornadoColor = new Color(0.95f, 0.1f, 0.1f);
    [Tooltip("Icon sizes as a fraction of the map width.")]
    public float playerIconSize = 0.07f;
    public float dotSize = 0.045f;
    [Tooltip("How far in from the map edge an off-map tornado is pinned (fraction of the map width).")]
    public float edgeInset = 0.08f;

    [Header("Debug (Editor and Development Builds only)")]
    [Tooltip("Spawns the tornado through TornadoSpawner (same path as finishing research, cutscene included).")]
    [SerializeField] KeyCode debugSpawnTornadoKey = KeyCode.F9;
    [Tooltip("Toggles 'Treat Existing Tornadoes As Detected' - instant fake detection, no cutscene.")]
    [SerializeField] KeyCode debugFakeDetectedKey = KeyCode.F10;
    [Tooltip("Show tornadoes that were already in the scene as detected, without spawning one.")]
    public bool treatExistingTornadoesAsDetected;

    public MapItem LocalPlayer { get; private set; }
    public IReadOnlyList<MapItem> Teammates => _teammates;
    public bool TornadoDetected => _detected || treatExistingTornadoesAsDetected;
    public Transform Tornado => _tornado != null ? _tornado.transform : null;
    public float Heading { get; private set; }
    public Vector3 Center { get; private set; }
    /// <summary>Smoothed tornado velocity on the ground plane (zero until it moves).</summary>
    public Vector3 TornadoVelocity { get; private set; }
    /// <summary>What the map shows this frame: nothing, the corner minimap, or the full-screen view.</summary>
    public enum Mode { Hidden, Minimap, Full }
    public Mode CurrentMode { get; private set; }

    Camera _cam;
    readonly List<MapItem> _teammates = new List<MapItem>();
    readonly HashSet<TornadoLift> _preExisting = new HashSet<TornadoLift>();
    TornadoSpawner _spawner;
    TornadoLift _tornado;
    bool _detected;
    Vector3 _lastTornadoPos;
    bool _hasLastTornadoPos;
    float _nextScan;
    LiveMapIcons _icons;

    void Awake()
    {
        Instance = this;
        _cam = GetComponent<Camera>();
        _cam.orthographic = true;
    }

    void Start()
    {
        // Tornadoes already placed in the scene are never "the" detected tornado.
        foreach (var t in FindObjectsByType<TornadoLift>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            _preExisting.Add(t);

        _spawner = FindFirstObjectByType<TornadoSpawner>();
        if (_spawner != null) _spawner.onTornadoSpawned.AddListener(OnTornadoSpawned);
        else Debug.LogWarning("LiveMap: no TornadoSpawner in the scene, so the tornado never shows on the map.", this);
    }

    void OnDestroy()
    {
        if (_spawner != null) _spawner.onTornadoSpawned.RemoveListener(OnTornadoSpawned);
        if (Instance == this) Instance = null;
    }

    void OnTornadoSpawned()
    {
        _detected = true;
        _tornado = null; // picked up by the next scan
        _nextScan = 0f;
    }

    void LateUpdate()
    {
        if (Time.time >= _nextScan) Scan();

        if (LocalPlayer == null)
        {
            if (renderOnlyWhileOpen) _cam.enabled = false;
            return;
        }

        // Test keys only work in the Editor and Development Builds, never in a release build,
        // so players can't spawn or reveal the tornado themselves.
        if (Debug.isDebugBuild)
        {
            if (Input.GetKeyDown(debugSpawnTornadoKey)) DebugSpawnTornado();
            if (Input.GetKeyDown(debugFakeDetectedKey)) DebugToggleExisting();
        }
        TrackTornadoVelocity();

        // Follow and turn with the local player: camera up = the way they face.
        var p = LocalPlayer.transform;
        Heading = p.eulerAngles.y;
        Center = p.position;
        transform.SetPositionAndRotation(Center + Vector3.up * heightAbovePlayer, Quaternion.Euler(90f, Heading, 0f));
        _cam.orthographicSize = viewRadius;
        _cam.nearClipPlane = 0.3f;
        _cam.farClipPlane = heightAbovePlayer + 200f;

        CurrentMode = LocalPlayer.IsUsing ? Mode.Full
            : cornerMinimapWhenEquipped && LocalPlayer.IsEquipped ? Mode.Minimap : Mode.Hidden;
        if (renderOnlyWhileOpen) _cam.enabled = CurrentMode != Mode.Hidden;

        if (_icons == null) _icons = LiveMapIcons.Attach(LocalPlayer, this);
        if (_icons != null) _icons.Refresh();
    }

    // Players join and leave and the tornado can appear at any time, so re-check twice a second.
    void Scan()
    {
        _nextScan = Time.time + 0.5f;

        var maps = FindObjectsByType<MapItem>(FindObjectsSortMode.None);
        var local = maps.FirstOrDefault(IsLocal);
        if (local != LocalPlayer)
        {
            LocalPlayer = local;
            if (_icons != null) Destroy(_icons.gameObject);
            _icons = null;
        }
        _teammates.Clear();
        _teammates.AddRange(maps.Where(m => m != LocalPlayer));

        if (TornadoDetected && _tornado == null)
        {
            var candidates = FindObjectsByType<TornadoLift>(FindObjectsSortMode.None)
                .Where(t => treatExistingTornadoesAsDetected || !_preExisting.Contains(t));
            _tornado = LocalPlayer == null ? candidates.FirstOrDefault()
                : candidates.OrderBy(t => (t.transform.position - LocalPlayer.transform.position).sqrMagnitude).FirstOrDefault();
        }
        else if (!TornadoDetected) _tornado = null;
    }

    // Online: the player this client owns. Offline there is only the one spawned player.
    static bool IsLocal(MapItem map)
    {
        if (!PlayerSpawnManager.IsNetworked) return true;
        var setup = map.GetComponentInParent<PlayerNetworkSetup>();
        return setup != null && setup.IsSpawned && setup.IsOwner;
    }

    /// <summary>World position -> map position in [-1, 1] (x right, y up = player's forward).</summary>
    public Vector2 WorldToMap(Vector3 world)
    {
        var d = Quaternion.Euler(0f, -Heading, 0f) * (world - Center);
        return new Vector2(d.x, d.z) / Mathf.Max(0.01f, viewRadius);
    }

    /// <summary>World direction -> map direction (x right, y up = player's forward), not normalised.</summary>
    public Vector2 WorldDirToMap(Vector3 dir)
    {
        var d = Quaternion.Euler(0f, -Heading, 0f) * dir;
        return new Vector2(d.x, d.z);
    }

    void TrackTornadoVelocity()
    {
        var t = TornadoDetected ? Tornado : null;
        if (t == null || Time.deltaTime <= 0f) { _hasLastTornadoPos = false; TornadoVelocity = Vector3.zero; return; }
        var p = t.position;
        if (_hasLastTornadoPos)
        {
            var v = (p - _lastTornadoPos) / Time.deltaTime;
            v.y = 0f;
            float k = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.05f, headingSmoothing));
            TornadoVelocity = Vector3.Lerp(TornadoVelocity, v, k);
        }
        _lastTornadoPos = p;
        _hasLastTornadoPos = true;
    }

    // ── Debug ────────────────────────────────────────────────────────────────

    [ContextMenu("Debug: Spawn Tornado Now")]
    public void DebugSpawnTornado()
    {
        if (_spawner != null) _spawner.SpawnTornado();
        else Debug.LogWarning("LiveMap: no TornadoSpawner to spawn from.", this);
    }

    [ContextMenu("Debug: Toggle Existing Tornadoes As Detected")]
    public void DebugToggleExisting()
    {
        treatExistingTornadoesAsDetected = !treatExistingTornadoesAsDetected;
        Debug.Log($"LiveMap: fake detection {(treatExistingTornadoesAsDetected ? "ON" : "OFF")}", this);
        _tornado = null;
        _nextScan = 0f;
    }

    public string Describe()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"LiveMap on '{name}': networked {PlayerSpawnManager.IsNetworked}, viewRadius {viewRadius} m, mode {CurrentMode}, camera {(_cam.enabled ? "rendering" : "idle")}");
        sb.AppendLine(LocalPlayer == null ? "  local player: none found"
            : $"  local player: {LocalPlayer.transform.parent?.name}/{LocalPlayer.name} at {Center:F1}, heading {Heading:F0}, map open {LocalPlayer.IsUsing}");
        foreach (var t in _teammates)
            sb.AppendLine($"  teammate {t.transform.parent?.name}: at {t.transform.position:F1}, map pos {WorldToMap(t.transform.position):F2}");
        sb.AppendLine($"  tornado detected {TornadoDetected} (event {_detected}, debug {treatExistingTornadoesAsDetected}), tracking {(Tornado ? $"{Tornado.name} at {Tornado.position:F1}, map pos {WorldToMap(Tornado.position):F2}" : "nothing")}");
        sb.AppendLine($"  scene tornadoes ignored until detection: {_preExisting.Count(t => t != null)}; tornado velocity {TornadoVelocity:F2}");
        if (_icons != null) sb.Append(_icons.Describe());
        return sb.ToString();
    }
}
