# TruckRoot — How It's Built, and Why It Slides

This is a plain-English walkthrough of everything under `Assets/Prefabs/TruckRoot.prefab` and
`Assets/TruckController.cs`, written to answer two questions: **how was this actually assembled**,
and **why does it just slide forward and refuse to stop**.

All fileIDs below are the internal Unity references inside the `.prefab`/`.unity` YAML, included so
you (or I, later) can jump straight to the right block with a text search instead of hunting through
the Inspector.

---

## 1. Prefab hierarchy

```
TruckRoot                              (ROOT — Rigidbody, 3x BoxCollider, scripts, netcode)
├─ PickUpTruck                         (visual mesh root — body, doors, dash)
│  ├─ L Door / R Door
│  ├─ Speedometer, Tachometer          (dashboard gauge meshes — not driven by any script)
│  ├─ Stearing                         (steering wheel mesh — not rotated by any script) [sic]
│  ├─ DriversSeat / PassengerSeat
│  │  └─ SeatsVisual
│  │     ├─ DriverSeatMesh  (BoxCollider, non-trigger)
│  │     └─ PassengerSeatMesh (BoxCollider, non-trigger)
│  ├─ FL Tire / FR Tire / BL Tire / BR Tire   (wheel MESHES — visual only, no physics)
│  └─ Camera / FollowCamera             (driving camera anchor)
├─ WheelCollider_FL  (WheelCollider)
├─ WheelCollider_FR  (WheelCollider)
├─ WheelCollider_BL  (WheelCollider)
├─ WheelCollider_BR  (WheelCollider)
├─ CentreOfMass                         (empty transform, used as rb.centerOfMass)
├─ ExitPoint                            (empty transform, where the player is placed on exit)
└─ (DriversSeat transform reference used by VehicleEnterExit as the seat/enter position)
```

The **wheel colliders are separate empties**, siblings of the visual mesh tree, positioned to line
up with the tire meshes. This is the standard Unity vehicle pattern: `WheelCollider` does the physics
and never rotates/moves the tire mesh itself — a script has to copy the collider's computed pose onto
the visual tire's `Transform` every frame. That's what `UpdateWheelVisual()` in the script does.

## 2. Components on the `TruckRoot` GameObject itself

| Component | Key values | Purpose |
|---|---|---|
| `Rigidbody` | mass 1200, linearDamping **0.5**, angularDamping 0.05, not kinematic, Discrete collision, no interpolation | The body's physics — **but see §4, these Inspector values are overwritten at runtime** |
| `BoxCollider` (trigger) | size 5.0 × 1.5 × 6.7, centered ~(0, 0.8, 0) | The "player is near the truck" trigger that `VehicleEnterExit` listens to for Enter/Exit |
| `TruckWheelDrive` (file `TruckController.cs`) | motorTorque 2200, brakeTorque 12000, maxSteerAngle 24°, maxSpeedKmh 85, downforce 60, antiRollFront/Rear 8000 | The driving physics |
| `VehicleEnterExit` | wires seat/exit/camera/prompt | Handles getting in and out of the truck |
| `NetworkObject` | `Ownership: 1` | Unity **Netcode for GameObjects** identity — this truck is a networked object |
| `NetworkTransform` | `AuthorityMode: 0` (Server) | Replicates the truck's transform to other clients |

Two more `BoxCollider`s exist elsewhere in the hierarchy: `DriverSeatMesh` and `PassengerSeatMesh`,
both plain 1×1×1 boxes (default size, i.e. never actually sized to the seat mesh — likely a leftover
from adding the component and not shaping it).

## 3. The 4 `WheelCollider`s

All four are configured identically:

| Property | Value |
|---|---|
| Radius | 0.42 m |
| Suspension spring / damper / target | 25000 / 3500 / 0.5 |
| **Suspension distance** | **0.15 m** |
| Mass | 20 kg |
| Wheel damping rate | 0.25 |
| Forward friction (extremum slip/value, asymptote slip/value, stiffness) | 0.4 / 1 / 0.8 / 0.5 / **1** |
| Sideways friction (same shape) | 0.2 / 1 / 0.5 / 0.75 / **1** |

Friction stiffness of `1` on both axes is Unity's un-tuned default — reasonable as a starting point,
not the problem by itself. The suspension travel (0.15 m) is the thing that stands out: for a
1200 kg body, that's a short, stiff spring, which matters below.

## 4. `TruckWheelDrive` (`Assets/TruckController.cs`) walkthrough

