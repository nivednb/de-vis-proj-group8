# Final submission status

Frozen application source: `9e3ea9c192d506e33e4227586efdd5cd5da994bd`. Unity 6000.4.7f1, Windows x86_64. The final release commit is the documentation/evidence commit containing this record; its exact SHA is written into the packaged `release-manifest.json` after commit. The manifest also records the frozen application SHA and verifies identical Assets/Packages/ProjectSettings trees. A commit cannot contain its own hash.

## Executed final verification

The existing `../final-tutorial-clean` clone was created without Library or generated builds, checked out at the frozen SHA, and had empty initial Git status. Verification ran from 2026-09-23 18:03:49.788479 UTC to 18:32:05.075200 UTC. No application source was changed afterward. The initial asset import completed successfully. Seven serialized files acquired Windows line endings; their normalized contents are byte-equivalent to HEAD and `git diff HEAD -- Assets Packages ProjectSettings` is empty. Package download caches on this machine were available; no Library was copied.

| Check | Executed result |
|---|---|
| Complete numerical/regression suite | **PASS: 1,586 assertions**, 240 independent analytical recycle cases, 24 electrolysis cases and 65 sensitivity points |
| Scene/project validator | **PASS: 480 objects / 35 required process segments**, runtime catalyst, shell and camera checks |
| Editor CSV validator | **PASS: external mass-closure error 1.13699e-09%**, 40 rows, actual file write |
| Player CSV, both resolutions | **PASS: external mass-closure error 4.25878532e-09%**, current recycle basis and external stream closure |
| Independent parsed CSV/input JSON check | **PASS**: recycle 25%, temperature 240 C; product agrees within 0.001 kg/h. Sum of rounded rows differs by -0.0002 kg/h (1.57734491012e-05%); aggregate/error rows round to 1267.954/1267.954 and zero. Rounded zero is not exact zero. |
| Fresh Windows x86_64 build | **Succeeded: 0 errors / 0 warnings** in BuildReport |
| Final 1920x1080 visible player | **PASS: 181 checks, 0 failed** |
| Final 1280x720 visible player | **PASS: 177 checks, 0 failed**; this was rerun on the final fresh-clone executable, not borrowed from preflight |
| Tutorial | First-launch path, NEXT/PREVIOUS/SKIP, HELP replay, all 23 steps, card bounds/text fit, target resolution, arrow target/mesh and FINISH passed |
| Camera tutorial | Synthetic Unity Input System mouse drag, Shift-drag and wheel changed camera state; spotlight raycast access passed |
| Process Map / PLANT PROCESS | Positive rectangles and no title/counter/content/button overlap passed for the tested panel state; screenshot inspection below |
| Analytics / OFAT | Right split pane retained; all five factor selections generated 13 points each without changing live inputs |
| State restoration | Help modal cleared, analytics closed, inputs unchanged during paused tour and paused state preserved; subsequent normal acceptance passed |
| Uninterrupted long soak | **910.5588 seconds**, one continuous player process; natural storage high-high shutdown and reset restoring production passed |
| Allocated memory | **117,319,154 → 117,529,604 bytes; +210,450 bytes**; coarse growth envelope passed, not proof of zero leaks |
| Objects / owners | **732 → 732 (delta 0)**; no duplicate simulator/dashboard owners |
| Runtime errors/exceptions | **0 observed** |
| Runtime warning | URP reduced additional punctual-light shadow resolution by 2 to fit six shadow maps in a 2048x2048 atlas; observed once in each player log. This is separate from BuildReport warning count. |
| Tested binary identity | **184 file hashes verified unchanged** against the execution record |

The clean import initially reported two `GUID` CS0246 diagnostics in ShaderGraph package-cache sources. Unity's API Updater updated eight cached package files, compilation recovered, the validator exited 0 and the subsequent build succeeded. These recovered import diagnostics are not omitted or described as a completely warning-free import. No tracked package configuration or application source was edited to obtain the pass.

## Evidence and inspection boundaries

`evidence/verification-execution.json` records source SHA, initial cleanliness, absent Library, timestamps and tested binary hashes. `release-validation-summary.json`, `editor-validation-summary.txt`, numerical/CSV data and both runtime reports contain exact measurements. Raw logs remain under the verification clone's ignored Logs directory; their SHA-256 values are retained in the summary.

There are **36 final 1920x1080 PNGs and 35 final 1280x720 PNGs**, plus each resolution's runtime report, soak CSV and exported mass-balance CSV. Captures include all tutorial steps, five automatic OFAT views, six application pages, zero-water operation and the long run's storage trip.

Screenshot visual inspection was performed on final 1920x1080 `overview`, `plant-process`, `reactor-lab`, `tutorial-00`, `tutorial-08`, `tutorial-10`, `tutorial-14`, `tutorial-20` and `ofat-1`. Text/buttons were readable in these captures; camera/Process Map spotlights and the warning arrow/caption were aligned with their intended regions; the Process Map's inspected title/counter/content/buttons were separate; analytics/OFAT occupied the right pane without covering its tutorial card. This is a bounded inspection of captured frames, not a signed human usability assessment or physical mouse/keyboard test. The 720p automated report is final evidence, but comprehensive manual 720p visual review remains pending.

The harness uses real UI callbacks and synthetic Unity input events, with native keyboard/mouse devices isolated inside the test process to prevent unrelated desktop input. It forces the first-launch path without modifying normal completion preferences. Persistence across ordinary launches and physical tutorial shortcuts are manual checks. Process inputs are compared while paused so expected tank accumulation is not mistaken for input mutation.

## Release location and identity

Executable: `Release/PtMeOH-FinalSubmission-Windows-x64/PtMeOH-DigitalTwin.exe`.

Executable SHA-256: `16726f6bc281a100105ae80f982a21a9332c870340bcd2e1d89bd82b76dd33c7`.

Distributable: `Release/PtMeOH-FinalSubmission-Windows-x64.zip`, with `.zip.sha256` sidecar. The complete folder includes runtime dependencies, documents/evidence, `PtMeOH-Source.zip` archived from the final release commit and the external `release-manifest.json`. The final ZIP checksum is computed after the documentation commit; see the sidecar and consolidated handoff report. `evidence/release-manifest.json` is the committed verification manifest; the package-root manifest resolves the exact final commit and all distributed file hashes without a self-referential Git hash.

## Remaining manual checks

- Physical mouse/keyboard operation, normal first-launch preference persistence, replay and Enter/Space/Backspace/Esc shortcuts; synthetic tests do not establish hardware behavior.
- Team visual sign-off on the actual presentation display/DPI: all Process Map stages, hover controls, graph tooltips/export UI, contrast, transparent reactor and pipe-seam continuity. Selected screenshots were inspected, not every possible operating state.
- Launch the complete package on a second Windows machine; verify permissions and user-facing export destinations.
- Empirical model calibration and measured learning effectiveness were not performed and are not claimed. This educational model is numerically verified, not an industrial predictive model.

## Team details to confirm before submission

Before submission, the team should confirm the university/module and supervisor details, final student contribution statement, original model/icon sources and permissions, any copied visual references, and the required institutional wording for AI/software assistance. See TOOLS_AND_ASSISTANCE.md and REFERENCES_AND_ASSET_PROVENANCE.md.
