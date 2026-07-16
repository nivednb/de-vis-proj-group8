# Final Flow Simulation Handoff

This branch contains the reduced `whole_plant` scene focused on the reactor and process pipe network. It is designed so the flow visualization runs automatically when the scene enters Play mode.

## What runs automatically

- `FinalPlantFlowBootstrap` creates `Final MSc Flow Simulation Runtime` on Play.
- `FinalPlantFlowRuntime` detects renamed pipe objects by prefix:
  - `H2_pipe_`
  - `H2Storage_pipe_`
  - `CO2_pipe_`
  - `RichAmine_pipe_`
  - `LeanAmine_pipe_`
  - `RecycleGas_pipe_`
  - `MixedFeed_pipe_`
  - `Syngas_pipe_`
  - `ReactorEffluent_pipe_`
  - `CrudeMeOH_pipe_`
  - `Liq_CrudeMeOH_pipe_`
  - `MethanolProduct_pipe_`
- Pipes are made semi-transparent and assigned dense animated process-flow bands.
- The reactor is made transparent.
- Internal reactor particles and a catalyst-bed condition visual are generated at runtime.

## Flow colors

- Hydrogen: green
- CO2: pale blue/white
- Rich amine: teal
- Lean amine: bright green-cyan
- Recycle gas: purple
- Mixed feed: yellow-green
- Cold syngas: yellow
- Heated syngas after heat exchanger: orange-yellow
- Reactor effluent: orange/red
- Crude methanol: blue
- Liquid crude methanol: cyan
- Methanol product: purple

`Syngas_pipe_5` is treated as the heat-exchanger boundary and `Syngas_pipe_6` onward is treated as heated reactor feed.

## Manual adjustment

There are two correction levels.

### 1. Global/route-level correction

When Play starts, select:

`Final MSc Flow Simulation Runtime`

Then edit `FinalPlantFlowRuntime`:

- `Global Speed Multiplier`
- `Global Density Multiplier`
- `Global Pipe Alpha Multiplier`
- `Global Intensity Multiplier`
- individual route `speed`, `density`, `pipeAlpha`, `flowIntensity`, `reverseDirection`, and `flowColor`

This is useful when an entire route is too slow, too faint, too dense, or reversed.

### 2. Per-pipe correction

If only one pipe is wrong:

1. Select that pipe object.
2. Add component `PipeFlowManualOverride`.
3. Enable `Use Override`.
4. Override only what is needed:
   - flow type
   - color
   - speed
   - density
   - opacity
   - direction
   - force flow off

This avoids editing code for small naming/direction mistakes.

## Legacy systems

Older experimental automatic flow systems are still present for reference, but their auto-start was disabled so they do not overlap with the final flow simulation.

