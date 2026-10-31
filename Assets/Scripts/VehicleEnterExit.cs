using System.Collections.Generic;
using UnityEngine;
using System.Collections;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using StarterAssets;
using Unity.Netcode;

public class VehicleEnterExit : NetworkBehaviour
{
    private const int DriverSeatIndex = 0;
    private static readonly string[] SeatLabels = { "Driver Seat", "Passenger Seat", "Truck Bed (Left)", "Truck Bed (Right)" };

    [Header("Truck")]
    [Tooltip("Assign whichever truck controller this vehicle actually uses - the WheelCollider truck, the raycast experiment, or PreconfiguredCarPhysics (the LuigiGameDev Car Controller bridge) - and leave the others empty. All three expose the same SetInputEnabled(bool), so this just calls whichever one is set instead of forcing them onto a shared interface (they're deliberately kept as separate, independent experiments).")]
    [SerializeField] private TruckWheelDrive wheelColliderTruck;
    [SerializeField] private TruckNewTEST raycastTruck;
    [SerializeField] private PreconfiguredCarPhysics preconfiguredCarTruck;
    [SerializeField] private GameObject followCameraObject;
    [SerializeField] private GameObject firstPersonCameraObject;
    [SerializeField] private KeyCode toggleViewKey = KeyCode.C;

    [Header("Seats — index 0 (Driver) is the only one that takes driving control")]
    [SerializeField] private Transform driverSeat;
    [SerializeField] private Transform passengerSeat;
    [SerializeField] private Transform bedSeatLeft;
    [SerializeField] private Transform bedSeatRight;
    [SerializeField] private Transform exitPoint;

    // Every seat gets its own correctly-seated-eye-height camera (same recipe as firstPersonCameraObject
    // above: a child of the seat with Camera + AudioListener + TruckFirstPersonLook). Without this, the
    // player's own standing-height camera pokes up through the roof when just the capsule is pinned to
    // a seated Y - that was the "ends up on the roof" bug.
    [Header("Seat cameras")]
    [SerializeField] private GameObject passengerCameraObject;
    [SerializeField] private GameObject bedLeftCameraObject;
    [SerializeField] private GameObject bedRightCameraObject;

    [Header("UI")]
    [SerializeField] private GameObject enterPromptUI;

    [Tooltip("Radius of the small trigger sphere spawned at each seat empty at Start - this is what makes 'press E' enter the seat you're actually standing next to, instead of whichever seat is nearest across one shared trigger spanning the whole vehicle.")]
    [SerializeField] private float seatEntryRadius = 1.2f;

    // Per-seat "is a player standing next to this seat right now" - set by the small SeatEntryTrigger
    // spheres SetupSeatTriggers() spawns at each seat empty. Replaces the old single shared trigger +
    // nearest-seat-by-distance approach, which always resolved to the bed because that one big trigger
    // covered the whole vehicle and the bed seats sat closest to its middle.
    private readonly bool[] _seatInRange = new bool[4];
    private int _mySeatIndex = -1; // -1 = this client's local player isn't seated
    private bool firstPersonActive;
    private GameObject playerRoot;
    private Vector3 _enterPosition; // where the player stood when getting in (exit fallback)

    // Cached player components frozen while in the vehicle
    private CharacterController _cc;
    private StarterAssets.FirstPersonController _fpc;
    private StarterAssetsInputs _inputs;
    private PlayerInput _playerInput;
    private Camera _playerCamera;
    private AudioListener _playerAudioListener;
    private Renderer[] _playerRenderers;
    private Collider[] _playerColliders;

    // Online: per-seat occupant, server-authoritative. 0 = empty, otherwise (clientId + 1) so
    // client 0 (the host) is still distinguishable from "empty". Only the driver seat also
    // transfers NetworkObject ownership (it's the only seat that drives).
    private readonly NetworkVariable<ulong> _driverOccupant = new NetworkVariable<ulong>(0);
    private readonly NetworkVariable<ulong> _passengerOccupant = new NetworkVariable<ulong>(0);
    private readonly NetworkVariable<ulong> _bedLeftOccupant = new NetworkVariable<ulong>(0);
    private readonly NetworkVariable<ulong> _bedRightOccupant = new NetworkVariable<ulong>(0);

