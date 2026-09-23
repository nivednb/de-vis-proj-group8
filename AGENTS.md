# Project guidance

- Current submission work is on `final-submission`, based on `nived-merged-improvements`. Do not use older flow-only copies as the integrated release.
- Unity 6000.4.7f1, URP 17.4.0, Windows x86_64; preserve the package lock and authored SampleScene.
- Read docs/IMPLEMENTATION_REFERENCE.md, SCIENTIFIC_VALIDATION.md and SUBMISSION_STATUS.md before changing process behavior.
- PlantProcessSimulator owns live state. Reuse Simulate(ProcessInputs) for hypothetical calculations; do not mutate live inputs for sweeps.
- Preserve renamed route prefixes and distinct species; maintain the existing FinalFlowSystem.
- Numerical verification: Unity batch entry SubmissionValidation.Run. Windows build: WindowsBuild.BuildWindows. Player QA: -ptmeoh-validate <output-folder> -ptmeoh-duration 900.
- Numerical agreement is not empirical calibration. Never mark visual, licensing, authorship or institutional sign-off complete without evidence.
- Keep generated logs/builds out of Git. Commit compact evidence under docs/evidence and record release hashes beside the distributable.
