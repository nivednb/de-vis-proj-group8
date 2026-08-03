# Power-to-Methanol Digital Twin

Interactive Unity 6 educational visualization of a complete Power-to-Methanol
(PtM) plant. The application combines a full industrial plant layout, process
dashboard, interactive operating controls, engineering stream visualization,
reactor/catalyst visualization, warnings, equipment focus views, and a
lightweight steady-state process model.

> This is a master's-project educational digital-twin demonstrator. It is not a
> CFD model, a rigorous thermodynamic/kinetic simulator, a plant control system,
> or certified process-safety software.

## Evaluated project baseline

- Submission preparation branch: `agent/submission-ready-masters-enhancement`
- Unity: `6000.4.7f1`
- Render pipeline: URP `17.4.0`
- Startup scene: `Assets/Scenes/SampleScene.unity`
- Default Windows build: `Builds/Windows/PtMeOH-DigitalTwin.exe`
- Release status: run the validation steps below on the final submission commit;
  recorded results must not be treated as a substitute for a fresh build.

## Documentation

- [Final project report](docs/FINAL_PROJECT_REPORT.md)
- [Implementation and equation reference](docs/IMPLEMENTATION_REFERENCE.md)
- [Unity project context](docs/DEVELOPMENT_NOTES/UnityProjectContext.md)
- [Project health and validation status](docs/DEVELOPMENT_NOTES/UnityProjectHealth.md)
- [Progress screenshots](docs/progress-screenshots.md)
- [Professor evaluation guide](docs/PROFESSOR_EVALUATION_GUIDE.md)
- [Submission readiness checklist](docs/SUBMISSION_READINESS_CHECKLIST.md)
- [Reproducible build instructions](docs/REPRODUCIBILITY_AND_BUILD.md)
- [Architecture and asset policy](docs/ARCHITECTURE_AND_ASSET_POLICY.md)
- [Tool-use and provenance note](docs/TOOLS_AND_ASSISTANCE.md)

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
`-- DEVELOPMENT_NOTES/          # Unity context and technical health notes
Packages/
ProjectSettings/
```

Unity-generated folders (`Library`, `Temp`, `Logs`, `UserSettings`, `.vs`,
`obj`) are excluded from Git.

## Validate and build

In Unity, use:

1. `Tools > Power-to-Methanol > Validate Release Scene`.
2. Confirm the Console contains `RELEASE_VALIDATION_OK` and no errors.
3. `Tools > Power-to-Methanol > Build Windows Application`.
4. Run the generated executable from `Builds/Windows` and complete the
   evaluation checklist in `docs/SUBMISSION_READINESS_CHECKLIST.md`.
