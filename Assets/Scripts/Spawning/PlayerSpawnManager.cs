using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using Unity.Netcode;

public class PlayerSpawnManager : MonoBehaviour
{
    public enum SpawnStrategy { Random, RoundRobin, First }

    [Header("Setup")]
    public GameObject playerPrefab;
    public SpawnStrategy strategy = SpawnStrategy.Random;

    [Header("Spawn Points (auto-found if empty)")]
    public List<SpawnPoint> spawnPoints = new();

    private string _lockedGroup = null;
    private readonly HashSet<SpawnPoint> _usedPoints = new();
    private readonly HashSet<ulong> _spawnedClients = new();
    private int _roundRobinIndex = 0;

    /// <summary>True when we're in an online session (host or client).</summary>
    public static bool IsNetworked =>
        NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    void Awake()
    {
        if (spawnPoints.Count == 0)
            spawnPoints = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None).ToList();
    }

    // ---------------------------------------------------------------------
    // Multiplayer: the HOST spawns a networked player for every client once
    // they've all finished loading this scene. Clients do nothing here —
    // their player arrives over the network. - sebastion
    // ---------------------------------------------------------------------

    void OnEnable()
    {
        if (!IsNetworked || !NetworkManager.Singleton.IsServer) return;

        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoadCompleted;
        NetworkManager.Singleton.SceneManager.OnSynchronizeComplete += OnLateJoinerSynced;
    }

    void OnDisable()
    {
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.SceneManager == null) return;

        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoadCompleted;
        NetworkManager.Singleton.SceneManager.OnSynchronizeComplete -= OnLateJoinerSynced;
    }

    // Fires on the host after everyone (host + clients) has loaded the scene.
    void OnSceneLoadCompleted(string sceneName, LoadSceneMode mode,
                              List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (sceneName != gameObject.scene.name) return;

        foreach (ulong clientId in clientsCompleted)
            SpawnNetworkedPlayer(clientId);

        if (clientsTimedOut.Count > 0)
            Debug.LogWarning($"PlayerSpawnManager: {clientsTimedOut.Count} client(s) timed out loading {sceneName}.");
    }

    // Fires on the host when someone joins after the game already started.
    void OnLateJoinerSynced(ulong clientId)
    {
        SpawnNetworkedPlayer(clientId);
    }

    // ---------------------------------------------------------------------

    // Singleplayer: spawns locally and wires up the camera.
    // Do NOT call this in an online session — other players won't see this player. -  Sebastion
    public GameObject SpawnPlayer()
    {
        if (IsNetworked)
        {
            Debug.LogWarning("PlayerSpawnManager: SpawnPlayer() called during an online session — ignored. " +
                             "Networked players are spawned automatically by the host.");
            return null;
        }

        if (playerPrefab == null)
        {
            Debug.LogError("PlayerSpawnManager: No player prefab assigned.");
            return null;
        }

        SpawnPoint point = GetSpawnPoint();
        if (point == null)
        {
            Debug.LogWarning($"PlayerSpawnManager: No available spawn point in group '{_lockedGroup}'.");
            return null;
        }

        _usedPoints.Add(point);
        var player = Instantiate(playerPrefab, point.transform.position, point.transform.rotation);
        HookUpCamera(player);
        HookUpHotbar(player);
        return player;
    }

    // Multiplayer: spawns as a networked player object owned by clientId. Host only.
    // Camera hookup is handled by PlayerNetworkSetup on the player prefab.
    public void SpawnNetworkedPlayer(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer) return;
        if (!_spawnedClients.Add(clientId)) return; // already spawned for this client

        if (playerPrefab == null)
        {
            Debug.LogError("PlayerSpawnManager: No player prefab assigned.");
            return;
        }

        SpawnPoint point = GetSpawnPoint();
        if (point == null)
        {
            Debug.LogWarning($"PlayerSpawnManager: No available spawn point in group '{_lockedGroup}'.");
            return;
        }

        _usedPoints.Add(point);
        var player = Instantiate(playerPrefab, point.transform.position, point.transform.rotation);

        var networkObject = player.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            Debug.LogError("PlayerSpawnManager: Player prefab is missing a NetworkObject component.");
            Destroy(player);
            return;
        }

        networkObject.SpawnAsPlayerObject(clientId, destroyWithScene: true);
        Debug.Log($"PlayerSpawnManager: Spawned networked player for client {clientId}.");
    }

    // Call between rounds/sessions to allow a new group to be selected.
    public void ResetGroupSelection()
    {
        _lockedGroup = null;
        _usedPoints.Clear();
        _spawnedClients.Clear();
        _roundRobinIndex = 0;
    }

    void HookUpCamera(GameObject player)
    {
        // The VCam's Follow is pre-wired to PlayerCameraRoot in the prefab.
        // This is a safety net in case that wiring is missing.
        // LookAt intentionally left alone — camera orientation is driven by
        // FirstPersonController rotating PlayerCameraRoot, not Cinemachine aim.
        var vcam = player.GetComponentInChildren<CinemachineVirtualCamera>();
        if (vcam == null) return;

        if (vcam.Follow != null) return; // already wired by prefab

        var target = player.GetComponentsInChildren<Transform>()
            .FirstOrDefault(t => t.CompareTag("CinemachineTarget"));

        if (target != null)
            vcam.Follow = target;
    }

    void HookUpHotbar(GameObject player)
    {
        // hotbarUI lives on a scene Canvas, so it can't be wired inside the player
        // prefab itself — connect it here now that the player actually exists in the scene.
        var hotbar = player.GetComponentInChildren<Hotbar>(true);
        if (hotbar == null || hotbar.hotbarUI != null) return;

        hotbar.hotbarUI = FindFirstObjectByType<HotbarUI>();
        hotbar.RefreshUI();
    }

    SpawnPoint GetSpawnPoint()
    {
        // Lock in a group on the first spawn of a session
        if (_lockedGroup == null)
        {
            var availableGroups = spawnPoints
                .Where(p => p != null && !p.disabled)
                .Select(p => p.spawnTag)
                .Distinct()
                .ToList();

            if (availableGroups.Count == 0) return null;

            _lockedGroup = availableGroups[Random.Range(0, availableGroups.Count)];
        }

        // Prefer points not yet used this session
        var candidates = spawnPoints
            .Where(p => p != null && !p.disabled && p.spawnTag == _lockedGroup && !_usedPoints.Contains(p))
            .ToList();

        // Fall back to allowing reuse if the group is exhausted
        if (candidates.Count == 0)
        {
            Debug.LogWarning($"PlayerSpawnManager: All points in group '{_lockedGroup}' used. Allowing reuse.");
            candidates = spawnPoints
                .Where(p => p != null && !p.disabled && p.spawnTag == _lockedGroup)
                .ToList();
        }

        if (candidates.Count == 0) return null;

        return strategy switch
        {
            SpawnStrategy.Random => candidates[Random.Range(0, candidates.Count)],
            SpawnStrategy.First => candidates[0],
            SpawnStrategy.RoundRobin => RoundRobin(candidates),
            _ => candidates[0],
        };
    }

    SpawnPoint RoundRobin(List<SpawnPoint> candidates)
    {
        _roundRobinIndex = _roundRobinIndex % candidates.Count;
        var point = candidates[_roundRobinIndex];
        _roundRobinIndex++;
        return point;
    }
}
