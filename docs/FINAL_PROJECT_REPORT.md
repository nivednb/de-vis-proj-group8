# Power-to-Methanol Digital Twin — Final Technical Report

## 1. Executive summary

This project is an interactive Unity application that explains the complete
Power-to-Methanol process through a navigable three-dimensional plant,
animated material streams, operating controls, live key performance
indicators, equipment-focused views, and educational process warnings.

The integrated application presents the following process chain:

1. renewable electricity and water electrolysis;
2. hydrogen production;
3. CO2 capture using an amine absorption/regeneration representation;
4. feed compression and mixing;
5. catalytic methanol synthesis;
6. cooling, condensation, flash separation, and gas recycle;
7. distillation/purification; and
8. methanol storage.

The implementation is intentionally lightweight enough to run on the project
laptop. The central process state is calculated by a deterministic,
steady-state educational model. All dashboard values, warning states, flow
visibility, flow speed, flow density, mixed-stream composition, reactor
activity, and catalyst color consume the same process snapshot. This shared
source of truth is the principal digital-twin architecture of the project.

## 2. Scope and claim boundary

### 2.1 What is implemented

- A full-plant 3D scene with major PtM equipment and interconnecting pipes.
- An industrial-dashboard application shell with module navigation, a stream
  legend, plant-status KPIs, equipment information, and operating controls.
- A central process simulator that responds continuously to UI inputs.
- Direction-aware flow visualization for pure, mixed-gas, liquid, and
  two-phase routes.
- Multi-species packets in mixed feed, recycle, and reactor-effluent pipes.
- A transparent reactor shell, lightweight upflow reaction visualization, and
  operating-state catalyst-bed color.
- Educational warnings for important operating limits.
- Windows desktop build and editor-side structural validation tools.

### 2.2 What is not claimed

The application is not:

- a CFD calculation;
- a rigorous equation-of-state, phase-equilibrium, or reactor-kinetics model;
- a validated Aspen Plus, Aspen HYSYS, DWSIM, MATLAB, or Modelica replacement;
- connected to live PLC, OPC UA, historian, laboratory, or plant data;
- a dynamic process-control or start-up/shutdown simulator;
- a quantitative HAZOP, SIL, relief, mechanical-design, or safety system; or
- certified for design, control, training, or plant operation.

Displayed values are educational estimates. This distinction also appears in
the dashboard as “EDUCATIONAL VISUALIZATION • SIMPLIFIED PROCESS VALUES.”

## 3. Development environment

| Area | Implementation |
| --- | --- |
| Engine | Unity `6000.4.7f1` |
| Rendering | Universal Render Pipeline `17.4.0` |
| Programming | C# MonoBehaviours and one custom ShaderLab shader |
| Input | Unity Input System `1.19.0` |
| UI | Unity uGUI and TextMesh Pro |
| Platform | Windows desktop |
| Scene | `Assets/Scenes/SampleScene.unity` |
| Version control | Git / GitHub |
| Release branch | `release/power_to_methanol-submission-v1` |

The project contains no first-party assembly-definition files. Runtime scripts
therefore compile into `Assembly-CSharp`, while scripts in `Assets/Editor`
compile into the editor assembly.

## 4. Process and chemical basis

### 4.1 Electrolysis

The electrolyzer converts water and electrical power into hydrogen. Hydrogen
production is limited by both the selected electrical-load factor and an
educational water-availability factor. Oxygen is reported from the mass ratio:

`m(O2) = 8 × m(H2)`

The current design reference is 215 kg/h H2 and 1,935 kg/h water at full design
conditions. The calculation is a mass-balance visualization, not an
electrochemical cell-voltage or stack-degradation model.

### 4.2 CO2 capture

Flue-gas throughput, amine circulation, regeneration temperature, and steam
input influence an empirical capture-efficiency function. The implementation
represents the engineering trends expected from an absorption/regeneration
system:

- greater solvent circulation generally increases capture;
- adequate regeneration temperature and steam improve solvent regeneration;
- inadequate regeneration reduces capture; and
- excessive temperature triggers an educational warning.

The model does not calculate column stages, mass-transfer coefficients,
solvent loading, degradation, corrosion, or rigorous vapor-liquid equilibrium.

### 4.3 Compression and feed preparation

