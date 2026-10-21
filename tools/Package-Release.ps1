param(
  [Parameter(Mandatory=$true)][ValidateSet(2021,2022,2023,2024,2025,2026,2027)][int[]]$Years,
  [ValidateSet('Debug','Release')][string]$Configuration='Release',
  [ValidateSet('Quick','Signed')][string]$Mode='Quick',
  [string]$SignThumbprint='',
  [string]$TimestampUrl='',
  [switch]$SkipBuild
)
$ErrorActionPreference='Stop'
$utf8=New-Object System.Text.UTF8Encoding($false);[Console]::OutputEncoding=$utf8;$OutputEncoding=$utf8
$root=Split-Path -Parent $PSScriptRoot
$years=@($Years|Sort-Object -Unique)
if(-not $SkipBuild){
  foreach($y in $years){& "$PSScriptRoot\Build.ps1" -Year $y -Configuration $Configuration;if($LASTEXITCODE -ne 0){throw "Build failed: $y"}}
  & "$PSScriptRoot\Build-Setup.ps1" -Configuration $Configuration
  if($LASTEXITCODE -ne 0){throw 'Setup build failed.'}
}
& "$PSScriptRoot\Package-Bundle.ps1" -Years $years
$bundle=Join-Path $root 'artifacts\YangCADAgent.bundle'
$setupExe=Join-Path $root 'artifacts\bin\Setup\YangCADAgent_Setup.exe'
if(-not(Test-Path $setupExe)){throw "缺少 Setup 构建产物：$setupExe"}

$releaseDir=Join-Path $root 'artifacts\release\YangCADAgent_0.9.0_release'
if(Test-Path $releaseDir){Remove-Item $releaseDir -Recurse -Force}
New-Item -ItemType Directory -Force -Path $releaseDir|Out-Null
Copy-Item $bundle (Join-Path $releaseDir 'YangCADAgent.bundle') -Recurse -Force
Copy-Item $setupExe (Join-Path $releaseDir 'YangCADAgent_Setup.exe') -Force
Copy-Item "$PSScriptRoot\Install-Release.ps1" (Join-Path $releaseDir 'Install-Release.ps1')
Copy-Item "$PSScriptRoot\Verify-Release.ps1" (Join-Path $releaseDir 'Verify-Release.ps1')

$signing=@{mode='unsigned-quick';publisher_thumbprint=$null;publisher_subject=$null;certificate_file=$null;timestamp_url=$null}
$installerMode='quick-trusted-path'
$trustStrategy='user-profile-trustedpaths+appautoload'
if($Mode -eq 'Signed'){
  if(-not $SignThumbprint){throw 'Signed 模式必须传 -SignThumbprint。'}
  $cert=Get-Item ("Cert:\CurrentUser\My\"+$SignThumbprint) -ErrorAction Stop
  if(-not $cert.HasPrivateKey){throw "签名证书没有私钥：$SignThumbprint"}
  $cerPath=Join-Path $releaseDir 'YangCADAgent_Publisher.cer'
  Export-Certificate -Cert $cert -FilePath $cerPath -Force | Out-Null
  & "$PSScriptRoot\Sign-Release.ps1" -Path $releaseDir -Thumbprint $SignThumbprint -TimestampUrl $TimestampUrl
  $signing=@{mode='signed';publisher_thumbprint=$cert.Thumbprint;publisher_subject=$cert.Subject;certificate_file='YangCADAgent_Publisher.cer';timestamp_url=$TimestampUrl}
  $installerMode='signed-user'
  $trustStrategy='authenticode-currentuser-publisher'
}elseif($SignThumbprint){
  throw 'Quick 模式不需要 -SignThumbprint；稳定发布时改用 -Mode Signed。'
}

$files=@()
Get-ChildItem $releaseDir -Recurse -File | Where-Object {$_.Name -ne 'release-manifest.json'} | ForEach-Object {
  $rel=$_.FullName.Substring($releaseDir.Length+1).Replace('\\','/')
  $sig=$null
  if($_.Extension -ieq '.dll' -or $_.Extension -ieq '.exe'){
    $as=Get-AuthenticodeSignature $_.FullName
    $sig=@{status=[string]$as.Status;thumbprint=if($as.SignerCertificate){$as.SignerCertificate.Thumbprint}else{$null};subject=if($as.SignerCertificate){$as.SignerCertificate.Subject}else{$null}}
  }
  $files+=@{path=$rel;sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash;size=$_.Length;signature=$sig}
}
$manifest=@{
  schema=3
  product='YangCAD Agent'
  version='0.9.0'
  years=$years
  contract=@{stdio=68;plain=31;civil=61}
  installer=@{
    file='YangCADAgent_Setup.exe';scope_default='User';requires_admin=$false;ui_language='zh-CN';
    mode=$installerMode;trust_strategy=$trustStrategy;secureload_change=$false
  }
  signing=$signing
  generated_at=(Get-Date).ToString('o')
  files=$files
}
$manifest | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $releaseDir 'release-manifest.json') -Encoding UTF8
& (Join-Path $releaseDir 'Verify-Release.ps1') -PackageRoot $releaseDir

$zip=Join-Path $root 'artifacts\release\YangCADAgent_0.9.0_release.zip'
if(Test-Path $zip){Remove-Item $zip -Force}
Compress-Archive -Path (Join-Path $releaseDir '*') -DestinationPath $zip -CompressionLevel Optimal
$sha=(Get-FileHash $zip -Algorithm SHA256).Hash
Write-Host "Release package: $zip"
Write-Host "SHA256: $sha"
Write-Host "Mode: $Mode"
Write-Host "User entry point: YangCADAgent_Setup.exe"
if($Mode -eq 'Quick'){
  Write-Host 'Trust: YangCAD bundle only via CurrentUser AutoCAD Profiles/TRUSTEDPATHS; SECURELOAD unchanged.'
}
