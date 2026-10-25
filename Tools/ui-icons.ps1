$ErrorActionPreference = 'Stop'
$projectPath = Split-Path $PSScriptRoot -Parent
uv run --with pillow python (Join-Path $projectPath 'art/scripts/build_ui_icons.py') --root $projectPath
if ($LASTEXITCODE -ne 0) { throw "UI icon build failed with code $LASTEXITCODE" }
