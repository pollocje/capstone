# Shader Graph: How the Wind Shader Works

Notes from building `SimpleWindShader` (wind sway for corn stalks / trees), written so the pattern can be reused for other procedural vertex-animation shaders.

## The big picture

Shader Graph is a visual editor that generates HLSL shader code. Every **node** is a math operation or a data source; every **wire** is data flowing from one node's output into another's input. Instead of typing `pow(x, 2)`, you drop a `Power` node and wire `x` into it. Same math, just visual.

## The Master Stack (Vertex / Fragment blocks)

This is the "output" of the whole graph — everything eventually funnels into one of these two blocks:

- **Vertex block**: runs once per mesh *vertex*, before rasterization. This is where geometry gets moved — anything plugged into `Position` physically displaces that corner of the mesh. This is how the wind sway works — geometry only, never color.
- **Fragment block**: runs once per *pixel* on screen, after the GPU fills in the triangles. `Base Color`, `Alpha`, `Smoothness`, `Normal` all live here — this controls how a surface *looks*, not *where it is*.

Keeping that distinction straight matters a lot — crossing color data into `Position` (or vice versa) was a real bug hit while building this.

## Spaces

A vertex's position can be described relative to different origins:

- **Object space**: relative to the mesh's own pivot. A tree's trunk base is near `(0,0,0)` in object space regardless of where the tree is placed in the world.
- **World space**: relative to the scene's origin. Two identical prefabs at different locations have different world positions but the *same* object-space positions.

This shader uses **two different `Position` nodes** for this reason: Object space for the actual displacement (so "up the trunk" always means the same thing regardless of where the plant sits), and World space just to generate the sine wave's phase (so plants in different spots sway slightly out of sync instead of every stalk in a field moving in perfect unison).

## Nodes used, and what each does

- **Position** — outputs a vertex's coordinates in whatever space you pick.
- **Split** — breaks a Vector (like a Vector3 position) into individual channels: `R/G/B/A`, which map to `X/Y/Z/W` in order. Always Split before grabbing "just the height" or "just X and Z".
- **Saturate** — clamps a value into the 0–1 range. Used so "how bent is this vertex" never goes negative or above 1.
- **Power** — `A ^ B` (A = base, B = exponent). Used to shape the falloff curve: squaring the height value (`B = 2`) means the bottom of the mask stays near-zero much longer than a straight linear ramp would — base barely bends, tip bends a lot. **Watch the input order** — `0 ^ something` produces invalid/NaN values (shows as magenta in the preview), so the Saturate output must go into `A`, not `B`.
- **Time** — a built-in clock; its plain `Time` output increases every frame, which is what makes anything driven by it *animate* instead of sitting static.
- **Sine** — oscillates between -1 and 1. Feed it something that changes over time (or space) and you get a wave.
- **Multiply / Add** — basic arithmetic, but also how independent effects get *combined*. Core trick of this shader: build each ingredient (mask, oscillation, strength, direction) as its own isolated value, then multiply them together at the end. Each one scales the final result without the others needing to know about it.
- **Combine** — packs separate float values back into a Vector (the opposite of Split). Used to reassemble an X and Z offset into a full Vector3 displacement (Y left at 0, since wind shouldn't stretch the plant taller).
- **Sample Texture 2D** — reads a texture at a given UV coordinate, outputs both the full color (`RGBA`) and individual channels (`R/G/B/A`). `RGBA` went to Base Color; the separate `A` output went to Alpha Clipping.
- **Properties / Blackboard** — variables exposed to the material Inspector. Anything dragged from the Blackboard onto the graph becomes a tunable knob per-material, without touching the graph again.

## The wind "recipe" as a reusable pattern

```
displacement = mask(where on the mesh) × oscillation(how it moves over time) × strength(how much) × direction(which way)
final position = original position + displacement
```

This generalizes to basically any per-vertex procedural animation — swap the mask for something else (e.g. distance from an explosion center) or the oscillation for something else (e.g. a noise texture instead of Sine) and it becomes a completely different effect using the same skeleton: water ripples, cloth flutter, damage shake, all the same shape of graph.

## Properties in `SimpleWindShader`

| Property | Type | What it controls |
|---|---|---|
| `Base_Texture` | Texture2D | Albedo/color texture |
| `Wind_Speed` | Float | How fast the oscillation cycles over time |
| `Wind_Strength` | Float | How large the displacement is (scale this relative to the mesh's actual size — small meshes need small values) |
| `Wind_Scale` | Float | Spatial frequency of the sway — how much phase varies across world position |
| `Wind_Direction` | Vector2 | XZ direction the sway leans toward |

## Debugging tips (learned the hard way)

- A node's **preview swatch** is a cheap way to sanity-check a value without touching the real mesh — grayscale for a float (black = 0, white = 1), and **magenta means invalid/NaN**, not an intentional color.
- Master Stack sockets only show up when relevant settings are enabled (`Alpha` only appears once Alpha Clipping is turned on in Graph Settings) — check Graph Inspector → Graph Settings when something seems to be missing.
- One output can feed multiple inputs at once — no need to disconnect something to also send it somewhere else temporarily for debugging (e.g. plugging a mask value straight into Base Color to visualize it as greyscale).
- The small preview sphere at the bottom of a material's Inspector uses a generic UV unwrap — it's not a reliable preview for a mesh using a hand-packed texture atlas. Judge the real result on the actual mesh in the Scene view instead.
- If a texture looks like solid grey with fragments of color on it, check whether the texture is actually a **UV atlas** (a sprite sheet with dead space) before assuming it's an alpha/transparency problem — the fix might just be "wrong texture assigned," not a shader bug.
