param(
  [Parameter(Mandatory=$true)][ValidateSet(2021,2022,2023,2024,2025,2026,2027)][int]$Year,
  [ValidateSet("User","Machine")][string]$Scope="User"
)
$utf8 = New-Object System.Text.UTF8Encoding($false)
[Console]::OutputEncoding = $utf8
$OutputEncoding = $utf8

$ErrorActionPreference="Stop"
$root=Split-Path -Parent $PSScriptRoot
& "$PSScriptRoot\Package-Bundle.ps1" -Years $Year
$src=Join-Path $root "artifacts\YangCADAgent.bundle"
if(-not(Test-Path(Join-Path $src "PackageContents.xml"))){throw "Bundle packaging failed: PackageContents.xml missing."}

if($Scope -eq "Machine"){
  $base=Join-Path $env:ProgramFiles "Autodesk\ApplicationPlugins"
}else{
  $base=Join-Path $env:APPDATA "Autodesk\ApplicationPlugins"
}
$dst=Join-Path $base "YangCADAgent.bundle"
New-Item -ItemType Directory -Force -Path $base|Out-Null
if(Test-Path $dst){Remove-Item $dst -Recurse -Force}
Copy-Item $src $dst -Recurse -Force

# Clear Mark-of-the-Web where possible. This does not replace AutoCAD trusted-location/signing policy.
Get-ChildItem $dst -Recurse -File | ForEach-Object { try { Unblock-File -LiteralPath $_.FullName -ErrorAction Stop } catch { } }

$primary=Join-Path $dst "Contents\$Year\YangCAD.AutoCAD.dll"
if(-not(Test-Path $primary)){throw "部署后缺少主 DLL：$primary"}
$batchWorker=Join-Path $dst "Contents\$Year\YangCAD.BatchWorker.dll"
if(-not(Test-Path $batchWorker)){throw "部署后缺少 BatchWorker DLL：$batchWorker"}

Write-Host "Deployed: $dst"
if($Scope -eq "Machine"){
  Write-Host "Install scope: Machine / Autodesk recommended trusted ApplicationPlugins path."
}else{
  Write-Warning "Install scope: User (%APPDATA%). Autodesk does not treat this location like %PROGRAMFILES%\\Autodesk\\ApplicationPlugins; an unsigned executable warning/trust configuration may still block loading."
  Write-Warning "TASK-001 首闭环若要排除安全策略变量，建议使用管理员 PowerShell：.\\tools\\Deploy-Dev.ps1 -Year $Year -Scope Machine"
}
Write-Host "Restart AutoCAD/Civil 3D, then run YANGCAD_MCP_STATUS and YANGCAD."
Write-Host "Runtime log: %LOCALAPPDATA%\YangCADAgent\logs\runtime.log"
