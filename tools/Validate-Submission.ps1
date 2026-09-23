param(
    [string]$Editor = 'C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe',
    [string]$Project = (Split-Path -Parent $PSScriptRoot),
    [string]$Output = '',
    [int]$SoakSeconds = 900
)
$ErrorActionPreference = 'Stop'
$Project = (Resolve-Path -LiteralPath $Project).Path
if (-not (Test-Path -LiteralPath $Editor)) { throw 'Pinned Unity Editor not found.' }
if (-not $Output) { $Output = Join-Path $Project 'Release\PtMeOH-FinalSubmission-Windows-x64' }
$Output = [IO.Path]::GetFullPath($Output)
$logs = Join-Path $Project 'Logs'
New-Item -ItemType Directory -Force -Path $logs, $Output | Out-Null
function Run-Unity([string]$Method, [string]$LogName, [string]$SuccessMarker) {
    $log = Join-Path $logs $LogName
    $run = Start-Process -FilePath $Editor -ArgumentList @('-batchmode','-nographics','-quit','-projectPath',('"'+$Project+'"'),'-executeMethod',$Method,'-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru -Wait
    if ($run.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -SimpleMatch $SuccessMarker -Quiet)) { throw "Unity validation failed: $Method. See $log" }
}
Run-Unity 'SubmissionValidation.Run' 'submission-numerical.log' 'SUBMISSION_VALIDATION_PASS'
$previousBuildPath = $env:PTMEOH_BUILD_PATH
try {
    $env:PTMEOH_BUILD_PATH = Join-Path $Output 'PtMeOH-DigitalTwin.exe'
    Run-Unity 'WindowsBuild.BuildWindows' 'submission-build.log' 'PtMeOH Windows build result: Succeeded'
} finally { $env:PTMEOH_BUILD_PATH = $previousBuildPath }
$exe = Join-Path $Output 'PtMeOH-DigitalTwin.exe'
foreach ($resolution in @(@(1920,1080,$SoakSeconds), @(1280,720,30))) {
    $w=$resolution[0]; $h=$resolution[1]; $duration=$resolution[2]
    $evidence = Join-Path $Project "docs\evidence\final-runtime-$h"
    $log = Join-Path $logs "final-player-$h.log"
    $player = Start-Process -FilePath $exe -ArgumentList @('-screen-fullscreen','0','-screen-width',$w,'-screen-height',$h,'-ptmeoh-validate',('"'+$evidence+'"'),'-ptmeoh-duration',$duration,'-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru -Wait
    $report = Join-Path $evidence 'runtime-validation.txt'
    if ($player.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $report) -or -not (Select-String -LiteralPath $report -SimpleMatch 'RUNTIME_VALIDATION_PASS' -Quiet)) { throw "Player validation failed at ${w}x${h}; see $log" }
}
Write-Output "Validation completed. Review evidence and manual checks before packaging: $Output"