Captured CO2, electrolytic H2, and recycled gas are represented as separate
species that converge in the synthesis-feed network. Compression ratio is
exposed as an operating control and the warning system identifies extreme
values. The mixed-gas visualization retains separate H2, CO2, and recycle
packets instead of assigning a physically misleading single “mixture color.”

### 4.4 Methanol synthesis

The primary reaction represented is:

`CO2 + 3 H2 → CH3OH + H2O`

The corresponding mass basis used by the simulator is:

`6 kg H2 + 44 kg CO2 → 32 kg CH3OH + 18 kg H2O`

The maximum theoretical methanol rate is the smaller of:

- `H2 rate × 32 / 6`; and
- `CO2 rate × 32 / 44`.

Reactor yield is influenced by temperature, pressure, H2/CO2 ratio, gas hourly
space velocity (GHSV), feed rate, and recycle. These relationships are
bounded heuristic response curves used to demonstrate correct qualitative
dependencies. They are not a Langmuir–Hinshelwood kinetic model and do not
resolve the reverse water-gas-shift reaction, heat transfer, pressure drop,
hot spots, or catalyst deactivation quantitatively.

The reactor model uses a side/lower feed nozzle and a top product outlet.
Reactants enter from the side, distribute into an upflow packed-bed region,
convert throughout the catalyst volume, and leave through the upper outlet as
methanol vapor, water vapor, and unreacted/recycled gas. This routing is
consistent with the existing 3D geometry and is explicitly a visual
representation rather than CFD.

### 4.5 Condensation, separation, recycle, and purification

Cooling rate and cooling-water temperature determine an empirical condenser
recovery. Separator temperature is most favorable near the configured
34 °C reference. Unconverted gas is partly recycled, and its calculated rate
is returned to the mixed synthesis feed visualization.

Reflux ratio and reboiler temperature affect an educational distillation
recovery, methanol purity, and energy index. Product water is inferred from
the reaction mass ratio:

`water rate = methanol rate × 18 / 32`

No rigorous flash, distillation-stage, azeotrope, or activity-coefficient
calculation is performed.

## 5. Software architecture

### 5.1 Shared process state

`PlantProcessSimulator` is the runtime process authority. It owns the user
inputs, recalculates a `PlantProcessSnapshot`, and exposes the latest snapshot
to all presentation systems.

```text
UI sliders
    ↓
PlantProcessSimulator
    ↓ one process snapshot
    ├── dashboard KPIs
    ├── equipment output labels
    ├── warning states
    ├── pipe speed/density/composition
    ├── reactor visual intensity
    └── catalyst-bed color
```

This avoids independent animations displaying contradictory operating states.

### 5.2 Runtime composition

The application uses a single integrated scene and several focused runtime
components. Auto-created components find scene equipment by established
names, attach visualization behavior, and build the dashboard/environment.
This approach reduced manual scene wiring during integration but creates a
dependency on stable hierarchy and route names.

### 5.3 Main runtime responsibilities

| System | Responsibility |
| --- | --- |
| `PlantProcessSimulator` | Central inputs, process calculation, outputs |
| `FinalPlantFlowRuntime` | Route discovery, species composition, live flow coupling |
| `PipeFlowAnimator` | Material-property animation for each pipe segment |
| `PipeFlow.shader` | Transparent carrier and discrete moving species packets |
| `LightweightReactorVisual` | Low-cost side-inlet/upflow reactor visualization |
| `CatalystBedColorAnimator` | Catalyst color from load, conversion, temperature |
| `IcodosDashboardRuntime` | Header, navigation, legend, status, KPIs, footer |
| `InteractiveModulePanelRuntime` | Equipment panels, sliders, live values |
| `SafetyWarningRuntime` | Educational operating-limit warnings |
| `OrbitCameraController` | Orbit, pan, zoom, overview, module focus |
| `PlantEnvironmentBuilder` | Lightweight industrial surroundings |

## 6. Pipe-flow implementation

### 6.1 Visual method

The integrated flow system uses the existing pipe meshes and a custom
transparent shader. It does not create thousands of pipe particles. Each
segment receives a `MaterialPropertyBlock` containing:

- flow offset;
- forward/reverse direction;
- packet density/tiling;
- opacity;
- intensity;
- liquid/two-phase flags; and
- up to three species colors and fractions.

This is substantially more efficient than individual GameObjects and allows
all pipe routes to remain visible across the complete plant.

