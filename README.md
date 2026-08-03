# Power-to-Methanol Digital Twin

Interactive Unity 6 educational visualization of a complete Power-to-Methanol
(PtM) plant. The application combines a full industrial plant layout, process
dashboard, interactive operating controls, engineering stream visualization,
reactor/catalyst visualization, warnings, equipment focus views, and a
lightweight steady-state process model.

> This is a master's-project educational digital-twin demonstrator. It is not a
> CFD model, a rigorous thermodynamic/kinetic simulator, a plant control system,
> or certified process-safety software.

## Current integrated version

- Branch: `codex-icodos-final-flow-integration`
- Unity: `6000.4.7f1`
- Render pipeline: URP `17.4.0`
- Startup scene: `Assets/Scenes/SampleScene.unity`
- Windows build: `D:\Builds\PowerToMethanolDigitalTwin\PowerToMethanolDigitalTwin.exe`
- Latest recorded validation: successful scene validation and successful
  Windows build with zero build errors

## Documentation

- [Final project report](docs/FINAL_PROJECT_REPORT.md)
- [Implementation and equation reference](docs/IMPLEMENTATION_REFERENCE.md)
- [Unity project context](docs/DEVELOPMENT_NOTES/UnityProjectContext.md)
- [Project health and validation status](docs/DEVELOPMENT_NOTES/UnityProjectHealth.md)
- [Progress screenshots](docs/progress-screenshots.md)

## Main systems

- Central process model and live operating snapshot
- Electrolyzer, CO2 capture, compression, methanol synthesis, condensation,
  separation/distillation, recycle, and storage visualization
- Flow speed, density, visibility, and stream composition coupled to process
  controls
- Discrete H2, CO2, and recycle packets in mixed-gas routes
- Transparent process pipes with direction-aware shader animation
- Transparent reactor shell, upflow reactor visualization, and
  conversion-dependent catalyst-bed color
- ICODOS-inspired dashboard, KPIs, module navigation, equipment controls, and
  educational warnings
- Orbit, pan, zoom, overview, and module-focus camera controls
- Lightweight procedurally generated plant environment

## Open and run

1. Open Unity Hub.
2. Add this repository folder.
3. Open it with Unity `6000.4.7f1`.
4. Open `Assets/Scenes/SampleScene.unity`.
5. Enter Play Mode.

Camera controls:

| Input | Action |
| --- | --- |
| Arrow keys | Orbit |
| A / D | Pan left / right |
| W / S | Zoom |
| Shift + arrow keys | Cycle module focus |
| Home | Return to plant overview |

## Repository structure

```text
Assets/
|-- Editor/                     # Inventory, release validation, Windows build
|-- Materials_N/                # Process-stream and equipment materials
|-- Scenes/SampleScene.unity    # Integrated plant scene
|-- Scripts_N/                  # Simulation, UI, flow, reactor, warnings
|-- Settings/                   # URP configuration
|-- *.fbx                       # Plant equipment and pipe assets
|-- PipeFlow.shader             # Multi-species packet flow shader
`-- legacy/support scripts      # Earlier panels and prototype utilities
docs/
|-- FINAL_PROJECT_REPORT.md
|-- IMPLEMENTATION_REFERENCE.md
`-- AI/                         # Persistent Unity context and health report
Packages/
ProjectSettings/
```

Unity-generated folders (`Library`, `Temp`, `Logs`, `UserSettings`, `.vs`,
`obj`) are excluded from Git.
