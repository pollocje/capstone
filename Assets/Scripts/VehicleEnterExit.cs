using UnityEngine;
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

    [Header("UI")]
    [SerializeField] private GameObject enterPromptUI;

    private bool playerInRange;
    private bool inVehicle;
    private GameObject playerRoot;

    private void Start()
    {
        truckController.SetInputEnabled(false);

        if (enterPromptUI != null)
            enterPromptUI.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
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
        playerRoot.transform.position = driverSeat.position;
        playerRoot.SetActive(false);

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
        playerRoot.SetActive(true);

        inVehicle = false;
        playerInRange = false;
    }
}
