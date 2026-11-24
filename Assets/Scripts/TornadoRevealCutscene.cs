using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using StarterAssets;
using Unity.Netcode;

/// <summary>
/// Lives on the Tornado prefab itself. The spawn is server-authoritative and networked (see
/// TornadoSpawner - only the host Instantiates + Spawns it), but the reveal cutscene - locking
/// input and swapping to RevealCam - is purely local presentation, so every machine plays its own
/// the moment this one shared tornado becomes visible to it: OnNetworkSpawn fires on the host
/// immediately after it spawns the tornado, and on each client once the spawn replicates there.
/// Holding TornadoLift still (then releasing it) only matters on the server, since that's the
/// only machine TornadoLift actually drives movement on - see TornadoLift.Update.
/// </summary>
public class TornadoRevealCutscene : NetworkBehaviour
{
    [Tooltip("Total length of the locked-input cutscene, in seconds.")]
    [SerializeField] private float cutsceneDuration = 5f;
    [SerializeField] private string playerTag = "Player";

    public override void OnNetworkSpawn()
    {
        StartCoroutine(RunCutscene());
    }

    private IEnumerator RunCutscene()
    {
        var lift = GetComponent<TornadoLift>();
        if (IsServer && lift != null) lift.enabled = false;

        GameObject player = FindLocalPlayer();
        var locked = LockPlayer(player);

        // The tornado prefab carries its own reveal camera as a child (positioned/angled by hand
        // in the prefab editor) — disabled by default, so it's just sitting there waiting.
        Camera cutsceneCamera = GetComponentInChildren<Camera>(true);
        AudioListener cutsceneListener = GetComponentInChildren<AudioListener>(true);

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

        // TODO: fade the tornado in via TornadoDissolve.Appear() once that's tuned — for now it
        // just spawns fully visible for the cutscene.

        var spawner = FindFirstObjectByType<TornadoSpawner>();
        if (spawner != null) spawner.onTornadoSpawned?.Invoke();

        yield return new WaitForSeconds(cutsceneDuration);

        if (IsServer && lift != null) lift.enabled = true;

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

    // Same ownership-aware lookup the rest of the tornado reveal flow needs - grabbing whichever
    // "Player"-tagged object comes first (a ghost, or a root's own capsule child) would lock and
    // camera-swap someone else's player instead of this machine's own.
    private GameObject FindLocalPlayer()
    {
        foreach (GameObject candidate in GameObject.FindGameObjectsWithTag(playerTag))
        {
            var netObj = candidate.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                if (netObj.IsSpawned && netObj.IsLocalPlayer) return candidate;
                continue; // a networked player root that isn't this machine's - skip it
            }

            // Not a networked player root at all (e.g. a capsule child, or offline testing) -
            // only acceptable as a match when there's no networking to disambiguate against.
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                return candidate;
        }
        return null;
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
