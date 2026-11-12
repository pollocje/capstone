using LuigiGameDev.CarController.Logic;
using Unity.Netcode;
using UnityEngine;

// Turns the truck's steering wheel when the driver steers, and holds the hand grip points.
// Pivot and grips get made by Tools > Vehicles > Set Up Steering Wheel.
public class SteeringWheel : NetworkBehaviour
{
    [Tooltip("Empty at the center of the wheel, the wheel mesh is a child of it.")]
    [SerializeField] private Transform pivot;
    [SerializeField] private Transform leftHandGrip;
    [SerializeField] private Transform rightHandGrip;
    [SerializeField] private CarInput carInput;

    [Tooltip("How far the wheel turns at full lock (degrees).")]
    [SerializeField] private float maxWheelAngle = 120f;
    [Tooltip("How fast the wheel turns (degrees per second).")]
    [SerializeField] private float turnSpeed = 360f;
    [Tooltip("Flip if the wheel turns the wrong way.")]
    [SerializeField] private bool invert;

    // Driver owns the truck, so the driver writes this and everyone else reads it
    private readonly NetworkVariable<float> _steer = new NetworkVariable<float>(0f,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private Quaternion _restLocalRot;
    private float _angle;

    public Transform LeftHandGrip => leftHandGrip;
    public Transform RightHandGrip => rightHandGrip;

    private void Awake()
    {
        if (carInput == null) carInput = GetComponent<CarInput>();
        if (pivot != null) _restLocalRot = pivot.localRotation;
    }

    private void Update()
    {
        float steer = 0f;
        if (!IsSpawned) steer = carInput != null ? carInput.Steer : 0f; // offline testing
        else
        {
            // Steer goes back to 0 when nobody is driving, so the wheel re-centers
            if (IsOwner && carInput != null && !Mathf.Approximately(_steer.Value, carInput.Steer))
                _steer.Value = carInput.Steer;
            steer = _steer.Value;
        }

        if (pivot == null) return;

        // Ease toward the target angle so it doesn't snap
        float target = steer * maxWheelAngle * (invert ? -1f : 1f);
        _angle = Mathf.MoveTowards(_angle, target, turnSpeed * Time.deltaTime);
        pivot.localRotation = _restLocalRot * Quaternion.AngleAxis(-_angle, Vector3.forward);
    }
}
