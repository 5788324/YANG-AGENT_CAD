$ErrorActionPreference = "Stop"
$utf8 = New-Object System.Text.UTF8Encoding($false)
[Console]::OutputEncoding = $utf8
$OutputEncoding = $utf8

$root = Split-Path -Parent $PSScriptRoot
$mcp = Join-Path $root "mcp"

if (Get-Command uv -ErrorAction SilentlyContinue) {
  Push-Location $mcp
  try {
    uv sync
    if ($LASTEXITCODE -ne 0) { throw "uv sync failed (exit code $LASTEXITCODE)." }
    Write-Host "MCP v2 ready"
    Write-Host "uv --directory `"$mcp`" run python -m yangcad_mcp.server"
  }
  finally { Pop-Location }
  exit 0
}

$py = Get-Command python -ErrorAction Stop
$venv = Join-Path $mcp ".venv"
$venvPython = Join-Path $venv "Scripts\python.exe"

if (-not (Test-Path $venvPython)) {
  & $py.Source -m venv $venv
  $venvExit = $LASTEXITCODE
  if ($venvExit -ne 0 -or -not (Test-Path $venvPython)) {
    Write-Warning "python -m venv failed; trying virtualenv fallback."
    if (Test-Path $venv) { Remove-Item $venv -Recurse -Force -ErrorAction SilentlyContinue }

    & $py.Source -m virtualenv --version 2>$null
    if ($LASTEXITCODE -ne 0) {
      & $py.Source -m pip install --user virtualenv
      if ($LASTEXITCODE -ne 0) { throw "venv failed and virtualenv could not be installed." }
    }
    & $py.Source -m virtualenv $venv
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $venvPython)) { throw "virtualenv fallback failed." }
  }
}

& $venvPython -m pip install --upgrade pip
if ($LASTEXITCODE -ne 0) { throw "pip upgrade failed (exit code $LASTEXITCODE)." }
& $venvPython -m pip install -e $mcp
if ($LASTEXITCODE -ne 0) { throw "YangCAD MCP install failed (exit code $LASTEXITCODE)." }
Write-Host "MCP v2 ready"
Write-Host "`"$venvPython`" -m yangcad_mcp.server"
