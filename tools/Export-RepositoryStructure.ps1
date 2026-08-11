param(
    [string]$BeforeRevision = "6b99b0f",
    [string]$OutputDirectory = "docs/PROJECT_STRUCTURE",
    [string]$LocalBeforeOutput = "Logs/BEFORE_RUNTIME_PREFAB_REFACTOR.md"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = (git rev-parse --show-toplevel).Trim()
if (-not $repositoryRoot) { throw "Not inside a Git repository." }

$outputPath = Join-Path $repositoryRoot $OutputDirectory
New-Item -ItemType Directory -Force -Path $outputPath | Out-Null

function Write-TreeDocument {
    param(
        [string]$Title,
        [string]$Revision,
        [string[]]$Paths,
        [string]$Destination
    )

    $ordered = $Paths | Where-Object { $_ } | Sort-Object -Unique
    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add("# $Title")
    $lines.Add("")
    $lines.Add(('- Repository: `{0}`' -f (Split-Path $repositoryRoot -Leaf)))
    $lines.Add(('- Revision: `{0}`' -f $Revision))
    $lines.Add("- File count: $($ordered.Count)")
    $lines.Add("- Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss K')")
    $lines.Add("")
    $lines.Add("Unity caches, local logs, builds, and other ignored machine output are intentionally excluded.")
    $lines.Add("")
    $lines.Add('```text')
    foreach ($path in $ordered) { $lines.Add($path.Replace('\', '/')) }
    $lines.Add('```')
    [System.IO.File]::WriteAllLines($Destination, $lines, [System.Text.UTF8Encoding]::new($false))
}

$beforePaths = git ls-tree -r --name-only $BeforeRevision
if ($LASTEXITCODE -ne 0) { throw "Cannot enumerate revision $BeforeRevision." }

$tracked = git ls-files
$untracked = git ls-files --others --exclude-standard
$finalStructureRelativePath = "$OutputDirectory/FINAL_PROJECT_STRUCTURE.md"
$afterPaths = @($tracked) + @($untracked) + @($finalStructureRelativePath)

Write-TreeDocument `
    -Title "Project Structure Before Runtime-Prefab Refactor" `
    -Revision $BeforeRevision `
    -Paths $beforePaths `
    -Destination (Join-Path $repositoryRoot $LocalBeforeOutput)

Write-TreeDocument `
    -Title "Final Project Structure After Runtime-Prefab Refactor" `
    -Revision "working tree on $(git branch --show-current)" `
    -Paths $afterPaths `
    -Destination (Join-Path $outputPath "FINAL_PROJECT_STRUCTURE.md")

Write-Host "Final repository structure written to $outputPath"
Write-Host "Local-only before snapshot written to $(Join-Path $repositoryRoot $LocalBeforeOutput)"
