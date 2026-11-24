# Cloud Research — Binoculars Scanning & HUD Research Bar

A plain-English walkthrough of the cloud-scanning feature built in `Assets/Scenes/Features/CloudsReaserchBar.unity`: look at a cloud through the binoculars, hold on it while a scan bar fills, and each completed scan adds a chunk to a persistent HUD research bar. Covers what was built, how the pieces connect, and every bug hit (and fixed) getting it working.

---

## 1. The gameplay loop

1. Player selects Binoculars from the hotbar (slot 2 by default) and holds left-click to zoom.
2. While zoomed, a raycast from the camera checks what's dead-center in view.
3. If it's hitting a `Scannable` object (e.g. the `Cloud`) that hasn't been scanned yet, a green fill bar appears and climbs to 100% over `scanDuration` seconds.
4. On completion, that object is marked scanned (won't re-trigger) and its `researchValue` is added to a red research bar that always lives on the HUD.
5. Look away, release the mouse, or the target gets scanned — the green bar resets/hides.

## 2. New scripts (`Assets/Scripts/Research/`)

### `Scannable.cs`
Drop this on anything that should be scannable (currently just `Cloud`).

| Field | Purpose |
|---|---|
| `researchValue` | % added to the HUD research bar on completion (default 10) |
| `scanDuration` | Seconds of continuous focus needed to complete the scan (default 3, currently set to **5** on `Cloud`) |
| `onScanned` | `UnityEvent` — hook up a visual change here (e.g. tint the material) if you want a "this one's done" cue; nothing wired to it yet |
| `IsScanned` | Read-only, flips true once `MarkScanned()` runs — prevents re-scanning |

No logic of its own beyond that — it's a data holder that `BinocularScanner` reads and calls `MarkScanned()` on.

### `ResearchManager.cs`
Singleton (same `Instance` pattern as `GameManager.cs`) that owns the HUD's research `MicroBar`.

- `Awake()` — standard singleton dedupe (`Destroy(gameObject)` if one already exists).
- `Start()` — `researchBar.Initialize(maxResearch)`, then **`researchBar.UpdateBar(0f, true)`**.
  The second call is required: `MicroBar.Initialize(maxValue)` (Microlight asset code, not ours) sets
  `_currentValue = maxValue` internally — it's written for health bars that start full. Without the
  explicit snap to 0 right after, the research bar opens already at 100%.
- `AddResearch(amount)` — `researchBar.UpdateBar(researchBar.CurrentValue + amount, UpdateAnim.Heal)`.
  The `Heal` animation type is what makes the bar play a nice fill animation instead of jumping instantly (see §4).

### `BinocularScanner.cs`
Lives on `PlayerCapsule` (same object as `Hotbar`/`Binoculars`). Runs every frame:

```
if binoculars not in use → clear target, bail
raycast from scanCamera forward, scanRange, scanMask
if hit has a Scannable and it's a different target than last frame → reset progress to 0
if no valid target, or it's already scanned → hide the bar, bail
progress += Time.deltaTime / target.scanDuration
scanProgressBar.fillAmount = progress
if progress >= 1 → target.MarkScanned(); ResearchManager.Instance?.AddResearch(target.researchValue); clear
```

Fields to know:
- `scanCamera` — explicitly wired to `Player/MainCamera`, **not** left to auto-resolve via `Camera.main`.
  The test scene originally had its own standalone `Main Camera` also tagged `MainCamera`; with two
  objects sharing that tag, `Camera.main` is ambiguous. The old scene camera is now deactivated, but
  the explicit reference is left in place as the more robust fix.
- `scanProgressBar` / `scanProgressUI` — the green `ScanBar` Image and its parent (`BinocularOverlay`).

### `Binoculars.cs` (existing script, one addition)
Added a public `IsUsing => _isUsing` getter so `BinocularScanner` can read zoom state without needing
its own duplicate flag. No other behavior changed.

## 3. Where everything actually lives

**Important:** `Assets/Prefabs/Binoculars_Item.prefab` is a dead prop — nothing in the codebase ever
instantiates it. It looks like an early standalone prototype (it has its own `Binoculars` + `Canvas`
under `BinocularManager`), but the *real*, input-driven `Binoculars` + `Hotbar` pair lives directly on
`PlayerCapsule` inside `Assets/Prefabs/PlayerPrefab2.prefab`. Early in this feature's build, the scan
bar was wired into `Binoculars_Item.prefab` by mistake — it compiled and looked fine sitting in a test
scene, but was completely disconnected from actual gameplay input. That work was reverted; everything
below is wired into `PlayerPrefab2.prefab` instead, the prefab that's actually spawned.

```
PlayerPrefab2.prefab
└─ Player
   ├─ MainCamera                          (tag: MainCamera — used explicitly by BinocularScanner)
   ├─ PlayerFollowCamera                  (Cinemachine vcam — used by Binoculars for zoom FOV)
   ├─ PlayerCapsule
   │  ├─ Hotbar                           (existing — items[1] = Binoculars.asset)
   │  ├─ Binoculars                       (existing — binocularUI now wired to BinocularOverlay below)
   │  └─ BinocularScanner                 (NEW)
   ├─ HUD Canvas                          (NEW — Screen Space, Overlay)
   │  ├─ ResearchBar                      (NEW — instance of Microlight's Image_SimpleMicroBar prefab)
   │  └─ BinocularOverlay                 (NEW — full-screen dim, inactive until zoomed in)
   │     └─ ScanBar                       (NEW — green horizontal Filled Image)
   └─ ResearchManager                     (NEW)
```

`HUD Canvas` and `ResearchManager` are children of `Player` (not loose scene objects) specifically so
they travel with the player wherever it's spawned — including through the multiplayer spawn path in
`GameManager.cs`/`PlayerSpawnManager.cs`, not just this one test scene.

`Cloud` (a plain capsule placeholder in `CloudsReaserchBar.unity`, unrelated to the actual
`NadoShader.shadergraph` tornado asset) has a `Scannable` component with `researchValue: 10`,
`scanDuration: 5`.

## 4. Bugs hit while building this (and the fixes)

1. **Canvas silently flips to World Space on reparent.** Moving `HUD Canvas` under `Player` via the
   CLI's `set_parent` (a scripted `Transform.SetParent`, not a Hierarchy-window drag) caused Unity to
   convert the `Canvas` component from `Screen Space - Overlay` to `World Space` — its RectTransform
   got the generic 100×100-unit default size, which renders as a giant plane filling the screen when a
   camera sits close to it. **Fix:** explicitly set `Canvas.renderMode` back to
   `Screen Space - Overlay` after any reparenting of a Canvas object. Nothing about the bar's own
   RectTransform values (anchors/size) was ever wrong — only the Canvas's render mode was.

2. **`MicroBar.Initialize()` starts the bar full.** It's asset code written for HP bars (start at max,
   go down). A research bar needs the opposite. **Fix:** call `UpdateBar(0f, true)` immediately after
   `Initialize()` in `ResearchManager.Start()` (the `true` skips the intro animation so it snaps
   straight to empty rather than visibly draining from full).

