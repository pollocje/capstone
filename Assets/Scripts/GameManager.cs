using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] PlayerSpawnManager spawnManager;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        // Guard against the deferred Destroy in Awake not completing before Start runs
        if (Instance != this) return;

        if (NetworkManager.Singleton != null)
            StartCoroutine(WaitAndSetupSpawning());
        else
            spawnManager?.SpawnPlayer();
    }

    IEnumerator WaitAndSetupSpawning()
    {
        // NetworkTestBootstrapper may not have run yet — wait until NGO is actually listening
        yield return new WaitUntil(() => NetworkManager.Singleton.IsListening);

        if (NetworkManager.Singleton.IsServer)
            SetupMultiplayerSpawning();
        // Clients do nothing here — the server spawns their player and NGO replicates it
    }

    void SetupMultiplayerSpawning()
    {
        spawnManager?.ResetGroupSelection();

        // Spawn all players already connected when the scene loaded (host + lobby clients)
        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
            spawnManager?.SpawnNetworkedPlayer(clientId);

        // Handle any late joiners
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    void OnClientConnected(ulong clientId)
    {
        spawnManager?.SpawnNetworkedPlayer(clientId);
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }
}
