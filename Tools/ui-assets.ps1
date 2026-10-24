param(
    [string]$UnityPath = $env:UNITY_EDITOR
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path $PSScriptRoot -Parent
if (-not $UnityPath) {
    $version = (Get-Content (Join-Path $projectPath 'ProjectSettings/ProjectVersion.txt') | Select-String 'm_EditorVersion:').ToString().Split(':')[1].Trim()
    $UnityPath = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$version/Editor/Unity.exe"
}
New-Item -ItemType Directory -Force (Join-Path $projectPath 'Builds/Logs') | Out-Null
$logFile = Join-Path $projectPath 'Builds/Logs/ui-assets.log'
$process = Start-Process -FilePath $UnityPath -ArgumentList @('-batchmode', '-quit', '-projectPath', "`"$projectPath`"", '-executeMethod', 'Game.Editor.UiAssets.Create', '-logFile', "`"$logFile`"") -Wait -PassThru -NoNewWindow
if ($process.ExitCode -ne 0) { throw "Unity exited with code $($process.ExitCode). Log: $logFile" }
Write-Host "UI assets ready: Assets/Game/Art/Fonts/*.asset, Assets/Game/Art/Resources/Ui/UiTheme.asset"
