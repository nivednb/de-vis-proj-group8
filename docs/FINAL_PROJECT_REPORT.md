# Power-to-Methanol: final technical report

## Release and scope

Branch: `final-submission`, derived from `nived-merged-improvements` at `be9dd88`. The exact submitted commit and binary checksums are recorded in the release manifest supplied beside the executable. Preparation began 2026-09-22 and resumed 2026-09-23; execution timestamps are retained in the evidence. Team/institution details remain explicitly pending in `TOOLS_AND_ASSISTANCE.md`.

This master's-project artifact is an interactive educational process simulator and plant visualization. Its research question, independent verification, scientific sources and limitations are detailed in [Scientific validation](SCIENTIFIC_VALIDATION.md). It is not a live-data digital twin, validated kinetic/thermodynamic package or certified engineering/safety tool.

## Implemented system

The single `Assets/Scenes/SampleScene.unity` contains the authored plant. Runtime systems add an industrial setting, dashboard, equipment controls, species-aware pipe motion, a transparent reactor, lightweight upflow effect, catalyst-state coloring, warnings and analytics. The process route covers electrolysis, CO2 capture/regeneration, compression/mixing, synthesis, cooling/separation, recycle/purge, purification and storage.

The dashboard has OVERVIEW, PLANT PROCESS, FLOW LAB, REACTOR LAB, ANALYTICS and SIMULATION navigation. Analytics uses a right-hand split pane; underlying dashboard panels are hidden to avoid overlap while the plant remains available. The pre-existing uncommitted split-pane improvement was preserved. A 23-step first-launch/replay tutorial is selectively adapted from the three Sep 11 Chaitanya commits; its instructions match current automatic OFAT and docked analytics. Runtime UI uses uGUI with both Text and TextMesh Pro; it is generated rather than prefab-authored.

## Architecture and implementation choices

`PlantProcessSimulator` owns input state and `ProcessSnapshot`. Its `Simulate(ProcessInputs)` evaluates hypothetical operating points without changing live inputs/recycle state, using configured design constants. `RecycleMassBalanceEngine.Calculate` supplies the species-resolved synthesis loop. `FinalPlantFlowRuntime`, UI, warnings and catalyst visuals consume the same process state. This avoids separate equations being embedded in each display.

Routes are discovered by stable object-name prefixes. Per-renderer shader animation shares a plant clock and reaction prerequisites; this does not by itself prove packet continuity at every geometry seam. Runtime generation makes integration lightweight, but hierarchy-name dependence and transparency/UV behavior remain limitations.

The bounded fixed-point recycle solver retains the original formulation and now scales its convergence tolerance to fresh feed. The expanded edge-case benchmark exposed and motivated this correction. The maximum iteration count is 32,768 to cover recycle up to 99.9%. This has a greater worst-case computational cost than normal operating points; sustained target-machine behavior is recorded separately.

## Equations and assumptions

See [Implementation reference](IMPLEMENTATION_REFERENCE.md) for exact formulas, units and controls, and [Scientific validation](SCIENTIFIC_VALIDATION.md) for the independent derivation.

Electrolysis respects `2 H2O -> 2 H2 + O2`: electrical capacity and available water independently bound hydrogen output. Zero water or power gives zero hydrogen/oxygen and no synthesis product at the evaluated steady state. Unused water is not treated as product.

Synthesis uses `CO2 + 3 H2 -> CH3OH + H2O`, precise internally consistent molar masses, ideal product removal, and equal H2/CO2 recycle splits. The per-pass conversion is an explicitly heuristic temperature/pressure/ratio/GHSV correlation bounded to 5-35%. Feed throughput and recycle affect production and overall conversion, not that per-pass correlation. Capture/separation/purity calculations are heuristic teaching approximations without calibration.

The displayed efficiency index measures methanol recovery relative to stoichiometric potential. It is not a thermodynamic or electrical efficiency. The MAX control selects a predefined high-recovery operating point; no general optimization or global-optimum proof is implemented.

## Evaluation design and evidence

The repeatable editor suite `SubmissionValidation.Run` combines structural scene validation, existing recycle/control/CSV checks, 240 independent analytical recycle cases, 24 electrolysis input cases and 65 OFAT samples. It checks external closure, carbon/hydrogen balance, limiting reactants, zero feed, finite results, storage shutdown and nonmutation of hypothetical inputs. Results are stored in `docs/evidence/`.

The optional `-ptmeoh-validate` player mode exercises real runtime components and invokes UI callbacks, captures page/tutorial screenshots, tests tutorial navigation and synthetic mouse camera interaction, tests camera focus, zero-water response, temperature sensitivity, pause/reset, CSV generation, and natural tank-trip/reset behavior during a 900-second run. Memory and object counts are sampled. This is programmatic acceptance coverage, not proof of physical mouse/keyboard operation or visual correctness. A coarse memory-growth envelope is not a memory-leak proof.

Fresh run results, build warnings, limitations and remaining manual checks are recorded in [Submission status](SUBMISSION_STATUS.md). Historical August/September build counts must not be substituted for the final run. Scientific validation is deliberately split into verified numerical consistency and still-unverified empirical predictive accuracy.

## Critical discussion

The value of the artifact is traceable interaction: a user can change one factor and inspect calculations, stream behavior and warnings together. OFAT experiments make assumptions inspectable, but cannot prove those assumptions. The symmetric ratio penalty and ideal separation omit real catalyst/selectivity effects. No learning-outcome experiment or comparison with a live industrial twin was performed.

The implementation uses one scene and existing MonoBehaviour conventions, avoiding a late architectural rewrite. Obsolete first-party discovery APIs were updated where ordering was not needed. Legacy scripts remain where removal could affect serialized references. Packages and Unity versions were not upgraded. Runtime and build checks take precedence over cosmetic refactoring.

## Reproduction and handoff

Use Unity 6000.4.7f1, URP 17.4.0 and Windows x86_64. Follow [Reproducibility](REPRODUCIBILITY_AND_BUILD.md), distribute the entire player folder, and provide the release manifest, source commit, evidence and limitations. Complete [Provenance](REFERENCES_AND_ASSET_PROVENANCE.md) and [Team/tool disclosure](TOOLS_AND_ASSISTANCE.md) using actual ownership and university rules before formal submission.
