# API Reference — URP Dynamic Weather System

---

## WeatherManager

**Namespace:** `DynamicWeatherSystem`
**Inherits:** `MonoBehaviour`
**Component path:** Add Component > Dynamic Weather System > Weather Manager

The central orchestrator. Place it on the root `WeatherSystem` GameObject. All modules must be children of this GameObject — they are discovered automatically via `GetComponentsInChildren` on `Awake`.

---

### Inspector Fields

| Field | Type | Default | Description |
|---|---|---|---|
| `initialState` | WeatherStateData | null | State applied immediately on scene start. If null, no state is applied. |
| `defaultTransitionDuration` | float (≥ 0) | 2.0 | Seconds used by `SetWeather(state)` when no duration is specified. |
| `transitionCurve` | AnimationCurve | EaseInOut | Controls transition acceleration. Evaluated over [0, 1] each frame. |

---

### Public Methods

#### `SetWeather(WeatherStateData state)`
Changes to `state` using `defaultTransitionDuration`.

```csharp
weatherManager.SetWeather(rainPreset);
```

---

#### `SetWeather(WeatherStateData state, float duration)`
Changes to `state` with a specific transition duration in seconds.

| Duration | Behavior |
|---|---|
| `> 0` | Smooth animated transition |
| `0` | Immediate (no transition) |
| `< 0` | Treated as `0` (immediate) |

```csharp
// 5-second transition
weatherManager.SetWeather(stormPreset, 5f);

// Instant
weatherManager.SetWeather(clearPreset, 0f);
```

**Interruption behavior:** If called while a transition is in progress, the current transition is stopped immediately. A runtime snapshot captures the exact interpolated values at that moment and becomes the new starting point — no visual jump occurs.

---

### Public Properties

#### `CurrentState` → `WeatherStateData` (read-only)
The target state of the last `SetWeather` call. Does **not** change back to the previous state if a transition is cancelled.

```csharp
if (weatherManager.CurrentState == stormPreset)
    Debug.Log("Storm is active or transitioning to storm.");
```

---

#### `IsTransitioning` → `bool` (read-only)
`true` while a coroutine-based transition is running.

```csharp
if (!weatherManager.IsTransitioning)
    weatherManager.SetWeather(nextPreset);
```

---

#### `TransitionProgress` → `float` (read-only)
Current transition progress in [0, 1], already evaluated through `transitionCurve`. Returns `0` when no transition is active.

```csharp
float t = weatherManager.TransitionProgress;  // 0 → 1 during transition, 0 at rest
```

---

## WeatherStateData

**Namespace:** `DynamicWeatherSystem`
**Inherits:** `ScriptableObject`
**Create menu:** Assets > Create > Dynamic Weather System > Weather State

All weather parameters for a single state. Create as many as you need.

### Fields

#### Identity
| Field | Type | Description |
|---|---|---|
| `stateName` | string | Display name shown in the editor inspector and console logs. |

#### Fog
| Field | Type | Range | Description |
|---|---|---|---|
| `fogEnabled` | bool | — | Enables Unity's scene fog (`RenderSettings.fog`) when this state is active. |
| `fogColor` | Color | — | Fog color. |
| `fogDensity` | float | 0 – 0.1 | Density for Exponential Squared fog mode. Values above 0.05 are very dense. |

#### Directional Light
| Field | Type | Range | Description |
|---|---|---|---|
| `lightColor` | Color | — | Color of the main directional light. |
| `lightIntensity` | float | 0 – 8 | Intensity of the main directional light. |

#### Ambient Light
| Field | Type | Description |
|---|---|---|
| `ambientColor` | Color | Scene ambient light color (`RenderSettings.ambientLight`). |

#### Sky
| Field | Type | Range | Description |
|---|---|---|---|
| `skyboxTint` | Color | — | Multiplicative tint on the skybox material. White = no change. |
| `skyboxExposure` | float | 0 – 8 | Skybox brightness. 1 = normal. Values below 1 darken the sky. |

