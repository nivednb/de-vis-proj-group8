# Implementation and Engineering Reference

This appendix records the current implementation at source-code level. Paths
are relative to the repository root.

## 1. Core file inventory

### 1.1 Integrated runtime

| File | Current role |
| --- | --- |
| `Assets/Scripts_N/PlantProcessSimulator.cs` | Central process inputs, equations, snapshot |
| `Assets/Scripts_N/FinalFlowSystem/FinalPlantFlowRuntime.cs` | Route discovery and process-to-flow coupling |
| `Assets/PipeFlowAnimator.cs` | Per-segment shader property animation |
| `Assets/PipeFlow.shader` | Transparent carrier and moving species packets |
| `Assets/Scripts_N/FinalFlowSystem/PlantFlowKind.cs` | Stream classifications |
| `Assets/Scripts_N/FinalFlowSystem/LightweightReactorVisual.cs` | Low-cost upflow reactor visual |
| `Assets/Scripts_N/FinalFlowSystem/CatalystBedColorAnimator.cs` | Catalyst operating-state color |
| `Assets/Scripts_N/IcodosDashboardRuntime.cs` | Main application dashboard |
| `Assets/Scripts_N/InteractiveModulePanelRuntime.cs` | Equipment panels and controls |
| `Assets/Scripts_N/SafetyWarningRuntime.cs` | Educational warnings |
| `Assets/OrbitCameraController.cs` | Camera navigation and focus |
| `Assets/Scripts_N/PlantEnvironmentBuilder.cs` | Runtime industrial environment |
| `Assets/Scripts_N/RuntimeValidationCapture.cs` | Automated runtime screenshot helper |

### 1.2 Editor and release tooling

| File | Role |
| --- | --- |
| `Assets/Editor/DeepProjectInventory.cs` | Deep hierarchy/asset inventory |
| `Assets/Editor/ReleaseValidation.cs` | Structural release validation |
| `Assets/Editor/WindowsBuild.cs` | Reproducible Windows build entry point |

### 1.3 Legacy and support files

| File | Status |
| --- | --- |
| `Assets/AbsorberController.cs` | Earlier absorber panel/controller |
| `Assets/ReactorController.cs` | Earlier reactor panel/controller |
| `Assets/OverviewPanelController.cs` | Earlier overview UI |
| `Assets/CameraController.cs` | Earlier camera support |
| `Assets/PlantPipeManager.cs` | Legacy primitive pipe generator; disabled by default |
| `Assets/ReactorPanelToggle.cs` | Legacy reactor-panel toggle |
| `Assets/Scripts_N/FlowPath.cs` | Waypoint path support/prototype |
| `Assets/Scripts_N/FlowFollower.cs` | Waypoint follower support/prototype |
| `Assets/Scripts_N/PipeWaypointGenerator.cs` | Waypoint generation support/prototype |

These files should not be described as the primary integrated architecture.

## 2. Nominal process inputs

| Variable | Nominal/default |
| --- | ---: |
| Plant throughput | 100% |
| Electrolyzer power | 75% |
| Water feed | 100% |
| Flue-gas feed | 100% |
| Amine circulation | 65% |
| Regeneration steam | 70% |
| Regenerator temperature | 105 °C |
| Compressor pressure ratio | 3 |
| Reactor temperature | 250 °C |
| Reactor pressure | 70 bar |
| H2/CO2 ratio | 3.0 |
| GHSV | 8,000 h⁻¹ |
| Reactor feed | 100% |
| Cooling rate | 70% |
| Cooling temperature | 24 °C |
| Separator temperature | 34 °C |
| Recycle | 65% |
| Reflux ratio | 3.2 |
| Reboiler temperature | 98 °C |

Design reference rates:

| Stream | Rate |
| --- | ---: |
| H2 | 215 kg/h |
| CO2 | 1,510 kg/h |
| Water feed | 1,935 kg/h |
| Methanol | 1,250 kg/h |
| Storage capacity | 12,000 kg |

## 3. Process equations

### 3.1 Electrolyzer

