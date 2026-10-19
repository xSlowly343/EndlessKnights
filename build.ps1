param(
    [string]$UnityPath = $env:UNITY_EDITOR,
    [switch]$Tests
)

$ErrorActionPreference = 'Stop'
$projectPath = $PSScriptRoot
$logDirectory = Join-Path $projectPath 'Builds/Logs'

function Find-UnityEditor {
    $version = (Get-Content (Join-Path $projectPath 'ProjectSettings/ProjectVersion.txt') | Select-String 'm_EditorVersion:').ToString().Split(':')[1].Trim()
    $hubEditor = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$version/Editor/Unity.exe"
    if (Test-Path $hubEditor) { return $hubEditor }
    throw "Unity $version not found. Install it through Unity Hub or pass -UnityPath."
}

function Invoke-Unity([string[]]$Arguments, [string]$LogName) {
    $logFile = Join-Path $logDirectory $LogName
    $process = Start-Process -FilePath $UnityPath -ArgumentList ($Arguments + @('-batchmode', '-nographics', '-projectPath', "`"$projectPath`"", '-logFile', "`"$logFile`"")) -Wait -PassThru -NoNewWindow
    if ($process.ExitCode -ne 0) { throw "Unity exited with code $($process.ExitCode). Log: $logFile" }
}

if (-not $UnityPath) { $UnityPath = Find-UnityEditor }
New-Item -ItemType Directory -Force $logDirectory | Out-Null

if ($Tests) {
    $results = Join-Path $logDirectory 'editmode-results.xml'
    Invoke-Unity @('-runTests', '-testPlatform', 'EditMode', '-testResults', "`"$results`"") 'tests.log'
    Write-Host "Tests passed. Results: $results"
}

$stopwatch = [Diagnostics.Stopwatch]::StartNew()
Invoke-Unity @('-quit', '-executeMethod', 'Game.Editor.WindowsBuild.Build') 'build.log'
Write-Host ("Build succeeded in {0:N0} s: {1}" -f $stopwatch.Elapsed.TotalSeconds, (Join-Path $projectPath 'Builds/Windows/EndlessKnights.exe'))
