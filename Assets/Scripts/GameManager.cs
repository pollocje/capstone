using UnityEngine;

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
        // Online: the host spawns networked players automatically (PlayerSpawnManager).
        // Offline: spawn a local player like before.
        if (spawnManager != null && !PlayerSpawnManager.IsNetworked)
            spawnManager.SpawnPlayer();
    }
}
