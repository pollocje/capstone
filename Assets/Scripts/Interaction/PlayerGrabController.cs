using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// REPO-style physical carrying. Interact (E) raycasts from the camera for a Grabbable and
/// hangs it off a HoldPoint in front of the camera with a ConfigurableJoint, so the item lags,
/// swings and still collides with the world. Interact again drops it; hold Throw (right mouse)
/// to charge and release to throw.
/// Sits alongside Hotbar on the player — it doesn't replace ItemPickup's hotbar pickup.
/// </summary>
public class PlayerGrabController : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("Camera to raycast from. Found under the player root if left empty.")]
    [SerializeField] private Transform cameraTransform;
    [Tooltip("Found in parents/children if left empty.")]
    [SerializeField] private PlayerInput playerInput;

    [Header("Input (action names in InputSystem_Actions)")]
    [SerializeField] private string interactActionName = "Interact";
    [SerializeField] private string throwActionName = "Throw";

    [Header("Grab")]
    [SerializeField] private float interactRange = 3f;
    [SerializeField] private LayerMask grabMask = ~0;
    [Tooltip("HoldPoint offset from the camera, in camera space.")]
    [SerializeField] private Vector3 holdOffset = new Vector3(0f, -0.2f, 1.5f);

    [Header("Joint — carrying (values are per kg of item mass)")]
    [Tooltip("How hard the item is pulled toward the HoldPoint. Higher = snappier, less lag.")]
    [SerializeField] private float spring = 150f;
    [Tooltip("Resists movement relative to the HoldPoint. Higher = less bounce/overshoot.")]
    [SerializeField] private float damper = 12f;
    [Tooltip("Hard limit on how far the grab point can trail the HoldPoint (m).")]
    [SerializeField] private float maxDistance = 1f;
    [Tooltip("If the item is stuck this far from the HoldPoint (e.g. snagged on a wall), it's dropped.")]
    [SerializeField] private float breakDistance = 1.75f;
    [Tooltip("How quickly the item turns back toward its grabbed orientation. Low = free swinging.")]
    [SerializeField] private float rotationStrength = 1.5f;
    [Tooltip("Angular damping applied while held, so items don't spin forever.")]
    [SerializeField] private float heldAngularDamping = 2f;

    [Header("Joint — steady hold (used while the item's IGrabSwingSuppressor is active)")]
    [SerializeField] private float steadySpring = 800f;
    [SerializeField] private float steadyDamper = 60f;
    [SerializeField] private float steadyRotationStrength = 20f;

    [Header("Throw")]
    [Tooltip("Impulse (N·s) for a tap of the throw key.")]
    [SerializeField] private float minThrowImpulse = 0.5f;
    [Tooltip("Impulse (N·s) at full charge.")]
    [SerializeField] private float maxThrowImpulse = 4f;
    [Tooltip("Seconds of holding Throw to reach full charge.")]
    [SerializeField] private float maxChargeTime = 1f;

    public Grabbable Held { get; private set; }
    /// <summary>0..1 throw charge, for a UI meter.</summary>
    public float ThrowCharge => _charging ? Mathf.Clamp01((Time.time - _chargeStart) / maxChargeTime) : 0f;

    private InputAction _interact;
    private InputAction _throw;

    private Transform _holdPoint;
    private Rigidbody _holdBody;
    private ConfigurableJoint _joint;
    private Collider[] _ownColliders;
    private Quaternion _rotationOffset;   // item rotation relative to the HoldPoint at grab time
    private bool _steady;

    // Rigidbody settings restored on release
    private float _savedAngularDamping;
    private RigidbodyInterpolation _savedInterpolation;

    private bool _charging;
    private float _chargeStart;

    private readonly RaycastHit[] _hits = new RaycastHit[16];

    void Awake()
    {
        if (cameraTransform == null)
        {
            // MainCamera is a sibling of PlayerCapsule on the player prefab, so search from the root.
            var cam = transform.root.GetComponentInChildren<Camera>(true);
            if (cam != null) cameraTransform = cam.transform;
        }
        if (playerInput == null) playerInput = GetComponentInParent<PlayerInput>();
        if (playerInput == null) playerInput = transform.root.GetComponentInChildren<PlayerInput>(true);

        _ownColliders = transform.root.GetComponentsInChildren<Collider>(true);
    }

    void Start()
    {
        if (playerInput != null && playerInput.actions != null)
        {
            _interact = playerInput.actions.FindAction(interactActionName);
            _throw = playerInput.actions.FindAction(throwActionName);
        }
        if (_interact == null) Debug.LogWarning($"PlayerGrabController: no '{interactActionName}' action found.", this);
        if (_throw == null) Debug.LogWarning($"PlayerGrabController: no '{throwActionName}' action found.", this);

        CreateHoldPoint();
    }

    void OnDisable() => Release();

    void OnDestroy()
    {
        if (_holdPoint != null) Destroy(_holdPoint.gameObject);
    }

    // NETWORK: PlayerNetworkSetup disables PlayerInput on non-owners, which keeps this Update
    // inert for remote players. Once items are networked, input must stay owner-only and the
    // grab/drop/throw requests below should go through the server.
    void Update()
    {
        if (cameraTransform == null || playerInput == null || !playerInput.enabled) return;

        if (_interact != null && _interact.WasPressedThisFrame())
        {
            if (Held != null) Release();
            else TryGrab();
        }

        if (Held == null)
        {
            _charging = false;
            return;
        }

        if (_throw != null)
        {
            if (_throw.WasPressedThisFrame())
            {
                _charging = true;
                _chargeStart = Time.time;
            }
            else if (_charging && _throw.WasReleasedThisFrame())
            {
                Throw(ThrowCharge);
            }
        }
    }

    void FixedUpdate()
    {
        if (Held == null) return;

        // Snagged on something and the player walked off — let go rather than fight the solver.
        Vector3 grabPoint = Held.transform.TransformPoint(_joint.anchor);
        if (Vector3.Distance(grabPoint, _holdPoint.position) > breakDistance)
        {
            Release();
            return;
        }

        bool steady = Held.SuppressSwing;
        if (steady != _steady)
        {
            _steady = steady;
            ApplyDrive();
        }

        // Turn the item back toward the orientation it had relative to the camera when grabbed.
        // Done with angular velocity (not MoveRotation) so it stays fully dynamic and collisions win.
        Rigidbody rb = Held.Body;
        Quaternion target = _holdPoint.rotation * _rotationOffset;
        (target * Quaternion.Inverse(rb.rotation)).ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;
        if (!float.IsFinite(axis.x)) return;

        Vector3 desired = axis * (angle * Mathf.Deg2Rad) * (_steady ? steadyRotationStrength : rotationStrength);
        float blend = _steady ? 1f : Time.fixedDeltaTime * 10f;
        rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, desired, blend);
    }

    // ── Grab / release ───────────────────────────────────────────────────────

    void CreateHoldPoint()
    {
        // Local-only helper — every client can have its own, it never needs to be networked.
        var go = new GameObject("HoldPoint");
        _holdPoint = go.transform;
        _holdPoint.SetParent(cameraTransform, false);
        _holdPoint.localPosition = holdOffset;
        _holdPoint.localRotation = Quaternion.identity;

        // Joints need a body to connect to; kinematic so it just follows the camera.
        // No interpolation here — it would overwrite the transform the camera parent sets.
        _holdBody = go.AddComponent<Rigidbody>();
        _holdBody.isKinematic = true;
    }

    void TryGrab()
    {
        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        int count = Physics.RaycastNonAlloc(ray, _hits, interactRange, grabMask, QueryTriggerInteraction.Ignore);

        // Closest hit that isn't part of our own player.
        RaycastHit? best = null;
        for (int i = 0; i < count; i++)
        {
            if (_hits[i].collider.transform.IsChildOf(transform.root)) continue;
            if (best == null || _hits[i].distance < best.Value.distance) best = _hits[i];
        }
        if (best == null) return;

        var grabbable = best.Value.collider.GetComponentInParent<Grabbable>();
        if (grabbable == null || grabbable.IsHeld) return;

        // NETWORK: this is where to ask the server for ownership of the item's NetworkObject
        // (e.g. a RequestGrabServerRpc that calls ChangeOwnership + sets the holder
        // NetworkVariable) and only attach once granted. The item will need a NetworkRigidbody
        // + owner-authoritative NetworkTransform so the joint simulates on the holder's
        // machine and everyone else just sees the synced pose.
        Attach(grabbable, best.Value.point);
    }

    void Attach(Grabbable grabbable, Vector3 hitPoint)
    {
        Held = grabbable;
        Rigidbody rb = grabbable.Body;

        // Keep it dynamic with collisions on — that's the whole point. Just smooth and calm it.
        rb.isKinematic = false;
        _savedAngularDamping = rb.angularDamping;
        _savedInterpolation = rb.interpolation;
        rb.angularDamping = heldAngularDamping;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // Don't collide with the carrier (looking down would otherwise shove it into our own capsule).
        // NETWORK: IgnoreCollision is local physics state — remote clients must apply the same
        // pairs when they learn who's holding the item, or the item will bump the holder there.
        SetIgnoreOwnColliders(grabbable, true);

        _rotationOffset = Quaternion.Inverse(_holdPoint.rotation) * rb.rotation;

        _joint = rb.gameObject.AddComponent<ConfigurableJoint>();
        _joint.connectedBody = _holdBody;
        _joint.autoConfigureConnectedAnchor = false;
        _joint.anchor = grabbable.GrabAtHitPoint
            ? rb.transform.InverseTransformPoint(hitPoint)
            : rb.transform.InverseTransformPoint(rb.worldCenterOfMass);
        _joint.connectedAnchor = Vector3.zero;
        _joint.enablePreprocessing = false;

        _joint.xMotion = _joint.yMotion = _joint.zMotion = ConfigurableJointMotion.Limited;
        _joint.linearLimit = new SoftJointLimit { limit = maxDistance };
        _joint.angularXMotion = _joint.angularYMotion = _joint.angularZMotion = ConfigurableJointMotion.Free;

        _steady = grabbable.SuppressSwing;
        ApplyDrive();

        _charging = false;
        grabbable.NotifyGrabbed(this);
    }

    /// <summary>Lets go of the held item (drop). Safe to call when nothing is held.</summary>
    // NETWORK: drop/throw should be sent to the server, which clears the holder and (optionally)
    // returns ownership to the server once the item comes to rest.
    public void Release()
    {
        if (Held == null) return;

        Grabbable grabbable = Held;
        Held = null;
        _charging = false;

        if (_joint != null) Destroy(_joint);
        _joint = null;

        if (grabbable != null)
        {
            Rigidbody rb = grabbable.Body;
            if (rb != null)
            {
                rb.angularDamping = _savedAngularDamping;
                rb.interpolation = _savedInterpolation;
            }
            SetIgnoreOwnColliders(grabbable, false);
            grabbable.NotifyReleased(this);
        }
    }

    void Throw(float charge)
    {
        Rigidbody rb = Held.Body;
        Release();

        // Impulse, so heavier items fly shorter — same as REPO.
        // NETWORK: apply on whichever machine owns the Rigidbody at the time (holder or server).
        float impulse = Mathf.Lerp(minThrowImpulse, maxThrowImpulse, charge);
        rb.AddForce(cameraTransform.forward * impulse, ForceMode.Impulse);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    void ApplyDrive()
    {
        if (_joint == null || Held == null) return;

        // Joint drives are forces, so scale by mass to make the tuning values feel the same
        // on a 0.2 kg map and a 5 kg crate.
        float mass = Held.Body.mass;
        var drive = new JointDrive
        {
            positionSpring = (_steady ? steadySpring : spring) * mass,
            positionDamper = (_steady ? steadyDamper : damper) * mass,
            maximumForce = float.MaxValue
        };
        _joint.xDrive = _joint.yDrive = _joint.zDrive = drive;
    }

    void SetIgnoreOwnColliders(Grabbable grabbable, bool ignore)
    {
        foreach (var itemCol in grabbable.SolidColliders)
        {
            if (itemCol == null) continue;
            foreach (var own in _ownColliders)
                if (own != null && !own.isTrigger) Physics.IgnoreCollision(itemCol, own, ignore);
        }
    }

    void OnValidate()
    {
        breakDistance = Mathf.Max(breakDistance, maxDistance + 0.1f);
        maxChargeTime = Mathf.Max(maxChargeTime, 0.01f);
    }

    void OnDrawGizmosSelected()
    {
        if (cameraTransform == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(cameraTransform.position, cameraTransform.position + cameraTransform.forward * interactRange);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(cameraTransform.TransformPoint(holdOffset), 0.05f);
    }
}