#### Audio
| Field | Type | Range | Description |
|---|---|---|---|
| `ambientClip` | AudioClip | — | Looping audio clip for this state. Leave null for silence. |
| `ambientVolume` | float | 0 – 1 | Target volume. Interpolated smoothly during transitions. |

#### Rain
| Field | Type | Range | Description |
|---|---|---|---|
| `rainIntensity` | float | 0 – 1 | 0 = no particles. 1 = maximum emission rate. |
| `rainColor` | Color | — | Rain particle color. Alpha controls drop opacity. |
| `rainSpeed` | float | 2 – 20 | Fall speed in metres per second. |

---

## WeatherModule (Abstract Base Class)

**Namespace:** `DynamicWeatherSystem`
**Inherits:** `MonoBehaviour`

Base class for all weather modules. Inherit from this to create custom modules.

### Methods

#### `Apply(WeatherStateData state)` (non-virtual, sealed)
Applies a state instantly without interpolation. Internally calls `Blend(state, state, 1f)`. You do not need to override this.

#### `Blend(WeatherStateData from, WeatherStateData to, float t)` (abstract)
Called every frame during an active transition, and once via `Apply` for immediate changes.

| Parameter | Description |
|---|---|
| `from` | Source weather state (t = 0) |
| `to` | Target weather state (t = 1) |
| `t` | Normalized progress [0, 1], already evaluated through AnimationCurve |

---

## LightModule

**Component path:** Add Component > Dynamic Weather System > Modules > Light Module

### Inspector Fields

| Field | Type | Description |
|---|---|---|
| `directionalLight` | Light | The directional light to control. Auto-found in `Reset()` if not assigned. |

---

## FogModule

**Component path:** Add Component > Dynamic Weather System > Modules > Fog Module

No public Inspector fields beyond the component itself. Controls `RenderSettings.fog`, `fogMode`, `fogColor`, and `fogDensity`.

**Fog-during-transition rule:** fog is kept enabled if *either* `from.fogEnabled` or `to.fogEnabled` is true. It is only disabled when the transition completes and `to.fogEnabled` is false.

---

## RainModule

**Component path:** Add Component > Dynamic Weather System > Modules > Rain Module

### Inspector Fields

| Field | Type | Default | Description |
|---|---|---|---|
| `rainParticleSystem` | ParticleSystem | null | Auto-created if left empty. |
| `maxEmissionRate` | float | 1000 | Particle emission rate at `rainIntensity = 1`. |
| `followTarget` | Transform | null | Transform the rain follows. Falls back to `Camera.main` if null. |

---

## SkyModule

**Component path:** Add Component > Dynamic Weather System > Modules > Sky Module

No Inspector fields. Requires the active skybox material to expose `_Tint` or `_SkyTint` (color) and `_Exposure` (float) properties.

**Safe fallback:** if the required shader properties are missing, a warning is logged once and the module does nothing — no errors or crashes.

---

## AudioModule

**Component path:** Add Component > Dynamic Weather System > Modules > Audio Module

### Inspector Fields

| Field | Type | Range | Default | Description |
|---|---|---|---|---|
| `masterVolume` | float | 0 – 1 | 1 | Global volume multiplier applied on top of each state's `ambientVolume`. |

---

## WeatherDemoUI

**Component path:** Add Component > Dynamic Weather System > Demo > Weather Demo UI

Runtime OnGUI panel. No Canvas or Input System required.

### Inspector Fields

| Field | Type | Description |
|---|---|---|
| `weatherManager` | WeatherManager | Auto-found if left empty. |
| `presetClear` | WeatherStateData | Preset for the "Clear" button. |
| `presetRain` | WeatherStateData | Preset for the "Rain" button. |
| `presetFog` | WeatherStateData | Preset for the "Fog" button. |
| `presetStorm` | WeatherStateData | Preset for the "Storm" button. |
| `transitionDuration` | float | 0.5 – 12 | Duration used when a button is pressed. Also exposed as a slider at runtime. |

Buttons for presets that are not assigned (`null`) are hidden automatically.

---

*URP Dynamic Weather System v1.0.0*
