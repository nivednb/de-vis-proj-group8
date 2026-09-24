# References and asset provenance

Status: technical inventory completed; original model/icon ownership and institution-specific permissions await team confirmation. Unknowns are explicit and are not licence grants.

## Scientific references and assumption mapping

See SCIENTIFIC_VALIDATION.md, sources S1-S4, for full bibliographic details and exactly what each source supports.

| Topic | Evidence | Boundary |
|---|---|---|
| Electrolysis chemistry | S1, DOE reaction explanation | Stoichiometry only, not electrical efficiency |
| Methanol reaction and operating context | S2, S3 primary research | No fitted kinetic parameters transferred |
| Process system boundary | S4, JRC report | No cost/emission claims transferred |
| 215/1510/1935 kg/h capacities and 12000 kg tank | Configured project assumptions | Team must provide original design brief if these were externally specified |
| Capture, recovery, purity coefficients | Explicit formulas in IMPLEMENTATION_REFERENCE.md | Educational assumptions; uncalibrated |
| 99% storage trip; reactor warning thresholds | Source-code thresholds, documented in IMPLEMENTATION_REFERENCE.md | Teaching thresholds, not certified limits |
| Catalyst color and particle appearance | Source-coded visual conventions | No claim of literal chemistry or trajectories |

## Assets

`evidence/asset-provenance-inventory.csv` enumerates each FBX/Blender asset with SHA-256. Every imported equipment model needs its original creator/source and permission confirmed by the team. Filenames and Git commits do not establish ownership. This includes reactor, tanks, columns, compressor, condenser, supports, bends and junctions.

| Content | Verified evidence | Remaining action |
|---|---|---|
| Liberation Sans | `Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt`: Google/Red Hat copyright notices and SIL OFL 1.1 | Preserve licence in distributed notices |
| Other TMP example fonts | Licence files accompany the example fonts under TextMesh Pro/Examples & Extras/Fonts | Preserve applicable notices if included in a distributed artifact |
| Unity/URP/Input/uGUI and dependencies | Resolved versions and observed licence notices in `evidence/package-licenses.csv` | Keep installed package notices; institution confirms Unity entitlement |
| Plant/equipment FBX files | Exact asset hashes inventoried | TEAM INPUT REQUIRED: author, origin, modifications, redistribution rights |
| `Assets/gear.png`, `Assets/Icons/gear.png` | Present in source | TEAM INPUT REQUIRED: original icon source/licence |
| Runtime C#/shader/material construction | Inspectable repository history | TEAM INPUT REQUIRED: contributions and tool assistance; Git author names are not an authorship certification |
| New QA screenshots | Captured from this application's QA runs | Record matching source/build manifest; original embedded assets still need permission |
| ICODOS-style visual inspiration | Existing source/doc naming states inspiration | TEAM INPUT REQUIRED: reference source and permission for copied material; no affiliation is claimed |

No blanket licence is assigned to unknown assets. Complete pending rows before sharing the final package beyond permitted assessment use. This file is a provenance record, not legal advice or a declaration that all assets are cleared.
