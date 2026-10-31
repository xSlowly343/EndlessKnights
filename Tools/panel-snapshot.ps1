param(
    [string]$UnityPath = $env:UNITY_EDITOR
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path $PSScriptRoot -Parent
if (-not $UnityPath) {
    $version = (Get-Content (Join-Path $projectPath 'ProjectSettings/ProjectVersion.txt') | Select-String 'm_EditorVersion:').ToString().Split(':')[1].Trim()
    $UnityPath = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$version/Editor/Unity.exe"
}
$logFile = Join-Path $projectPath 'Builds/Logs/panel-snapshot.log'
$process = Start-Process -FilePath $UnityPath -ArgumentList @('-batchmode', '-quit', '-projectPath', "`"$projectPath`"", '-executeMethod', 'Game.Editor.PanelSnapshot.Capture', '-logFile', "`"$logFile`"") -Wait -PassThru -NoNewWindow
if ($process.ExitCode -ne 0) { throw "Unity exited with code $($process.ExitCode). Log: $logFile" }
Write-Host "Snapshots in $(Join-Path $projectPath 'Builds/Logs'): strip.png, panel-subclass-choose.png, panel-subclass.png, panel-hero.png, panel-items.png, panel-rarities.png, panel-party.png, panel-book.png, panel-book-drag.png, panel-book-half.png, panel-stats.png, panel-hero-half.png, panel-more.png, panel-settings.png, panel-settings-sound.png, panel-notice.png, panel-report.png, panel-consent.png, strip-notifications.png"