### 6.2 Process coupling

Every 0.08 seconds, `FinalPlantFlowRuntime` reads the process snapshot.
Normalized stream rate controls:

- animation speed;
- packet density;
- carrier visibility; and
- packet intensity.

A stream becomes effectively invisible at negligible process flow. Therefore,
moving a process slider changes not only the displayed number but also the
corresponding visual flow.

### 6.3 Mixed streams

Mixed-feed fractions use molar estimates:

- H2 rate divided by 2.016 kg/kmol;
- CO2 rate divided by 44.01 kg/kmol; and
- recycle rate divided by an approximate 12.5 kg/kmol effective molecular
  weight.

The shader displays these as distinct packets. Fixed illustrative fractions
are used for recycle and reactor-effluent visual breakdowns where the
simplified process model does not expose a full species balance.

### 6.4 Direction

Route direction is configured from source equipment toward destination
equipment. H2 and CO2 routes marked as reversed are reversed relative to their
imported mesh ordering so that both visually converge at the T-junction.
Synthesis feed then travels toward the reactor, reactor effluent toward
cooling/separation, recycle back toward the junction, and liquid product
toward purification/storage.

Curved fittings use mesh UV/geometry fallbacks. Consequently, packet spacing
can appear somewhat different from different viewing angles or on meshes with
inconsistent UVs. This is a visualization limitation, not a change in the
calculated flow direction.

## 7. Reactor and catalyst implementation

### 7.1 Reactor transparency

The reactor shell and caps are rendered semi-transparently so internal
activity remains visible without removing the engineering geometry.

### 7.2 Lightweight reaction visual

Earlier dense reactor particle prototypes caused stability problems on the
development laptop. The current implementation intentionally caps the live
reactor population below approximately 260 particles. It depicts:

1. H2, CO2, and recycle entering through the side/lower nozzle;
2. distribution into the full packed-bed cross-section;
3. conversion activity through the catalyst region; and
4. methanol vapor, water vapor, and remaining gas moving upward to the top
   product outlet.

The effect is an explanatory flow field, not a molecular simulation.

### 7.3 Catalyst-bed color

The catalyst bed is a persistent scene mesh with a dedicated material instance.
Its color changes between:

- idle/low-load ochre;
- active green;
- high-conversion orange; and
- overtemperature red.

The color is driven by synthesis-feed load, calculated conversion, and reactor
temperature. It communicates operating state; it is not a literal prediction
of commercial catalyst color or catalyst surface chemistry.

## 8. User interface and interaction

### 8.1 Dashboard

The runtime dashboard provides:

- top navigation for Overview, Electrolyzer, Carbon Capture, Synthesis,
  Separation, and Storage;
- a process-stream color legend;
- plant status;
- efficiency, methanol-production, and CO2-utilization KPIs;
- module-specific bottom summaries;
- equipment information and process controls;
- previous/next module navigation; and
- reset and help actions.

The visual direction is based on team-supplied interface concepts and industrial process-dashboard references,
while remaining a Unity-native runtime UI.

### 8.2 Interactive controls

Available controls cover:

- plant throughput;
- electrolyzer power and water;
- flue-gas, amine, steam, and regeneration temperature;
- compressor ratio;
- reactor temperature, pressure, H2/CO2 ratio, GHSV, and feed;
- cooling rate and temperature;
- separator temperature;
- recycle ratio;
- reflux ratio and reboiler temperature; and
- storage/production-related views.

All controls write to `PlantProcessSimulator`, allowing numeric results and
visual flows to update together.

### 8.3 Camera

Arrow keys orbit the selected focus, A/D translate the camera laterally, W/S
zoom, Shift+arrow cycles equipment modules, and Home restores the overview.
Focused navigation enables close inspection without losing the complete-plant
context.

## 9. Warnings and engineering communication

The warning overlay covers illustrative cases including:

- insufficient electrolyzer water;
- low capture or capture-input mismatch;
- inadequate/excessive regeneration conditions;
- extreme compressor ratio;
- low reactor temperature/pressure;
- H2/CO2 deficiency or excess;
- excessive GHSV;
- reactor overtemperature/catalyst-sintering risk;
- inadequate condenser conditions;
- abnormal recycle;
- low methanol purity; and
- high storage level.

