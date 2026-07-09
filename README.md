# PtMeOH Interactive Simulation

An interactive educational Unity visualization of a **Power-to-Methanol (PtMeOH)** plant. The current Unity 6 branch focuses on a full-plant overview with process equipment, corrected routing, camera focus controls, and early animated flow visualization.

---

## Progress screenshots

### Current Unity full-plant view

![Unity full plant game view](docs/images/unity-full-plant-game-view.png)

### Target visual direction

The intended final direction is a more polished ICODOS-style industrial dashboard, with labelled process equipment, stream colors, live KPIs, and process navigation.

![Target UI reference](docs/images/target-ui-reference-icodos.jpeg)

More images are documented in [docs/progress-screenshots.md](docs/progress-screenshots.md).

---

## Tech stack

- Unity `6000.4.7f1` - primary simulation engine
- Blender - 3D asset creation/export
- C# - interaction, UI, camera, and flow logic
- Git - version control

---

## Project structure

```text
Assets/
|-- Scenes/              # Unity scenes
|-- Scripts_N/           # Added flow/waypoint scripts for full-plant visualization
|-- Prefabs_N/           # Flow particle prefab(s)
|-- Settings/            # URP settings
|-- *.fbx                # Plant equipment models
|-- Mat_*.mat            # Stream/material colors
|-- PipeFlow.shader      # Pipe-flow shader prototype
`-- TextMesh Pro/        # TMP UI assets

Packages/
ProjectSettings/
README.md
```

## Branch status

- `nived-unity6-progress` - Nived's Unity 6 full-plant progress branch.
- `chaitanya` - teammate full-plant branch used as the visual base.
- `main` - original starter branch.

## Current progress

- Full plant equipment scene in Unity 6.
- Added support structures, storage tank, mixing point, and corrected route layout.
- Orbit camera with module focus/navigation.
- Added `Scripts_N` flow visualization layer:
  - `AutoWholePlantFlowRuntime`
  - `FlowPath`
  - `FlowFollower`
  - `PipeWaypointGenerator`
- Early moving flow-dot visualization along named process routes.

## Open in Unity

1. Open Unity Hub.
2. Click **Add project from disk**.
3. Select this repository folder.
4. Open with Unity `6000.4.7f1`.

---

## Notes

Unity-generated folders such as `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `.vs/`, and `obj/` are intentionally excluded from Git.
