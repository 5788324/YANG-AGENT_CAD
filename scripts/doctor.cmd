@echo off
setlocal
set "ROOT=%~dp0.."
set "PYTHON="

where python >nul 2>nul
if not errorlevel 1 set "PYTHON=python"

if "%PYTHON%"=="" (
  where py >nul 2>nul
  if not errorlevel 1 set "PYTHON=py"
)

if "%PYTHON%"=="" (
  set "CODEX_PY=%USERPROFILE%\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe"
  if exist "%CODEX_PY%" set "PYTHON=%CODEX_PY%"
)

if "%PYTHON%"=="" (
  set "CODEX_PY=C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe"
  if exist "%CODEX_PY%" set "PYTHON=%CODEX_PY%"
)

if "%PYTHON%"=="" (
  echo No Python found. Install Python 3.10+ or run inside Codex desktop.
  exit /b 1
)

set "PYTHONPATH=%ROOT%\src"
"%PYTHON%" -m yang_cad_agent.cli doctor --json
