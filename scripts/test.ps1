$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Python = & (Join-Path $PSScriptRoot "python.ps1")
$env:PYTHONPATH = Join-Path $Root "src"
& $Python -m unittest discover -s (Join-Path $Root "tests")

