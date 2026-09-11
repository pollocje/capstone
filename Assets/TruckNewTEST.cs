using UnityEngine;

// From-scratch vehicle controller: ONE Rigidbody (the one already on this GameObject), no
// WheelCollider components at all. Each wheel is just a raycast, and every FixedUpdate it
// tracks exactly three physics quantities and turns each into a force applied to the
// Rigidbody at that wheel's ground contact point:
//
//   1) Suspension - vertical. force = offset * suspensionStrength, where `offset` is how far
//      the spring is compressed (positive) or stretched (negative) from its resting length.
//   2) Drive/brake - forward/backward along the wheel's own forward axis (which follows
//      steering for the front wheels).
//   3) Grip ("no sideways slip") - sideways along the wheel's right axis. Measures how fast
//      the contact point is moving sideways and cancels it, so the truck can't skid sideways.
//
// This is deliberately a separate experiment from TruckController.cs/TruckWheelDrive (the
// WheelCollider-based truck) - that one is untouched. This script is meant to be tuned from
// a blank slate in its own test scene before anyone considers swapping it into the real truck.
[RequireComponent(typeof(Rigidbody))]
public class TruckNewTEST : MonoBehaviour
{
    [System.Serializable]
    public class Wheel
    {
        [Tooltip("Where the suspension is mounted to the chassis - the ray fires straight down from here.")]
        public Transform rayOrigin;
        [Tooltip("The tire mesh to move/spin/steer. Optional - leave empty for physics-only testing.")]
        public Transform visual;
        public bool isSteered;
        public bool isDriven;

        [HideInInspector] public float suspensionOffset;      // 0 = resting length, +compressed, -stretched
        [HideInInspector] public float previousSuspensionOffset;
        [HideInInspector] public float springLength;          // current ray-distance-to-ground minus wheel radius
        [HideInInspector] public bool grounded;
        [HideInInspector] public Vector3 contactPoint;
        [HideInInspector] public float currentSteerAngle;
        [HideInInspector] public float spinAngle;
        [HideInInspector] public float debugSuspensionForce;
        [HideInInspector] public float debugGripForce;
    }

    [Header("Debug")]
    [Tooltip("Draws an on-screen readout of each wheel's live grounded/offset/force numbers while playing, so what's actually happening doesn't have to be guessed from how it looks.")]
    [SerializeField] private bool showDebugOverlay = true;

    [Header("Wheels")]
    [SerializeField] private Wheel frontLeft;
    [SerializeField] private Wheel frontRight;
    [SerializeField] private Wheel backLeft;
    [SerializeField] private Wheel backRight;

    [Header("Suspension")]
    [Tooltip("Ray distance (minus wheel radius) at which the spring's offset is 0 - its natural, unloaded length.")]
    [SerializeField] private float restLength = 0.3f;
    [Tooltip("How far the spring can compress or stretch away from restLength.")]
    [SerializeField] private float suspensionTravel = 0.3f;
    [Tooltip("force = offset * suspensionStrength. Bigger = stiffer ride, less sag under load.")]
    [SerializeField] private float suspensionStrength = 22000f;
    [Tooltip("Resists the RATE the spring is compressing/extending, so it settles instead of bouncing forever. 0 = pure force=offset*strength spring, no damping.")]
    [SerializeField] private float suspensionDamping = 3000f;
    [SerializeField] private float wheelRadius = 0.42f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Driving")]
    [SerializeField] private float driveForce = 3000f;
    [SerializeField] private float brakeForce = 6000f;
    [SerializeField] private float maxSpeedKmh = 85f;
    [SerializeField] private float maxSteerAngle = 24f;
    [SerializeField] private float steerSpeed = 120f;

    [Header("Grip")]
    [Tooltip("0 = no sideways grip (slides like ice). 1 = sideways velocity fully cancelled every step - the truck cannot slide sideways at all.")]
    [Range(0f, 1f)]
    [SerializeField] private float gripStrength = 1f;

    private Rigidbody rb;
    private float moveInput;
    private float steerInput;
    private bool isBraking;
    private bool inputEnabled = true;

