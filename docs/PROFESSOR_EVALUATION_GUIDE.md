# Examiner walkthrough

This is an educational Power-to-Methanol process simulator and visualization. It has no live plant connection and is not a calibrated kinetic, thermodynamic or safety package.

1. Launch `PtMeOH-DigitalTwin.exe` from its complete Windows folder. Complete or skip the first-launch tutorial; replay it with HELP → START TUTORIAL. Read the overview and process legend.
2. Follow PLANT PROCESS through electrolysis, capture, mixing, synthesis, separation/recycle and storage. Use module focus to inspect equipment.
3. Set water feed to zero: after display smoothing settles, hydrogen and oxygen production approach zero. Restore/reset the inputs.
4. In REACTOR LAB/ANALYTICS, compare temperature, pressure, ratio and feed sweeps. Explain which values are held constant and why feed changes throughput without changing the per-pass correlation.
5. Compare single-pass conversion with overall recycle utilization. Use the mass-balance CSV to distinguish internal recycle from external inputs/outputs.
6. Inspect mixed-species streams, transparent reactor and catalyst-state color. These are explanatory visuals, not CFD or literal catalyst chemistry.
7. Exercise pause/resume and reset. Observe the accelerated tank high-high shutdown and reset behavior.
8. Read SCIENTIFIC_VALIDATION.md and the actual evidence files. Numerical balance verification does not establish empirical accuracy.

Keyboard controls: arrows orbit, A/D pan, W/S zoom, Shift+arrows change focus, Home overview. Physical input and visual review status is recorded in SUBMISSION_STATUS.md.

The legacy efficiency label is a material-recovery index, not energy efficiency. MAX is a predefined high-recovery preset, not a demonstrated global optimum. See IMPLEMENTATION_REFERENCE.md for the exact formulas and warning thresholds.

The release manifest identifies the source commit and file hashes. Team/institution details, asset provenance and the assistance disclosure are recorded separately in TOOLS_AND_ASSISTANCE.md and REFERENCES_AND_ASSET_PROVENANCE.md.
