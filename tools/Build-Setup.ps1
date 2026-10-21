param(
  [ValidateSet('Debug','Release')][string]$Configuration='Release'
)
$ErrorActionPreference='Stop'
$utf8=New-Object System.Text.UTF8Encoding($false);[Console]::OutputEncoding=$utf8;$OutputEncoding=$utf8
$root=Split-Path -Parent $PSScriptRoot
$out=Join-Path $root 'artifacts\bin\Setup'
$logDir=Join-Path $root 'artifacts\logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
if(Test-Path $out){Remove-Item $out -Recurse -Force}
Write-Host "[YangCAD] Build Setup ($Configuration)..."
dotnet build "$root\src\YangCAD.Setup\YangCAD.Setup.csproj" -c $Configuration -bl:"$logDir\build-setup.binlog"
if($LASTEXITCODE -ne 0){throw "YangCAD Setup build failed (dotnet exit code $LASTEXITCODE)."}
$exe=Join-Path $out 'YangCADAgent_Setup.exe'
if(-not(Test-Path $exe)){throw "Setup build returned success but YangCADAgent_Setup.exe is missing."}
Write-Host "Setup build finished: $exe"