    public void SetInputEnabled(bool enabled) => inputEnabled = enabled;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    private void Update()
    {
        if (inputEnabled)
        {
            moveInput = Input.GetAxisRaw("Vertical");
            steerInput = Input.GetAxisRaw("Horizontal");
            isBraking = Input.GetKey(KeyCode.Space);
        }
        else
        {
            moveInput = 0f;
            steerInput = 0f;
            isBraking = true; // no driver -> parking brake, same reasoning as the original truck
        }

        UpdateVisual(frontLeft);
        UpdateVisual(frontRight);
        UpdateVisual(backLeft);
        UpdateVisual(backRight);
    }

    private void FixedUpdate()
    {
        RunWheel(frontLeft);
        RunWheel(frontRight);
        RunWheel(backLeft);
        RunWheel(backRight);
    }

    private void RunWheel(Wheel wheel)
    {
        if (wheel == null || wheel.rayOrigin == null) return;

        float maxRayDistance = restLength + suspensionTravel + wheelRadius;
        wheel.grounded = Physics.Raycast(wheel.rayOrigin.position, -wheel.rayOrigin.up, out RaycastHit hit,
            maxRayDistance, groundMask, QueryTriggerInteraction.Ignore);

        // Steering happens whether or not the wheel is currently touching the ground, same as
        // a real steering rack - only the forces below need the wheel to actually be grounded.
        if (wheel.isSteered)
        {
            float targetSteer = steerInput * maxSteerAngle;
            wheel.currentSteerAngle = Mathf.MoveTowards(wheel.currentSteerAngle, targetSteer, steerSpeed * Time.fixedDeltaTime);
        }

        if (!wheel.grounded)
        {
            // Nothing below the wheel: no suspension, no drive, no grip. It's just hanging/falling,
            // which is exactly why an airborne wheel spins freely under motor torque with no traction.
            wheel.previousSuspensionOffset = 0f;
            wheel.debugSuspensionForce = 0f;
            wheel.debugGripForce = 0f;
            return;
        }

        wheel.contactPoint = hit.point;
        wheel.springLength = Mathf.Clamp(hit.distance - wheelRadius, restLength - suspensionTravel, restLength + suspensionTravel);

        // ---- 1) Suspension: force = offset * strength ----
        // suspensionVelocity > 0 means the wheel is compressing further (offset increasing).
        // A real damper resists whichever way the spring is currently moving: extra push back
        // while compressing (adds to the spring force), less push back while rebounding/extending
        // (subtracts from it) - so the damping term must be ADDED here, not subtracted. Subtracting
        // it does the opposite: during every rebound it adds energy back in instead of bleeding it
        // off, so the suspension can never actually settle no matter how big suspensionDamping is -
        // turning it up just injects more energy each bounce instead of damping anything.
        wheel.suspensionOffset = restLength - wheel.springLength;
        float suspensionVelocity = (wheel.suspensionOffset - wheel.previousSuspensionOffset) / Time.fixedDeltaTime;
        wheel.previousSuspensionOffset = wheel.suspensionOffset;

        float suspensionForceMagnitude = wheel.suspensionOffset * suspensionStrength + suspensionVelocity * suspensionDamping;
        suspensionForceMagnitude = Mathf.Max(suspensionForceMagnitude, 0f); // a spring only ever pushes, never pulls
        wheel.debugSuspensionForce = suspensionForceMagnitude;
        rb.AddForceAtPosition(wheel.rayOrigin.up * suspensionForceMagnitude, wheel.contactPoint);

        // Wheel-plane axes at the current steer angle - used for both drive and grip below.
        Quaternion steerRotation = Quaternion.AngleAxis(wheel.currentSteerAngle, wheel.rayOrigin.up);
        Vector3 wheelForward = steerRotation * wheel.rayOrigin.forward;
        Vector3 wheelRight = steerRotation * wheel.rayOrigin.right;
        Vector3 contactVelocity = rb.GetPointVelocity(wheel.contactPoint);

        // ---- 2) Drive / brake: force along the wheel's forward axis ----
        if (isBraking)
        {
            float forwardSpeed = Vector3.Dot(contactVelocity, wheelForward);
            float brakeMagnitude = Mathf.Min(Mathf.Abs(forwardSpeed) * rb.mass * 0.25f, brakeForce);
            rb.AddForceAtPosition(-wheelForward * Mathf.Sign(forwardSpeed) * brakeMagnitude, wheel.contactPoint);
        }
        else if (wheel.isDriven && !Mathf.Approximately(moveInput, 0f))
        {
            float speedKmh = Vector3.Dot(rb.linearVelocity, transform.forward) * 3.6f;
            if (Mathf.Abs(speedKmh) < maxSpeedKmh || Mathf.Sign(moveInput) != Mathf.Sign(speedKmh))
            {
                rb.AddForceAtPosition(wheelForward * moveInput * driveForce, wheel.contactPoint);
            }
        }

        // ---- 3) Grip: cancel sideways (slip) velocity so the truck can't slide sideways ----
        // The force that would cancel ALL sideways velocity in exactly one physics step is
        // mass * velocity / dt - dividing by a ~0.02s timestep massively amplifies it. That's
        // fine as long as lateralSpeed only ever reflects gentle sideways drift, but the moment
        // the chassis has any rotation (e.g. settling onto its suspension right after spawning),
        // GetPointVelocity includes a contribution from angular velocity that can spike this
        // number - and an unbounded force reacting to that spike is exactly what was flipping
        // the truck the instant the wheels started actually touching the ground. Real tires
        // don't have infinite grip either: they're limited by how hard they're pressed into the
        // road (friction proportional to normal load). Capping the correction the same way -
        // to a multiple of this wheel's own suspension force - keeps grip physically bounded no
        // matter what the raw velocity math says.
        float lateralSpeed = Vector3.Dot(contactVelocity, wheelRight);
        float desiredForce = -lateralSpeed * (rb.mass * 0.25f) / Time.fixedDeltaTime;
        float maxGripForce = suspensionForceMagnitude * gripStrength * 3f;
        float clampedForce = Mathf.Clamp(desiredForce, -maxGripForce, maxGripForce);
        wheel.debugGripForce = clampedForce;
        rb.AddForceAtPosition(wheelRight * clampedForce, wheel.contactPoint);
    }