These thresholds support teaching and interaction. They must not be interpreted
as equipment trips, alarms, relief settings, or certified safe operating limits.

## 10. Plant environment and visual design

`PlantEnvironmentBuilder` creates a lightweight industrial context using Unity
primitives: slab, roads, safety markings, fencing, pipe racks, utility areas,
containment, tank-farm context, service frames, control structures, and
equipment plinths. This provides a complete plant setting without importing a
large commercial asset library.

The equipment models remain relatively low-poly to maintain laptop
performance. The dashboard, stream visualization, transparency, close-up
views, and animated process state provide the primary visual sophistication.

## 11. Validation and build status

### 11.1 Confirmed evidence

The editor release validator recorded:

`RELEASE_VALIDATION_OK scene=Assets/Scenes/SampleScene.unity objects=480 processSegments=35 catalyst=runtime-animated shell=present cameraPan=enabled`

The Windows build log recorded:

- result: Build Successful;
- errors: 0;
- warnings: 37; and
- executable:
  `Builds/Windows/PtMeOH-DigitalTwin.exe`.

The structural validator checks:

- the startup scene;
- missing script references;
- required route-prefix segment counts;
- catalyst and reactor-shell renderers;
- the `Custom/PipeFlow` shader;
- orbit-camera presence; and
- positive lateral pan speed.

### 11.2 Validation limitations

- No first-party EditMode or PlayMode unit tests were found.
- The 37 build warnings have not all been eliminated.
- Structural validation does not prove every camera angle or operating
  combination is visually perfect.
- Chemical results have not been calibrated against a rigorous external
  process simulator or experimental plant data.

## 12. Performance and optimization decisions

- Pipe flow is shader-driven instead of using one object per packet.
- Runtime properties use `MaterialPropertyBlock` to avoid unnecessary material
  duplication.
- Reactor particles are deliberately capped after laptop crashes during
  denser implementations.
- The environment uses reusable procedural primitives.
- Process updates are throttled rather than recalculated for every rendered
  frame.
- The project uses one primary scene and a shared process snapshot.

Remaining opportunities include consolidating legacy scripts, adding assembly
definitions, profiling draw calls and transparency overdraw, pooling any
future GameObject particles, and adding automated tests.

## 13. Known limitations and technical debt

1. Several older controllers and waypoint prototypes remain compiled alongside
   the integrated systems.
2. Runtime object discovery relies partly on hierarchy names and route prefixes.
3. Different source meshes have inconsistent UVs, affecting curved-pipe packet
   appearance.
4. The model is steady-state and empirical, with no transport delay or
   controller dynamics.
5. The reaction visual is explanatory, not spatially predictive.
6. The UI is generated at runtime, which makes some layout editing less direct
   than a fully prefab-based UI.
7. No live data connector or persistence layer is implemented.
8. No comprehensive automated regression suite is present.

## 14. Recommended next work

### Priority 1 — final presentation

- Capture a clean 1080p walkthrough of Overview and each module.
- Demonstrate at least three sliders and show synchronized numeric, warning,
  catalyst, flow-speed, and flow-density changes.
- Explain the educational model boundary before presenting values.
- Use close-ups to show separate packets in the mixed feed and upflow through
  the reactor.

### Priority 2 — academic defensibility

- Compare nominal outputs against a documented Aspen/DWSIM/literature case.
- Cite reaction, capture, separation, and operating-range sources.
- Add a table of input ranges, units, nominal values, and sensitivity.
- Add automated calculation tests for stoichiometry and monotonic trends.

### Priority 3 — software quality

- Remove or archive superseded controllers.
- Add first-party assembly definitions.
- Convert core runtime-generated UI to reusable prefabs where useful.
- Add EditMode tests for the process model and PlayMode smoke tests for the
  scene, controls, warnings, camera, and stream directions.

## 15. Conclusion

The current application is a complete interactive educational PtM digital-twin
demonstrator rather than a static 3D model. Its strongest engineering feature
is the coupling of one process snapshot to all visual and numeric outputs.
Its strongest communication feature is the ability to move from a whole-plant
overview to equipment-level inspection while stream composition and operating
state remain visible.

The project is suitable for demonstrating process integration, operating
relationships, material-flow direction, reaction stoichiometry, recycle, and
the effect of operating controls at master's-project presentation level,
provided that its simplified-model boundary is stated clearly.