```text
powerFactor = powerPercent / 100
waterFactor = waterPercent / 100
electrolyzerFactor = min(powerFactor, Lerp(0.15, 1.10, waterFactor))
H2 = designH2 × plantRamp × electrolyzerFactor
waterFeed = designWater × plantRamp × waterFactor
O2 = H2 × 8
```

### 3.2 Capture

```text
amineFactor = (aminePercent / 100)^0.55
regenFactor = InverseLerp(82, 118, regenerationTemperature)
steamFactor = (steamPercent / 100)^0.45
captureEfficiency =
    clamp(0.18 + 0.46×amineFactor
               + 0.22×regenFactor
               + 0.14×steamFactor)
capturedCO2 = inputCO2 × captureEfficiency
```

### 3.3 Synthesis feed and yield

```text
syngasFeed = (H2 + capturedCO2) × feedFactor
temperatureRate = InverseLerp(210, 255, reactorTemperature)
highTemperaturePenalty = 0 … 0.32 between 255 and 310 °C
pressureFactor = reactorPressure / 100
ratioFactor = 1 - clamp(|H2CO2Ratio - 3| / 3) × 0.42
residenceFactor = clamp(8000 / GHSV, 0.35, 1.35)
recycleBoost = Lerp(0.86, 1.18, recyclePercent / 100)
reactorYield = bounded product of the above factors
```

Stoichiometric production:

```text
theoreticalFromH2 = H2 × 32 / 6
theoreticalFromCO2 = capturedCO2 × 32 / 44
theoreticalMethanol = min(theoreticalFromH2, theoreticalFromCO2)
```

### 3.4 Recovery and purification

```text
condenserRecovery = Lerp(0.55, 0.98, coolingFactor)
separatorFactor = 1 - temperature penalty around 34 °C
distillationFactor =
    0.72 + 0.11×normalizedReflux + 0.17×normalizedReboiler
methanolPurity =
    clamp(90 + 7.2×normalizedReflux + 2.4×normalizedReboiler,
          88, 99.85)
distillationEnergyIndex =
    18 + 12×refluxRatio + 36×normalizedReboiler
methanol =
    min(designMethanol, theoreticalMethanol)
    × reactorYield
    × condenserRecovery
    × separatorFactor
    × distillationFactor
recycleGas = unconvertedGas × recycleFraction × 0.36
overallEfficiency = methanol / theoreticalMethanol
waterProduct = methanol × 18 / 32
```

## 4. Flow response

`FinalPlantFlowRuntime` refresh interval: 0.08 s.

```text
response = sqrt(normalizedMassFlow)
speed = baseSpeed × Lerp(0.35, 1.35, response)
density = baseDensity × Lerp(0.55, 1.30, normalizedMassFlow)
intensity = baseIntensity × Lerp(0.25, 1.25, response)
visible = normalizedMassFlow >= 0.005
```

Reference normalizations:

| Route family | Reference rate |
| --- | ---: |
| H2 | 215 kg/h |
| CO2 | 1,510 kg/h |
| Synthesis feed | 1,725 kg/h |
| Methanol | 1,250 kg/h |
| Crude condensate | 1,953.125 kg/h |
| Recycle | 450 kg/h |

## 5. Stream/species mapping

| Process stream | Visual species |
| --- | --- |
| H2 route | H2 |
| CO2 route | CO2 |
| Mixed synthesis feed | calculated H2 + CO2 + recycle |
| Recycle gas | 74% recycle, 20% CO2, 6% H2 (illustrative) |
| Reactor effluent | 58% hot product, 32% recycle, 10% water (illustrative) |
| Crude condensed product | 64% methanol, 36% water (illustrative) |
| Purified product | methanol |

Mixed-feed molar weighting:

```text
nH2 = H2 mass rate / 2.016
nCO2 = CO2 mass rate / 44.01
nRecycle = recycle mass rate / 12.5
species fraction = species molar estimate / total molar estimate
```

The recycle effective molecular weight and fixed effluent fractions are visual
approximations because the central model does not expose a complete
component-by-component stream table.

## 6. Stream colors

Values below are approximate sRGB hex conversions of the configured Unity
colors.

