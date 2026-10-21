param(
  [Parameter(Mandatory=$true)][string]$PackageRoot
)
$ErrorActionPreference="Stop"
$utf8=New-Object System.Text.UTF8Encoding($false);[Console]::OutputEncoding=$utf8;$OutputEncoding=$utf8

$manifestPath=Join-Path $PackageRoot 'release-manifest.json'
$bundle=Join-Path $PackageRoot 'YangCADAgent.bundle'
$setup=Join-Path $PackageRoot 'YangCADAgent_Setup.exe'
if(-not(Test-Path $manifestPath)){throw "缺少 release-manifest.json"}
if(-not(Test-Path(Join-Path $bundle 'PackageContents.xml'))){throw "缺少 YangCADAgent.bundle/PackageContents.xml"}
if(-not(Test-Path $setup)){throw "缺少 YangCADAgent_Setup.exe"}
$requiredRuntime=@(
  'Contents\Mcp\Start-YangCAD-Mcp.ps1',
  'Contents\Mcp\pyproject.toml',
  'Contents\Mcp\yangcad_mcp\__init__.py',
  'Contents\Mcp\yangcad_mcp\batch.py',
  'Contents\Mcp\yangcad_mcp\extensions.py',
  'Contents\Mcp\yangcad_mcp\gateway.py',
  'Contents\Mcp\yangcad_mcp\server.py',
  'Contents\ImageEmbed\Start-YangCAD-ImageEmbed.ps1'
)
foreach($relative in $requiredRuntime){
  if(-not(Test-Path (Join-Path $bundle $relative))){throw "发布 Bundle 缺少 runtime：$relative"}
}
$m=Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
foreach($item in $m.files){
  $p=Join-Path $PackageRoot $item.path
  if(-not(Test-Path -LiteralPath $p)){throw "release manifest 缺少文件：$($item.path)"}
  $sha=(Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash
  if($sha -ne $item.sha256){throw "SHA mismatch: $($item.path)"}
  if($item.size -and (Get-Item -LiteralPath $p).Length -ne [int64]$item.size){throw "Size mismatch: $($item.path)"}
}
[xml]$xml=Get-Content (Join-Path $bundle 'PackageContents.xml') -Raw -Encoding UTF8
if($xml.ApplicationPackage.AppVersion -ne $m.version){throw "PackageContents AppVersion 与 release manifest 不一致。"}
$entries=@($xml.ApplicationPackage.Components.ComponentEntry)
if(-not $entries){throw "PackageContents 没有 ComponentEntry。"}
foreach($e in $entries){
  if($e.HasAttribute('LoadOnAutoCADStartup')){throw "发现非官方 ComponentEntry 属性 LoadOnAutoCADStartup；Release 禁止。"}
  if($e.HasAttribute('AppType') -and $e.AppType -eq 'ManagedPlugin'){throw "发现非官方 AppType=ManagedPlugin；Release 禁止。"}
}

$mode=[string]$m.installer.mode
if($mode -eq 'quick-trusted-path'){
  if([string]$m.signing.mode -ne 'unsigned-quick'){throw 'Quick 模式 signing.mode 必须是 unsigned-quick。'}
  if($m.installer.requires_admin){throw 'Quick 模式禁止 require admin。'}
  if($m.installer.secureload_change){throw 'Quick 模式禁止修改 SECURELOAD。'}
  if([string]$m.installer.trust_strategy -ne 'user-profile-trustedpaths+appautoload'){throw 'Quick 模式 trust_strategy 不匹配。'}
}elseif($mode -eq 'signed-user'){
  if(-not $m.signing -or $m.signing.mode -ne 'signed'){throw 'Signed 模式 release manifest 未声明 signed。'}
  if([string]$m.signing.certificate_file -ne 'YangCADAgent_Publisher.cer'){throw 'Signed 模式 certificate_file 必须是 YangCADAgent_Publisher.cer。'}
  $cer=Join-Path $PackageRoot 'YangCADAgent_Publisher.cer'
  if(-not(Test-Path $cer)){throw "缺少公开发布者证书：$cer"}
  $peFiles=Get-ChildItem $PackageRoot -Recurse -File | Where-Object { $_.Extension -ieq '.dll' -or $_.Extension -ieq '.exe' }
  foreach($f in $peFiles){
    $sig=Get-AuthenticodeSignature -FilePath $f.FullName
    if($sig.Status -ne 'Valid'){throw "DLL/EXE 签名无效/未受信任：$($f.FullName) [$($sig.Status)]"}
    if(-not $sig.SignerCertificate -or $sig.SignerCertificate.Thumbprint -ne $m.signing.publisher_thumbprint){throw "签名发布者 Thumbprint 不一致：$($f.FullName)"}
  }
}else{
  throw "未知 installer.mode：$mode"
}
Write-Host "Release verify PASS: version=$($m.version), files=$($m.files.Count), mode=$mode"
