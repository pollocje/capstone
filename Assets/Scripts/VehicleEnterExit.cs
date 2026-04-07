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
        _cc = playerRoot.GetComponentInChildren<CharacterController>();
        _fpc = playerRoot.GetComponentInChildren<FirstPersonController>();
        _inputs = playerRoot.GetComponentInChildren<StarterAssetsInputs>();
        _playerInput = playerRoot.GetComponentInChildren<PlayerInput>();

        if (_cc != null) _cc.enabled = false;
        if (_fpc != null) _fpc.enabled = false;
        if (_inputs != null) _inputs.enabled = false;
        if (_playerInput != null) _playerInput.enabled = false;

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

        playerRoot.transform.position = exitPoint.position;

        // Wait a fixed frame before re-enabling movement so the CharacterController
        // has time to resolve its grounded state at the new position.
        StartCoroutine(ReactivatePlayer());

        inVehicle = false;
        playerInRange = false;
    }

    private IEnumerator ReactivatePlayer()
    {
        yield return new WaitForFixedUpdate();

        if (_cc != null) _cc.enabled = true;
        if (_fpc != null) _fpc.enabled = true;
        if (_inputs != null) _inputs.enabled = true;
        if (_playerInput != null) _playerInput.enabled = true;
    }
}
