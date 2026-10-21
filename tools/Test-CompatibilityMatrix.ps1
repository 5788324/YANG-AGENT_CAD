param(
  [ValidateSet(2021,2022,2023,2024,2025,2026,2027)][int[]]$Years=@(2021,2022,2023,2024,2025,2026,2027),
  [ValidateSet('Debug','Release')][string]$Configuration='Release'
)
$ErrorActionPreference='Stop'
$utf8=New-Object System.Text.UTF8Encoding($false);[Console]::OutputEncoding=$utf8;$OutputEncoding=$utf8
$root=Split-Path -Parent $PSScriptRoot
$outDir=Join-Path $root 'artifacts\release';New-Item -ItemType Directory -Force -Path $outDir|Out-Null
$framework=@{2021='net48';2022='net48';2023='net48';2024='net48';2025='net8.0-windows';2026='net8.0-windows';2027='net10.0-windows'}
$series=@{2021='R24.0';2022='R24.1';2023='R24.2';2024='R24.3';2025='R25.0';2026='R25.1';2027='R26.0'}
$rows=@()
foreach($y in ($Years|Sort-Object -Unique)){
  $acad="C:\Program Files\Autodesk\AutoCAD $y"
  $civil=Join-Path $acad 'C3D'
  $acadInstalled=Test-Path(Join-Path $acad 'AcMgd.dll')
  $civilInstalled=Test-Path(Join-Path $civil 'AeccDbMgd.dll')
  $autoOut=Join-Path $root "artifacts\bin\$y\AutoCAD"
  $batchOut=Join-Path $root "artifacts\bin\$y\BatchWorker"
  $civilOut=Join-Path $root "artifacts\bin\$y\Civil3D"

  $build='NOT_INSTALLED';$buildErr=$null
  $autocadBuild=if($acadInstalled){'NOT_TESTED'}else{'NOT_INSTALLED'}
  $batchBuild=if($acadInstalled){'NOT_TESTED'}else{'NOT_INSTALLED'}
  $civilBuild=if($civilInstalled){'NOT_TESTED'}else{'NOT_INSTALLED'}
  $systemTextJson=if($acadInstalled -and $y -le 2024){'NOT_TESTED'}else{'N/A'}

  if($acadInstalled){
    try{
      & "$PSScriptRoot\Build.ps1" -Year $y -Configuration $Configuration -SkipCivil:(-not $civilInstalled)
    }catch{
      $buildErr=$_.Exception.Message
    }

    $autocadBuild=if(Test-Path(Join-Path $autoOut 'YangCAD.AutoCAD.dll')){'PASS'}else{'FAIL'}
    $batchBuild=if(Test-Path(Join-Path $batchOut 'YangCAD.BatchWorker.dll')){'PASS'}else{'FAIL'}
    if($civilInstalled){$civilBuild=if(Test-Path(Join-Path $civilOut 'YangCAD.Civil3D.dll')){'PASS'}else{'FAIL'}}

    if($y -le 2024){
      $jsonFound=(Test-Path(Join-Path $autoOut 'System.Text.Json.dll')) -or (Test-Path(Join-Path $batchOut 'System.Text.Json.dll'))
      $systemTextJson=if($jsonFound){'FAIL'}else{'PASS'}
    }

    $components=@($autocadBuild,$batchBuild)
    if($civilInstalled){$components+=$civilBuild}
    $build=if($components -contains 'FAIL'){'FAIL'}else{'PASS'}
  }

  $rows+=@{
    year=$y;series=$series[$y];framework=$framework[$y];
    autocad_installed=$acadInstalled;civil3d_installed=$civilInstalled;
    clean_build=$build;build_error=$buildErr;
    autocad_build=$autocadBuild;batch_build=$batchBuild;civil_build=$civilBuild;
    system_text_json=$systemTextJson;
    runtime_autocad='NOT_TESTED';runtime_civil3d='NOT_TESTED';batch_runtime='NOT_TESTED';contract='NOT_TESTED';token='NOT_TESTED';notes=''
  }
}
$result=@{schema=2;generated_at=(Get-Date).ToString('o');source_version='0.9.0';rows=$rows}
$path=Join-Path $outDir 'compatibility-matrix.json';$result|ConvertTo-Json -Depth 6|Set-Content $path -Encoding UTF8
Write-Host "Compatibility build matrix: $path"
$rows|Format-Table year,framework,autocad_installed,civil3d_installed,autocad_build,batch_build,civil_build,clean_build -AutoSize
