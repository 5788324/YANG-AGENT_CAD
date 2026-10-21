param([Parameter(Mandatory=$true)][ValidateSet(2021,2022,2023,2024,2025,2026,2027)][int[]]$Years)
$utf8 = New-Object System.Text.UTF8Encoding($false)
[Console]::OutputEncoding = $utf8
$OutputEncoding = $utf8

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$bundle = Join-Path $root "artifacts\YangCADAgent.bundle"
if (Test-Path $bundle) { Remove-Item $bundle -Recurse -Force }
New-Item -ItemType Directory -Force -Path (Join-Path $bundle "Contents") | Out-Null
$series = @{2021="R24.0"; 2022="R24.1"; 2023="R24.2"; 2024="R24.3"; 2025="R25.0"; 2026="R25.1"; 2027="R26.0"}
$parts = New-Object System.Collections.Generic.List[string]

foreach ($year in ($Years | Sort-Object -Unique)) {
    $dest = Join-Path $bundle "Contents\$year"
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    $a = Join-Path $root "artifacts\bin\$year\AutoCAD"
    $c = Join-Path $root "artifacts\bin\$year\Civil3D"
    $b = Join-Path $root "artifacts\bin\$year\BatchWorker"
    if (-not (Test-Path (Join-Path $a "YangCAD.AutoCAD.dll"))) { throw "Missing $year AutoCAD build" }
    Copy-Item "$a\*" $dest -Recurse -Force
    if (-not (Test-Path (Join-Path $b "YangCAD.BatchWorker.dll"))) { throw "Missing $year BatchWorker build" }
    Copy-Item (Join-Path $b "YangCAD.BatchWorker.dll") $dest -Force
    if ($year -le 2024 -and (Test-Path (Join-Path $dest "System.Text.Json.dll"))) {
        throw "$year bundle should not contain System.Text.Json.dll"
    }

    $s = $series[$year]
    $q = [char]34
    if(Test-Path(Join-Path $c "YangCAD.Civil3D.dll")){
        Copy-Item (Join-Path $c "YangCAD.Civil3D.dll") $dest -Force
        $s=$series[$year]
        $parts.Add("  <Components>`r`n    <RuntimeRequirements OS='Win64' Platform='Civil3D' SeriesMin='$s' SeriesMax='$s'/>`r`n    <ComponentEntry AppName='YangCAD.Civil3D.$year' ModuleName='./Contents/$year/YangCAD.Civil3D.dll'/>`r`n  </Components>".Replace("'", [string][char]34))
    }

    $acadEntry = "  <Components>`r`n    <RuntimeRequirements OS=" + $q + "Win64" + $q + " Platform=" + $q + "AutoCAD*" + $q + " SeriesMin=" + $q + $s + $q + " SeriesMax=" + $q + $s + $q + "/>`r`n    <ComponentEntry AppName=" + $q + "YangCAD.AutoCAD.$year" + $q + " ModuleName=" + $q + "./Contents/$year/YangCAD.AutoCAD.dll" + $q + "/>`r`n  </Components>"
    $parts.Add($acadEntry)
}


# Package the stdio MCP bridge next to the Autodesk binaries so "Copy MCP Config"
# can produce a repository-independent local command. Runtime dependencies are
# prepared under %LOCALAPPDATA%\YangCADAgent\mcp-runtime on first launch.
$mcpSource = Join-Path $root "mcp"
$mcpDest = Join-Path $bundle "Contents\Mcp"
if (-not (Test-Path (Join-Path $mcpSource "Start-YangCAD-Mcp.ps1"))) {
    throw "Missing MCP launcher: $mcpSource\Start-YangCAD-Mcp.ps1"
}
New-Item -ItemType Directory -Force -Path $mcpDest | Out-Null
Get-ChildItem $mcpSource -Recurse -File | Where-Object {
    $_.FullName -notmatch '[\\/](?:\.venv|__pycache__)[\\/]' -and $_.Extension -ne '.pyc'
} | ForEach-Object {
    $rel = $_.FullName.Substring($mcpSource.Length + 1)
    $target = Join-Path $mcpDest $rel
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
    Copy-Item $_.FullName $target -Force
}


# Package the image-embed batch worker. It is intentionally a script-driven external
# AutoCAD COM worker: live acad.exe never opens hundreds of DWGs and source files stay read-only.
$imageEmbedSource = Join-Path $root "workers\ImageEmbed"
$imageEmbedDest = Join-Path $bundle "Contents\ImageEmbed"
$imageEmbedLauncher = Join-Path $imageEmbedSource "Start-YangCAD-ImageEmbed.ps1"
if (-not (Test-Path $imageEmbedLauncher)) {
    throw "Missing ImageEmbed worker: $imageEmbedLauncher"
}
New-Item -ItemType Directory -Force -Path $imageEmbedDest | Out-Null
Copy-Item $imageEmbedLauncher (Join-Path $imageEmbedDest "Start-YangCAD-ImageEmbed.ps1") -Force

$template = Get-Content "$root\bundle\PackageContents.template.xml" -Raw -Encoding UTF8
$template.Replace("<!-- __COMPONENTS__ -->", ($parts -join "`r`n")) | Set-Content (Join-Path $bundle "PackageContents.xml") -Encoding UTF8
Write-Host "Bundle: $bundle"
