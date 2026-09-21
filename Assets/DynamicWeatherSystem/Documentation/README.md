# URP Dynamic Weather System — Documentation

**Version 1.0.0 | Unity 2022.3+ | Universal Render Pipeline only**

---

## Table of Contents

1. [Overview](#overview)
2. [Requirements](#requirements)
3. [Package Contents](#package-contents)
4. [Quick Start (5 minutes)](#quick-start)
5. [Modules Reference](#modules-reference)
6. [API Reference](#api-reference)
7. [Creating Custom Modules](#creating-custom-modules)
8. [Weather State Data Fields](#weather-state-data-fields)
9. [Editor Tools](#editor-tools)
10. [Demo Scene](#demo-scene)
11. [FAQ](#faq)

---

## Overview

**URP Dynamic Weather System** is a modular, drop-in weather framework for Unity's Universal Render Pipeline.

It lets you define weather states as ScriptableObject assets, then blend smoothly between them at runtime — with full control over directional light, ambient light, fog, rain particles, skybox, and ambient audio. Transitions can be interrupted at any time without visual popping.

**Included weather states:** Clear · Rain · Fog · Storm

---

## Requirements

| Requirement | Version |
|---|---|
| Unity | 2022.3 LTS or newer |
| Universal Render Pipeline | Any URP version compatible with your Unity version |
| Render Pipeline | **URP only** — Built-in and HDRP are not supported |

No other packages or dependencies are required.

---

## Package Contents

```
DynamicWeatherSystem/
├── Runtime/
│   ├── Core/
│   │   ├── WeatherStateData.cs     — ScriptableObject: all weather parameters
│   │   ├── WeatherManager.cs       — Central orchestrator
│   │   └── WeatherModule.cs        — Abstract base for all modules
│   ├── Modules/
│   │   ├── LightModule.cs          — Directional light + ambient color
│   │   ├── FogModule.cs            — Scene fog (Exponential Squared)
│   │   ├── RainModule.cs           — Particle-based rain
│   │   ├── SkyModule.cs            — Skybox tint + exposure
│   │   └── AudioModule.cs          — Ambient audio crossfade
│   └── Demo/
│       └── WeatherDemoUI.cs        — Runtime demo UI (OnGUI, no Canvas needed)
├── Editor/
│   ├── WeatherManagerEditor.cs     — Custom inspector with live preview
│   └── WeatherPresetsCreator.cs    — One-click preset asset generator
├── Presets/
│   ├── WS_Clear.asset
│   ├── WS_Rain.asset
│   ├── WS_Fog.asset
│   └── WS_Storm.asset
├── Audio/
│   ├── low_rain_loop.wav
│   ├── storm-wind-loop.wav
│   └── wind_loop.wav
├── Example/
│   └── Scene WeatherSystem.unity   — Demo scene
└── Documentation/
    ├── README.md                   — This file
    ├── QuickStart.md
    ├── API_Reference.md
    └── Custom_Modules.md
```

---

## Quick Start

See **[QuickStart.md](QuickStart.md)** for the full step-by-step setup guide.

**TL;DR — 5 steps:**

1. Create an empty GameObject named `WeatherSystem`.
2. Add `WeatherManager` to it.
3. Add child GameObjects with the modules you need (`LightModule`, `FogModule`, `RainModule`, `SkyModule`, `AudioModule`).
4. Assign a `WeatherStateData` preset to the **Initial State** field on `WeatherManager`.
5. Press Play — call `SetWeather(preset)` from your code to change the weather.

---

## Modules Reference

Each module is a `MonoBehaviour` that must be a **child** of the `WeatherManager` GameObject. The manager auto-discovers all children on `Awake`.

### LightModule
Controls the scene's directional light color/intensity and ambient light color.
- Auto-finds the first Directional Light in the scene if none is assigned.
- Set the light reference manually in the Inspector for multi-light scenes.

### FogModule
Controls Unity's built-in scene fog (`RenderSettings`).
- Uses **Exponential Squared** mode for smooth distance-based falloff.
- Fog is kept active during a transition if either the source or target state has fog enabled, preventing visual popping.

### RainModule
Particle-based rain that follows the main camera.
- Auto-creates and configures a `ParticleSystem` if none is assigned.
- Rain particles use world-space simulation and stretch rendering for a realistic look.
- Set `followTarget` in the Inspector to override the default `Camera.main` tracking.
- Controlled by `rainIntensity`, `rainColor`, and `rainSpeed` on `WeatherStateData`.

### SkyModule
Adjusts the active skybox material's tint and exposure at runtime.
- Creates a **material instance** at startup — your original skybox asset is never modified.
- If the skybox material doesn't have `_Tint`/`_SkyTint` or `_Exposure` properties, a warning is logged and the module is skipped gracefully.
- Requires a Procedural or custom skybox material; plain color backgrounds are not affected.

### AudioModule
Crossfades between two ambient audio clips synced to the transition duration.
- Uses two `AudioSource` components created as children automatically.
- If source and target states share the same clip, only the volume is interpolated (no restart).
- Assign `AudioClip` assets to each `WeatherStateData` preset in the Inspector.
- Use `masterVolume` on the component to globally attenuate all weather audio.

---

## API Reference

See **[API_Reference.md](API_Reference.md)** for the full reference.

### WeatherManager — Public API

```csharp
// Change weather using the default transition duration (set in Inspector)
weatherManager.SetWeather(WeatherStateData state);

// Change weather with a specific duration in seconds
weatherManager.SetWeather(WeatherStateData state, float duration);

// Immediate change (no transition)
weatherManager.SetWeather(state, 0f);

// Read current state
WeatherStateData current = weatherManager.CurrentState;

// Check if a transition is running
bool transitioning = weatherManager.IsTransitioning;

// Transition progress [0, 1], eased by AnimationCurve
float progress = weatherManager.TransitionProgress;
```

---

## Creating Custom Modules

See **[Custom_Modules.md](Custom_Modules.md)** for a full example with code.

**Summary:** Inherit from `WeatherModule`, implement `Blend(from, to, t)`, and add your component as a child of the `WeatherManager` GameObject.

```csharp
public class MyModule : WeatherModule
{
    public override void Blend(WeatherStateData from, WeatherStateData to, float t)
    {
        // t = 0 → pure "from" state
        // t = 1 → pure "to" state
        float value = Mathf.Lerp(from.someField, to.someField, t);
        // apply value to your system
    }
}
```

---

## Weather State Data Fields

| Field | Type | Range | Description |
|---|---|---|---|
| `stateName` | string | — | Display name used in logs and inspector |
| `fogEnabled` | bool | — | Enables scene fog for this state |
| `fogColor` | Color | — | Fog color |
| `fogDensity` | float | 0–0.1 | Fog density (Exponential Squared) |
| `lightColor` | Color | — | Directional light color |
| `lightIntensity` | float | 0–8 | Directional light intensity |
| `ambientColor` | Color | — | Scene ambient light color |
| `skyboxTint` | Color | — | Multiplicative tint on the skybox |
| `skyboxExposure` | float | 0–8 | Skybox brightness multiplier |
| `ambientClip` | AudioClip | — | Looping ambient audio clip |
| `ambientVolume` | float | 0–1 | Target volume for this state |
| `rainIntensity` | float | 0–1 | 0 = no rain, 1 = maximum rain |
| `rainColor` | Color | — | Rain particle color (alpha = opacity) |
| `rainSpeed` | float | 2–20 | Downward fall speed in m/s |

---

## Editor Tools

### Custom Inspector (WeatherManager)
Select the `WeatherManager` component to access:
- **Weather State** dropdown — lists all `WeatherStateData` assets in the project.
- **Transition Duration** — override duration for the next preview transition.
- **Apply with Transition** — triggers a live transition in Play Mode.
- **Apply Immediate** — applies the state instantly with no fade.
- **Progress bar** — shows active transition progress, live-updated in the Inspector.

### Preset Creator
`Assets > Create > Dynamic Weather System > Create Sample Presets`

Creates all four preset assets (WS_Clear, WS_Rain, WS_Fog, WS_Storm) in the currently selected project folder. If the assets already exist, their values are updated without duplicating them.

### Creating a New Weather State
`Right-click in Project window > Create > Dynamic Weather System > Weather State`

---

## Demo Scene

Open `Example/Scene WeatherSystem.unity`. The scene includes:
- A fully configured `WeatherSystem` hierarchy (Manager + all modules)
- A `WeatherDemoUI` component providing an in-game panel with:
  - Four preset buttons with color-coded accents
  - Transition duration slider
  - Live transition progress bar

No Canvas or Input System package is required.

---

## FAQ

**Q: The rain particles don't appear.**
A: The `RainModule` auto-creates a `ParticleSystem`, but needs a URP-compatible particle material. If your project's URP version doesn't include `Particles/Unlit`, create a `ParticleSystem` manually, assign a valid URP material to it, then assign the `ParticleSystem` reference on `RainModule` in the Inspector.

**Q: The skybox doesn't change.**
A: Check the Console for warnings from `SkyModule`. Your skybox material must expose `_Tint` or `_SkyTint` and `_Exposure` properties. Standard URP skyboxes (Procedural, Six Sided) support these. Shader Graph skyboxes may require adding those properties manually.

**Q: Audio clips aren't playing.**
A: The sample presets ship with `ambientClip = null`. Open each `WS_*.asset` in the Inspector and assign an audio clip. The included `.wav` files in the `Audio/` folder are ready to use.

**Q: Can I use this with Built-in or HDRP?**
A: Not in V1. The fog, light, and skybox APIs target URP. HDRP support is planned for a future version.

**Q: Can I add more weather states beyond the four included?**
A: Yes. Create a new `WeatherStateData` asset, fill in the values, and pass it to `SetWeather()`. There is no limit on the number of states.

**Q: What happens if I call SetWeather during an active transition?**
A: The current transition is stopped and a runtime snapshot is created capturing the exact visual state at that moment. The new transition starts from that snapshot — no visual jump occurs.

---

*URP Dynamic Weather System v1.0.0 — © 2024 All rights reserved.*
