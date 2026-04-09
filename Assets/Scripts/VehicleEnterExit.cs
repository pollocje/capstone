using UnityEngine;
using System.Collections;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using StarterAssets;

public class VehicleEnterExit : MonoBehaviour
{
    [Header("Truck")]
    [SerializeField] private TruckWheelDrive truckController;
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
    private FirstPersonController _fpc;
    private StarterAssetsInputs _inputs;
    private PlayerInput _playerInput;

    private void Start()
    {
        truckController.SetInputEnabled(false);

        if (enterPromptUI != null)
            enterPromptUI.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // In multiplayer, ignore players we don't own
        var netObj = other.transform.root.GetComponent<Unity.Netcode.NetworkObject>();
        if (netObj != null && !netObj.IsOwner) return;

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
        if (!Input.GetKeyDown(KeyCode.E)) return;

        if (!inVehicle && playerInRange)
            EnterVehicle();
        else if (inVehicle)
            ExitVehicle();
    }

    private void EnterVehicle()
    {
        // Cache and disable player components instead of deactivating the whole GO.
        // Deactivating a NetworkObject causes NGO lifecycle conflicts.
        // Components live on PlayerCapsule (child of NestedParent root), not the root itself.
        _cc     = playerRoot.GetComponentInChildren<CharacterController>(true);
        _fpc    = playerRoot.GetComponentInChildren<FirstPersonController>(true);
        _inputs = playerRoot.GetComponentInChildren<StarterAssetsInputs>(true);
        _playerInput = playerRoot.GetComponentInChildren<PlayerInput>(true);

        // Disable FPC first so its Update can't call Move() on a CC we're about to deactivate.
        if (_fpc != null) _fpc.enabled = false;
        if (_inputs != null) _inputs.enabled = false;
        if (_playerInput != null) _playerInput.enabled = false;
        if (_cc != null) _cc.enabled = false;

        playerRoot.transform.position = driverSeat.position;

        truckController.SetInputEnabled(true);
        followCameraObject.SetActive(true);

        if (enterPromptUI != null)
            enterPromptUI.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        inVehicle = true;
    }

    private void ExitVehicle()
    {
        truckController.SetInputEnabled(false);
        followCameraObject.SetActive(false);

        // Raycast down from above the exit point to place on actual terrain surface.
        Vector3 spawnPos = exitPoint.position;
        if (Physics.Raycast(exitPoint.position + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 15f))
            spawnPos = hit.point + Vector3.up * 0.1f;

        playerRoot.transform.position = spawnPos;
        Physics.SyncTransforms();

        if (_cc != null) _cc.enabled = true;
        if (_fpc != null) _fpc.enabled = true;
        if (_inputs != null) _inputs.enabled = true;
        if (_playerInput != null) _playerInput.enabled = true;

        inVehicle = false;
        playerInRange = false;
    }
}
