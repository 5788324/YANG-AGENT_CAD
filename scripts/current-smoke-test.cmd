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
echo [YANG Agent CAD] Current drawing LISP smoke test
echo [YANG Agent CAD] Default mode is dry-run. It will not connect to AutoCAD or modify DWG files.
echo [YANG Agent CAD] Before --execute, open AutoCAD and open a test DWG.
echo.

if /I "%~1"=="--execute" (
  echo [YANG Agent CAD] Execute mode: trying to connect to the current AutoCAD drawing.
  echo [YANG Agent CAD] This only runs current_smoke. It prints a test message and does not save DWG files.
  echo [YANG Agent CAD] Preflight will stop early if AutoCAD COM is not attachable.
  echo.
  "%PYTHON%" -m yang_cad_agent.cli current-smoke --root "%ROOT%" --execute
) else (
  echo [YANG Agent CAD] Dry-run mode: generating wrapper_path and completion_marker only.
  echo.
  "%PYTHON%" -m yang_cad_agent.cli current-smoke --root "%ROOT%"
)

set "RC=%ERRORLEVEL%"
echo.
echo [YANG Agent CAD] Result guide:
echo   completed        = AutoCAD executed the wrapper and wrote completion_marker.
echo   sent_unconfirmed = Command was sent, but completion_marker was not seen yet.
echo   failed           = Execution failed. Copy task_id and ask AI to run task_error_detail.
echo   blocked          = Preflight stopped early. Follow next_step; there may be no task_id.
echo.
echo [YANG Agent CAD] If you see error_code, give the JSON output above to AI.
echo [YANG Agent CAD] Common causes: AutoCAD is closed, no active DWG, missing pywin32, or AutoCAD COM is unavailable.
echo [YANG Agent CAD] You can also run scripts\current-com-diagnose.cmd for a read-only COM diagnosis.
exit /b %RC%