| Species/stream | Unity RGB | Hex |
| --- | --- | --- |
| Hydrogen | (0.10, 1.00, 0.22) | `#1AFF38` |
| Carbon dioxide | (0.86, 0.94, 1.00) | `#DBF0FF` |
| Recycle gas | (0.72, 0.28, 1.00) | `#B847FF` |
| Rich amine | (0.04, 0.72, 0.42) | `#0AB86B` |
| Lean amine | (0.05, 0.92, 0.52) | `#0DEB85` |
| Hot syngas | (1.00, 0.58, 0.12) | `#FF941F` |
| Reactor effluent | (1.00, 0.42, 0.12) | `#FF6B1F` |
| Crude vapor/product | (0.72, 0.18, 1.00) | `#B82EFF` |
| Crude liquid | (0.35, 0.42, 1.00) | `#596BFF` |
| Methanol product | (0.20, 0.78, 1.00) | `#33C7FF` |

Catalyst states:

| State | Unity RGB | Meaning |
| --- | --- | --- |
| Idle | (0.72, 0.60, 0.24) | Low/no synthesis load |
| Active | (0.12, 0.78, 0.40) | Normal loaded operation |
| Converting | (1.00, 0.48, 0.04) | High conversion/activity |
| Overtemperature | (1.00, 0.08, 0.02) | Temperature warning |

## 7. Route direction reference

| Route | Engineering direction |
| --- | --- |
| Electrolyzer H2 | Electrolyzer → mixing T-junction |
| Captured CO2 | Capture/compression → mixing T-junction |
| Recycle gas | Separator/recycle loop → mixing T-junction |
| Mixed feed/syngas | T-junction → reactor feed preparation → reactor side inlet |
| Reactor internal | Side/lower inlet → packed bed → top outlet |
| Reactor effluent | Reactor top outlet → condenser/separation |
| Crude methanol/water | Condenser/separator → purification |
| Methanol product | Purification → storage |

`reverse=true` on an imported route means the shader direction is corrected
against mesh ordering; it does not reverse the engineering process.

## 8. Reactor visual limits

- Maximum live population: approximately 260 particles.
- Simulation space: world-oriented visual root to avoid inherited 3× imported
  model scaling.
- Shell alpha: approximately 0.16.
- Cap alpha: approximately 0.18.
- Entry: side/lower product-named geometry used as current feed side in the
  imported model.
- Exit: top nozzle.
- Conversion occupies the catalyst-bed volume.
- Products shown: methanol vapor, water vapor, remaining gas.

## 9. Educational warning thresholds

| Module | Examples |
| --- | --- |
| Electrolyzer | Low water relative to power |
| Capture | Low capture; flue/amine mismatch |
| Regeneration | Insufficient steam/temperature; excessive temperature |
| Compressor | Ratio ≥90 warning, ≥98 critical on UI scale |
| Reactor | ≥285 °C critical; >270 °C sintering risk; ≤215 °C low rate |
| Reactor feed | Pressure <60 bar; ratio <2.5 H2-deficient; >4.5 H2-excess |
| Residence | GHSV >9,500 h⁻¹ |
| Condenser | Cooling temperature ≥38 °C; cooling rate ≤20%; recovery <70% |
| Recycle | <25% or >90% |
| Distillation | Purity <95%; reflux >4.2; reboiler ≥108 °C |
| Storage | ≥85% warning; ≥95% critical |

These are presentation/teaching thresholds, not certified trip limits.

## 10. Assets and equipment represented

The integrated project contains models for the absorber column, desorber
column, electrolyzer, compressor, condenser/heat exchanger, flash separator,
distillation column, reactor and reactor skirt, H2/CO2/methanol tanks, pipe
bends, T-junction, saddles, skirts, and structural/support elements.

Important reactor children include:

- `Reactor_Shell`;
- `Catalyst_Bed`;
- caps;
- feed/product nozzles;
- cooling-water nozzles/flanges; and
- skirt/support geometry.

## 11. Build and validation commands

Editor tooling supplies:

- deep inventory;
- release validation; and
- Windows build.

The current logs record a successful structural validation and successful
Windows build. See `FINAL_PROJECT_REPORT.md` for the exact evidence and
limitations.