    // Lets InteractPrompt find nearby seats without the truck needing to know about the HUD.
    private static readonly List<VehicleEnterExit> _active = new List<VehicleEnterExit>();
    public static IReadOnlyList<VehicleEnterExit> Active => _active;

    private void OnEnable() => _active.Add(this);
    private void OnDisable() => _active.Remove(this);

    public override void OnNetworkSpawn()
    {
        _driverOccupant.OnValueChanged += ApplyOccupantVisual;
        _passengerOccupant.OnValueChanged += ApplyOccupantVisual;
        _bedLeftOccupant.OnValueChanged += ApplyOccupantVisual;
        _bedRightOccupant.OnValueChanged += ApplyOccupantVisual;

        // Late joiners don't get an OnValueChanged callback for seats that were already taken
        // before they connected - apply the current occupants once so those riders start hidden.
        ApplyOccupantVisual(0, _driverOccupant.Value);
        ApplyOccupantVisual(0, _passengerOccupant.Value);
        ApplyOccupantVisual(0, _bedLeftOccupant.Value);
        ApplyOccupantVisual(0, _bedRightOccupant.Value);
    }

    public override void OnNetworkDespawn()
    {
        _driverOccupant.OnValueChanged -= ApplyOccupantVisual;
        _passengerOccupant.OnValueChanged -= ApplyOccupantVisual;
        _bedLeftOccupant.OnValueChanged -= ApplyOccupantVisual;
        _bedRightOccupant.OnValueChanged -= ApplyOccupantVisual;
    }

    // The renderer toggle in EnterVehicle/ExitVehicle only ever runs on the seated player's own
    // machine (NotifySeatRange ignores colliders it doesn't own), so it only stops that player
    // from seeing their own body - everyone else still saw the rider's capsule, which visibly
    // lags behind a fast-moving vehicle since it's a manually-repositioned world-space
    // NetworkTransform, not an actual child of the truck. Hiding the whole body for every
    // machine via these already-synced occupant NetworkVariables sidesteps that lag entirely.
    private void ApplyOccupantVisual(ulong previousOccupant, ulong currentOccupant)
    {
        if (previousOccupant != 0) SetRidingVisible(previousOccupant - 1, true);
        if (currentOccupant != 0) SetRidingVisible(currentOccupant - 1, false);
    }

