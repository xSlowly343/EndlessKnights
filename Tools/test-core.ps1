$ErrorActionPreference = 'Stop'
dotnet test (Join-Path $PSScriptRoot 'CoreBuild/Game.Core.Tests/Game.Core.Tests.csproj') --nologo
exit $LASTEXITCODE
