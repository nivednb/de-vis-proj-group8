# Power-to-Methanol Educational Simulator

This project is an interactive Unity application for exploring the main stages of a Power-to-Methanol plant. It combines a 3D process visualization with operating controls, material streams, reaction and recycle behaviour, warnings, guided explanations and simple analytics.

The application is intended for education and process exploration. It does not use live plant data and should not be treated as a calibrated thermodynamic, kinetic or industrial safety model.

## Final submission

- Branch: `final-submission`
- Unity: **6000.4.7f1**
- Render pipeline: URP **17.4.0**
- Platform: Windows x86_64
- Startup scene: `Assets/Scenes/SampleScene.unity`
- Executable: `Release/PtMeOH-FinalSubmission-Windows-x64/PtMeOH-DigitalTwin.exe`

The application source used for the verified build is frozen at commit `9e3ea9c192d506e33e4227586efdd5cd5da994bd`. The release manifest records the build identity and file hashes.

More detail is available in the [submission status](docs/SUBMISSION_STATUS.md), [scientific validation](docs/SCIENTIFIC_VALIDATION.md), [project report](docs/FINAL_PROJECT_REPORT.md), [implementation reference](docs/IMPLEMENTATION_REFERENCE.md), [evaluation guide](docs/PROFESSOR_EVALUATION_GUIDE.md) and [reproduction notes](docs/REPRODUCIBILITY_AND_BUILD.md).

## Using the application

The main pages are OVERVIEW, PLANT PROCESS, FLOW LAB, REACTOR LAB, ANALYTICS and SIMULATION. Process controls update a shared process state so that changes can be followed across the plant. The analytics page includes one-factor-at-a-time (OFAT) studies, while CSV export records the current steady-state inputs and calculated streams.

A guided tutorial starts on first launch and can be opened again through **HELP → START TUTORIAL**.

Mouse controls:
- Drag: orbit
- Shift + drag: pan
- Mouse wheel: zoom

Keyboard controls:
- Arrow keys: orbit
- A/D: pan
- W/S: zoom
- Shift + arrow keys: change focus
- Home: return to overview

The process model includes a water-limited electrolyser, species-based recycle and a finite purge. Zero water or zero electrical input therefore produces zero electrolytic hydrogen at steady state. The displayed legacy efficiency value represents a material-recovery index rather than energy efficiency. The MAX option is a predefined high-recovery case and is not a mathematical global optimum.

## Validation

The submission includes automated checks for the process calculations, scene structure, CSV export, tutorial, navigation, OFAT analysis and runtime behaviour. The final Windows build completed with **0 build errors and 0 build warnings**. The clean-clone numerical suite passed **1,586 assertions**, and the final player tests passed at both 1920×1080 and 1280×720.

The validation script is `tools/Validate-Submission.ps1`. Detailed results and remaining manual checks are recorded in `docs/SUBMISSION_STATUS.md` and `docs/SUBMISSION_READINESS_CHECKLIST.md`.

Project limitations, asset provenance and the use of development/AI assistance are documented separately rather than hidden from the submission.
