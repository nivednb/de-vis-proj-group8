# Reproduce the final submission

## Requirements

Windows, Git, a valid Unity licence, Unity 6000.4.7f1 with Windows Build Support. Keep the package manifest/lock pinned. Python 3 is used only for manifest/archive generation. A clean clone may use the machine's existing package download cache but must not copy Library, Temp or Builds from another checkout.

## Source identity

The distributable manifest records the final source commit (including report/evidence) and the tested build-source commit. Documentation is finalized after execution. The manifest tool refuses packaging if these commits differ under Assets, Packages or ProjectSettings, and records those Git tree identities. Thus a commit does not need to contain its own SHA. `PtMeOH-Source.zip` is created with git archive from the final commit. A mutable branch name alone is not release identity.

## Fresh-clone verification

Clone/check out the exact manifest SHA, or extract the source archive into a fresh folder. Then run:

```powershell
.\tools\Validate-Submission.ps1 -Editor 'C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe'
```

Optional `-Project` and `-Output` parameters select other folders. The script waits for exit codes and success markers from:

1. `SubmissionValidation.Run`: scene, recycle, professor-control, CSV and expanded numerical checks;
2. `WindowsBuild.BuildWindows`: Windows x86_64 StrictMode build;
3. the resulting player at 1920x1080 with a 900-second soak;
4. the same player at 1280x720 with a 30-second soak phase.

Logs remain in Logs; compact reports, CSVs and captures go to docs/evidence. Player validation invokes UI callbacks/component APIs and synthetic Unity Input System mouse/keyboard events, not physical hardware input. It forces the tutorial first-launch path without reading or writing the normal completion preference, then checks replay, navigation, warning arrow, layout and camera input. It runs only with an explicit validation argument. Run one player at a time.

For direct Unity execution use `-batchmode -nographics -quit -projectPath <project> -executeMethod SubmissionValidation.Run -logFile <log>`; use `WindowsBuild.BuildWindows` for building and set `PTMEOH_BUILD_PATH` to the desired EXE path. On Windows, wait for the process: invoking Unity.exe alone can return before it finishes.

## Freeze and package

After committing source/docs and reviewing successful evidence:

```powershell
python tools/release_manifest.py --package Release/PtMeOH-FinalSubmission-Windows-x64 --build-source-commit <tested-sha> --build-log <successful-build-log>
```

The tool requires a clean checkout, matching application input trees, successful build/player markers and a completed >=900-second soak. It copies docs/README, includes the Git source archive, hashes package files, creates release-manifest.json, archives the whole folder and emits a ZIP SHA-256 sidecar. It does not perform or invent validation. Raw machine-specific logs remain local.

The EXE is not a single-file deliverable. Distribute its `_Data` folder, UnityPlayer.dll, MonoBleedingEdge and every other produced player file. Exact executed results and package paths are in SUBMISSION_STATUS.md.

## Manual sign-off

Review live UI/readability/contrast and pipe continuity, physical input and graph-export UX, and launch on a second machine/presentation display. Complete team, asset and institution sign-off. Automated PASS markers do not establish these facts.

## Recorded frozen-source execution

The final run used `9e3ea9c192d506e33e4227586efdd5cd5da994bd` in `../final-tutorial-clean`, with empty initial Git status and no Library. The existing run completed on 2026-09-23; it was inspected and documented afterward, not rerun under a different source SHA. Both resolutions used the same fresh executable. See SUBMISSION_STATUS.md for all results.

Unity's initial import can run its API Updater against cached ShaderGraph sources; this run recovered two transient GUID diagnostics automatically. Seven serialized files received only line-ending rewrites, confirmed equivalent to HEAD. Preserve pinned packages; do not hand-edit cached dependencies to conceal import problems. The visible player is required for valid screenshots. Native input is isolated only in the automated test process, and synthetic events exercise the real Unity input path.

The committed evidence manifest identifies the tested application commit and binary hashes. Packaging is performed after the evidence commit, so the external manifest and source archive can identify the exact final release SHA. ZIP hashes are sidecars outside the ZIP to avoid circular hashes.
