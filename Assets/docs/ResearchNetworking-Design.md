# Networked Research System — What Was Built

Built on the `Networkcleanup` branch. Makes the anemometer, binoculars/`Scannable`, and the
walk-over `ResearchPickup` coins all feed one **shared, summed** research total instead of each
player having their own private, unsynced bar. Follow-up to
[`CloudResearch-Explainer.md`](CloudResearch-Explainer.md), whose §5 originally flagged this as
unaddressed.

---

## 1. The model: one team pool, host-authoritative

If you finish an anemometer reading and your friend is in the same session, your friend's bar
goes up too - it's the same bar. Whoever completes a reading/scan/pickup reports it to the host;
the host is the only one who actually changes the total; the host then tells everyone (including
whoever triggered it) what the new total is. Standard "server decides, broadcasts the result"
shape, same one `RoundManager` already uses elsewhere in this project.

### Why follow `RoundManager`'s pattern instead of `NetworkVariable`/RPCs

`ResearchManager` lives on the player prefab (`PlayerPrefab2`/`MainPlayer Variant`), not on a
spawned, dedicated `NetworkObject` of its own - RPCs and `NetworkVariable` need a `NetworkBehaviour`
on a `NetworkObject` to hang off. `RoundManager` hits the exact same constraint (it's a
`DontDestroyOnLoad` scene singleton, not a `NetworkObject`) and already solved it with NGO's
`CustomMessagingManager`: plain named messages, `SendNamedMessage` client→host,
`SendNamedMessageToAll` host→everyone. `ResearchManager` now uses the same mechanism, registered
the same way (`Register()`/`Unregister()` watching `NetworkManager.Singleton`, swapping which
messages it listens for based on `IsServer`).

## 2. What changed, file by file

### `ResearchManager.cs`
- `AddResearch(amount, source, sourceIndex)` - the three existing call sites (`AnemometerResearch`,
  `BinocularScanner` via `Scannable`, `ResearchPickup`) call this the same way as before; `source`
  defaults to `Source.Anemometer` and `sourceIndex` to `-1`, so `AnemometerResearch`'s call site
  didn't need to change at all.
- On the host (or offline, where `IsAuthority` is always true): applies the gain directly,
  updates the bar, and broadcasts the new total to every client (`Research.Credit` message).
- On a client: sends a `Research.Gain` message to the host instead of touching the bar itself -
  the bar only ever moves in response to the host's broadcast, so every machine's bar reflects
  the same number.
- `Source.Scan`/`Source.Pickup` carry a `sourceIndex` (see `SceneIndex` below) so the host can
  refuse a second report for the same cloud/coin (`HashSet<int>.Add` returns `false` if it's
  already there) - whoever's report lost the race just gets ignored, and still sees the correct
  result because the winner's broadcast reaches them too.
- A new `onSourceConsumed` event replays "object N of this kind is done" to every machine,
  including machines that never touched that object - this is what lets `Scannable`/
  `ResearchPickup` update their own visuals (tint, destroy) in response to someone else's scan.
- `OnClientConnected` catches a late joiner up: current total, plus every already-consumed
  Scan/Pickup index, so they don't start at an empty bar or get to double-collect something
  already gone.

### `SceneIndex.cs` (new)
Scene-placed objects like `Cloud` or a coin pickup aren't spawned `NetworkObject`s, so they have
no NGO-assigned network ID to report over the wire. `SceneIndex.Of<T>(this)` sorts every `T` in
the scene by full hierarchy path and returns this object's position - identical on every client
since everyone loads the same scene. That index is what `Research.Gain`/`Research.Credit`
messages use to mean "this specific cloud" without needing a manual ID or a `NetworkObject`.

One caveat worth remembering when placing new scannables/pickups: two objects of the same type
with the *exact* same hierarchy path (e.g. two children both literally named `Cloud` under the
same parent) would tie in that sort. Keep sibling names distinct.

### `Scannable.cs`
`IsScanned` is no longer set directly by `BinocularScanner`. Instead:
- `BinocularScanner` calls the new `ReportScanned()` once its local fill bar completes.
- `ReportScanned()` calls `ResearchManager.AddResearch(researchValue, Source.Scan, myIndex)`.
- `IsScanned` only actually flips, and `onScanned` only actually fires, when `ResearchManager`
  confirms consumption via `onSourceConsumed` - which happens for every client that has this
  `Scannable` in its scene, not just whoever scanned it.
- Subscribes to `ResearchManager.Instance` via the same "wait a frame until it exists" coroutine
  `TornadoSpawner` already used, since `ResearchManager` is spawned at runtime with the player.

### `ResearchPickup.cs`
Same shape as `Scannable`: `OnTriggerEnter` now *reports* a pickup instead of immediately calling
`Destroy(gameObject)`. The object only actually disappears (on every client) once
`ResearchManager` confirms it via `onSourceConsumed` - so two players walking over the same coin
within the same network round-trip can't both bank it.

### `BinocularScanner.cs`
One line: `target.MarkScanned(); ResearchManager.Instance?.AddResearch(...)` became
`target.ReportScanned();` - the credit-and-mark-scanned sequencing moved inside `Scannable`/
`ResearchManager` where the dedup logic lives.

### `AnemometerResearch.cs`
No changes. It has no world object to dedupe against, so it was already calling the one
signature that still works unmodified.

## 3. What this fixes vs. what it doesn't

- **Fixed:** a reading/scan/pickup from any player now adds to one visible, shared total on
  every connected player's HUD bar, instead of silently only updating the local scorer's own
  unsynced copy.
- **Fixed as a side effect:** `RoundManager.PollResearch()` already reports each client's local
  `ResearchManager.Instance.Progress` to the host and keeps the max seen, purely to decide
  round win/loss - no changes were needed there, since once `Progress` is itself a synced shared
  value, every client reports the same number and "max across clients" is trivially that number.
- **Not addressed (pre-existing, flagged in `CloudResearch-Explainer.md` §5):**
  `ResearchManager`'s `Awake()` dedupe picks whichever player object's copy spawns first on a
  given client - in multiplayer that could be a remote player's clone rather than your own, since
  nothing currently disables non-owned clones' HUD the way `PlayerNetworkSetup` already disables
  their camera/controls. Didn't fix this now since it's a separate "where does per-player UI
  live" cleanup, not required for the shared total itself to work correctly - `NetworkManager`
  is a true global singleton regardless of which GameObject hosts `ResearchManager`, so the
  message plumbing above works correctly either way.
- **Not addressed, worth a look before `TornadoSpawner` is ever wired into a scene** (per
  existing memory, it currently isn't): `TornadoSpawner` listens for
  `ResearchManager.onResearchComplete` and calls `Instantiate()` locally, with no server check.
  Once research completion is a synced broadcast, every client's `onResearchComplete` now fires
  at roughly the same moment - which means every client would independently spawn its *own*
  local tornado GameObject, not one shared tornado. That needs the spawn itself gated to
  `IsAuthority` and turned into an actual networked spawn before it's safe to use online; out of
  scope here since the system isn't in any scene yet.
- **Not addressed:** round-restart reset. Neither the old nor new `ResearchManager` resets
  `_completed` or the consumed-object sets when `RoundManager.StartOver()` reloads the gameplay
  scene. Didn't chase this down - it depends on whether the player prefab (and so
  `ResearchManager`) actually survives that scene reload at all, which wasn't something this
  change touched or needed to verify.
