$ErrorActionPreference = 'Stop'
$projectPath = Split-Path $PSScriptRoot -Parent
$script = Join-Path $projectPath 'art/scripts/build_ui_icons.py'
if (Get-Command uv -ErrorAction SilentlyContinue) {
    uv run --with pillow python $script --root $projectPath
} else {
    python $script --root $projectPath
}
if ($LASTEXITCODE -ne 0) { throw "UI icon build failed with code $LASTEXITCODE" }
