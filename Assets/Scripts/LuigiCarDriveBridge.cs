using UnityEngine;
using LuigiGameDev.CarController.Logic;
using LuigiGameDev.CarController.View;

/// <summary>
/// Connects this project's input/enter-exit conventions (see VehicleEnterExit) to the
/// LuigiGameDev Car Controller asset, which otherwise expects its own InputActionAsset-driven
/// PlayerController and ViewStateLinker bootstrap. Exposes the same SetInputEnabled(bool) the
/// other truck scripts use, and registers this car with ViewStateLinker so CarWheelsVisual's
/// wheel spin/suspension animation actually receives data.
/// </summary>
[RequireComponent(typeof(CarInput))]
[RequireComponent(typeof(ViewStateLinker))]
public class LuigiCarDriveBridge : MonoBehaviour
{
    private CarInput carInput;
    private ViewStateLinker viewStateLinker;
    private bool inputEnabled = true;

    public void SetInputEnabled(bool enabled) => inputEnabled = enabled;

    private void Awake()
    {
        carInput = GetComponent<CarInput>();
        viewStateLinker = GetComponent<ViewStateLinker>();
    }

    private void Start()
    {
        viewStateLinker.Add(gameObject, GetComponent<CarViewState>());
    }

    private void Update()
    {
        if (!inputEnabled)
        {
            carInput.Steer = 0f;
            carInput.Throttle = 0f;
            carInput.Handbrake = false;
            return;
        }

        carInput.Steer = Input.GetAxisRaw("Horizontal");
        carInput.Throttle = Input.GetAxisRaw("Vertical");
        carInput.Handbrake = Input.GetKey(KeyCode.Space);
    }
}
