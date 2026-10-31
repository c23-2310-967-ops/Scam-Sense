# Runs every Scam Sense test from the command line, without opening the editor window.
# Close Unity first (Unity cannot open the same project twice), then from the project folder:
#     powershell -ExecutionPolicy Bypass -File Assets/Tests/run-tests.ps1
# Results: TestResults/results.xml (full) and a summary printed here. Exit code 0 = all passed.

$ErrorActionPreference = 'Stop'
$project = (Resolve-Path "$PSScriptRoot/../..").Path
$version = (Select-String -Path "$project/ProjectSettings/ProjectVersion.txt" -Pattern '^m_EditorVersion: (.+)$').Matches[0].Groups[1].Value.Trim()
$unity = "C:/Program Files/Unity/Hub/Editor/$version/Editor/Unity.exe"
if (-not (Test-Path $unity)) { Write-Error "Unity $version not found at $unity" }

# Unity keeps Temp/UnityLockfile locked while the project is open in the editor.
$lock = Join-Path $project 'Temp/UnityLockfile'
if (Test-Path $lock) {
    try { [System.IO.File]::Open($lock, 'Open', 'ReadWrite', 'None').Close() }
    catch {
        Write-Host "Unity is open on this project. Either close Unity and run this again," -ForegroundColor Yellow
        Write-Host "or run the tests inside Unity: Tools > Scam Sense > Run All Tests." -ForegroundColor Yellow
        exit 3
    }
}

$out = Join-Path $project 'TestResults'
New-Item -ItemType Directory -Force $out | Out-Null
$xml = Join-Path $out 'results.xml'
if (Test-Path $xml) { Remove-Item $xml }

Write-Host "Running tests with Unity $version (takes a minute)..."
$p = Start-Process -FilePath $unity -Wait -PassThru -ArgumentList @(
    '-batchmode', '-projectPath', "`"$project`"", '-runTests', '-testPlatform', 'EditMode',
    '-testResults', "`"$xml`"", '-logFile', "`"$(Join-Path $out 'unity.log')`"")

if (-not (Test-Path $xml)) {
    Write-Host "No results were written - is Unity still open on this project? See TestResults/unity.log" -ForegroundColor Red
    exit 2
}

[xml]$doc = Get-Content $xml
$run = $doc.'test-run'
foreach ($case in $doc.SelectNodes('//test-case')) {
    if ($case.result -eq 'Failed') {
        Write-Host "FAIL  $($case.fullname)" -ForegroundColor Red
        Write-Host ("      " + ($case.failure.message.'#cdata-section' -replace "`n", "`n      ").Trim())
    }
}
$color = 'Green'
if ([int]$run.failed -gt 0) { $color = 'Red' }
Write-Host "$($run.passed) passed, $($run.failed) failed, $($run.skipped) skipped" -ForegroundColor $color
exit $p.ExitCode
