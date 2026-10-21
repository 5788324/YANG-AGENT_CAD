$ErrorActionPreference = "Stop"

function Write-Err([string]$Text) {
    [Console]::Error.WriteLine($Text)
}

$mcpRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $env:LOCALAPPDATA) {
    Write-Err "LOCALAPPDATA is not available."
    exit 2
}

$runtimeRoot = Join-Path $env:LOCALAPPDATA "YangCADAgent\mcp-runtime"
$venv = Join-Path $runtimeRoot "venv"
$venvPython = Join-Path $venv "Scripts\python.exe"
$depsMarker = Join-Path $runtimeRoot "mcp-v2-ready.txt"

try {
    if (-not (Test-Path -LiteralPath $venvPython)) {
        New-Item -ItemType Directory -Force -Path $runtimeRoot | Out-Null

        $pyLauncher = Get-Command py -ErrorAction SilentlyContinue
        if ($pyLauncher) {
            & $pyLauncher.Source -3 -m venv $venv 1>&2
        }
        else {
            $python = Get-Command python -ErrorAction SilentlyContinue
            if (-not $python) {
                throw "Python 3.10+ was not found. Install Python or uv, then retry the YangCAD MCP connection."
            }
            & $python.Source -m venv $venv 1>&2
        }
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $venvPython)) {
            throw "Failed to create the YangCAD MCP Python virtual environment."
        }
    }

    & $venvPython -c "import sys; raise SystemExit(0 if sys.version_info >= (3,10) else 3)" 1>$null
    if ($LASTEXITCODE -ne 0) {
        throw "YangCAD MCP requires Python 3.10 or newer."
    }

    $needsDeps = -not (Test-Path -LiteralPath $depsMarker)
    if (-not $needsDeps) {
        # Import alone is insufficient: a stale shared venv could contain an incompatible MCP major version.
        & $venvPython -c "import importlib.metadata as m,re; v=m.version('mcp'); x=re.match(r'^\s*(\d+)',v); raise SystemExit(0 if x and int(x.group(1)) == 2 else 4)" 1>$null 2>$null
        $needsDeps = $LASTEXITCODE -ne 0
    }

    if ($needsDeps) {
        Write-Err "Preparing YangCAD MCP runtime for the first launch..."
        & $venvPython -m pip install --disable-pip-version-check "mcp[cli]>=2.0,<3" 1>&2
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to install the MCP Python dependency. Check network access and retry."
        }
        Set-Content -LiteralPath $depsMarker -Value "mcp[cli]>=2.0,<3" -Encoding ASCII
    }

    Push-Location $mcpRoot
    try {
        & $venvPython -m yangcad_mcp.server
        exit $LASTEXITCODE
    }
    finally {
        Pop-Location
    }
}
catch {
    Write-Err ("YangCAD MCP launcher failed: " + $_.Exception.Message)
    exit 1
}
