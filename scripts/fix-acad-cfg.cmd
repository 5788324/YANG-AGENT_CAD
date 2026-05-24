@echo off
setlocal

set "SRC=%USERPROFILE%\AppData\Local\Autodesk\AutoCAD 2027\R26.0\chs\acad2027.cfg"
set "DST=C:\Program Files\Autodesk\AutoCAD 2027\acad2027.cfg"

echo.
echo YANG Agent CAD - AutoCAD 2027 cfg repair
echo.
echo Source:
echo   %SRC%
echo Destination:
echo   %DST%
echo.

net session >nul 2>nul
if not "%ERRORLEVEL%"=="0" (
  echo Please run this script as Administrator.
  echo Right-click scripts\fix-acad-cfg.cmd and choose "Run as administrator".
  echo.
  pause
  exit /b 1
)

if not exist "%SRC%" (
  echo Source config not found:
  echo %SRC%
  echo.
  echo Open AutoCAD 2027 once, then run this script again as Administrator.
  echo.
  pause
  exit /b 1
)

if exist "%DST%" (
  echo Destination already exists:
  echo %DST%
  echo.
  echo Nothing to copy. Now run scripts\doctor.cmd again.
  echo.
  pause
  exit /b 0
)

copy "%SRC%" "%DST%"
if not "%ERRORLEVEL%"=="0" (
  echo Failed to copy config.
  echo.
  pause
  exit /b 1
)

echo Copied AutoCAD config:
echo From: %SRC%
echo To:   %DST%
echo Now run scripts\doctor.cmd again.
echo.
pause
