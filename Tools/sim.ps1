$ErrorActionPreference = 'Stop'
dotnet run --project (Join-Path $PSScriptRoot 'CoreBuild/Game.Sim/Game.Sim.csproj') -c Release -- @args
exit $LASTEXITCODE
