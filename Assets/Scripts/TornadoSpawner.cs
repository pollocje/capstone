using System.Collections;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using StarterAssets;

/// <summary>
/// Spawns the tornado prefab at a random point inside a TornadoSpawnArea, then plays a short
/// locked-input cutscene: swap to a camera that's a child of the tornado prefab itself (so you
/// can position/angle it exactly in the prefab editor, it just rides along with wherever the
/// tornado spawns) while the tornado dissolves into view (TornadoDissolve.Appear()). Subscribes
/// itself to ResearchManager.onResearchComplete at runtime (see Start/WaitAndSubscribe) instead
/// of being wired in the Inspector, since ResearchManager only exists on the player, which is
/// spawned at runtime by the networked lobby flow and isn't present in the scene to drag a
/// reference from/to at edit time.
/// </summary>
public class TornadoSpawner : MonoBehaviour
{
    [SerializeField] private GameObject tornadoPrefab;
    [SerializeField] private TornadoSpawnArea spawnArea;

    [Header("Appear Cutscene")]
    [Tooltip("Total length of the locked-input cutscene, in seconds.")]
    [SerializeField] private float cutsceneDuration = 5f;
    [SerializeField] private string playerTag = "Player";

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

        StartCoroutine(SpawnSequence());
    }

    private IEnumerator SpawnSequence()
    {
        Vector3 point = spawnArea.GetRandomPoint();
        GameObject tornado = Instantiate(tornadoPrefab, point, Quaternion.identity);

        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        var locked = LockPlayer(player);

        // The tornado prefab carries its own reveal camera as a child (positioned/angled by hand
        // in the prefab editor) — disabled by default, so it's just sitting there waiting.
        Camera cutsceneCamera = tornado.GetComponentInChildren<Camera>(true);
        AudioListener cutsceneListener = tornado.GetComponentInChildren<AudioListener>(true);

        Camera playerCamera = null;
        AudioListener playerListener = null;
        bool playingCutscene = cutsceneCamera != null;
        if (playingCutscene)
        {
            if (player != null)
            {
                playerCamera = player.GetComponentInChildren<Camera>(true);
                playerListener = player.GetComponentInChildren<AudioListener>(true);
            }

            if (playerCamera != null) playerCamera.enabled = false;
            if (playerListener != null) playerListener.enabled = false;
            cutsceneCamera.gameObject.SetActive(true);
            cutsceneCamera.enabled = true;
            if (cutsceneListener != null) cutsceneListener.enabled = true;
        }

        var dissolve = tornado.GetComponent<TornadoDissolve>();
        if (dissolve != null) dissolve.Appear();

        // Hold the tornado still for the cutscene — TornadoLift.Start() hasn't run yet (it's
        // disabled before its first frame), so re-enabling it below fires Start() for the first
        // time and it picks its wander target from wherever it's sitting at that point.
        var lift = tornado.GetComponent<TornadoLift>();
        if (lift != null) lift.enabled = false;

        onTornadoSpawned?.Invoke();

        yield return new WaitForSeconds(cutsceneDuration);

        if (lift != null) lift.enabled = true;

        if (playingCutscene)
        {
            cutsceneCamera.enabled = false;
            cutsceneCamera.gameObject.SetActive(false);
            if (cutsceneListener != null) cutsceneListener.enabled = false;
            if (playerCamera != null) playerCamera.enabled = true;
            if (playerListener != null) playerListener.enabled = true;
        }

        UnlockPlayer(locked);
    }

    // Caches exactly the components VehicleEnterExit disables when putting the player in a
    // truck, plus Hotbar so number keys / mouse-use don't fire mid-cutscene.
    private struct LockedPlayer
    {
        public FirstPersonController fpc;
        public StarterAssetsInputs inputs;
#if ENABLE_INPUT_SYSTEM
        public PlayerInput playerInput;
#endif
        public Hotbar hotbar;
        public PlayerGrabController grab;
    }

    private LockedPlayer LockPlayer(GameObject player)
    {
        var locked = new LockedPlayer();
        if (player == null) return locked;

        locked.fpc = player.GetComponentInChildren<FirstPersonController>(true);
        locked.inputs = player.GetComponentInChildren<StarterAssetsInputs>(true);
#if ENABLE_INPUT_SYSTEM
        locked.playerInput = player.GetComponentInChildren<PlayerInput>(true);
        if (locked.playerInput != null) locked.playerInput.enabled = false;
#endif
        locked.hotbar = player.GetComponentInChildren<Hotbar>(true);
        locked.grab = player.GetComponentInChildren<PlayerGrabController>(true);

        if (locked.fpc != null) locked.fpc.enabled = false;
        if (locked.inputs != null) locked.inputs.enabled = false;
        if (locked.hotbar != null) locked.hotbar.enabled = false;
        if (locked.grab != null) locked.grab.enabled = false;

        return locked;
    }

    private void UnlockPlayer(LockedPlayer locked)
    {
        if (locked.fpc != null) locked.fpc.enabled = true;
        if (locked.inputs != null) locked.inputs.enabled = true;
        if (locked.hotbar != null) locked.hotbar.enabled = true;
        if (locked.grab != null) locked.grab.enabled = true;
#if ENABLE_INPUT_SYSTEM
        if (locked.playerInput != null) locked.playerInput.enabled = true;
#endif
    }
}
