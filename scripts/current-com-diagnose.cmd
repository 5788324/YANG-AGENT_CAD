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

echo [YANG Agent CAD] AutoCAD COM read-only diagnosis
echo [YANG Agent CAD] This does not send LISP, save DWG, or modify drawings.
echo.
"%PYTHON%" -m yang_cad_agent.cli acad-com-diagnose --root "%ROOT%"
set "RC=%ERRORLEVEL%"
echo.
echo [YANG Agent CAD] If attachable is false and acad_process.running is true:
echo   Close AutoCAD, reopen it normally, open a test DWG, wait for command line, then retry.
echo [YANG Agent CAD] If diagnostics include acad_process_without_top_level_window:
echo   acad.exe is running in the background but no usable AutoCAD window exists.
echo   Do not send LISP. Close AutoCAD from Task Manager only after confirming no unsaved drawings.
echo [YANG Agent CAD] If diagnostics include acad_not_in_running_object_table:
echo   AutoCAD is running but not visible to COM ROT. Check startup/license dialogs or repair COM registration.
echo [YANG Agent CAD] If attachable is true:
echo   You can retry scripts\current-smoke-test.cmd --execute on a test DWG.
exit /b %RC%
