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
  echo [YANG Agent CAD] Python was not found. Run inside Codex Desktop or install Python 3.10+.
  exit /b 1
)

set "PYTHONPATH=%ROOT%\src"
echo [YANG Agent CAD] AutoCAD test launcher
echo [YANG Agent CAD] Default mode is dry-run. It will not start AutoCAD.
echo [YANG Agent CAD] Execute mode opens AutoCAD with a DWG test copy under .agent\tmp.
echo [YANG Agent CAD] This script does not close AutoCAD and does not save DWG files.
echo.

if /I "%~1"=="--execute" (
  echo [YANG Agent CAD] Execute mode: launching AutoCAD if the detected path is valid.
  echo.
  "%PYTHON%" -m yang_cad_agent.cli acad-open --root "%ROOT%" --execute
) else (
  echo [YANG Agent CAD] Dry-run mode: showing AutoCAD path, test DWG copy, and command only.
  echo.
  "%PYTHON%" -m yang_cad_agent.cli acad-open --root "%ROOT%"
)

set "RC=%ERRORLEVEL%"
echo.
echo [YANG Agent CAD] Next safe steps:
echo   1. Wait until AutoCAD is fully opened and the command line is ready.
echo   2. Run scripts\current-com-diagnose.cmd.
echo   3. Only if attachable=true, run scripts\current-smoke-test.cmd --execute.
echo.
echo [YANG Agent CAD] Close AutoCAD manually after saving or discarding your own drawings.
exit /b %RC%
