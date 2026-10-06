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
    [Tooltip("Assign whichever truck controller this vehicle actually uses - the WheelCollider truck, the raycast experiment, or the LuigiGameDev Car Controller - and leave the others empty. All three expose the same SetInputEnabled(bool), so this just calls whichever one is set instead of forcing them onto a shared interface (they're deliberately kept as separate, independent experiments).")]
    [SerializeField] private TruckWheelDrive wheelColliderTruck;
    [SerializeField] private TruckNewTEST raycastTruck;
    [SerializeField] private LuigiCarDriveBridge luigiCarTruck;
    [SerializeField] private GameObject followCameraObject;
    [SerializeField] private Transform driverSeat;
    [SerializeField] private Transform exitPoint;

    [Header("UI")]
    [SerializeField] private GameObject enterPromptUI;

    private bool playerInRange;
    private bool inVehicle;
    private GameObject playerRoot;

    // Cached player components frozen while in the vehicle
    private CharacterController _cc;
    private StarterAssets.FirstPersonController _fpc;
    private StarterAssetsInputs _inputs;
    private PlayerInput _playerInput;

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
        if (luigiCarTruck != null) luigiCarTruck.SetInputEnabled(enabled);
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

        if (!Input.GetKeyDown(KeyCode.E)) return;

        if (!inVehicle && playerInRange)
            EnterVehicle();
        else if (inVehicle)
            ExitVehicle();
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

        // Disable FPC first so its Update can't call Move() on a CC we're about to deactivate.
        if (_fpc != null) _fpc.enabled = false;
        if (_inputs != null) _inputs.enabled = false;
        if (_playerInput != null) _playerInput.enabled = false;
        if (_cc != null) _cc.enabled = false;

        // Online:Move the capsule (the part that actually moves/syncs), not the root.
        if (_cc != null) _cc.transform.position = driverSeat.position;
        else playerRoot.transform.position = driverSeat.position;

        SetTruckInputEnabled(true);
        followCameraObject.SetActive(true);

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

        // [Online: Free the truck for other players.
        if (IsSpawned) ReleaseDriveRpc();

        // Raycast down from above the exit point to place on actual terrain surface.
        Vector3 spawnPos = exitPoint.position;
        if (Physics.Raycast(exitPoint.position + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 15f))
            spawnPos = hit.point + Vector3.up * 0.1f;

        // Online: Move the capsule (the part that actually moves/syncs), not the root.
        if (_cc != null) _cc.transform.position = spawnPos;
        else playerRoot.transform.position = spawnPos;
        Physics.SyncTransforms();

        if (_cc != null) _cc.enabled = true;
        if (_fpc != null) _fpc.enabled = true;
        if (_inputs != null) _inputs.enabled = true;
        if (_playerInput != null) _playerInput.enabled = true;

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