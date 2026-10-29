using UnityEngine;
using System.Collections;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using StarterAssets;
using Unity.Netcode;

public class VehicleEnterExit : NetworkBehaviour
{
    [Header("Truck")]
    [Tooltip("Assign whichever truck controller this vehicle actually uses - the WheelCollider truck, the raycast experiment, or PreconfiguredCarPhysics (the LuigiGameDev Car Controller bridge) - and leave the others empty. All three expose the same SetInputEnabled(bool), so this just calls whichever one is set instead of forcing them onto a shared interface (they're deliberately kept as separate, independent experiments).")]
    [SerializeField] private TruckWheelDrive wheelColliderTruck;
    [SerializeField] private TruckNewTEST raycastTruck;
    [SerializeField] private PreconfiguredCarPhysics preconfiguredCarTruck;
    [SerializeField] private GameObject followCameraObject;
    [SerializeField] private GameObject firstPersonCameraObject;
    [SerializeField] private KeyCode toggleViewKey = KeyCode.C;
    [SerializeField] private Transform driverSeat;
    [SerializeField] private Transform exitPoint;

    [Header("UI")]
    [SerializeField] private GameObject enterPromptUI;

    private bool playerInRange;
    private bool inVehicle;
    private bool firstPersonActive;
    private GameObject playerRoot;
    private Vector3 _enterPosition; // where the player stood when getting in (exit fallback)

    // Cached player components frozen while in the vehicle
    private CharacterController _cc;
    private StarterAssets.FirstPersonController _fpc;
    private StarterAssetsInputs _inputs;
    private PlayerInput _playerInput;
    private Camera _playerCamera;
    private AudioListener _playerAudioListener;
    private Renderer[] _playerRenderers;
    private Collider[] _playerColliders;

    // Online: True while someone is driving. Only the host changes it; everyone can read it.
    private readonly NetworkVariable<bool> occupied = new NetworkVariable<bool>(false);

    private void Start()
    {
        SetTruckInputEnabled(false);

        // Unoccupied at scene start - the driving camera has no business being live
        // until someone actually gets in. ExitVehicle() also turns this off, but that
        // only covers the enter->exit round trip, not the initial state.
        if (followCameraObject != null)
            followCameraObject.SetActive(false);

        if (enterPromptUI != null)
            enterPromptUI.SetActive(false);
    }

