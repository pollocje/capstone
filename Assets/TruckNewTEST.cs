using UnityEngine;

// From-scratch raycast vehicle controller: ONE Rigidbody, no WheelColliders. Each wheel is a
// raycast that applies three forces every FixedUpdate: suspension, drive/brake, and grip.
//
// Pipeline per wheel (each stage has its own enable toggle under Debug > Systems, so a
// misbehaving truck can be diagnosed by switching stages off one at a time instead of reading
// through one big method that does everything):
//   1) Raycast   - ground probe, always runs (every later stage needs wheel.grounded/contactPoint).
//   2) Steering  - speed-sensitive steer angle on isSteered wheels.
//   3) Suspension- spring + damper + progressive bump stop along the mount's up axis.
//   4) Kinematics- caches contact velocity / wheel basis / forward speed for stages 5-6.
//   5) Drive/brake - along the wheel's forward axis.
//   6) Grip      - sideways, based on SLIP ANGLE (not raw sideways speed), capped at tireGrip * load.
// Then, once per frame (not per wheel): anti-roll, roll stabilization, yaw/roll damping or parking brake.
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
        [HideInInspector] public float springLength;
        [HideInInspector] public bool grounded;
        [HideInInspector] public bool wasGrounded;
        [HideInInspector] public Vector3 contactPoint;
        [HideInInspector] public float currentSteerAngle;
        [HideInInspector] public float spinAngle;

        // Cached each FixedUpdate by the kinematics stage, read by drive/brake and grip so they
        // don't each re-derive the same contact velocity / wheel basis independently.
        [HideInInspector] public Vector3 wheelForward;
        [HideInInspector] public Vector3 wheelRight;
        [HideInInspector] public float forwardSpeed;
        [HideInInspector] public float lateralSpeed;
        [HideInInspector] public float normalLoad;

        [HideInInspector] public float debugSuspensionForce;
        [HideInInspector] public float debugGripForce;
        [HideInInspector] public float debugLateralSpeed;
        [HideInInspector] public float debugSlipAngle;
    }

    [Header("Debug")]
    [SerializeField] private bool showDebugOverlay = true;
    [SerializeField] private bool drawWheelRays = true;

    [Header("Debug > Systems (switch off to isolate which stage is misbehaving)")]
    [SerializeField] private bool enableSuspension = true;
    [SerializeField] private bool enableSteering = true;
    [SerializeField] private bool enableDrive = true;
    [SerializeField] private bool enableGrip = true;
    [SerializeField] private bool enableAntiRoll = true;
    [SerializeField] private bool enableRollStabilization = true;
    [SerializeField] private bool enableYawRollDamping = true;
    [SerializeField] private bool enableParkingBrake = true;

    [Header("Wheels")]
    [SerializeField] private Wheel frontLeft;
    [SerializeField] private Wheel frontRight;
    [SerializeField] private Wheel backLeft;
    [SerializeField] private Wheel backRight;

    [Header("Suspension")]
    [SerializeField] private float restLength = 0.3f;
    [SerializeField] private float suspensionTravel = 0.3f;
    [SerializeField] private float suspensionStrength = 22000f;
    [SerializeField] private float suspensionDamping = 3000f;
    [SerializeField] private float maxSuspensionVelocity = 5f;
    [SerializeField] private float maxSuspensionForce = 50000f;
    [SerializeField] private float wheelRadius = 0.42f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Bump Stop")]
    [Range(0.5f, 0.95f)]
    [SerializeField] private float bumpStopThreshold = 0.75f;
    [SerializeField] private float bumpStopStiffness = 65000f;

    [Header("Anti-Roll")]
    [SerializeField] private float antiRollFront = 20000f;
    [SerializeField] private float antiRollRear = 20000f;

    [Header("Roll Stabilization")]
    [SerializeField] private float rollStabilizationStrength = 8000f;
    [SerializeField] private float rollStabilizationDamping = 2000f;

    [Header("Driving")]
    [SerializeField] private float driveForce = 3000f;
    [SerializeField] private float brakeForce = 6000f;
    [SerializeField] private float maxSpeedKmh = 85f;
    [SerializeField] private float maxSteerAngle = 24f;
    [Tooltip("Fraction of maxSteerAngle still available at maxSpeedKmh. 1 = no reduction.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float highSpeedSteerFactor = 0.4f;
    [SerializeField] private float steerSpeed = 120f;

    [Header("Grip (slip-angle based)")]
    [Tooltip("Friction coefficient: max sideways force = tireGrip * wheel load. ~1 = road tire.")]
    [SerializeField] private float tireGrip = 1.0f;
    [Tooltip("Slip angle (degrees) at which the tire reaches full grip. Lower = sharper/twitchier, higher = softer.")]
    [SerializeField] private float peakSlipAngle = 8f;
    [Tooltip("Minimum forward speed used in the slip-angle calc, so slip angle doesn't explode near standstill.")]
    [SerializeField] private float lowSpeedSlipReference = 2f;
    [SerializeField] private float lateralDeadZone = 0.03f;

    [Header("Stabilization")]
    [Tooltip("Always-on roll/pitch angular decay (fraction/second). Does NOT touch yaw.")]
    [SerializeField] private float angularStabilization = 3f;
    [Tooltip("Yaw decay (fraction/second), only applied while NOT steering - keeps straight-line driving stable without fighting turns.")]
    [SerializeField] private float yawStabilizationWhenStraight = 3f;
    [Tooltip("Parking brake only engages below this horizontal speed (m/s) with no throttle.")]
    [SerializeField] private float parkSpeedThreshold = 1.5f;
    [SerializeField] private float parkingBrakeLinearDamping = 15f;
    [SerializeField] private float parkingBrakeAngularDamping = 20f;

    [Header("Center of Mass")]
    [Tooltip("Place the center of mass at the midpoint of the four wheel mounts instead of Unity's collider-based guess (which a truck bed drags far rearward, unloading the front tires and killing steering).")]
    [SerializeField] private bool autoCenterOfMass = true;
    [Tooltip("Offset added to the auto center of mass, in local space. Negative Y = lower = more stable. Small +Z shifts weight toward the front.")]
    [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0f, -0.2f, 0f);

    private Rigidbody rb;
    private float moveInput;
    private float steerInput;
    private bool isBraking;
    private bool inputEnabled = true;
    private bool parked;

    public void SetInputEnabled(bool enabled) => inputEnabled = enabled;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        if (autoCenterOfMass)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (Wheel w in new[] { frontLeft, frontRight, backLeft, backRight })
            {
                if (w == null || w.rayOrigin == null) continue;
                sum += transform.InverseTransformPoint(w.rayOrigin.position);
                count++;
            }
            if (count > 0)
                rb.centerOfMass = sum / count + centerOfMassOffset;
        }
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
            isBraking = true;
        }

        UpdateVisual(frontLeft);
        UpdateVisual(frontRight);
        UpdateVisual(backLeft);
        UpdateVisual(backRight);
    }

    private void FixedUpdate()
    {
        Vector3 v = rb.linearVelocity;
        float horizontalSpeed = new Vector3(v.x, 0f, v.z).magnitude;

        // Only "parked" when there's no throttle AND we're basically stopped - just moveInput == 0
        // would also kill grip while coasting through a turn.
        parked = Mathf.Approximately(moveInput, 0f) && horizontalSpeed < parkSpeedThreshold;

        RunWheelPipeline(frontLeft);
        RunWheelPipeline(frontRight);
        RunWheelPipeline(backLeft);
        RunWheelPipeline(backRight);

        if (enableAntiRoll)
        {
            ApplyAntiRoll(frontLeft, frontRight, antiRollFront);
            ApplyAntiRoll(backLeft, backRight, antiRollRear);
        }

        if (enableRollStabilization)
            ApplyRollStabilization();

        if (parked && enableParkingBrake)
            ApplyParkingBrake();
        else if (enableYawRollDamping)
            ApplyContinuousStabilization();
    }

    // Runs one wheel through every stage in order. Each stage early-outs cleanly if its toggle
    // is off or the wheel isn't grounded, so stages stay independent of each other.
    private void RunWheelPipeline(Wheel wheel)
    {
        if (wheel == null || wheel.rayOrigin == null) return;

        UpdateRaycast(wheel);

        if (wheel.isSteered && enableSteering)
            UpdateSteerAngle(wheel);

        if (!wheel.grounded)
        {
            wheel.wasGrounded = false;
            wheel.normalLoad = 0f;
            wheel.debugSuspensionForce = 0f;
            wheel.debugGripForce = 0f;
            wheel.debugLateralSpeed = 0f;
            wheel.debugSlipAngle = 0f;
            return;
        }

        wheel.normalLoad = enableSuspension ? ApplySuspension(wheel) : 0f;
        UpdateContactKinematics(wheel);

        if (enableDrive)
            ApplyDriveAndBrake(wheel);

        if (enableGrip)
            ApplyGrip(wheel);
    }

    // ---- 1) Raycast ----
    private void UpdateRaycast(Wheel wheel)
    {
        float maxRayDistance = restLength + suspensionTravel + wheelRadius;
        wheel.grounded = Physics.Raycast(wheel.rayOrigin.position, -wheel.rayOrigin.up, out RaycastHit hit,
            maxRayDistance, groundMask, QueryTriggerInteraction.Ignore);

        if (drawWheelRays)
            Debug.DrawRay(wheel.rayOrigin.position, -wheel.rayOrigin.up * maxRayDistance, wheel.grounded ? Color.green : Color.red);

        if (wheel.grounded)
        {
            wheel.contactPoint = hit.point;
            wheel.springLength = Mathf.Clamp(hit.distance - wheelRadius, restLength - suspensionTravel, restLength + suspensionTravel);
        }
    }

    // ---- 2) Steering ---- speed-sensitive: full lock at low speed, reduced at high speed.
    private void UpdateSteerAngle(Wheel wheel)
    {
        float dt = Time.fixedDeltaTime;
        float speed01 = Mathf.Clamp01(Mathf.Abs(Vector3.Dot(rb.linearVelocity, transform.forward)) * 3.6f / maxSpeedKmh);
        float steerLimit = maxSteerAngle * Mathf.Lerp(1f, highSpeedSteerFactor, speed01);
        wheel.currentSteerAngle = Mathf.MoveTowards(wheel.currentSteerAngle, steerInput * steerLimit, steerSpeed * dt);
    }

    // ---- 3) Suspension ---- spring + damper + progressive bump stop. Returns the normal load
    // (>=0) so drive/grip can scale against it without recomputing suspension themselves.
    private float ApplySuspension(Wheel wheel)
    {
        float dt = Time.fixedDeltaTime;
        wheel.suspensionOffset = restLength - wheel.springLength;

        float suspensionVelocity = wheel.wasGrounded
            ? (wheel.suspensionOffset - wheel.previousSuspensionOffset) / dt
            : 0f;
        suspensionVelocity = Mathf.Clamp(suspensionVelocity, -maxSuspensionVelocity, maxSuspensionVelocity);

        wheel.previousSuspensionOffset = wheel.suspensionOffset;
        wheel.wasGrounded = true;

        float baseSpringForce = wheel.suspensionOffset * suspensionStrength;

        float limitOffset = suspensionTravel * bumpStopThreshold;
        float bumpStopForce = 0f;
        if (wheel.suspensionOffset > limitOffset)
        {
            float r = (wheel.suspensionOffset - limitOffset) / (suspensionTravel - limitOffset);
            bumpStopForce = r * r * bumpStopStiffness;
        }
        else if (wheel.suspensionOffset < -limitOffset)
        {
            float r = (-wheel.suspensionOffset - limitOffset) / (suspensionTravel - limitOffset);
            bumpStopForce = -r * r * bumpStopStiffness;
        }

        float pushOnlyForce = Mathf.Max(baseSpringForce + suspensionVelocity * suspensionDamping, 0f);
        float suspensionForceMagnitude = Mathf.Clamp(pushOnlyForce + bumpStopForce, -maxSuspensionForce, maxSuspensionForce);
        wheel.debugSuspensionForce = suspensionForceMagnitude;

        rb.AddForceAtPosition(wheel.rayOrigin.up * suspensionForceMagnitude, wheel.rayOrigin.position);

        return Mathf.Max(suspensionForceMagnitude, 0f);
    }

    // ---- 4) Kinematics ---- cache contact velocity / wheel basis / forward+lateral speed once,
    // shared by drive/brake and grip so they read the same numbers instead of re-deriving them.
    private void UpdateContactKinematics(Wheel wheel)
    {
        Quaternion steerRotation = Quaternion.AngleAxis(wheel.currentSteerAngle, wheel.rayOrigin.up);
        wheel.wheelForward = steerRotation * wheel.rayOrigin.forward;
        wheel.wheelRight = steerRotation * wheel.rayOrigin.right;

        Vector3 contactVelocity = rb.GetPointVelocity(wheel.contactPoint);
        wheel.forwardSpeed = Vector3.Dot(contactVelocity, wheel.wheelForward);
        wheel.lateralSpeed = Vector3.Dot(contactVelocity, wheel.wheelRight);
    }

    // ---- 5) Drive / brake ----
    private void ApplyDriveAndBrake(Wheel wheel)
    {
        if (isBraking)
        {
            float brakeMagnitude = Mathf.Min(Mathf.Abs(wheel.forwardSpeed) * rb.mass * 0.25f, brakeForce);
            rb.AddForceAtPosition(-wheel.wheelForward * Mathf.Sign(wheel.forwardSpeed) * brakeMagnitude, wheel.rayOrigin.position);
        }
        else if (wheel.isDriven && !Mathf.Approximately(moveInput, 0f))
        {
            float speedKmh = Vector3.Dot(rb.linearVelocity, transform.forward) * 3.6f;
            if (Mathf.Abs(speedKmh) < maxSpeedKmh || Mathf.Sign(moveInput) != Mathf.Sign(speedKmh))
                rb.AddForceAtPosition(wheel.wheelForward * moveInput * driveForce, wheel.rayOrigin.position);
        }
    }

    // ---- 6) Grip: slip-angle based ----
    // Lateral force depends on the ANGLE between where the tire points and where it's actually
    // going, not raw sideways m/s. Same steering input = same grip demand at 20 km/h or 80 km/h,
    // so the front tires don't saturate just because speed built up during a held turn.
    private void ApplyGrip(Wheel wheel)
    {
        float dt = Time.fixedDeltaTime;
        wheel.debugLateralSpeed = wheel.lateralSpeed;

        if (parked || Mathf.Abs(wheel.lateralSpeed) < lateralDeadZone)
        {
            wheel.debugGripForce = 0f;
            wheel.debugSlipAngle = 0f;
            return;
        }

        float slipAngleDeg = Mathf.Atan2(wheel.lateralSpeed, Mathf.Max(Mathf.Abs(wheel.forwardSpeed), lowSpeedSlipReference)) * Mathf.Rad2Deg;
        wheel.debugSlipAngle = slipAngleDeg;

        float gripFactor = Mathf.Clamp(slipAngleDeg / peakSlipAngle, -1f, 1f);
        float gripForce = -gripFactor * tireGrip * wheel.normalLoad;

        // Never push harder than what cancels this wheel's share of the slip in one step,
        // so it can't overshoot and flip sign every frame (jitter).
        float maxStepForce = rb.mass * 0.25f * Mathf.Abs(wheel.lateralSpeed) / dt;
        gripForce = Mathf.Clamp(gripForce, -maxStepForce, maxStepForce);
        wheel.debugGripForce = gripForce;

        rb.AddForceAtPosition(wheel.wheelRight * gripForce, wheel.rayOrigin.position);
    }

    // Applied at the mount point, same as the suspension force, so both act at consistent positions.
    private void ApplyAntiRoll(Wheel left, Wheel right, float antiRollForce)
    {
        if (left == null || right == null || left.rayOrigin == null || right.rayOrigin == null) return;

        float fullTravel = restLength + suspensionTravel;
        float travelLeft = left.grounded ? left.springLength / fullTravel : 1f;
        float travelRight = right.grounded ? right.springLength / fullTravel : 1f;

        float force = (travelLeft - travelRight) * antiRollForce;

        if (left.grounded)
            rb.AddForceAtPosition(left.rayOrigin.up * -force, left.rayOrigin.position);
        if (right.grounded)
            rb.AddForceAtPosition(right.rayOrigin.up * force, right.rayOrigin.position);
    }

    private void ApplyRollStabilization()
    {
        float rollAngle = Vector3.SignedAngle(Vector3.up, transform.up, transform.forward);
        float rollRate = Vector3.Dot(rb.angularVelocity, transform.forward);

        float torque = -rollAngle * Mathf.Deg2Rad * rollStabilizationStrength - rollRate * rollStabilizationDamping;
        rb.AddTorque(transform.forward * torque);
    }

    // Damps roll/pitch always, yaw only when not steering. Damping yaw while steering is what
    // was directly cancelling the turn.
    private void ApplyContinuousStabilization()
    {
        float dt = Time.fixedDeltaTime;
        Vector3 w = rb.angularVelocity;
        Vector3 up = transform.up;

        Vector3 yaw = Vector3.Project(w, up);
        Vector3 rollPitch = w - yaw;

        rollPitch *= Mathf.Clamp01(1f - angularStabilization * dt);
        if (Mathf.Approximately(steerInput, 0f))
            yaw *= Mathf.Clamp01(1f - yawStabilizationWhenStraight * dt);

        rb.angularVelocity = rollPitch + yaw;
    }

    private void ApplyParkingBrake()
    {
        float dt = Time.fixedDeltaTime;
        Vector3 vel = rb.linearVelocity;
        Vector3 horizontal = new Vector3(vel.x, 0f, vel.z) * Mathf.Clamp01(1f - parkingBrakeLinearDamping * dt);
        rb.linearVelocity = new Vector3(horizontal.x, vel.y, horizontal.z);
        rb.angularVelocity *= Mathf.Clamp01(1f - parkingBrakeAngularDamping * dt);
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

    private void OnGUI()
    {
        if (!showDebugOverlay) return;

        float yawRate = Vector3.Dot(rb.angularVelocity, transform.up);
        GUI.Label(new Rect(10, 10, 900, 20),
            $"speed: {rb.linearVelocity.magnitude * 3.6f:F1} km/h   yawRate: {yawRate:F2} rad/s   angularVel: {rb.angularVelocity.magnitude:F2}   parked: {parked}   " +
            $"frontLoad: {frontLeft.normalLoad + frontRight.normalLoad:F0}N  rearLoad: {backLeft.normalLoad + backRight.normalLoad:F0}N   CoM(local): {rb.centerOfMass}");
        GUI.Label(new Rect(10, 110, 1100, 20),
            $"systems: susp={enableSuspension} steer={enableSteering} drive={enableDrive} grip={enableGrip} antiRoll={enableAntiRoll} rollStab={enableRollStabilization} yawDamp={enableYawRollDamping} parkBrake={enableParkingBrake}");
        DrawWheelDebug("FL", frontLeft, 30);
        DrawWheelDebug("FR", frontRight, 50);
        DrawWheelDebug("BL", backLeft, 70);
        DrawWheelDebug("BR", backRight, 90);
    }

    private void DrawWheelDebug(string label, Wheel wheel, int y)
    {
        if (wheel == null) return;
        string text = wheel.grounded
            ? $"{label}: grounded  spring={wheel.springLength:F3}m offset={wheel.suspensionOffset:F3}m susForce={wheel.debugSuspensionForce:F0}N gripForce={wheel.debugGripForce:F0}N lat={wheel.debugLateralSpeed:F3} slip={wheel.debugSlipAngle:F1}deg"
            : $"{label}: AIRBORNE";
        GUI.Label(new Rect(10, y, 1100, 20), text);
    }
}
