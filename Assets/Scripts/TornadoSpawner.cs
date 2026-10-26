using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Spawns the tornado prefab at a random point inside a TornadoSpawnArea. Wire SpawnTornado() to
/// ResearchManager.onResearchComplete in the Inspector to spawn one once research finishes.
/// </summary>
public class TornadoSpawner : MonoBehaviour
{
    [SerializeField] private GameObject tornadoPrefab;
    [SerializeField] private TornadoSpawnArea spawnArea;

    public UnityEvent onTornadoSpawned;

    public void SpawnTornado()
    {
        if (tornadoPrefab == null || spawnArea == null)
        {
            Debug.LogError("TornadoSpawner: tornadoPrefab or spawnArea not assigned.", this);
            return;
        }

        Vector3 point = spawnArea.GetRandomPoint();
        Instantiate(tornadoPrefab, point, Quaternion.identity);
        onTornadoSpawned?.Invoke();
    }
}