*(Note: the file is `TruckController.cs` but the class inside is `TruckWheelDrive` — harmless, but
worth renaming one to match the other so a search for "TruckController" doesn't come up empty.)*

- **`Awake()`**
  1. Calls `SnapToGround()` (see §5) to reposition the truck before physics starts.
  2. Grabs the `Rigidbody` and then **hard-codes** `rb.mass = 1200`, `rb.linearDamping = 0.05`,
     `rb.angularDamping = 0.5`, `rb.interpolation = Interpolate`, `rb.collisionDetectionMode = Continuous`.
  3. Sets `rb.centerOfMass` from the `CentreOfMass` transform.

  **This silently overwrites whatever is set on the Rigidbody component in the Inspector.** The
  Inspector currently shows `linearDamping: 0.5`, but the moment the truck wakes up, that becomes
  `0.05` — a 10x reduction in linear drag — no matter what anyone tunes in the prefab. Whoever is
  tuning "how much the truck coasts" by editing the Rigidbody in the Inspector is tuning a value
  that gets thrown away every time the scene runs.

- **`Update()`** — reads raw input (`Input.GetAxisRaw("Vertical"/"Horizontal")`,
  `Input.GetKey(KeyCode.Space)`) **only if `inputEnabled` is true**, and always re-poses the 4 visual
  wheel meshes from their colliders.

- **`FixedUpdate()`** — in order: steering, motor, brakes, downforce, then anti-roll front/rear.

- **`ApplyMotor()`** — if braking, zero motor torque and return. Otherwise apply `moveInput * motorTorque`
  to the two rear `WheelCollider`s, capped by `maxSpeedKmh`.

- **`ApplyBrakes()`** — if `isBraking`, sets `brakeTorque` (12000) on all 4 wheels **and** manually
  multiplies `rb.linearVelocity` by `0.94` every physics step. At the project's fixed timestep
  (0.02 s → 50 steps/sec), holding the brake should cut velocity to ~5% of its starting value within
  about 1 second — this is an aggressive, code-level "give me a car that actually stops" hack layered
  on top of the normal wheel brake torque.

- **`SnapToGround()`** — see §5, this runs once, in `Awake()`, before the Rigidbody reference is
  even fetched.

- **`OnDrawGizmos()` / `DrawWheelDiagnostic()`** — explicitly labeled `// TEMP DIAGNOSTIC ... Remove
  once the terrain issue is found`. **This comment is still in the code**, which means whoever wrote
  it did not consider the terrain/grounding issue solved yet.

## 5. `SnapToGround()` — likely still broken

```csharp
Vector3 rayOrigin = transform.position + Vector3.up * 50f;
Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 1000f)
```

No `LayerMask`, no `QueryTriggerInteraction`. I checked `ProjectSettings/DynamicsManager.asset`:

```
m_QueriesHitTriggers: 1
```

Raycasts hit trigger colliders project-wide by default. `TruckRoot` itself carries a **trigger**
`BoxCollider` sized 5.0 × 1.5 × 6.7, centered right over its own body (the enter/exit zone from
§2). A ray fired straight down from 50 m above the truck's own pivot will pass directly through that
box on the way to the real terrain — and `Physics.Raycast` returns the *first* hit, not the ground.
So this raycast has a strong chance of hitting **the truck's own trigger collider** and computing the
spawn height from that, not from the actual ground underneath.

That would explain the wheel diagnostic gizmos being left in the code: if the snap height is wrong,
wheels sit too high (floating — bouncing on landing, near-zero suspension load, near-zero grip) or
too low (embedded — the physics solver has to shove the body back up, imparting velocity).

## 6. `VehicleEnterExit.cs` (`Assets/Scripts/VehicleEnterExit.cs`)

Plain `MonoBehaviour` (not network-aware) that:
- Shows an enter prompt when a player who **owns** their own `NetworkObject` walks into the trigger.
- On `E`: disables the player's `CharacterController`/`FirstPersonController`/input, teleports them
  to `driverSeat`, and calls `truckController.SetInputEnabled(true)`.
- On exit: re-raycasts from above `exitPoint` to place the player back on real terrain, re-enables
  their controller/input.

Two things worth knowing:
- **Input is gated on `inputEnabled`, and `inputEnabled` starts `false`.** Since `isBraking` is read
  through the same gate as throttle/steer (see `Update()` in §4), **an unoccupied truck has zero
  brake torque available, always** — it can coast/slide but nothing can arrest it until a player is
  physically sitting in the driver's seat. There is no parking brake.
- The scene's saved `PrefabInstance` overrides for this component still list fields that no longer
  exist on the class — `playerRoot`, `playerInput`, `playerObject`, `playerController`,
  `characterController`, `playerCameraObject`. The current script caches these itself at runtime via
  `GetComponentInChildren<>()` instead of taking them as serialized references. These leftover entries
  are harmless (Unity ignores modifications to properties that no longer exist) but they're a sign
  the script was refactored after the scene was last saved with it selected — worth a resave to clear
  the clutter.

## 7. Netcode: `NetworkObject` + `NetworkTransform`

The truck is a **networked object** (`Unity.Netcode.NetworkObject`, `AuthorityMode: 0` = Server on
its `NetworkTransform`), but:
- `TruckWheelDrive` is a plain `MonoBehaviour`, not a `NetworkBehaviour` — it has **no ownership
  check anywhere**. Every machine that has this object loaded runs its own local physics simulation
  against local input.
- Nothing in `VehicleEnterExit` ever calls `NetworkObject.ChangeOwnership(...)`. Ownership never
  transfers to whoever gets in and drives.
- There's no `NetworkRigidbody` — a plain `Rigidbody` is being driven locally and only its resulting
  `Transform` gets synced by `NetworkTransform`.

**If you only ever test this solo/host, none of this matters** — the host is the server, so its own
local simulation *is* the authoritative one, and `NetworkTransform` never has anything to correct.
But if you test with a second client driving the truck: that client's local physics moves its own
Rigidbody from its own input, while `NetworkTransform` keeps trying to snap/interpolate the visible
transform back to whatever the **server's** copy of the truck is doing (which never received that
client's brake/throttle input at all). Local sim pushing one way, network correction pulling another,
with no reconciliation between them, produces exactly "it slides and nothing I press stops it" —
because what you're doing to your local Rigidbody isn't the copy that matters.

**Diagnostic**: if the sliding happens in solo/host testing too, this is not the cause — skip to §8.
If it only happens for a non-host client, this is almost certainly it.

## 8. Ranked causes of "it slides forward and won't stop"

1. **`Awake()` forces `linearDamping` down to `0.05`** (§4) regardless of the Inspector value. Very
   little natural drag once rolling — reproducible solo, no netcode/terrain needed.
2. **`SnapToGround()` likely self-hits the truck's own trigger collider** (§5), placing the truck at
   the wrong height so the wheels don't sit correctly on the ground — inconsistent suspension load
   means inconsistent grip, so brake torque doesn't reliably translate into a stopping *force*.
3. **No parking brake — brake torque is only ever non-zero while `inputEnabled` is true** (§6), so
   any slide that starts before/without a driver seated (e.g. from #1/#2 above, or spawning on a
   slope) cannot be stopped at all until someone gets in.
4. **Short, stiff suspension** (0.15 m travel, §3) on uneven prototype terrain invites the wheels to
   skip/lose contact rather than track the ground, which reads as intermittent traction loss.
5. **(Multiplayer only)** No ownership/authority handoff on enter (§7) — local physics vs. network
   correction fighting each other.

## 9. Fixes, roughly in the order I'd do them

1. **Stop overwriting Rigidbody values in `Awake()`.** Either delete those four lines and just tune
   mass/damping/interpolation/collision-detection on the Rigidbody component directly, or — if the
   intent is "guarantee these values regardless of what a prefab variant has" — keep them, but also
   update the Inspector-visible values to match so nobody is fooled, and comment *why* it's forced.
2. **Fix `SnapToGround()`**: add `QueryTriggerInteraction.Ignore` and/or a ground-only `LayerMask` so
   it can't hit the truck's own trigger collider (or any other trigger).
   ```csharp
   Physics.Raycast(rayOrigin, Vector3.down, out hit, 1000f, groundMask, QueryTriggerInteraction.Ignore)
   ```
3. **Add a parking brake.** When `!inputEnabled`, drive `brakeTorque` to max on all four wheels
   instead of leaving it at 0 — right now "no driver" means "no gas *and* no brakes," which should
   be reversed.
4. **Loosen the suspension a bit** — try `suspensionDistance` ~0.25–0.35 and re-check the spring;
   short + stiff on rough terrain is what causes wheels to skip.
5. **If this needs to work in multiplayer**: transfer ownership of the truck's `NetworkObject` to the
   driver on enter and back to the server (or nobody) on exit, and gate `TruckWheelDrive`'s
   input/physics so only the authoritative side runs it (either make it a `NetworkBehaviour` with an
   `IsOwner` guard, or add a `NetworkRigidbody` so the physics itself is replicated instead of just
   the end transform).
6. **Remove the temp diagnostic gizmos** once you're confident the grounding issue is actually gone
   (they're clearly marked as intended to be removed).
7. **Resave the scene** with `VehicleEnterExit` selected once, to drop the stale serialized fields
   left over from its refactor (§6) — cosmetic, but keeps future diffs honest.
8. Minor: rename `TruckController.cs` ↔ `TruckWheelDrive` so the file and class names match.

## 10. Already fixed in the current diff (nothing to do here)

- `backLeftWheel` used to be wired to the same transform as `backRightWheel` (both pointed at the
  "BR Tire" mesh), so the back-left wheel never got its visual updated. It's now correctly wired to
  "BL Tire". Purely visual — not related to the sliding.

## 11. Steering / center-of-mass / "gravity feels off" pass (2026-09-11)

Separate complaint from the sliding issue above: steering felt sluggish and the truck's handling felt
generally wrong ("gravity feels off"), independent of whether it could stop. Found and fixed:

- **`CentreOfMass` was placed underground.** Its local Y was `-0.5` (scene override even had it at
  `-1.35` at one point) on a truck whose `WheelCollider`s sit at local Y ≈ 0.45 with radius 0.42 —
  i.e. the wheel *bottoms* are at Y ≈ 0.03. A center of mass at Y −0.5 is half a meter **below the
  tires**, outside the vehicle envelope entirely (the body's `BoxCollider` itself spans Y 0.05–1.55).
  Every torque/suspension/anti-roll calculation was balancing around a point that isn't inside the
  truck, which is what read as "gravity is off" — odd pitching/rolling response, suspension fighting
  itself. Moved to local Y `0.35` (low in the chassis, but actually inside it) in both the prefab and
  the scene's `PrefabInstance` override.
- **Steering used a `Lerp` factor instead of a rate.** `ApplySteering()` did
  `Mathf.Lerp(current, target, steerSmoothness * Time.fixedDeltaTime)` with `steerSmoothness = 5` —
  at the 0.02s fixed timestep that's a lerp factor of 0.1 per physics step, closing only 10% of the
  gap to full lock each step and taking ~1 full second to reach full lock. Replaced with
  `Mathf.MoveTowards` driven by a flat `steerSpeed` (renamed field, 120°/s), which reaches full lock
  in ~0.2s and is frame-rate independent.
- **Suspension `targetPosition`/`forceAppPointDistance` change — tried, reverted, was wrong.**
  Set `targetPosition: 0.5 → 0` and `forceAppPointDistance: 0.1 → 0.3` on all four wheels based on
  general "Unity's defaults are bad" advice, without checking the direction against this truck's
  actual spring/damper values. This was a mistake: Unity's spring force is
  `spring * (targetPosition - compression)`, so at `targetPosition: 0` there is **no compression
  value that produces a positive (supporting) force** — the spring can only ever push at or below
  zero, so every wheel bottoms out and goes rigid under the truck's own weight, all the time. With
  no suspension travel left to absorb weight transfer, the first turn taken at any speed had nothing
  to stop the inside wheel(s) from lifting and the truck tipping over and staying tipped (no spring
  event left to right it), with the now-airborne wheels free-spinning under motor torque. **Reverted
  both values to their originals** (`targetPosition: 0.5`, `forceAppPointDistance: 0.1`) — those are
  known to actually hold the vehicle's weight, even if they're the twitchier Unity-default feel.
  If suspension feel still needs work, the safe lever is the spring/damper magnitude, not
  `targetPosition` — change one value at a time and drive it before touching the next.
- **`ApplyDownforce()` used full 3D velocity magnitude**, so vertical bounce velocity from the
  suspension (landing, going over a bump) spiked downforce too, feeding back into more compression —
  part of the "off" feel. Switched to `Vector3.ProjectOnPlane(rb.linearVelocity, transform.up)` so
  only forward/lateral speed drives downforce.
- **Braking double-dipped.** On top of `brakeTorque` (12000), `ApplyBrakes()` also did
  `rb.linearVelocity *= 0.94f` every `FixedUpdate` — a non-physical hard stop layered on the real
  wheel brake torque, making stops feel like hitting a wall. Removed; braking now goes entirely
  through `WheelCollider.brakeTorque`.
- **Motor torque was sports-car aggressive for a "truck."** 2200 Nm over 0.42 m wheels on a 1200 kg
  body works out to ~0.89g of acceleration. Dropped to 1400 Nm (~0.55g).
- Anti-roll bars had been cranked to 8000 front/rear in the scene override, almost certainly as a
  band-aid for the instability the underground center of mass was causing. Dropped back to 5000/5000
  now that the center of mass is fixed; revisit if it still feels too stiff/soft.

None of this required touching §1–§9 above — those are about the separate sliding/netcode issue and
are still accurate except where noted (e.g. §2's antiRollFront/Rear values and §3's suspension numbers
are now stale — see the current prefab for live values).
