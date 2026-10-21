$utf8 = New-Object System.Text.UTF8Encoding($false)
[Console]::OutputEncoding = $utf8
$OutputEncoding = $utf8

$root=Split-Path -Parent $PSScriptRoot
@("$root\artifacts",
  "$root\src\YangCAD.Core\bin", "$root\src\YangCAD.Core\obj",
  "$root\src\YangCAD.AutoCAD\bin", "$root\src\YangCAD.AutoCAD\obj",
  "$root\src\YangCAD.BatchWorker\bin", "$root\src\YangCAD.BatchWorker\obj",
  "$root\src\YangCAD.Civil3D\bin", "$root\src\YangCAD.Civil3D\obj",
  "$root\src\YangCAD.Setup\bin", "$root\src\YangCAD.Setup\obj",
  "$root\mcp\.venv") | ForEach-Object { if(Test-Path $_){ Remove-Item $_ -Recurse -Force } }

Get-ChildItem $root -Recurse -Directory -Filter "__pycache__" -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
Get-ChildItem $root -Recurse -File -Include "*.pyc", "*.binlog" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue

Write-Host "Clean complete"

