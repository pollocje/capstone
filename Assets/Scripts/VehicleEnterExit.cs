using UnityEngine;
using StarterAssets;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class VehicleEnterExit : MonoBehaviour
{
    [Header("Truck")]
    [SerializeField] private TruckWheelDrive truckController;
    [SerializeField] private GameObject followCameraObject;
    [SerializeField] private Transform driverSeat;
    [SerializeField] private Transform exitPoint;

    [Header("Player")]
    [SerializeField] private GameObject playerObject;
    [SerializeField] private GameObject playerCameraObject;

    [Header("UI")]
    [SerializeField] private GameObject enterPromptUI;

    private FirstPersonController fpsController;
    private CharacterController characterController;
#if ENABLE_INPUT_SYSTEM
    private PlayerInput playerInput;
#endif

    private bool playerInRange;
    private bool inVehicle;

    private void Start()
    {
        if (playerObject != null)
        {
            fpsController = playerObject.GetComponent<FirstPersonController>();
            characterController = playerObject.GetComponent<CharacterController>();
#if ENABLE_INPUT_SYSTEM
            playerInput = playerObject.GetComponent<PlayerInput>();
#endif
        }

        truckController.SetInputEnabled(false);

        if (enterPromptUI != null)
            enterPromptUI.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
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
        // Disable player movement and camera
        fpsController.enabled = false;
        characterController.enabled = false;
#if ENABLE_INPUT_SYSTEM
        playerInput.enabled = false;
#endif
        playerCameraObject.SetActive(false);

        // Move player to driver seat (hides them inside the truck)
        playerObject.transform.position = driverSeat.position;

        // Enable truck input and camera
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
        // Disable truck input and camera
        truckController.SetInputEnabled(false);
        followCameraObject.SetActive(false);

        // Place player at exit point and re-enable
        characterController.enabled = true;
        playerObject.transform.position = exitPoint.position;
        fpsController.enabled = true;
#if ENABLE_INPUT_SYSTEM
        playerInput.enabled = true;
#endif
        playerCameraObject.SetActive(true);

        inVehicle = false;
        playerInRange = false;
    }
}
