# Remaining limitations and manual sign-off

- Empirical predictive accuracy is unverified. Published experimental context is cited, but no matched catalyst/space-velocity/selectivity dataset was calibrated.
- Physical mouse/keyboard operation, visual clipping/contrast and packet continuity at every pipe seam require human review; programmatic callback checks are not equivalent.
- Original equipment-model/icon authors and redistribution rights require team confirmation.
- University, module, supervisor, rubric, student contributions and assistance declaration require team information.
- Several controls saturate at 100% design capacity despite higher UI ranges. Recycle 100% is internally bounded to 99.9%; a no-purge closed loop is not modeled.
- Compressor ratio has no thermodynamic duty model. The efficiency field is a material-recovery index, not energy efficiency. MAX is a preset.
- The visible warning overlay and the engine hotspot helper use different documented educational thresholds.
- Runtime assembly/name-based discovery and transparent rendering retain maintenance/performance risks. No package or architectural rewrite was attempted for submission.
- Soak memory/object measurements apply to the tested Windows machine and settings, not all hardware.

## Final-run observations

- Windows BuildReport: zero errors and zero warnings. Runtime logs separately contain one URP shadow-atlas resolution-reduction warning per player run; shadows may have lower detail. No source change was made after freeze.
- Initial clean import emitted two ShaderGraph GUID CS0246 diagnostics; Unity API Updater recovered them in Library/PackageCache and validation/build subsequently completed successfully. The package lock and application inputs remained unchanged.
- The 910.5588-second soak measured +210,450 allocated bytes (117,319,154 to 117,529,604), with 732 objects at both endpoints and no observed runtime errors. This is not a zero-memory-leak claim.
- Selected final 1080p screenshots were inspected; comprehensive presentation-display, physical-input, normal preference-persistence and second-machine sign-off remain.
