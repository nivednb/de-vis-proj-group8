# Professor Evaluation Guide

## Purpose

This Unity application is an educational Power-to-Methanol digital-twin
demonstrator. It combines a navigable plant, process controls, stream
visualisation, simplified steady-state calculations, warnings, and engineering
explanations. It is intended to communicate system behaviour; it is not a CFD,
kinetic, control-system, or process-safety design package.

## Recommended evaluation route

1. Launch `PtMeOH-DigitalTwin.exe` at 1920 x 1080 if available.
2. Open **Overview** and inspect the complete plant and process legend.
3. Visit Electrolyzer, Carbon Capture, Synthesis, Separation, and Storage.
4. Change one operating control at a time and compare the numeric result,
   stream speed/density, warning state, and analytics response.
5. Use **Show Streams** and confirm mixed-feed routes contain distinct H2, CO2,
   and recycle tracer packets.
6. Focus the reactor and observe its transparent shell and catalyst-bed colour
   response.
7. Test storage-capacity warnings and reset behaviour.
8. Export analytics, if enabled in the evaluated build.

## Controls

| Input | Action |
| --- | --- |
| Arrow keys | Orbit around the current focus |
| A / D | Pan left / right |
| W / S | Zoom in / out |
| Shift + arrow keys | Select previous / next module |
| Home | Restore overview |

## Evidence supplied

- Architecture, equations, assumptions, and limitations: `FINAL_PROJECT_REPORT.md`
- Script-to-feature mapping: `IMPLEMENTATION_REFERENCE.md`
- Current visual record: `progress-screenshots.md`
- Repeatable validation/build procedure: `REPRODUCIBILITY_AND_BUILD.md`
- Team/tool provenance: `TOOLS_AND_ASSISTANCE.md`

## Important limitations

Displayed values are simplified educational process values. They must not be
used for equipment sizing, relief design, hazard studies, emissions compliance,
commercial forecasting, or plant operation.
