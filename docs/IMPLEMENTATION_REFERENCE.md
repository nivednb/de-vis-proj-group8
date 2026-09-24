# Implementation and engineering reference

Current branch: `final-submission`. Paths are repository-relative. This supersedes the earlier approximate equation inventory.

## Source map

| Source | Role |
|---|---|
| `Assets/Scripts_N/PlantProcessSimulator.cs` | Live input owner, `ProcessInputs`, `ProcessSnapshot`, `Simulate`, storage clock/interlock |
| `Assets/Scripts_N/RecycleMassBalanceEngine.cs` | Species-resolved iterative recycle/purge balance and per-pass correlation |
| `Assets/Scripts_N/MassBalanceCsvExporter.cs` | Synthesis-boundary CSV with units and assumptions |
| `Assets/Scripts_N/FinalFlowSystem/FinalPlantFlowRuntime.cs` | Prefix-based routes, shared clock, staged activation, species/flow coupling |
| `Assets/PipeFlowAnimator.cs`, `Assets/PipeFlow.shader` | Per-renderer flow properties and shader |
| `Assets/Scripts_N/FinalFlowSystem/LightweightReactorVisual.cs` | Bounded educational reactor upflow effect |
| `Assets/Scripts_N/FinalFlowSystem/CatalystBedColorAnimator.cs` | Operating-state catalyst color |
| `Assets/Scripts_N/IcodosDashboardRuntime.cs` | Six-page dashboard, split analytics pane, help, preset/export actions |
| `Assets/Scripts_N/InteractiveModulePanelRuntime.cs` | Equipment controls and slider bindings |
| `Assets/Scripts_N/OfatTimelineGraphRuntime.cs` | Controlled sweeps and time-series experiment recording |
| `Assets/Scripts_N/SafetyWarningRuntime.cs` | Educational warning thresholds |
| `Assets/OrbitCameraController.cs` | Mouse/keyboard camera navigation and focus |
| `Assets/Scripts_N/RuntimeValidationCapture*.cs` | Opt-in screenshot and player acceptance harness |
| `Assets/Editor/SubmissionValidation.cs` | Numerical oracle, edge cases, evidence CSVs |
| `Assets/Editor/*Validation.cs`, `WindowsBuild.cs` | Existing scene/control/recycle/CSV checks and Windows build |

## Baseline and dimensions

Exact baseline fields and results are generated in `docs/evidence/nominal-inputs.json` and `nominal-output.json`. Defaults include 250 C, 70 bar, H2/CO2=3 mol/mol, GHSV=8000 h^-1, 65% recycle, 75% electrolyzer power, 100% water and reactor feed. Timeline=100 produces a 95% plant ramp under the retained startup schedule. Design assumptions are 215 kg/h H2, 1510 kg/h CO2, 1935 kg/h water, 1250 kg/h methanol cap and 12000 kg storage.

## Equations

For ramp L, power fraction p and water fraction w (each capacity fraction clamped to 0..1):

`water = 1935 L w`

`H2 = min(215 L p, water * 2.01588/18.01528)`

`O2 = H2 * 15.99940/2.01588`

A 130% slider setting may saturate at design capacity; percentages above 100 are not evidence of modeled overload physics.

Capture efficiency is `clamp01(0.18 + 0.46 a^0.55 + 0.22 t + 0.14 s^0.45)` with a=amine/100, s=steam/100, and t=InverseLerp(82,118,regenerator C). Captured CO2 is available flue-gas CO2 times this factor.

Per-pass conversion is `clamp(0.25 exp[-0.0005(T-240)^2] (max(1,P)/70)^0.35 (8000/max(1000,GHSV))^0.2 * q, 0.05, 0.35)`, where `q=1-0.42 clamp01(abs(ratio-3)/3)`. This is an educational response curve, not fitted kinetics. The live model limits reactor feeds by both available species and the requested molar ratio.

The recycle solver iterates species recycle until change is below `max(1e-9,1e-10*max(1,F_CO2,F_H2))` kmol/h or 32768 iterations. r is bounded to 0..0.999. It reports convergence and external mass error. The independent algebraic solution and boundary conditions are in `SCIENTIFIC_VALIDATION.md`.

Condenser recovery is 0.55..0.98 from cooling flow/temperature; separator recovery has a maximum at 34 C; distillation recovery combines reflux and reboiler factors. Refined methanol equals min(reactor methanol,1250) times all three recovery factors. Product purity is a separate heuristic (88..99.85%). Reactor-product water is from reaction extent, before downstream recovery, not inferred from refined methanol.

The legacy efficiency field is `100 * clamp01(refined methanol/max(stoichiometric methanol,1))`. It is a material recovery index. Compressor ratio has UI/warning effects but no compressor work or thermodynamic pressure calculation.

## Time and warnings

Snapshot displays are exponentially smoothed with rate 4.5/s. Storage adds production times 0.035 simulated hours per real second. A 99% high-high inventory condition latches production shutdown until reset/unload. This is accelerated teaching behavior, not validated process dynamics. Pause stops model updates and flow clock.

Visible reactor overlay: critical at >=285 C, caution above 270 C, low-temperature caution <=215 C, low-pressure caution <60 bar, H2 deficiency <2.5 and excess >4.5, high GHSV >9500 h^-1. The separate engine helper `IsHotspotAlarmActive` tests >260 C; it is not the visible overlay threshold. These thresholds are project assumptions, not certified equipment limits.

## Validation boundaries

No broad NUnit EditMode/PlayMode suite exists. Custom batch-compatible editor validators now provide repeatable numerical tests. The runtime harness checks component behavior and callback wiring; human visual/input inspection is still required. No empirical calibration, energy balance, side chemistry, real-time data feed or user study is implemented.

## Guided tutorial integration

The 23-step `TutorialRuntime` is selectively ported from `chaitanya-dev` commits cf8db347, 973cc52b and a25253e5. That branch was not merged. Dashboard hooks reuse current page/analytics methods and preserve the right-side analytics pane and reactor/OFAT lock ownership. `PLANT PROCESS` is the current label for the Process Map requested in the integration brief. Its title/counter, content columns and buttons have separate rectangles.

The tour starts when `PtmDigitalTwin.TutorialCompleted` is absent/zero. SKIP or FINISH marks it complete; HELP → START TUTORIAL always replays it. NEXT/PREVIOUS and Enter/Space, Backspace, Esc navigate. The camera step opens a pointer-accessible spotlight for mouse drag, Shift-drag and wheel zoom. Other steps block pointer interaction beneath the overlay. The arrow targets the actual Warning Panel, even when no warning is visible. Tutorial navigation changes views and lock selection, not process input values. Validation mode isolates the completion preference; it is not a test of OS registry persistence across installations.
