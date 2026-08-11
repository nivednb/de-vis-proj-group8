# Project Structure Snapshots

This directory records the final repository file structure after the
runtime-to-prefab refactor:

- `FINAL_PROJECT_STRUCTURE.md` represents the final refactored working tree and
  is regenerated immediately before the refactor commit.

For the project owner's private comparison, the generator also writes the exact
pre-refactor commit tree to the ignored local file
`Logs/BEFORE_RUNTIME_PREFAB_REFACTOR.md`. That baseline is intentionally not
committed.

Both inventories list every repository file at their stated revision. Unity
caches (`Library`, `Temp`, `Logs`), local builds, IDE state, and other ignored
machine output are excluded because they are reproducible and must not be kept
in source control.

To see files used in older or intermediate project states, Git remains the
authoritative history:

```powershell
git log --all --name-status -- Assets
git ls-tree -r --name-only <commit-or-branch>
```

To regenerate the committed final snapshot and local-only baseline from the
repository root:

```powershell
powershell -ExecutionPolicy Bypass -File tools/Export-RepositoryStructure.ps1
```

Change `-BeforeRevision` when establishing a different comparison baseline.
