# Architecture and Asset Policy

## Design rule

Persist content that represents stable authored design. Generate content at
runtime when it represents changing simulation state. This preserves manual
editability without turning transient data into thousands of scene objects.

## Prefer scene objects, prefabs, or ScriptableObjects for

- stable equipment placement and reusable equipment assemblies;
- manually tuned dashboard panels and navigation layout;
- reusable particle-system appearance/templates;
- authored process-route definitions and species metadata;
- equipment descriptions, units, safe ranges, citations, and UI labels;
- environment modules reused in more than one scene.

ScriptableObjects are preferred for process/equipment data shared by simulation,
UI, analytics, warnings, and flow visuals. Prefabs are preferred for repeated
visual hierarchies. Unique plant-wide layout may remain serialized in the scene.

## Keep runtime-generated

- moving tracer particles and mixed-species packets;
- graph samples and exported datasets;
- live calculation results and KPI text;
- temporary warning instances and state highlights;
- flow density/speed changes driven by controls;
- pooled effects whose quantity depends on operating state.

## Migration approach

1. Inventory each runtime-created hierarchy and identify its owner.
2. Separate configuration from transient state.
3. Create a prefab only for the reusable stable visual hierarchy.
4. Move process metadata into typed serialized data or ScriptableObjects.
5. Preserve runtime pooling and state updates.
6. Compare hierarchy counts, appearance, slider response, memory, and build
   behaviour before and after each small migration.

Do not convert everything in one pass. It creates duplicated state, broken
references, larger scenes, and difficult merges. The final submission may retain
documented runtime composition where it is deterministic and validated.
