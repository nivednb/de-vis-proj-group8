# Architecture and asset policy

`PlantProcessSimulator` owns live inputs/storage and publishes `ProcessSnapshot`. Hypothetical OFAT evaluations use copied `ProcessInputs` and the same calculation path. `RecycleMassBalanceEngine` owns the iterative species balance. CSV export evaluates one current steady-state operating point and synchronizes its input/output basis; smoothed display values are not mixed with unrelated engine defaults.

UI, warnings, reactor/catalyst and pipe visuals observe shared process state. Runtime creation is intentional and owned by the existing bootstrap components. Flow discovery depends on authored route prefixes. Preserve those names and the single-owner startup behavior.

Prefer serialized assets for stable reusable art and layout when practical; no late blanket prefab conversion is required for this release. Generated particles, live graph samples and process values remain runtime data. Do not delete legacy source or vendor samples without checking references.

The opt-in runtime acceptance harness extends RuntimeValidationCapture. It is inactive without an explicit validation/capture argument. It may create local output files and quit its own player after testing; ordinary interactive use is unchanged.

Use the pinned Editor/packages and retain asset .meta files. Provenance and font/package notices are tracked separately. Unknown asset permissions are never inferred from a filename or Git author. See REFERENCES_AND_ASSET_PROVENANCE.md.
