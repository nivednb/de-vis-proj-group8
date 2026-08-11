# Repository Guidance

## Scope and canonical state

- This is the Unity 6 Power-to-Methanol educational digital twin. Treat GitHub `nivednb/de-vis-proj-group8` `main` as the canonical current baseline unless the user explicitly selects a release or development branch.
- Before substantial work, read `README.md`, `docs/DEVELOPMENT_NOTES/UnityProjectContext.md`, `docs/DEVELOPMENT_NOTES/UnityProjectHealth.md`, `docs/IMPLEMENTATION_REFERENCE.md`, and the relevant submission/build guidance.
- Confirm branch, commit, remote state, and Git status. Multiple older working copies exist on this machine; do not synchronize, merge, or publish between them without comparing exact commits and preserving local Unity-generated changes.

## Architecture and implementation

- Use Unity `6000.4.7f1`, URP `17.4.0`, and Input System `1.19.0`. Do not upgrade Unity or packages unless explicitly requested.
- `Assets/Scenes/SampleScene.unity` is the single enabled startup scene. Avoid broad scene, prefab, material, font-atlas, or ProjectSettings serialization churn.
- `PlantProcessSimulator` and its process snapshot are the presentation-data source of truth. UI, warnings, analytics, flow, reactor, catalyst, and storage behavior must derive from it rather than parallel hard-coded values.
- Preserve stable route names/prefixes and mixed-species H2/CO2/recycle behavior. Extend `Assets/Scripts_N/FinalFlowSystem/` and existing runtime systems instead of adding competing flow/bootstrap architectures.
- The desired flow presentation is a single continuous, reaction-driven plant route rather than visibly independent loops on each pipe segment. This is a current requirement but is not verified complete on `main`; do not document it as finished until Play Mode evidence confirms continuity across segment boundaries and process-state transitions.
- Keep the model explicitly educational. Do not present values, warnings, equations, or visual behavior as rigorous CFD, certified process control/safety, or chemically validated without new evidence.

## Safety and generated files

- Preserve unrelated work. Unity may touch render-pipeline settings, font assets, Build Settings, and ProjectSettings merely by opening the project; inspect byte/content diffs before including them.
- Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `.vs/`, `obj/`, generated builds, local captures, credentials, or machine-specific paths.
- Keep legitimate licensing, references, asset provenance, and tool-assistance disclosures. Remove accidental prompt/checkpoint debris, but never falsify authorship or provenance.

## Verification and documentation

- After code or scene changes, compile with zero errors, inspect the Console, run `Tools > Power-to-Methanol > Validate Release Scene`, and perform targeted Play Mode checks in `SampleScene`.
- For a deliverable, run `Tools > Power-to-Methanol > Build Windows Application`, keep the complete Unity build folder together, and execute the submission checklist. The `.exe` alone is not distributable.
- Add calculation/EditMode or PlayMode tests when changing deterministic process logic; the project currently has no first-party automated regression suite.
- Update existing living documents in the same task: architecture/context for system changes, health for confirmed risks, implementation reference for equations/behavior, reproducibility for commands, and the submission checklist for acceptance criteria. Create `PLAN.md` only for a concrete multi-session goal not already represented by these documents.

