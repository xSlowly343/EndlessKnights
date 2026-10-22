param([int]$Runs = 100)
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'CoreBuild/Game.SaveCheck/Game.SaveCheck.csproj'
$output = Join-Path $PSScriptRoot 'CoreBuild/Game.SaveCheck/bin/kill-test'
dotnet build $project -c Release -o $output --nologo | Out-Null
$exe = Join-Path $output 'save-check.exe'
$folder = Join-Path ([System.IO.Path]::GetTempPath()) ('ek-save-kill-' + [guid]::NewGuid().ToString('N'))
$failed = 0
$repoRoot = Split-Path $PSScriptRoot -Parent
for ($run = 1; $run -le $Runs; $run++) {
    $writer = Start-Process -FilePath $exe -ArgumentList 'write', $folder -WorkingDirectory $repoRoot -PassThru -WindowStyle Hidden
    Start-Sleep -Milliseconds (Get-Random -Minimum 300 -Maximum 900)
    Stop-Process -Id $writer.Id -Force
    $writer.WaitForExit()
    $check = & $exe check $folder
    if ($LASTEXITCODE -ne 0) { $failed++ }
    Write-Output "run ${run}: $($check | Select-Object -Last 1)"
}
Remove-Item -Recurse -Force $folder
Write-Output "$Runs kills, $failed without a readable save"
exit $failed
