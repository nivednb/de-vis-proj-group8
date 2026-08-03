# Submission Readiness Checklist

Use this checklist on the exact commit and executable submitted for assessment.
Record the operator, date, commit SHA, Unity version, and result beside each
item. A checked item means it was observed, not assumed.

## 1. Academic and project ownership

- [ ] Every team member can explain the plant route, assumptions, controls,
      equations, warnings, and their own contribution.
- [ ] Third-party models, textures, packages, references, and software tools are
      listed with licences or citations.
- [ ] Tool assistance is disclosed according to the university's policy; no
      authorship or provenance claim is misleading.
- [ ] Text has been reviewed by the team for accuracy and written in language
      the presenters naturally understand.
- [ ] Student names, module, supervisor, date, version, repository, and contact
      information are complete.

## 2. Engineering and chemical review

- [ ] Stream names, colours, directions, phases, and source/destination equipment
      match the process narrative.
- [ ] Mixed-feed pipes visibly distinguish H2, CO2, and recycle components.
- [ ] Reactor inlet/outlet direction, catalyst location, conversion depiction,
      product route, temperature, pressure, and H2/CO2 ratio are documented.
- [ ] Mass-flow units, temperatures, pressures, efficiencies, yields, storage
      levels, and graph axes are labelled consistently.
- [ ] Slider ranges and derived outputs remain inside documented educational
      operating ranges.
- [ ] Storage-full, abnormal-ratio, temperature, and pressure warnings have
      defined behaviour and reset logic.
- [ ] Model limitations are visible in the report and application help.

## 3. Unity project hygiene

- [ ] Open with Unity `6000.4.7f1`; allow package resolution to finish.
- [ ] Console has zero compile errors and zero unexplained runtime exceptions.
- [ ] `SampleScene` has no missing scripts, materials, meshes, or references.
- [ ] Only required scenes are enabled in Build Settings.
- [ ] Git excludes `Library`, `Temp`, `Logs`, `UserSettings`, `.vs`, `obj`, and
      generated builds.
- [ ] No passwords, tokens, personal paths, private data, or large raw captures
      are committed.
- [ ] Package list contains only required runtime/editor dependencies.
- [ ] Final project opens and builds from a clean clone.

## 4. Prefab and runtime-generation review

- [ ] Repeated equipment and stable visual assemblies are prefabs where reuse or
      manual scene editing is valuable.
- [ ] Stable flow routes are serialized as scene/prefab data or ScriptableObjects
      rather than reconstructed from hard-coded names where practical.
- [ ] Reusable particle-system appearance is stored in prefab templates.
- [ ] Runtime-generated objects have one clear owner, deterministic names, and
      cleanup/rebuild behaviour.
- [ ] Transient tracer packets, plot samples, live values, calculations, and
      warning instances remain runtime data; they are not converted to prefabs.
- [ ] Runtime generation is documented and justified; a blanket conversion of
      all generated objects has not been attempted.

## 5. Functional acceptance

- [ ] Overview and every process page open correctly.
- [ ] All module labels and information panels are readable and correct.
- [ ] Camera orbit, pan, zoom, focus navigation, reset, mouse input, and keyboard
      input work without conflict with UI controls.
- [ ] Sliders update numeric results and corresponding flow speed/density.
- [ ] Pipe tracers remain visible from overview and close-up angles.
- [ ] Reactor transparency, catalyst visibility, and catalyst colour animation
      remain stable from multiple angles.
- [ ] Analytics graphs update, use labelled axes/units, and export a valid file.
- [ ] Storage capacity reaches warning state predictably and reset is verified.
- [ ] Application can run for 15 minutes without increasing memory, freezing,
      crashing, or accumulating duplicate runtime objects.

## 6. Build and hand-off

- [ ] Run `Tools > Power-to-Methanol > Validate Release Scene` successfully.
- [ ] Create a clean Windows x86_64 build using the documented menu command.
- [ ] Test the executable on a second Windows account or computer if available.
- [ ] Verify 1280 x 720, 1920 x 1080, and the presentation display resolution.
- [ ] Check UI scaling, text clipping, colour contrast, and performance.
- [ ] Include README, report, implementation reference, evaluation guide,
      licences/citations, screenshots, and a known-issues list.
- [ ] Tag the evaluated commit and archive the exact executable with its commit
      SHA and checksum.

## Evidence record

| Field | Value |
| --- | --- |
| Date | |
| Evaluator | |
| Git commit | |
| Unity version | 6000.4.7f1 |
| Windows version | |
| Executable SHA-256 | |
| Validation result | |
| Known issues | |
