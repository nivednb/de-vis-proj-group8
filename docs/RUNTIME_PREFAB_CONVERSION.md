# Runtime Prefab Conversion

## Purpose

This refactor makes stable runtime-authored structures editable before Play mode
without freezing the dynamic process simulation into scene data. It preserves
Unity `.meta` files, GUID references, route naming, the continuous flow clock,
mixed-gas composition, catalyst animation, transparent reactor shell, and live
UI bindings.

## Editable prefab assets

| Prefab | Contents | Normal editing workflow |
|---|---|---|
| `Assets/Prefabs_N/Runtime/Systems/PlantRuntimeServices.prefab` | One composition root containing the simulator, flow runtime, six-page dashboard, module panel runtime, safety warnings, and environment owner | Edit serialized defaults on the prefab; do not add competing manager instances to the scene |
| `Assets/Prefabs_N/Runtime/UI/SafetyWarningOverlay.prefab` | Warning canvas, panel, and text hierarchy | Edit dimensions, anchors, typography, colours, and opacity in Prefab Mode |
| `Assets/Prefabs_N/Environment/IndustrialPlantEnvironment.prefab` | Stable plant floor, structures, safety markings, lighting fixtures, and environmental props | Edit geometry and placement in Prefab Mode or apply deliberate scene overrides |

Persistent materials used by the environment are in
`Assets/Materials_N/Environment`.

## Script classification

| Script | Conversion result | Retained responsibility |
|---|---|---|
| `SafetyWarningRuntime` | Stable visual hierarchy converted to `SafetyWarningOverlay.prefab` | Tank-limit monitoring, alarm state, live message text, visibility, and compatibility fallback |
| `PlantEnvironmentBuilder` | Generated output baked to `IndustrialPlantEnvironment.prefab`; component remains on the services prefab with startup rebuilding disabled | Deliberate editor regeneration and recovery when the authored environment must be rebuilt |
| `PlantProcessSimulator` | Serialized as a component of `PlantRuntimeServices.prefab`; not replaced by static objects | Authoritative educational mass/energy/process state, slider inputs, KPIs, storage fill, alarms, and efficiency calculation |
| `FinalPlantFlowRuntime` | Serialized as a component of `PlantRuntimeServices.prefab`; moving visuals remain transient | Route discovery, continuous plant-wide phase clock, direction, speed/density response, mixed H2/CO2/recycle species, shader/material control, and simulator coupling |
| `IcodosDashboardRuntime` | Configuration host moved to `PlantRuntimeServices.prefab`; live page contents remain runtime-bound | Six-page navigation, overview, process controls, flow view, reactor view, analytics/export, education/help, live values, and Max Efficiency control |
| `InteractiveModulePanelRuntime` | Configuration host moved to `PlantRuntimeServices.prefab`; equipment-specific panels remain runtime-bound | Equipment discovery, information panels, safe control ranges, and live simulator binding |
| `LightweightReactorVisual` | Left dynamic | Geometry-relative reactor tracers, catalyst-region behaviour, and transient VFX materials |
| `CatalystBedColorAnimator` | Left dynamic on the authored catalyst object | Process-driven catalyst colour/conversion indication |
| `PipeFlowAnimator` | Left dynamic | Per-renderer pipe-flow shader parameters and continuous phase response |
| `FlowPath` / `FlowFollower` | Left dynamic | Waypoint route representation and transient particle traversal |
| `OrbitCameraController` / `CameraController` | Left dynamic | Runtime mouse/keyboard camera navigation and module views |
| `PipeWaypointGenerator` | Left as an editor tool | Authoring and repairing waypoint hierarchies; it does not run in the player |
| `RuntimeValidationCapture` | Left as validation tooling | Automated runtime evidence capture and checks |
| `WindowsBuild` / `ReleaseValidation` / `ProjectInventory` | Left as editor tooling | Reproducible builds, release-scene checks, and project inventory export |

Legacy specialist controllers are retained where referenced for compatibility;
they are not promoted into new runtime roots. New work should use the process
simulator and final flow runtime as the authoritative systems.

## Re-running the conversion

Use **Tools > Power-to-Methanol > Convert Runtime Structures to Prefabs** only
when deliberately rebaking the architecture. The operation updates the three
prefabs, persistent environment materials, and their instances in
`Assets/Scenes/SampleScene.unity`. Commit or stash unrelated scene work first.

## Verification checklist

1. Open `Assets/Scenes/SampleScene.unity` and confirm the services and environment
   are prefab instances.
2. Run **Tools > Power-to-Methanol > Validate Release Scene**.
3. Enter Play mode and test all process sliders, Max Efficiency, storage fill and
   warning/reset behaviour, camera movement, mixed-stream composition, reactor
   transparency, and catalyst colour animation.
4. Build through **Tools > Power-to-Methanol > Build Windows Application** and
   distribute the complete build folder, not the `.exe` alone.

## Verified refactor baseline

Verified on 2026-08-11 with Unity 6000.4.7f1:

- batch compilation completed with return code 0;
- release-scene validation reported `RELEASE_VALIDATION_OK` with 765 scene
  objects, 35 process segments, an animated catalyst, a present reactor shell,
  and enabled camera panning;
- the Windows player build completed successfully with 0 errors at
  `D:\Builds\PtMeOH-PrefabRefactor\PtMeOH-DigitalTwin.exe`.

The build still reports pre-existing Unity API deprecation warnings. They do
not block compilation or the player build and were not mechanically rewritten
as part of this prefab-boundary refactor.
