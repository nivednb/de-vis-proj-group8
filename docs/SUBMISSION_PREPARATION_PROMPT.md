# Submission Preparation Prompt

Use the prompt below for a future review assistant. It requests evidence and
does not authorise hiding provenance or changing the project without review.

```text
Act as a technical reviewer for a master's-level Unity Power-to-Methanol
educational digital-twin project. Audit the exact checked-out commit; do not
assume earlier builds are current. First inventory scenes, prefabs,
ScriptableObjects, runtime-generated hierarchies, packages, shaders, materials,
UI pages, simulation scripts, warnings, analytics, build settings, licences,
documentation, and screenshots.

Check: chemical species and phases; complete stream source/destination and flow
direction; mixed-stream composition; reactor inlet/outlet/catalyst depiction;
units and equations; slider-to-calculation-to-visual coupling; storage boundary
behaviour; camera/UI input; missing references; errors and warnings; memory and
performance; clean-clone reproducibility; Windows build; accessibility and UI
scaling.

Classify stable authored visuals/configuration that should be scenes, prefabs,
or ScriptableObjects, and transient values/particles/graphs/warnings that should
remain runtime. Do not blanket-convert runtime objects to prefabs.

For every finding provide file/scene evidence, severity, proposed correction,
validation method, and whether it is required before submission. Preserve team
authorship and list third-party/tool assistance honestly according to academic
policy. Never fabricate tests, citations, provenance, or an "AI-free" claim.

Finish with: a professor evaluation path, exact build instructions, a release
checklist, known limitations, contribution/citation gaps, and a go/no-go verdict
whose confidence matches the evidence collected.
```
