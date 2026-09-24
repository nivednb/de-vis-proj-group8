# Power-to-Methanol educational simulator

Interactive Unity application for exploring a complete Power-to-Methanol plant, operating controls, material streams, reaction/recycle behavior, warnings and analytics. This master's-project simulator/visualization has no live plant-data connection and is not a calibrated thermodynamic, kinetic or safety package.

## Final submission

- Branch: `final-submission`, based on `nived-merged-improvements`.
- Unity **6000.4.7f1**, URP **17.4.0**, Windows x86_64.
- Startup: `Assets/Scenes/SampleScene.unity`.
- Release: `Release/PtMeOH-FinalSubmission-Windows-x64/PtMeOH-DigitalTwin.exe`, with the entire adjacent runtime/data folder contents.
- The external `release-manifest.json` records the final source commit, build-source commit and hashes. The final documentation commit must have identical Assets/Packages/ProjectSettings trees to the tested build-source commit.

See [submission status](docs/SUBMISSION_STATUS.md), [scientific validation](docs/SCIENTIFIC_VALIDATION.md), [technical report](docs/FINAL_PROJECT_REPORT.md), [implementation](docs/IMPLEMENTATION_REFERENCE.md), [examiner walkthrough](docs/PROFESSOR_EVALUATION_GUIDE.md) and [reproduction](docs/REPRODUCIBILITY_AND_BUILD.md).

## Run

Open this repository in the pinned Unity Editor, open SampleScene and enter Play. Runtime bootstrap adds the process simulator, dashboard, flows, warnings and environment. Older flow-only copies are not this release.

Navigation: OVERVIEW, PLANT PROCESS, FLOW LAB, REACTOR LAB, ANALYTICS and SIMULATION. Equipment sliders drive the shared process snapshot. Analytics includes controlled model sweeps; CSV exports record current steady-state inputs, streams and assumptions.

The guided tutorial starts on first launch and is replayable from HELP → START TUTORIAL. Mouse: drag to orbit, Shift-drag to pan, wheel to zoom.

Keyboard: arrows orbit, A/D pan, W/S zoom, Shift+arrows change focus, Home overview. Manual input/visual sign-off is tracked separately from programmatic callback tests.

Zero water or power gives zero electrolytic hydrogen at steady state. The model uses species recycle balances and a finite purge. The legacy efficiency index measures material recovery, not energy efficiency; MAX is a predefined high-recovery preset, not a global optimization result.

## Validate and hand off

`tools/Validate-Submission.ps1` runs scene/numerical checks, a Windows build and player acceptance, including a 900-second soak. `SubmissionValidation.Run` is the editor suite; `WindowsBuild.BuildWindows` is the build entry point.

Review [acceptance](docs/SUBMISSION_READINESS_CHECKLIST.md), [known limitations](docs/KNOWN_ISSUES.md), [provenance](docs/REFERENCES_AND_ASSET_PROVENANCE.md) and [team/assistance](docs/TOOLS_AND_ASSISTANCE.md). University details, individual contributions, original model/icon ownership and institutional declarations remain explicit team inputs.