3. **`UGUI Image` with no `sprite` ignores `fillAmount` entirely.** `Image.OnPopulateMesh()` only runs
   the `Filled`/`fillAmount` geometry logic when `sprite` (or `overrideSprite`) is non-null — with no
   sprite assigned it falls back to `Graphic`'s default `OnPopulateMesh`, which just draws a plain full
   rectangle, silently ignoring `Type: Filled` and `fillAmount` altogether. The green `ScanBar` was
   built from scratch (`add_component` → `Image`) and never got a sprite, so it always rendered as a
   solid block regardless of scan progress. **Fix:** assigned Unity's built-in
   `AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd")` to `ScanBar`'s `Image.sprite`.
   The red bar (from the Microlight asset's prefab) already ships with a proper sprite
   (`spr_microBarImage_new.png`), so it never had this problem — only its Initialize-full issue (#2).

4. **Duplicate `MainCamera` tag.** `CloudsReaserchBar.unity`'s original standalone `Main Camera` and
   the player's own `Player/MainCamera` both carry the `MainCamera` tag, which makes `Camera.main`
   ambiguous. Deactivated the scene's original camera and had `BinocularScanner` reference
   `Player/MainCamera` explicitly rather than relying on `Camera.main`.

## 5. Known limitations / not addressed

- **`ResearchManager` is a scene-wide singleton, now living on the player.** Fine for single-player.
  If multiple `PlayerPrefab2` instances are ever spawned locally (this project has Netcode scaffolding
  in `GameManager.cs`), only the first one's `ResearchManager` survives `Awake()`'s dedupe — other
  players' research bars would have no manager driving them. Not an issue for the current single-player
  test setup; revisit if/when this feature needs to work over the network.
- **`onScanned` UnityEvent on `Scannable` is unwired.** There's no visual feedback on the `Cloud` itself
  when it's been scanned (material tint, particle, etc.) — hook this up in the Inspector when there's
  a visual treatment decided on.
- **`Assets/Prefabs/Binoculars_Item.prefab` is still an unused, standalone leftover** (its own
  `Binoculars`/`Canvas`, no scan wiring). Left alone rather than deleted, since it's unclear whether it
  was intentionally kept around for something else — worth asking before removing it.
