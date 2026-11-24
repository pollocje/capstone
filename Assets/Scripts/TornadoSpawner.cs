using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;

/// <summary>
/// Decides WHEN the shared tornado appears (ResearchManager.onResearchComplete) and, online, WHO
/// actually creates it: only the server Instantiates + NetworkObject.Spawn()s it, at a point
/// picked by the server, so every connected machine ends up watching the exact same tornado
/// instead of each independently faking its own at a different spot. The reveal cutscene itself
/// (camera swap, input lock) lives on the prefab - see TornadoRevealCutscene - since that has to
/// run per-machine the moment the networked tornado actually becomes visible there, which isn't
/// necessarily the same moment as this one's SpawnTornado() call.
/// Subscribes itself to ResearchManager.onResearchComplete at runtime (see Start/WaitAndSubscribe)
/// instead of being wired in the Inspector, since ResearchManager only exists on the player, which
/// is spawned at runtime by the networked lobby flow and isn't present in the scene to drag a
/// reference from/to at edit time.
/// </summary>
public class TornadoSpawner : MonoBehaviour
{
    [SerializeField] private GameObject tornadoPrefab;
    [SerializeField] private TornadoSpawnArea spawnArea;

    /// <summary>Fired by TornadoRevealCutscene on every machine, the moment the shared tornado
    /// becomes visible there - not by this spawner itself, since only the server ever calls
    /// SpawnTornado() with any effect once networked.</summary>
    public UnityEvent onTornadoSpawned;

    void Start()
    {
        StartCoroutine(WaitAndSubscribe());
    }

    private IEnumerator WaitAndSubscribe()
    {
        while (ResearchManager.Instance == null)
            yield return null;

        ResearchManager.Instance.onResearchComplete.AddListener(SpawnTornado);
    }

    public void SpawnTornado()
    {
        if (tornadoPrefab == null || spawnArea == null)
        {
            Debug.LogError("TornadoSpawner: tornadoPrefab or spawnArea not assigned.", this);
            return;
        }

        // Online: research completing broadcasts to every machine, so each would otherwise try to
        // spawn its own tornado independently - only the server actually creates the shared one;
        // everyone else just reacts to it arriving via Netcode's own spawn replication.
        bool networked = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (networked && !NetworkManager.Singleton.IsServer) return;

        Vector3 point = spawnArea.GetRandomPoint();
        GameObject tornado = Instantiate(tornadoPrefab, point, Quaternion.identity);

        if (networked)
        {
            var netObj = tornado.GetComponent<NetworkObject>();
            if (netObj != null)
                netObj.Spawn();
            else
                Debug.LogError("TornadoSpawner: tornadoPrefab has no NetworkObject - it won't be visible to other players.", this);
        }
    }
}
