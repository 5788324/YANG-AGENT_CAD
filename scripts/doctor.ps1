$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Python = & (Join-Path $PSScriptRoot "python.ps1")
$env:PYTHONPATH = Join-Path $Root "src"
& $Python -m yang_cad_agent.cli doctor --json

