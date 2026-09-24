# Scientific basis and validation

The application is an educational steady-state process simulator with a 3D visualization. It has no live plant-data connection. This document separates numerical verification of the implemented model from empirical validation against real process data.

## Research question and contribution

The project investigates whether a shared, lightweight process model can make relationships between operating inputs, material streams, recycle and product recovery easier to inspect in an interactive whole-plant application. The main contribution is the combination of deterministic calculations, species-aware visualization, controllable inputs and reproducible one-factor-at-a-time (OFAT) experiments.

No user study was conducted. Improved learning is therefore an intended educational benefit rather than a measured result.

## Electrolysis

Water electrolysis follows `2 H2O -> 2 H2 + O2` [S1]. On the mass basis used by the synthesis model, hydrogen production is limited by both electrical capacity and the available water. The water limit is calculated from the mass ratio `2.01588/18.01528`. Oxygen production is calculated from the corresponding stoichiometric balance, and unused water remains as unreacted feed.

This is a stoichiometric capacity calculation rather than a detailed electrical-efficiency model. During final verification, an earlier non-zero response at zero water was identified and corrected so that zero water or zero power produces zero electrolytic hydrogen.

## Independent synthesis-loop benchmark

For the independent recycle check, fresh molar feeds are represented by F_C for CO2 and F_H for H2. The per-pass conversion is x, the recycled fraction is r, and the reaction extent is e in kmol/h. Products are assumed to be removed before recycle, both gas species use the same recycle split, and inerts and side reactions are omitted.

At steady state:

`C_in = (F_C - r e)/(1-r)`

`H_in = (F_H - 3 r e)/(1-r)`

Substitution into `e = min(x C_in, H_in/3)` gives:

`e = min(x F_C/[1-r(1-x)], F_H/3)`.

The application uses fixed-point iteration, while the validator uses this closed-form expression as an independent comparison. Methanol and water are calculated as `32.04186 e` and `18.01528 e` kg/h. External purge is `F_C-e` and `F_H-3e` kmol/h. Carbon, hydrogen and total external mass are checked for closure. Internal recycle is not counted as an external input or output.

For a test case with 1,152 kg/h CO2, 158 kg/h H2, x = 0.20 and r = 0.95, the calculated overall CO2 conversion is approximately 83.3333% and methanol production is approximately 698.94 kg/h before downstream recovery. This is an analytical verification case, not a measured plant operating point.

`SubmissionValidation.Run` evaluates 240 cases including zero reactants, zero conversion, zero recycle, H2-limited feed and recycle values up to 0.999. It compares the production calculation with the analytical solution and checks atom balances, non-negative purge, solver convergence and external mass closure below 0.001%.

An expanded validation run exposed a convergence problem at high recycle because the stopping criterion was scaled to the large internal recycle flow. The corrected solver uses fresh-feed scaling, a relative tolerance of 1e-10 and a 32,768-iteration limit. A recycle fraction of exactly 1 is singular, so the model internally limits it to 0.999. The UI endpoint of 100% should therefore be interpreted as an approximation rather than a sealed no-purge steady state.

## Empirical context and limits

Reference [S2] reports experiments at 240–280 °C, 40–80 bar and H2/CO2 ratios of 3.0–3.4. These ranges support the use of temperature, pressure and feed ratio as meaningful educational controls, but they do not validate the application's uncalibrated response equation. The reported influence of hydrogen ratio also shows why the simplified symmetric penalty around a ratio of 3 should not be interpreted as universal reaction kinetics.

Reference [S3], Section 3.2 and Table 2, reports 17.8% CO2 conversion for its CZ-2 catalyst at 240 °C, 30 bar and H2/CO2 = 3. The catalyst, space-velocity basis, CO side-product and selectivity differ from the assumptions used in this project. A direct matched-case validation error would therefore not be meaningful, and the application is not fitted to this single literature point.

A quantitative predictive validation would require a consistent experimental dataset or reproducible process-simulation case with matching catalyst, feed composition, residence-time basis and separation boundaries. CO formation and selectivity would also need to be included. Such calibration is outside the scope of this project.

## Sensitivity methodology

`docs/evidence/nominal-inputs.json` records the baseline operating point. The Unity `Simulate` method generates 13 points for each of five factors: temperature, pressure, H2/CO2 ratio, feed and recycle. This gives 65 sensitivity points in total. Only the selected factor is changed during each sweep, and the validator checks that the original input state is not modified.

`sensitivity.csv` stores the units, single-pass conversion, refined methanol rate and recovery index. In the implemented model, temperature peaks near the specified 240 °C optimum, pressure increases the conversion correlation, the H2/CO2 ratio affects both feed availability and a simplified response penalty, feed changes throughput without changing the single-pass correlation, and recycle changes overall utilization rather than per-pass conversion.

These trends describe the behaviour of the implemented educational model. They should not be interpreted as experimentally discovered process laws or statistical causal relationships.

## Model boundaries

- The dashboard value `overallEfficiencyPercent` represents refined methanol divided by stoichiometric potential. It is a material-recovery index, not an energy-efficiency calculation.
- Electricity consumption, compressor work and heat duties are not calculated.
- Capture, condenser recovery, purity and distillation-energy relationships are educational correlations and have not been calibrated using the cited papers.
- The 1,250 kg/h methanol design value is a model cap rather than a guaranteed nominal production rate.
- The plant-load schedule reaches 95% at timeline = 100 as part of the existing visualization behaviour.
- Storage changes on an accelerated time scale, while the process streams otherwise represent steady-state values. Display smoothing should not be interpreted as a dynamic process model.
- Reactor colour and particle animations are visual explanations of process state. They do not represent molecular trajectories, CFD results, literal catalyst colour or certified safety instrumentation.
- Life-cycle emissions, economics, detailed equipment sizing, thermal safety analysis and measured learning effectiveness are outside the project scope.

## Sources (accessed 2026-09-22)

[S1] US Department of Energy, *Hydrogen Production: Electrolysis*, reaction description. https://www.energy.gov/cmei/fuels/hydrogen-production-electrolysis

[S2] Che Yifei, Li Tao, Zhang Haitao (2020), *Intrinsic Kinetics of Hydrogenation of CO2 towards Methanol on a Cu/ZnO/Al2O3 Modified Catalyst*, 46(3), 326-333. DOI: 10.14135/j.cnki.1006-3080.20190227001.

[S3] Lei, Zheng and Liu (2019), *Cylindrical shaped ZnO combined Cu catalysts for the hydrogenation of CO2 to methanol*, RSC Advances 9, 13696-13704. DOI: 10.1039/C9RA00658C.

[S4] European Commission JRC (2016), *Techno-economic and environmental evaluation of CO2 utilisation for fuel production. Synthesis of methanol and formic acid*, JRC99380. This source is used only to provide context for the system boundary; cost and emission results are not transferred to the model.

## Final numerical verification

The final clean-clone suite executed **1,586 assertions** on application source `9e3ea9c192d506e33e4227586efdd5cd5da994bd`. The editor CSV external mass-closure error was **1.13699e-09%**, and the player CSV closure error was **4.25878532e-09%** at its tested operating point.

Re-summing rounded exported stream values can produce a slightly larger residual. In the 25% recycle regression case this was 0.0002 kg/h. These values demonstrate numerical consistency of the implemented calculations; they are not evidence that the simplified model has been empirically validated as an industrial predictive model.

Detailed execution results are recorded in `SUBMISSION_STATUS.md` and `NUMERICAL_RESULTS.md`.
