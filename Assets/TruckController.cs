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
    [SerializeField] private float motorTorque = 1400f;
    [SerializeField] private float brakeTorque = 5000f;
    [SerializeField] private float maxSteerAngle = 24f;
    [SerializeField] private float maxSpeedKmh = 85f;
    // Degrees/second the wheels turn toward the target steer angle. This used to be a
    // Lerp factor multiplied by Time.fixedDeltaTime (5 * 0.02 = 0.1 per physics step),
    // which only closed 10% of the gap to full lock each step and took ~1 full second
    // to reach full lock - that's the "doesn't steer well" sluggishness. MoveTowards at
    // a flat deg/sec rate reaches full lock in ~0.2s and stays frame-rate independent.
    [SerializeField] private float steerSpeed = 120f;

    [Header("Stability")]
    [SerializeField] private Transform centerOfMass;
    [SerializeField] private float downforce = 60f;
    [SerializeField] private float antiRollFront = 5000f;
    [SerializeField] private float antiRollRear = 5000f;

    private Rigidbody rb;
    private float moveInput;
    private float steerInput;
    private bool isBraking;
    private float currentSteerAngle;
    private bool inputEnabled = true;

    public void SetInputEnabled(bool enabled) => inputEnabled = enabled;

    private void Awake()
    {
        SnapToGround();

        rb = GetComponent<Rigidbody>();

        // Interpolation + continuous collision detection matter for a fast-moving vehicle
        // no matter what a prefab variant sets, so they're enforced here. Mass/damping are
        // deliberately NOT touched anymore - those are "feel" knobs and belong solely on the
        // Rigidbody component in the Inspector. (They used to be force-set here, which silently
        // overrode whatever was tuned on the component - including dropping linearDamping to a
        // near-frictionless 0.05, which is why the truck used to coast/slide almost indefinitely.)
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        if (centerOfMass != null)
        {
            rb.centerOfMass = transform.InverseTransformPoint(centerOfMass.position);
        }
    }

    private void Update()
    {
        if (!inputEnabled)
        {
            moveInput = 0f;
            steerInput = 0f;
            isBraking = false;
        }
        else
        {
            moveInput = Input.GetAxisRaw("Vertical");
            steerInput = Input.GetAxisRaw("Horizontal");
            isBraking = Input.GetKey(KeyCode.Space);
        }

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
        ApplyAntiRoll(frontLeftCollider, frontRightCollider, antiRollFront);
        ApplyAntiRoll(backLeftCollider, backRightCollider, antiRollRear);
    }

    // Couples left/right suspension travel on an axle so cornering load transfer
    // compresses one side and pushes down on the other, instead of letting the
    // unloaded wheel droop to its suspension limit and lift off the ground.
    private void ApplyAntiRoll(WheelCollider left, WheelCollider right, float antiRollForce)
    {
        WheelHit hit;
        float travelLeft = 1f;
        float travelRight = 1f;

        bool groundedLeft = left.GetGroundHit(out hit);
        if (groundedLeft)
            travelLeft = (-left.transform.InverseTransformPoint(hit.point).y - left.radius) / left.suspensionDistance;

        bool groundedRight = right.GetGroundHit(out hit);
        if (groundedRight)
            travelRight = (-right.transform.InverseTransformPoint(hit.point).y - right.radius) / right.suspensionDistance;

        float antiRollForceValue = (travelLeft - travelRight) * antiRollForce;

        if (groundedLeft)
            rb.AddForceAtPosition(left.transform.up * -antiRollForceValue, left.transform.position);
        if (groundedRight)
            rb.AddForceAtPosition(right.transform.up * antiRollForceValue, right.transform.position);
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
        currentSteerAngle = Mathf.MoveTowards(currentSteerAngle, targetSteer, steerSpeed * Time.fixedDeltaTime);

        frontLeftCollider.steerAngle = currentSteerAngle;
        frontRightCollider.steerAngle = currentSteerAngle;
    }

    private void ApplyBrakes()
    {
        // Also brake fully whenever there's no driver in control (inputEnabled false).
        // Without this, brakeTorque only ever applies while someone is seated, so an
        // unoccupied truck that starts rolling (e.g. spawned on a slope) has zero brakes
        // and nothing can stop it until a player gets in - effectively a parking brake.
        bool applyBrakes = isBraking || !inputEnabled;
        float currentBrakeTorque = applyBrakes ? brakeTorque : 0f;

        frontLeftCollider.brakeTorque = currentBrakeTorque;
        frontRightCollider.brakeTorque = currentBrakeTorque;
        backLeftCollider.brakeTorque = currentBrakeTorque;
        backRightCollider.brakeTorque = currentBrakeTorque;

        // NOTE: braking used to also do `rb.linearVelocity *= 0.94f` every FixedUpdate on
        // top of brakeTorque - that's a non-physical hard stop (velocity to ~4% in one
        // second regardless of how strong brakeTorque is) stacked on an already-strong
        // 12000 brakeTorque, which is what made stops feel like hitting a wall. Braking
        // now goes entirely through WheelCollider.brakeTorque like the rest of the sim.
    }

    private void ApplyDownforce()
    {
        // Use planar (forward/lateral) speed only, not the full 3D velocity magnitude.
        // Using the full magnitude meant vertical bounce velocity from the suspension
        // (going over a bump, landing) spiked downforce too, feeding back into more
        // suspension compression/bounce - part of the "gravity feels off" sensation.
        Vector3 planarVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, transform.up);
        rb.AddForce(-transform.up * downforce * planarVelocity.magnitude);
    }

    // Hand-placed spawn transforms drift out of sync with the actual terrain
    // collider (the terrain is authored/edited after objects get positioned
    // against its old rendered height). Rather than trust the saved Y, find
    // the real ground under the truck's XZ and rest the lowest wheel on it,
    // with a little clearance so the suspension settles onto it naturally.
    //
    // A plain Physics.Raycast here will happily hit the truck's OWN colliders
    // on the way down - most importantly the enter/exit trigger box that sits
    // right over the body - and use that height instead of the real ground.
    // So this ignores triggers entirely and walks past any hit that belongs to
    // this truck's own hierarchy to find the first real ground hit beneath it.
    private void SnapToGround()
    {
        Vector3 rayOrigin = transform.position + Vector3.up * 50f;
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, 1000f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        RaycastHit hit = default;
        bool foundGround = false;
        foreach (var candidate in hits)
        {
            if (candidate.collider.transform.IsChildOf(transform)) continue;
            hit = candidate;
            foundGround = true;
            break;
        }

        if (!foundGround)
            return;

        float lowestWheelBottomLocalY = float.MaxValue;
        foreach (var wheel in new[] { frontLeftCollider, frontRightCollider, backLeftCollider, backRightCollider })
        {
            if (wheel == null) continue;
            float bottomLocalY = wheel.transform.localPosition.y - wheel.radius;
            lowestWheelBottomLocalY = Mathf.Min(lowestWheelBottomLocalY, bottomLocalY);
        }

        if (lowestWheelBottomLocalY == float.MaxValue) return;

        const float clearance = 0.1f;
        float targetY = hit.point.y + clearance - lowestWheelBottomLocalY;
        transform.position = new Vector3(transform.position.x, targetY, transform.position.z);
    }

    private void UpdateWheelVisual(WheelCollider wheelCollider, Transform wheelVisual)
    {
        if (wheelCollider == null || wheelVisual == null) return;

        wheelCollider.GetWorldPose(out Vector3 position, out Quaternion rotation);
        wheelVisual.position = position;
        wheelVisual.rotation = rotation;
    }

    // TEMP DIAGNOSTIC: draws each wheel's full suspension reach (yellow) and,
    // when it finds ground, the actual contact point (green) plus the gap
    // between the wheel's lowest reach and the ground (red, if grounded but
    // near the limit). If a wheel never shows green, its suspension physically
    // cannot reach the ground at that spot. Remove once the terrain issue is found.
    private void OnDrawGizmos()
    {
        DrawWheelDiagnostic(frontLeftCollider);
        DrawWheelDiagnostic(frontRightCollider);
        DrawWheelDiagnostic(backLeftCollider);
        DrawWheelDiagnostic(backRightCollider);
    }

    private void DrawWheelDiagnostic(WheelCollider wheel)
    {
        if (wheel == null) return;

        Vector3 origin = wheel.transform.position;
        float reach = wheel.radius + wheel.suspensionDistance;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin, origin - wheel.transform.up * reach);

        if (wheel.GetGroundHit(out WheelHit hit))
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(hit.point, 0.08f);
        }
        else
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(origin - wheel.transform.up * reach, 0.08f);
        }
    }
}