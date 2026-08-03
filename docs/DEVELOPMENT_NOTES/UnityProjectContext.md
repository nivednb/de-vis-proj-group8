# Unity Project Context

## Project Summary

- Project root: `work/de-vis-proj-group8`
- Purpose: Interactive educational Power-to-Methanol plant digital twin for OVGU.
- Last analyzed: 2026-07-28
- Last analyzed commit: `d7b31827d41cfc160a6d259beed60a37ef57a09f`
- Integration branch: `codex-icodos-complete`

## Confirmed Environment

- Unity version: `6000.4.7f1` (`f3c3c4248748`)
- Render pipeline: Universal Render Pipeline 17.4.0
- Input system: Unity Input System 1.19.0, with a project input-actions asset
- Target platform: Windows desktop is the intended first build target

## Important Packages And Frameworks

| Area | Finding | Confidence | Evidence |
| --- | --- | --- | --- |
| UI | uGUI and TextMesh Pro | High | `Packages/manifest.json`, scene, runtime scripts |
| Rendering | URP with VFX Graph available | High | package manifest and URP settings assets |
| Input | New Input System | High | package manifest and `InputSystem_Actions.inputactions` |
| Process model | Lightweight illustrative `PlantProcessSimulator` | High | `Assets/Scripts_N/PlantProcessSimulator.cs` |
| Unity MCP | Official Unity AI Assistant package is present, but Editor connection is unavailable | High | package manifest; no Unity Editor installed on host |

## Directory Structure

| Path | Purpose | Confidence | Evidence |
| --- | --- | --- | --- |
| `Assets/Scenes/SampleScene.unity` | Full plant and UI startup scene | High | enabled Build Settings scene |
| `Assets/Scripts_N/` | Process simulation, environment, UI, warning, and flow runtime systems | High | representative script inspection |
| `Assets/Prefabs_N/Flow/` | Flow visualization prefab | High | asset inventory |
| `Assets/Settings/` | URP renderer and pipeline assets | High | asset inventory |
| `docs/images/` | Current-state and ICODOS visual references | High | repository docs |

## Assembly Boundaries

There are no first-party `.asmdef` files. First-party runtime scripts compile into the default
`Assembly-CSharp`; editor tooling under `Assets/Editor` compiles separately when present.

## Scenes And Startup Flow

- Build scenes: `Assets/Scenes/SampleScene.unity`
- Likely startup scene: `SampleScene`
- Runtime bootstrap components auto-create the process simulator, plant environment, flow
  visualization, warning overlay, reactor detail visualization, and module panels after scene load.

## Architecture

| Pattern | Finding | Confidence | Evidence |
| --- | --- | --- | --- |
| Scene composition | MonoBehaviour-centric single-scene application | High | scene and scripts |
| Runtime augmentation | Several focused auto-created runtime components | High | `RuntimeInitializeOnLoadMethod` usage |
| Process state | `PlantProcessSimulator` owns the illustrative operating snapshot | High | simulator implementation |
| Presentation | UI and flow systems observe the process snapshot | High | runtime UI/flow scripts |
| Camera | One orbit controller with module focus points | High | `OrbitCameraController.cs` |

## Coding Conventions

- Namespace style: global namespace
- Serialized fields: mix of public Inspector fields and `[SerializeField] private`
- Async: none in first-party runtime code
- Comments/docs: XML summaries for major runtime components; domain intent is documented

## Testing And Validation

- EditMode tests: none detected
- PlayMode tests: none detected
- CI/build validation: none detected
- Current limitation: Unity Editor `6000.4.7f1` and Unity Hub are not installed on this laptop

## Available Project Tooling

Repository inspection is available from a local checkout. Compilation, scene validation, and build
testing require Unity Editor `6000.4.7f1`.

## Important Constraints

- This is an educational engineering visualization, not CFD, Aspen, industrial validation, or
  chemically accurate plant control software.
- Preserve lightweight runtime visualization and modular separation between process state, UI,
  camera, warnings, environment, and flow.
- Treat the interface as an educational visualisation; displayed values require independent
  engineering validation before any real-world use.
- Existing remote branches must remain untouched. All integration work belongs on
  `codex-icodos-complete`.

## Unknowns And Confidence

- Actual Unity compilation, package import state, scene references, and runtime appearance remain
  unverified until the exact Editor version is installed and connected.

## Source Files Inspected

- `README.md`
- `docs/FINAL_FLOW_SIMULATION_HANDOFF.md`
- `docs/images/unity-full-plant-game-view.png`
- `Packages/manifest.json`
- `Packages/packages-lock.json`
- `ProjectSettings/ProjectVersion.txt`
- `ProjectSettings/EditorBuildSettings.asset`
- representative scripts under `Assets/` and `Assets/Scripts_N/`

