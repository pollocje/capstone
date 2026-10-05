# Creating Custom Modules — URP Dynamic Weather System

The module system is designed to be extended. Any effect that can be interpolated between two states — post-processing volumes, wind zones, particle density, material properties, etc. — can be wrapped in a custom module.

---

## How It Works

The `WeatherManager` calls `Blend(from, to, t)` on every `WeatherModule` it finds as a child every frame during a transition. When you call `SetWeather(state, 0f)`, it calls `Apply(state)` instead — which internally calls `Blend(state, state, 1f)`.

All you need to do is:
1. Create a class that inherits `WeatherModule`.
2. Implement `Blend()`.
3. Add your component as a child of the `WeatherSystem` GameObject.

That's it. No registration, no lists to update, no events to subscribe to.

---

## Adding Custom Fields to WeatherStateData

`WeatherStateData` is the single ScriptableObject that carries all weather parameters. To drive your custom module you will likely need to add new fields.

**Option A — Modify WeatherStateData directly** *(recommended for in-house projects)*

Open `Runtime/Core/WeatherStateData.cs` and add your fields in a new `[Header]` section:

```csharp
[Header("Wind")]
[Range(0f, 1f)]
public float windStrength = 0f;

public Vector3 windDirection = Vector3.forward;
```

All existing presets will show these new fields with their default values. Use the **Preset Creator** (`Assets > Create > Dynamic Weather System > Create Sample Presets`) to regenerate presets with the new values.

**Option B — Separate ScriptableObject** *(for distributable modules)*

Create a companion `ScriptableObject` that holds only your extra parameters, then reference it from your module. This avoids modifying the core asset.

---

## Example 1 — WindZone Module

This example controls a Unity `WindZone` component based on a `windStrength` field added to `WeatherStateData`.

```csharp
using UnityEngine;
using DynamicWeatherSystem;

[AddComponentMenu("Dynamic Weather System/Modules/Wind Module")]
public class WindModule : WeatherModule
{
    [SerializeField] private WindZone windZone;

    [Tooltip("Wind turbulence at maximum strength.")]
    [SerializeField, Range(0f, 1f)] private float maxTurbulence = 0.4f;

    private void Reset()
    {
        windZone = GetComponentInChildren<WindZone>();
    }

    public override void Blend(WeatherStateData from, WeatherStateData to, float t)
    {
        if (windZone == null) return;

        float strength = Mathf.Lerp(from.windStrength, to.windStrength, t);
        windZone.windMain      = strength * 3f;             // scale to WindZone range
        windZone.windTurbulence = strength * maxTurbulence;
    }
}
```

Add this component as a child of `WeatherSystem`. The manager picks it up automatically on next Play.

---

## Example 2 — URP Volume Override Module

This example blends a `ColorAdjustments` post-processing override from the URP Volume system.

> Requires the URP `Post Processing` package and `using UnityEngine.Rendering.Universal;`

```csharp
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using DynamicWeatherSystem;

[AddComponentMenu("Dynamic Weather System/Modules/Post Process Module")]
public class PostProcessModule : WeatherModule
{
    [SerializeField] private Volume volume;

    private ColorAdjustments _colorAdjustments;

    private void Awake()
    {
        if (volume != null)
            volume.profile.TryGet(out _colorAdjustments);
    }

    public override void Blend(WeatherStateData from, WeatherStateData to, float t)
    {
        if (_colorAdjustments == null) return;

        // Example: darken saturation during storm states
        // Add a "saturation" float field to WeatherStateData (range -100 to 100)
        float sat = Mathf.Lerp(from.saturation, to.saturation, t);
        _colorAdjustments.saturation.Override(sat);
    }
}
```

---

## Example 3 — Material Property Module

Blend a float property on any renderer's material — useful for wet surface effects, snow accumulation, etc.

```csharp
using UnityEngine;
using DynamicWeatherSystem;

[AddComponentMenu("Dynamic Weather System/Modules/Wet Surface Module")]
public class WetSurfaceModule : WeatherModule
{
    [SerializeField] private Renderer[] surfaceRenderers;

    private static readonly int WetnessPropID = Shader.PropertyToID("_Wetness");

    public override void Blend(WeatherStateData from, WeatherStateData to, float t)
    {
        // Drive wetness from rain intensity
        float wetness = Mathf.Lerp(from.rainIntensity, to.rainIntensity, t);

        foreach (var r in surfaceRenderers)
        {
            if (r == null) continue;
            r.material.SetFloat(WetnessPropID, wetness);
        }
    }
}
```

> Note: `r.material` creates a material instance per renderer. For many objects, use a `MaterialPropertyBlock` instead to avoid memory allocation.

---

## Tips and Best Practices

**Cache everything in `Awake`.**
`Blend` is called every frame. Never call `GetComponent`, `FindObjectOfType`, or `Shader.PropertyToID` inside `Blend`.

**Null-check your references.**
If a required component is missing, log a warning in `Awake` and return early in `Blend`. Never let a module throw exceptions — it would break the entire weather system.

**Use `[AddComponentMenu]`.**
Adding this attribute places your module in the Add Component menu alongside the built-in modules. It's a small touch that makes the product feel polished.

**Respect the t value.**
`t` has already been evaluated through the `WeatherManager`'s `AnimationCurve`. Do not apply your own easing curve on top of it — you'd be double-easing.

**Apply is free.**
You don't need to override `Apply`. The base class calls `Blend(state, state, 1f)` which gives you `t = 1`, meaning the `to` value at full weight — the correct immediate result.

---

## Assembly Definition

If your custom module is in a separate folder with its own `.asmdef`, make sure to reference the runtime assembly:

```json
{
    "name": "MyGame.WeatherExtensions",
    "references": [
        "DynamicWeatherSystem.Runtime"
    ],
    "autoReferenced": true
}
```

---

*URP Dynamic Weather System v1.0.0*
