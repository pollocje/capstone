using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;

public class PlayerSpawnManager : MonoBehaviour
{
    public enum SpawnStrategy { Random, RoundRobin, First }

    [Header("Setup")]
    public GameObject playerPrefab;
    public SpawnStrategy strategy = SpawnStrategy.Random;

    [Header("Spawn Points (auto-found if empty)")]
    public List<SpawnPoint> spawnPoints = new();

    private int _roundRobinIndex = 0;

    void Awake()
    {
        if (spawnPoints.Count == 0)
            spawnPoints = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None).ToList();
    }

    public GameObject SpawnPlayer(string spawnTag = "Default")
    {
        if (playerPrefab == null)
        {
            Debug.LogError("PlayerSpawnManager: No player prefab assigned.");
            return null;
        }

        SpawnPoint point = GetSpawnPoint(spawnTag);
        if (point == null)
        {
            Debug.LogWarning($"PlayerSpawnManager: No available spawn point with tag '{spawnTag}'.");
            return null;
        }

        var player = Instantiate(playerPrefab, point.transform.position, point.transform.rotation);
        HookUpCamera(player);
        return player;
    }

    void HookUpCamera(GameObject player)
    {
        var vcam = FindAnyObjectByType<CinemachineVirtualCamera>();
        if (vcam == null) return;

        var target = player.GetComponentsInChildren<Transform>()
            .FirstOrDefault(t => t.CompareTag("CinemachineTarget"));

        if (target != null)
        {
            vcam.Follow = target;
            vcam.LookAt = target;
        }
    }

    SpawnPoint GetSpawnPoint(string spawnTag)
    {
        var candidates = spawnPoints
            .Where(p => p != null && !p.disabled && p.spawnTag == spawnTag)
            .ToList();

        if (candidates.Count == 0) return null;

        return strategy switch
        {
            SpawnStrategy.Random     => candidates[Random.Range(0, candidates.Count)],
            SpawnStrategy.First      => candidates[0],
            SpawnStrategy.RoundRobin => RoundRobin(candidates),
            _                        => candidates[0],
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