    private void UpdateVisual(Wheel wheel)
    {
        if (wheel == null || wheel.visual == null || wheel.rayOrigin == null) return;

        float length = wheel.grounded ? wheel.springLength : restLength + suspensionTravel;
        wheel.visual.position = wheel.rayOrigin.position - wheel.rayOrigin.up * length;

        float forwardSpeed = Vector3.Dot(rb.linearVelocity, wheel.rayOrigin.forward);
        wheel.spinAngle += (forwardSpeed / wheelRadius) * Mathf.Rad2Deg * Time.deltaTime;

        Quaternion steerRotation = wheel.isSteered ? Quaternion.AngleAxis(wheel.currentSteerAngle, wheel.rayOrigin.up) : Quaternion.identity;
        Quaternion spinRotation = Quaternion.AngleAxis(wheel.spinAngle, Vector3.right);
        wheel.visual.rotation = wheel.rayOrigin.rotation * steerRotation * spinRotation;
    }

    // On-screen readout of what each wheel is actually doing, so a wrong result can be diagnosed
    // from the real numbers (grounded? how compressed? how much force?) instead of guessing from
    // how the truck looks. Toggle off with showDebugOverlay once the physics is behaving.
    private void OnGUI()
    {
        if (!showDebugOverlay) return;

        GUI.Label(new Rect(10, 10, 500, 20), $"speed: {rb.linearVelocity.magnitude * 3.6f:F1} km/h   angularVel: {rb.angularVelocity.magnitude:F2}");
        DrawWheelDebug("FL", frontLeft, 30);
        DrawWheelDebug("FR", frontRight, 50);
        DrawWheelDebug("BL", backLeft, 70);
        DrawWheelDebug("BR", backRight, 90);
    }

    private void DrawWheelDebug(string label, Wheel wheel, int y)
    {
        if (wheel == null) return;
        string text = wheel.grounded
            ? $"{label}: grounded  spring={wheel.springLength:F3}m offset={wheel.suspensionOffset:F3}m susForce={wheel.debugSuspensionForce:F0}N gripForce={wheel.debugGripForce:F0}N"
            : $"{label}: AIRBORNE";
        GUI.Label(new Rect(10, y, 700, 20), text);
    }
}
