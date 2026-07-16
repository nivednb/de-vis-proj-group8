# Pipe-only flow prototype

This project now includes a lightweight workflow for developing the flow visualization separately from the full plant model.

## Why

The full plant scene is heavy on the laptop. The pipe-only prototype lets us polish:

- transparent pipe shells;
- colored material flow;
- moving flow direction;
- speed/intensity changes;
- route readability.

## Option A: extract the real pipe layout

Use this when you want the same pipe positions/layout as the whole plant.

1. Open the full whole-plant scene in Unity.
2. Go to `Tools > Nived > Flow Prototype > Create Pipe-Only Scene From Current Scene`.
3. Unity creates:

   `Assets/Scenes/PipeOnlyFlowPrototype.unity`

4. Open that scene and press Play.

This copies pipe objects such as:

- `Piping`
- `pipe bend (...)`
- `Cylinder (...)`
- `t junction`

It strips colliders and unrelated behaviours, adds a camera/light/floor, and attaches the existing route-based flow runtime.

## Option B: export to a separate Unity project

After creating the pipe-only scene:

1. Go to `Tools > Nived > Flow Prototype > Export Pipe-Only UnityPackage`.
2. Unity exports:

   `PipeOnlyFlowPrototype.unitypackage`

3. Import that package into a fresh lightweight Unity project.

## Option C: abstract generated prototype

Attach `PipeFlowPrototypeRuntime` to an empty GameObject in a blank scene and press Play.

This creates a simplified pipe network from code. It is useful for testing flow styles quickly, but it does not use the exact plant pipe layout.

## Material color mapping

- H2: green
- CO2: pale blue-white
- Rich amine: teal
- Lean amine: green-teal
- Recycle gas: violet
- Syngas: yellow
- Hot reactor product: orange
- Crude methanol: cyan
- Final methanol: purple
