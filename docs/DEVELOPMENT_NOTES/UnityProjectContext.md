# Unity Project Context

## Project summary

- Repository: `de-vis-proj-group8`
- Integrated working copy: repository root (location is machine-dependent)
- Purpose: interactive educational Power-to-Methanol digital twin
- Last analyzed: 2026-07-29
- Last analyzed starting commit: `834f3a67cc8f3c87a627d4bcc68ad399ff997594`
- Release branch: `release/power_to_methanol-submission-v1`
- Remote: `https://github.com/nivednb/de-vis-proj-group8.git`

## Confirmed environment

- Unity `6000.4.7f1` (`f3c3c4248748`)
- Universal Render Pipeline `17.4.0`
- Unity Input System `1.19.0`
- uGUI and TextMesh Pro
- Startup/build scene: `Assets/Scenes/SampleScene.unity`
- Windows desktop build target
- No first-party `.asmdef` files

## Architecture

The application is a MonoBehaviour-centric, single-scene simulation.
`PlantProcessSimulator` is the source of truth. Runtime UI, warnings, pipe
flow, reactor visuals, and catalyst color observe its process snapshot.
Several systems create themselves after scene load and discover objects by
stable hierarchy/route names.

## Main source locations

| Path | Purpose |
| --- | --- |
| `Assets/Scenes/SampleScene.unity` | Integrated full plant |
| `Assets/Scripts_N/PlantProcessSimulator.cs` | Central educational process model |
| `Assets/Scripts_N/FinalFlowSystem/` | Flow integration and reactor/catalyst visuals |
| `Assets/PipeFlowAnimator.cs` | Pipe-segment material animation |
| `Assets/PipeFlow.shader` | Multi-species packet shader |
| `Assets/Scripts_N/IcodosDashboardRuntime.cs` | Dashboard shell |
| `Assets/Scripts_N/InteractiveModulePanelRuntime.cs` | Interactive controls |
| `Assets/Scripts_N/SafetyWarningRuntime.cs` | Educational warnings |
| `Assets/OrbitCameraController.cs` | Camera controls |
| `Assets/Scripts_N/PlantEnvironmentBuilder.cs` | Industrial environment |
| `Assets/Editor/` | Inventory, validation, and build tooling |
| `docs/` | Report, implementation reference, project context |

## Startup flow

1. `SampleScene` loads.
2. Runtime bootstrap methods ensure the process simulator and presentation
   systems exist.
3. The flow runtime finds renamed routes and applies `Custom/PipeFlow`.
4. The dashboard and module panels bind to the simulator.
5. Reactor transparency, lightweight upflow visualization, and catalyst color
   are configured.
6. Environment and warning overlays are built.
7. All systems update from the shared process snapshot.

## Important packages

Package presence does not prove active feature use.

| Package | Version | Confirmed use |
| --- | --- | --- |
| URP | 17.4.0 | Yes |
| Input System | 1.19.0 | Yes |
| uGUI | 2.0.0 | Yes |
| Test Framework | 1.6.0 | Package present; no first-party tests found |
| AI Navigation | 2.0.12 | Package present; no core dependency confirmed |
| Visual Scripting | 1.9.11 | Package present; no core dependency confirmed |

## Controls

- Arrow keys: orbit
- A/D: lateral pan
- W/S: zoom
- Shift+arrow: module focus
- Home: overview
- Dashboard sliders: write into `PlantProcessSimulator`

## Validation status

- Editor release validation: passed.
- Windows build: succeeded with 0 errors and 37 warnings.
- Build output:
  `Builds/Windows/PtMeOH-DigitalTwin.exe`
- First-party automated tests: none detected.
- Chemical calibration against rigorous external simulation: not performed.

## Constraints

- Preserve the central process snapshot as the only presentation-data source.
- Preserve renamed route prefixes; flow discovery depends on them.
- Preserve mixed-gas packets as separate species.
- Preserve reactor side/lower inlet to top-outlet upflow consistent with the
  imported geometry.
- Keep the lightweight reactor population bounded for laptop stability.
- Treat all process values and warnings as educational approximations.
- Submission changes are reviewed through a pull request into `main`.

## Canonical documentation

- `docs/FINAL_PROJECT_REPORT.md`
- `docs/IMPLEMENTATION_REFERENCE.md`
- `docs/DEVELOPMENT_NOTES/UnityProjectHealth.md`