    private void SetTruckInputEnabled(bool enabled)
    {
        if (wheelColliderTruck != null) wheelColliderTruck.SetInputEnabled(enabled);
        if (raycastTruck != null) raycastTruck.SetInputEnabled(enabled);
        if (preconfiguredCarTruck != null) preconfiguredCarTruck.SetInputEnabled(enabled);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // In multiplayer, ignore players we don't own
        var netObj = other.transform.root.GetComponent<Unity.Netcode.NetworkObject>();
        if (netObj != null && netObj.IsSpawned && !netObj.IsOwner) return;

        playerRoot = other.transform.root.gameObject;
        playerInRange = true;
        if (enterPromptUI != null && !inVehicle)
            enterPromptUI.SetActive(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = false;
        if (enterPromptUI != null)
            enterPromptUI.SetActive(false);
    }

    private void Update()
    {
        // Online: Keep the driver's body in the seat so other players see them ride along.
        // (The capsule is what's network-synced, so move it rather than the root.)
        if (inVehicle && _cc != null)
            _cc.transform.SetPositionAndRotation(driverSeat.position, driverSeat.rotation);

        if (inVehicle && Input.GetKeyDown(toggleViewKey))
            ToggleCameraView();

        if (!Input.GetKeyDown(KeyCode.E)) return;

        if (!inVehicle && playerInRange)
            EnterVehicle();
        else if (inVehicle)
            ExitVehicle();
    }

    private void ToggleCameraView()
    {
        // Nothing to switch to if this truck doesn't have a first-person camera assigned.
        if (firstPersonCameraObject == null) return;

        firstPersonActive = !firstPersonActive;
        followCameraObject.SetActive(!firstPersonActive);
        firstPersonCameraObject.SetActive(firstPersonActive);
    }

    private void EnterVehicle()
    {
        // Online: don't enter if someone else is driving, otherwise ask the host for ownership
        // so this machine's driving is what gets synced to everyone.
        if (IsSpawned)
        {
            if (occupied.Value) return;
            RequestDriveRpc();
        }

        // Cache and disable player components instead of deactivating the whole GO.
        // Deactivating a NetworkObject causes NGO lifecycle conflicts.
        // Components live on PlayerCapsule (child of NestedParent root), not the root itself.
        _cc = playerRoot.GetComponentInChildren<CharacterController>(true);
        _fpc = playerRoot.GetComponentInChildren<StarterAssets.FirstPersonController>(true);
        _inputs = playerRoot.GetComponentInChildren<StarterAssetsInputs>(true);
        _playerInput = playerRoot.GetComponentInChildren<PlayerInput>(true);
        _playerCamera = playerRoot.GetComponentInChildren<Camera>(true);
        _playerAudioListener = playerRoot.GetComponentInChildren<AudioListener>(true);
        _playerRenderers = playerRoot.GetComponentsInChildren<Renderer>(true);
        _playerColliders = playerRoot.GetComponentsInChildren<Collider>(true);

        // Disable FPC first so its Update can't call Move() on a CC we're about to deactivate.
        if (_fpc != null) _fpc.enabled = false;
        if (_inputs != null) _inputs.enabled = false;
        if (_playerInput != null) _playerInput.enabled = false;
        if (_cc != null) _cc.enabled = false;

        // The player's own camera must not stay live once the truck's follow camera takes
        // over - otherwise both cameras (and both AudioListeners) are active at once, and the
        // leftover player camera ends up jammed inside the cab mesh at the driver seat position.
        if (_playerCamera != null) _playerCamera.enabled = false;
        if (_playerAudioListener != null) _playerAudioListener.enabled = false;

        // The capsule keeps getting repositioned to the driver seat every frame (below, for
        // other players to see), but nothing stops its own mesh from rendering - without this
        // the player's body rides along stuck to the truck like it's bolted on. Toggling
        // Renderer.enabled (not the GameObjects) keeps Animator/scripts intact for when NGO
        // observers need the pose, it just stops drawing it locally.
        if (_playerRenderers != null)
        {
            foreach (var renderer in _playerRenderers)
                if (renderer != null) renderer.enabled = false;
        }

        // The CharacterController above isn't the only collider on the player - PlayerCapsule
        // has a separate solid CapsuleCollider (distinct from the CC) on its "Capsule" child.
        // Leaving it enabled while the capsule gets teleported into the driver seat every frame
        // means it keeps physically overlapping the truck's own chassis collider, and since a
        // Rigidbody can't push a collider that's reset back every frame, the truck gets shoved
        // instead - straight into the ground.
        if (_playerColliders != null)
        {
            foreach (var col in _playerColliders)
                if (col != null) col.enabled = false;
        }

        _enterPosition = _cc != null ? _cc.transform.position : playerRoot.transform.position;

        // Online:Move the capsule (the part that actually moves/syncs), not the root.
        if (_cc != null) _cc.transform.position = driverSeat.position;
        else playerRoot.transform.position = driverSeat.position;

        SetTruckInputEnabled(true);

        // E puts you straight in the driver's seat view; C can still flip to the chase cam.
        firstPersonActive = firstPersonCameraObject != null;
        followCameraObject.SetActive(!firstPersonActive);
        if (firstPersonCameraObject != null) firstPersonCameraObject.SetActive(true);

        if (enterPromptUI != null)
            enterPromptUI.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        inVehicle = true;
    }

    private void ExitVehicle()
    {
        SetTruckInputEnabled(false);
        followCameraObject.SetActive(false);
        if (firstPersonCameraObject != null) firstPersonCameraObject.SetActive(false);

        // [Online: Free the truck for other players.
        if (IsSpawned) ReleaseDriveRpc();

        // Exit onto solid ground even if the truck is on its side or upside down: ExitPoint turns
        // with the truck and can end up underground. Try spots around the truck, then where we got
        // in, then the nearest spawn point; the old ExitPoint raycast is the last resort.
        Vector3 spawnPos = exitPoint.position;
        float radius = _cc != null ? _cc.radius : 0.5f, height = _cc != null ? _cc.height : 1.8f;
        if (!SafeGround.TryFindExit(transform, radius, height, out spawnPos) &&
            !SafeGround.TryGroundAt(_enterPosition, _enterPosition.y, transform, radius, height, out spawnPos, 1.5f) &&
            !SafeGround.TryNearestSpawn(_enterPosition, radius, height, out spawnPos))
        {
            spawnPos = exitPoint.position;
            if (Physics.Raycast(exitPoint.position + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 15f))
                spawnPos = hit.point + Vector3.up * 0.1f;
        }

        // Stand the player upright, facing the truck's heading. While seated the capsule copies the
        // seat's rotation, so after a flip it is upside down - and an upside-down CharacterController
        // has its capsule below the feet, inside the ground, so the player sinks through the terrain.
        var body = _cc != null ? _cc.transform : playerRoot.transform;
        body.SetPositionAndRotation(spawnPos, SafeGround.Upright(transform));
        Physics.SyncTransforms();

        if (_cc != null) _cc.enabled = true;
        if (_fpc != null) _fpc.enabled = true;
        if (_inputs != null) _inputs.enabled = true;
        if (_playerInput != null) _playerInput.enabled = true;
        if (_playerCamera != null) _playerCamera.enabled = true;
        if (_playerAudioListener != null) _playerAudioListener.enabled = true;
        if (_playerRenderers != null)
        {
            foreach (var renderer in _playerRenderers)
                if (renderer != null) renderer.enabled = true;
        }
        if (_playerColliders != null)
        {
            foreach (var col in _playerColliders)
                if (col != null) col.enabled = true;
        }

        inVehicle = false;
        playerInRange = false;
    }

    // ---------------------------------------------------------------------
    // Online: Runs on the host: hand the truck to whoever asked to drive.
    // ---------------------------------------------------------------------
    [Rpc(SendTo.Server)]
    private void RequestDriveRpc(RpcParams rpcParams = default)
    {
        if (occupied.Value) return; // someone got in first
        occupied.Value = true;
        NetworkObject.ChangeOwnership(rpcParams.Receive.SenderClientId);
    }

    [Rpc(SendTo.Server)]
    private void ReleaseDriveRpc()
    {
        occupied.Value = false;
    }
}
