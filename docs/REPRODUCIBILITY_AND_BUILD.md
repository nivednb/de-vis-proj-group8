# Reproducibility and Windows Build

## Required software

- Git
- Unity Hub
- Unity Editor `6000.4.7f1` with Windows Build Support

The project uses URP `17.4.0`. Unity's Navigation package and built-in AI module
provide NavMesh and pathfinding support.

## Clean-clone verification

1. Clone the repository into a short local path with sufficient free space.
2. Check out the evaluated commit or release tag.
3. Open the folder in Unity Hub with `6000.4.7f1`.
4. Wait for package import and shader compilation to finish.
5. Open `Assets/Scenes/SampleScene.unity`.
6. Clear the Console, enter Play Mode, and exercise the acceptance checklist.

## Structural validation

Run `Tools > Power-to-Methanol > Validate Release Scene`. A successful run logs
`RELEASE_VALIDATION_OK`. This checks required route segments, catalyst and
reactor-shell renderers, the flow shader, camera panning, and missing scripts.
It does not replace visual, chemical, performance, or build testing.

## Windows build

Run `Tools > Power-to-Methanol > Build Windows Application`. The default output
is:

```text
Builds/Windows/PtMeOH-DigitalTwin.exe
```

For automated or alternate output, set `PTMEOH_BUILD_PATH` to the full `.exe`
path before starting Unity. The editor script uses Windows x86_64 and StrictMode,
so a build error fails the build.

## Release evidence

Store these outside the Unity source tree or in a release attachment:

- exact Git commit SHA and optional signed tag;
- Unity Editor version;
- validation Console log;
- build log and warning review;
- executable SHA-256;
- short acceptance-test recording;
- final screenshots and known-issues list.

Never submit `Library`, `Temp`, `Logs`, `UserSettings`, `.vs`, `obj`, or a stale
build from a different commit.

## Validation record for this release branch

On 3 August 2026, Unity 6000.4.7f1 completed the automated release-scene
validation and a Windows build in batch mode.

- Validation passed (`RELEASE_VALIDATION_OK`), reporting 480 scene objects, 35
  process segments, the runtime-animated catalyst, reactor shell, and camera pan.
- The Windows build succeeded with 0 errors. The build was written to an
  external release directory so generated binaries were not committed to the
  Unity source tree.
- The compiler reported 40 warnings. These are primarily Unity 6 API
  deprecation warnings in existing runtime-discovery calls, plus one
  member-hiding warning. They do not block this build, but should be removed in
  a future maintenance pass rather than hidden from the submission record.

The executable is not a single-file deliverable. Distribute it together with
`PtMeOH-DigitalTwin_Data`, `UnityPlayer.dll`, `UnityCrashHandler64.exe`,
`MonoBleedingEdge`, and the other files produced in the same build directory.