    private static void SetRidingVisible(ulong clientId, bool visible)
    {
        foreach (var playerGO in GameObject.FindGameObjectsWithTag("Player"))
        {
            var netObj = playerGO.GetComponent<NetworkObject>();
            if (netObj == null || netObj.OwnerClientId != clientId) continue;

            foreach (var renderer in playerGO.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = visible;
            break;
        }
    }

    private void Start()
    {
        SetTruckInputEnabled(false);

        // Unoccupied at scene start - the driving camera has no business being live
        // until someone actually gets in. ExitVehicle() also turns this off, but that
        // only covers the enter->exit round trip, not the initial state.
        if (followCameraObject != null)
            followCameraObject.SetActive(false);

        if (enterPromptUI != null)
            enterPromptUI.SetActive(false);

        SetupSeatTriggers();
    }

    // Spawns a small trigger sphere as a child of each seat empty. Each one only knows its own
    // seatIndex, so entering is "whichever seat's trigger you're standing in", not a distance
    // comparison across every seat on the vehicle.
    private void SetupSeatTriggers()
    {
        for (int i = 0; i < SeatLabels.Length; i++)
        {
            Transform seat = SeatPoint(i);
            if (seat == null) continue;

            var triggerGO = new GameObject("EntryTrigger_" + SeatLabels[i]);
            triggerGO.transform.SetParent(seat, false);
            triggerGO.layer = seat.gameObject.layer;

            var sphere = triggerGO.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = seatEntryRadius;

            triggerGO.AddComponent<SeatEntryTrigger>().Init(this, i);
        }
    }

    private void SetTruckInputEnabled(bool enabled)
    {
        if (wheelColliderTruck != null) wheelColliderTruck.SetInputEnabled(enabled);
        if (raycastTruck != null) raycastTruck.SetInputEnabled(enabled);
        if (preconfiguredCarTruck != null) preconfiguredCarTruck.SetInputEnabled(enabled);
    }

    private Transform SeatPoint(int seatIndex) => seatIndex switch
    {
        0 => driverSeat,
        1 => passengerSeat,
        2 => bedSeatLeft,
        3 => bedSeatRight,
        _ => null
    };

    private NetworkVariable<ulong> OccupantVar(int seatIndex) => seatIndex switch
    {
        0 => _driverOccupant,
        1 => _passengerOccupant,
        2 => _bedLeftOccupant,
        3 => _bedRightOccupant,
        _ => null
    };

    private GameObject SeatCamera(int seatIndex) => seatIndex switch
    {
        0 => firstPersonCameraObject,
        1 => passengerCameraObject,
        2 => bedLeftCameraObject,
        3 => bedRightCameraObject,
        _ => null
    };

    /// <summary>Closest unoccupied in-range seat, or -1 if nothing is both in range and free.
    /// Almost always there's exactly one seat in range at a time (each trigger sphere is small
    /// and seats are spaced apart) - the distance check here is just a tie-breaker for the rare
    /// case two neighboring seat spheres overlap.</summary>
    private int NearestFreeSeatInRange()
    {
        if (playerRoot == null) return -1;

        int best = -1;
        float bestSqrDist = float.MaxValue;
        for (int i = 0; i < SeatLabels.Length; i++)
        {
            if (!_seatInRange[i]) continue;
            if (OccupantVar(i).Value != 0) continue; // taken

            Transform point = SeatPoint(i);
            if (point == null) continue;

            float sqrDist = (point.position - playerRoot.transform.position).sqrMagnitude;
            if (sqrDist < bestSqrDist)
            {
                bestSqrDist = sqrDist;
                best = i;
            }
        }
        return best;
    }

    /// <summary>Used by InteractPrompt: the seat E would put this player in right now, if any.</summary>
    public bool TryGetSeatPrompt(Vector3 playerPosition, out string seatLabel)
    {
        seatLabel = null;
        if (_mySeatIndex >= 0) return false;

        int seat = NearestFreeSeatInRange();
        if (seat < 0) return false;

        seatLabel = SeatLabels[seat];
        return true;
    }

    // Called by each seat's SeatEntryTrigger. Online: ignore players we don't own, same check
    // the old shared-trigger OnTriggerEnter used to do.
    public void NotifySeatRange(int seatIndex, Collider other, bool entered)
    {
        var netObj = other.transform.root.GetComponent<Unity.Netcode.NetworkObject>();
        if (netObj != null && netObj.IsSpawned && !netObj.IsOwner) return;

        _seatInRange[seatIndex] = entered;
        if (entered)
            playerRoot = other.transform.root.gameObject;

        if (enterPromptUI == null || _mySeatIndex >= 0) return;

        bool anyInRange = false;
        foreach (bool inRange in _seatInRange)
            if (inRange) { anyInRange = true; break; }
        enterPromptUI.SetActive(anyInRange);
    }

    private void Update()
    {
        // Online: Keep the rider's body in the seat so other players see them ride along.
        // (The capsule is what's network-synced, so move it rather than the root.) Every seat
        // now has its own dedicated camera (see SeatCamera), which handles look-around locally
        // via TruckFirstPersonLook - so the capsule itself can be fully pinned (position +
        // rotation) for all seats, not just the driver.
        if (_mySeatIndex >= 0 && _cc != null)
        {
            Transform seat = SeatPoint(_mySeatIndex);
            _cc.transform.SetPositionAndRotation(seat.position, seat.rotation);
        }

        if (_mySeatIndex == DriverSeatIndex && Input.GetKeyDown(toggleViewKey))
            ToggleCameraView();

        if (!Input.GetKeyDown(KeyCode.E)) return;

        if (_mySeatIndex >= 0)
        {
            ExitVehicle();
            return;
        }

        int seatIndex = NearestFreeSeatInRange();
        if (seatIndex >= 0) EnterVehicle(seatIndex);
    }

    private void ToggleCameraView()
    {
        // Nothing to switch to if this truck doesn't have a first-person camera assigned.
        if (firstPersonCameraObject == null) return;

        firstPersonActive = !firstPersonActive;
        followCameraObject.SetActive(!firstPersonActive);
        firstPersonCameraObject.SetActive(firstPersonActive);
    }

    private void EnterVehicle(int seatIndex)
    {
        bool isDriver = seatIndex == DriverSeatIndex;

        // Online: ask the host for the seat (and, for the driver, ownership of the truck so
        // this machine's driving is what gets synced to everyone). Entering happens optimistically
        // below rather than waiting for the server's ack - same simplification the single-seat
        // version used; a simultaneous press by two players on the same seat is a known edge case.
        if (IsSpawned)
            RequestSeatRpc(seatIndex);

        // Cache and disable player components instead of deactivating the whole GO.
        // Deactivating a NetworkObject causes NGO lifecycle conflicts.
        // Components live on PlayerCapsule (child of NestedParent root), not the root itself.
        _cc = playerRoot.GetComponentInChildren<CharacterController>(true);
        _fpc = playerRoot.GetComponentInChildren<StarterAssets.FirstPersonController>(true);
        _inputs = playerRoot.GetComponentInChildren<StarterAssetsInputs>(true);
        _playerInput = playerRoot.GetComponentInChildren<PlayerInput>(true);
        _playerCamera = playerRoot.GetComponentInChildren<Camera>(true);
        _playerAudioListener = playerRoot.GetComponentInChildren<AudioListener>(true);
        _playerRenderers = playerRoot.GetComponentsInChildren<Renderer>(true);
        _playerColliders = playerRoot.GetComponentsInChildren<Collider>(true);

        // The CharacterController above isn't the only collider on the player - PlayerCapsule
        // has a separate solid CapsuleCollider (distinct from the CC) on its "Capsule" child.
        // Leaving it enabled while the capsule gets teleported into the seat every frame means
        // it keeps physically overlapping the truck's own chassis collider, and since a Rigidbody
        // can't push a collider that's reset back every frame, the truck gets shoved instead.
        if (_cc != null) _cc.enabled = false;

        // Disable FPC first so its Update can't call Move() on a CC we're about to deactivate.
        // Every seat swaps to its own dedicated camera (SeatCamera), so the player's own camera/
        // input/controller get frozen the same way regardless of which seat this is - only the
        // driver additionally takes over the truck's own controls (below).
        if (_fpc != null) _fpc.enabled = false;
        if (_inputs != null) _inputs.enabled = false;
        if (_playerInput != null) _playerInput.enabled = false;

        // The player's own camera must not stay live once a seat camera takes over - otherwise
        // both cameras (and both AudioListeners) are active at once, and since the seat position
        // is a seated Y (not the player's full standing height), the leftover standing camera
        // ends up poking up above the vehicle roof instead of showing the seat's own eye line.
        if (_playerCamera != null) _playerCamera.enabled = false;
        if (_playerAudioListener != null) _playerAudioListener.enabled = false;

        // The capsule keeps getting repositioned to the seat every frame (above, for other players
        // to see), but nothing stops its own mesh from rendering - without this the player's body
        // would double-render oddly from its own first-person view. Toggling Renderer.enabled (not
        // the GameObjects) keeps Animator/scripts intact for when NGO observers need the pose, it
        // just stops drawing it locally.
        if (_playerRenderers != null)
        {
            foreach (var renderer in _playerRenderers)
                if (renderer != null) renderer.enabled = false;
        }

        if (_playerColliders != null)
        {
            foreach (var col in _playerColliders)
                if (col != null) col.enabled = false;
        }

        Transform seat = SeatPoint(seatIndex);
        _enterPosition = _cc != null ? _cc.transform.position : playerRoot.transform.position;

        // Online: Move the capsule (the part that actually moves/syncs), not the root.
        if (_cc != null) _cc.transform.SetPositionAndRotation(seat.position, seat.rotation);
        else playerRoot.transform.position = seat.position;

        if (isDriver)
        {
            SetTruckInputEnabled(true);

            // E puts you straight in the driver's seat view; C can still flip to the chase cam.
            // (Only the driver has a chase-cam option - passengers/bed riders just get their seat cam.)
            firstPersonActive = firstPersonCameraObject != null;
            followCameraObject.SetActive(!firstPersonActive);
        }

        GameObject seatCamera = SeatCamera(seatIndex);
        if (seatCamera != null) seatCamera.SetActive(true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (enterPromptUI != null)
            enterPromptUI.SetActive(false);

        _mySeatIndex = seatIndex;
    }

    private void ExitVehicle()
    {
        int seatIndex = _mySeatIndex;
        bool wasDriver = seatIndex == DriverSeatIndex;

        if (wasDriver)
        {
            SetTruckInputEnabled(false);
            followCameraObject.SetActive(false);
        }

        GameObject seatCamera = SeatCamera(seatIndex);
        if (seatCamera != null) seatCamera.SetActive(false);

        // Online: Free the seat for other players.
        if (IsSpawned) ReleaseSeatRpc(seatIndex);

        // Exit onto solid ground even if the truck is on its side or upside down: ExitPoint turns
        // with the truck and can end up underground. Try spots around the truck, then where we got
        // in, then the nearest spawn point; the old ExitPoint raycast is the last resort.
        Vector3 spawnPos = exitPoint.position;
        float radius = _cc != null ? _cc.radius : 0.5f, height = _cc != null ? _cc.height : 1.8f;
        if (!SafeGround.TryFindExit(transform, radius, height, out spawnPos) &&
            !SafeGround.TryGroundAt(_enterPosition, _enterPosition.y, transform, radius, height, out spawnPos, 1.5f) &&
            !SafeGround.TryNearestSpawn(_enterPosition, radius, height, out spawnPos))
        {
            spawnPos = exitPoint.position;
            if (Physics.Raycast(exitPoint.position + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 15f))
                spawnPos = hit.point + Vector3.up * 0.1f;
        }

        // Stand the player upright, facing the truck's heading. While seated the capsule copies the
        // seat's rotation, so after a flip it is upside down - and an upside-down CharacterController
        // has its capsule below the feet, inside the ground, so the player sinks through the terrain.
        var body = _cc != null ? _cc.transform : playerRoot.transform;
        body.SetPositionAndRotation(spawnPos, SafeGround.Upright(transform));
        Physics.SyncTransforms();

        if (_cc != null) _cc.enabled = true;
        if (_fpc != null) _fpc.enabled = true;
        if (_inputs != null) _inputs.enabled = true;
        if (_playerInput != null) _playerInput.enabled = true;
        if (_playerCamera != null) _playerCamera.enabled = true;
        if (_playerAudioListener != null) _playerAudioListener.enabled = true;
        if (_playerRenderers != null)
        {
            foreach (var renderer in _playerRenderers)
                if (renderer != null) renderer.enabled = true;
        }
        if (_playerColliders != null)
        {
            foreach (var col in _playerColliders)
                if (col != null) col.enabled = true;
        }

        _mySeatIndex = -1;

        // The exit teleport moves the player well clear of every seat sphere, but physics only
        // reconciles that next step - clear the flags now so the prompt doesn't flash stale.
        System.Array.Clear(_seatInRange, 0, _seatInRange.Length);
    }

    // ---------------------------------------------------------------------
    // Online: Runs on the host: hand a seat to whoever asked for it.
    // ---------------------------------------------------------------------
    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void RequestSeatRpc(int seatIndex, RpcParams rpcParams = default)
    {
        NetworkVariable<ulong> occupant = OccupantVar(seatIndex);
        if (occupant == null || occupant.Value != 0) return; // invalid seat, or someone got there first

        ulong sender = rpcParams.Receive.SenderClientId;
        occupant.Value = sender + 1;

        if (seatIndex == DriverSeatIndex)
            NetworkObject.ChangeOwnership(sender);
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void ReleaseSeatRpc(int seatIndex)
    {
        NetworkVariable<ulong> occupant = OccupantVar(seatIndex);
        if (occupant != null) occupant.Value = 0;
    }
}
