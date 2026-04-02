using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class TruckWheelDrive : MonoBehaviour
{
    [Header("Wheel Colliders")]
    [SerializeField] private WheelCollider frontLeftCollider;
    [SerializeField] private WheelCollider frontRightCollider;
    [SerializeField] private WheelCollider backLeftCollider;
    [SerializeField] private WheelCollider backRightCollider;

    [Header("Wheel Visuals")]
    [SerializeField] private Transform frontLeftWheel;
    [SerializeField] private Transform frontRightWheel;
    [SerializeField] private Transform backLeftWheel;
    [SerializeField] private Transform backRightWheel;

    [Header("Driving")]
    [SerializeField] private float motorTorque = 2200f;
    [SerializeField] private float brakeTorque = 5000f;
    [SerializeField] private float maxSteerAngle = 24f;
    [SerializeField] private float maxSpeedKmh = 85f;
    [SerializeField] private float steerSmoothness = 5f;

    [Header("Stability")]
    [SerializeField] private Transform centerOfMass;
    [SerializeField] private float downforce = 60f;

    private Rigidbody rb;
    private float moveInput;
    private float steerInput;
    private bool isBraking;
    private float currentSteerAngle;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.mass = 1200f;
        rb.linearDamping = 0.05f;
        rb.angularDamping = 0.5f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        if (centerOfMass != null)
        {
            rb.centerOfMass = transform.InverseTransformPoint(centerOfMass.position);
        }
    }

    private void Update()
    {
        moveInput = Input.GetAxisRaw("Vertical");
        steerInput = Input.GetAxisRaw("Horizontal");
        isBraking = Input.GetKey(KeyCode.Space);

        UpdateWheelVisual(frontLeftCollider, frontLeftWheel);
        UpdateWheelVisual(frontRightCollider, frontRightWheel);
        UpdateWheelVisual(backLeftCollider, backLeftWheel);
        UpdateWheelVisual(backRightCollider, backRightWheel);
    }

    private void FixedUpdate()
    {
        ApplySteering();
        ApplyMotor();
        ApplyBrakes();
        ApplyDownforce();
    }

    private void ApplyMotor()
    {
        if (isBraking)
        {
            backLeftCollider.motorTorque = 0f;
            backRightCollider.motorTorque = 0f;
            return;
        }

        float speedKmh = rb.linearVelocity.magnitude * 3.6f;

        float torqueToApply = 0f;
        if (speedKmh < maxSpeedKmh || moveInput < 0f)
        {
            torqueToApply = moveInput * motorTorque;
        }

        backLeftCollider.motorTorque = torqueToApply;
        backRightCollider.motorTorque = torqueToApply;
    }

    private void ApplySteering()
    {
        float targetSteer = steerInput * maxSteerAngle;
        currentSteerAngle = Mathf.Lerp(currentSteerAngle, targetSteer, steerSmoothness * Time.fixedDeltaTime);

        frontLeftCollider.steerAngle = currentSteerAngle;
        frontRightCollider.steerAngle = currentSteerAngle;
    }

    private void ApplyBrakes()
    {
        float currentBrakeTorque = isBraking ? brakeTorque : 0f;

        frontLeftCollider.brakeTorque = currentBrakeTorque;
        frontRightCollider.brakeTorque = currentBrakeTorque;
        backLeftCollider.brakeTorque = currentBrakeTorque;
        backRightCollider.brakeTorque = currentBrakeTorque;

      
        if (isBraking)
        {
            rb.linearVelocity *= 0.94f; 
        }
    }

    private void ApplyDownforce()
    {
        rb.AddForce(-transform.up * downforce * rb.linearVelocity.magnitude);
    }

    private void UpdateWheelVisual(WheelCollider wheelCollider, Transform wheelVisual)
    {
        if (wheelCollider == null || wheelVisual == null) return;

        wheelCollider.GetWorldPose(out Vector3 position, out Quaternion rotation);
        wheelVisual.position = position;
        wheelVisual.rotation = rotation;
    }
} 