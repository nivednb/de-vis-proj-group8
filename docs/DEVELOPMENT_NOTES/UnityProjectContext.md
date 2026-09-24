# Current project context

Current submission branch: `final-submission`, derived from `nived-merged-improvements` (`be9dd88`). Older `whole_plant` flow-only copies are not the integrated release.

Unity 6000.4.7f1; URP 17.4.0; Input System 1.19.0; uGUI; Windows x86_64; enabled startup scene Assets/Scenes/SampleScene.unity. Main source paths and actual formulas are in ../IMPLEMENTATION_REFERENCE.md.

The current implementation includes species recycle/purge calculations, water-limited electrolysis, live dashboard/equipment controls, analytics with OFAT sweeps, pipe/reaction visuals, storage interlock and CSV export. Existing analytics split-pane work is preserved. Custom batch editor validators and an opt-in player acceptance harness are present; a comprehensive NUnit suite is not.

Read ../SUBMISSION_STATUS.md for actual fresh results and remaining checks; do not treat the August build report as current evidence. Scientific assumptions and independent numerical derivation are in ../SCIENTIFIC_VALIDATION.md. Empirical calibration and institutional/asset sign-off remain separate from software verification.
