# de-vis-proj-group8

# PtMeOH Interactive Simulation

An interactive educational simulation of the **Power-to-Methanol (PtMeOH)** process, built in Unity. Users can explore plant units, adjust operating parameters via sliders, and observe how temperature, pressure, flow rate, and feed ratio affect methanol yield in real time.

---

## Tech Stack

Unity v6.3 - Primary simulation engine
Blender v5.1 - 3D asset creation
VSCode - Code editing
Git - Version control

---

## Project Structure

```
PtMeOH-Simulation/
│
├── Assets/
│   ├── Scenes/          # Unity scenes (one per plant unit + main overview)
│   ├── Scripts/         # C# scripts (slider logic, UI, reactor formula)
│   ├── Models/          # Imported Blender .fbx models
│   ├── UI/              # UI prefabs, panels, wireframes
│   └── Materials/       # Textures and materials
│
├── CEE-Docs/            # Process description, reactor formula, info panel texts
│
├── Documentation/       # Work plan, sprint notes, meeting notes
│
└── README.md
```

## Git Workflow

- `main` — stable, presentation-ready branch. Only merge here before sprint reviews.
- `nived` — active development branch. All individual changes merge here first.
- `starlin` — active development branch. All individual changes merge here first.
- `chaitanya` — active development branch. All individual changes merge here first.

## Getting Started

### Prerequisites

- Unity 6.3 installed via Unity Hub
- Blender 5.1 installed
- VSCode with the C# extension installed
- Git installed

### Open in Unity

1. Open Unity Hub
2. Click **Add project from disk**
3. Select the cloned folder
4. Open with Unity 6.3

### Blender → Unity Pipeline

1. Create/edit model in Blender 5.1
2. Export as `.fbx` → place in `Assets/Models/`
3. Unity will auto-import on next project refresh

---
