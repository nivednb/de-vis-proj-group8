# References and Asset Provenance

This register is part of the submission record. Complete every blank field before the evaluated release is tagged. Do not infer a licence or author from a filename; confirm it from the original source.

## Unity and package dependencies

| Dependency | Version | Source | Licence / terms | Purpose |
| --- | --- | --- | --- | --- |
| Unity Editor | 6000.4.7f1 | Unity Technologies | Verify the institutional licence and current Unity terms | Development and Windows build |
| Universal Render Pipeline | 17.4.0 | Unity Package Manager | Verify against the installed package documentation | Rendering |
| TextMesh Pro | Project package lock | Unity Package Manager | Verify against the installed package documentation | Interface text |
| Unity UI | Project package lock | Unity Package Manager | Verify against the installed package documentation | Runtime interface |
| Unity AI Navigation | Project package lock | Unity Package Manager | Verify against the installed package documentation | NavMesh/pathfinding support |

Confirm the complete resolved package list in `Packages/packages-lock.json` and add any package not covered above.

## Models, textures, materials, icons, fonts, and audio

| Asset or folder | Author / owner | Original source | Licence / permission | Modified by team? | Notes |
| --- | --- | --- | --- | --- | --- |
| Plant and equipment FBX assets |  |  |  |  | Confirm each source model and any team-authored modifications. |
| Runtime materials and shaders |  |  |  |  | Identify team-authored and third-party items separately. |
| Interface icons and fonts |  |  |  |  | Include the exact font licence and icon source. |
| Screenshots in `docs/images` | Project team | Project executable / Unity scene | Team-authored evidence | No | Record the exact release commit used for final evidence. |

## Engineering and scientific references

| Topic or value | Reference | Page / section / DOI or URL | How it is used |
| --- | --- | --- | --- |
| Methanol synthesis stoichiometry |  |  |  |
| Reactor operating range |  |  |  |
| Electrolyser assumptions |  |  |  |
| CO2 capture assumptions |  |  |  |
| Separation and recycle assumptions |  |  |  |
| Storage limits and warning thresholds |  |  |  |

## Interface and visual references

List external screenshots, applications, design files, or industrial dashboards used as inspiration. Describe the specific ideas adopted and avoid redistributing external images unless permission allows it.

| Reference | Owner / source | Permission or citation | Ideas used |
| --- | --- | --- | --- |
| Team interface concept |  |  | Navigation, information hierarchy, analytics, or visual language |
| Industrial process-dashboard references |  |  | Stream legend, equipment focus, KPI presentation |

## Final review

- [ ] Every non-team asset has an author, source, and licence or written permission.
- [ ] Every important engineering value has a traceable source or is clearly labelled as an educational assumption.
- [ ] External reference images not permitted for redistribution are absent from the repository and release package.
- [ ] Team-authored assets and individual contributions are identified accurately.
- [ ] The submitted executable contains no unlicensed third-party content.
