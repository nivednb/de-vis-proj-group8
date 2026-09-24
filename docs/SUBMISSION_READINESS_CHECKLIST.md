# Final submission acceptance matrix

Consult SUBMISSION_STATUS.md and release-manifest.json for the exact build and executed results. No historical checklist is treated as a fresh pass.

| Criterion | Evidence or disposition |
|---|---|
| Pinned Editor/packages/startup scene | Fresh validation/build records |
| Compile/build errors and warnings | Final build summary |
| Required routes, scripts, reactor, shader, camera | ReleaseValidation.Run |
| Zero-water/zero-power and available-water bound | 24 electrolysis cases |
| Independent recycle/atom/external closure | 240 analytical cases, threshold <0.001% |
| Current-state CSV values and stream closure | CSV regression artifacts and player checks |
| OFAT held-constant inputs/nonmutation | 65 samples and assertions |
| Startup, singleton owners, six navigation callbacks | Player runtime report |
| Tutorial startup/navigation/replay/state restoration | Final player reports: PASS; normal preference persistence remains manual |
| Camera focus and synthetic drag/Shift-drag/wheel | Final player reports: PASS; physical input is separate |
| Five automatic OFAT selections | 13 points each; no live-input mutation; PASS |
| Live zero-water/temperature response, pause/reset | Player checks |
| Natural storage trip/reset | 900-second player run |
| 15-minute stability, memory/object sampling | soak.csv; coarse envelope, not proof of zero leaks |
| 1920x1080 and 1280x720 execution | Recorded actual resolution and runtime report |
| Readability, clipping, contrast, pipe seams/reactor views | MANUAL VISUAL SIGN-OFF REQUIRED |
| Physical mouse/keyboard/UI conflicts | MANUAL INPUT SIGN-OFF REQUIRED |
| Graph/image export UX and second-machine launch | MANUAL CHECK REQUIRED; CSV generation is separately automated |
| Scientific derivation, assumptions and primary citations | SCIENTIFIC_VALIDATION.md |
| Empirical predictive calibration | NOT PERFORMED; no aligned dataset available; excluded claim |
| Measured learning effectiveness | NOT PERFORMED; excluded claim |
| Final commit/build identity/checksums | External release manifest and ZIP checksum |
| Clean-clone reproduction | Actual result in SUBMISSION_STATUS.md |
| University/module/supervisor/rubric | TEAM INPUT REQUIRED |
| Individual contributions and oral-defense ownership | TEAM INPUT REQUIRED |
| Original model/icon rights | TEAM INPUT REQUIRED; asset hashes inventoried |
| Font/package notices | Installed notices inventoried; preserve with distribution |
| Institutional AI declaration | TEAM INPUT REQUIRED; draft disclosure supplied |

Automated engineering verification does not replace the team's information and sign-off.
