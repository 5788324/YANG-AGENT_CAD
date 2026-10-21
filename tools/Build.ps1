param(
  [Parameter(Mandatory=$true)][ValidateSet(2021,2022,2023,2024,2025,2026,2027)][int]$Year,
  [ValidateSet("Debug","Release")][string]$Configuration="Release",
  [switch]$SkipCivil
)
$utf8 = New-Object System.Text.UTF8Encoding($false)
[Console]::OutputEncoding = $utf8
$OutputEncoding = $utf8

$ErrorActionPreference="Stop"
$root=Split-Path -Parent $PSScriptRoot
$acadRoot="C:\Program Files\Autodesk\AutoCAD $Year"
$civilRoot=Join-Path $acadRoot "C3D"
$logDir=Join-Path $root "artifacts\logs"
$autoOut=Join-Path $root "artifacts\bin\$Year\AutoCAD"
$civilOut=Join-Path $root "artifacts\bin\$Year\Civil3D"
$batchOut=Join-Path $root "artifacts\bin\$Year\BatchWorker"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

if(-not(Test-Path(Join-Path $acadRoot "AcMgd.dll"))){throw "AutoCAD $Year API not found: $acadRoot"}
if(-not(Test-Path(Join-Path $acadRoot "AdWindows.dll"))){throw "AutoCAD Ribbon API not found: $(Join-Path $acadRoot 'AdWindows.dll')"}

if(Test-Path $autoOut){Remove-Item $autoOut -Recurse -Force}
if(Test-Path $civilOut){Remove-Item $civilOut -Recurse -Force}
if(Test-Path $batchOut){Remove-Item $batchOut -Recurse -Force}

Write-Host "[YangCAD] Build AutoCAD $Year ($Configuration)..."
dotnet build "$root\src\YangCAD.AutoCAD\YangCAD.AutoCAD.csproj" -c $Configuration -p:CadYear=$Year -p:AcadRoot="$acadRoot" -bl:"$logDir\build-autocad-$Year.binlog"
if($LASTEXITCODE -ne 0){throw "AutoCAD $Year build failed (dotnet exit code $LASTEXITCODE)."}
if(-not(Test-Path(Join-Path $autoOut "YangCAD.AutoCAD.dll"))){throw "AutoCAD build returned success but YangCAD.AutoCAD.dll is missing."}

if($Year -le 2024 -and (Test-Path(Join-Path $autoOut "System.Text.Json.dll"))){
  throw "AutoCAD $Year/net48 output contains System.Text.Json.dll; current design requires 0 System.Text.Json external dependency."
}

Write-Host "[YangCAD] Build BatchWorker $Year ($Configuration)..."
dotnet build "$root\src\YangCAD.BatchWorker\YangCAD.BatchWorker.csproj" -c $Configuration -p:CadYear=$Year -p:AcadRoot="$acadRoot" -bl:"$logDir\build-batch-$Year.binlog"
if($LASTEXITCODE -ne 0){throw "BatchWorker $Year build failed (dotnet exit code $LASTEXITCODE)."}
if(-not(Test-Path(Join-Path $batchOut "YangCAD.BatchWorker.dll"))){throw "BatchWorker build returned success but YangCAD.BatchWorker.dll is missing."}
if($Year -le 2024 -and (Test-Path(Join-Path $batchOut "System.Text.Json.dll"))){
  throw "BatchWorker $Year/net48 output contains System.Text.Json.dll; current design requires 0 System.Text.Json external dependency."
}

if(-not $SkipCivil){
  if(-not(Test-Path(Join-Path $civilRoot "AeccDbMgd.dll"))){throw "Civil 3D $Year API not found: $civilRoot; use -SkipCivil to build AutoCAD only"}
  $aecBase = if(Test-Path(Join-Path $acadRoot "AecBaseMgd.dll")){ Join-Path $acadRoot "AecBaseMgd.dll" } else { Join-Path $civilRoot "AecBaseMgd.dll" }
  if(-not(Test-Path $aecBase)){throw "Civil 3D base API AecBaseMgd.dll not found: $aecBase"}
  Write-Host "[YangCAD] Build Civil 3D $Year ($Configuration)..."
  dotnet build "$root\src\YangCAD.Civil3D\YangCAD.Civil3D.csproj" -c $Configuration -p:CadYear=$Year -p:AcadRoot="$acadRoot" -p:CivilRoot="$civilRoot" -bl:"$logDir\build-civil3d-$Year.binlog"
  if($LASTEXITCODE -ne 0){throw "Civil 3D $Year build failed (dotnet exit code $LASTEXITCODE)."}
  if(-not(Test-Path(Join-Path $civilOut "YangCAD.Civil3D.dll"))){throw "Civil build returned success but YangCAD.Civil3D.dll is missing."}
}
Write-Host "Build finished: $Year"
