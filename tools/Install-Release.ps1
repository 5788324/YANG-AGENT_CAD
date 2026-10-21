param(
  [ValidateSet('Install','Update','Repair','Uninstall','Status')][string]$Action='Install',
  [string]$PackageRoot='',
  [ValidateSet('User','Machine')][string]$Scope='User',
  [switch]$PurgeUserData
)
$ErrorActionPreference='Stop'
$utf8=New-Object System.Text.UTF8Encoding($false);[Console]::OutputEncoding=$utf8;$OutputEncoding=$utf8

$scriptRoot=Split-Path -Parent $MyInvocation.MyCommand.Path
if(-not $PackageRoot){$PackageRoot=$scriptRoot}
$manifestPath=Join-Path $PackageRoot 'release-manifest.json'
if(-not(Test-Path $manifestPath)){throw "缺少 release-manifest.json：$PackageRoot"}
$manifest=Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$mode=[string]$manifest.installer.mode
if($mode -eq 'quick-trusted-path' -and $Scope -ne 'User'){throw 'Quick 模式只支持 User-scope；Machine-scope 留给后续 Signed 正式发布。'}
$base=if($Scope -eq 'Machine'){Join-Path $env:ProgramFiles 'Autodesk\ApplicationPlugins'}else{Join-Path $env:APPDATA 'Autodesk\ApplicationPlugins'}
$dst=Join-Path $base 'YangCADAgent.bundle'
$bundle=Join-Path $PackageRoot 'YangCADAgent.bundle'
$cer=Join-Path $PackageRoot 'YangCADAgent_Publisher.cer'
$trustedEntry=$dst.TrimEnd('\')+'\...'
$releaseKeys=@('R24.0','R24.1','R24.2','R24.3','R25.0','R25.1','R26.0')

function Assert-HostStopped{
  $running=@(Get-Process -Name acad,AcCoreConsole -ErrorAction SilentlyContinue)
  if($running){throw "检测到 acad.exe/AcCoreConsole.exe 正在运行。安装/更新/修复/卸载前请关闭 Autodesk 宿主。PID: $($running.Id -join ',')"}
}
function Bundle-Version([string]$path){
  $pc=Join-Path $path 'PackageContents.xml'; if(-not(Test-Path $pc)){return $null}; [xml]$x=Get-Content $pc -Raw -Encoding UTF8; return [string]$x.ApplicationPackage.AppVersion
}
function Assert-BundleCopyMatches([string]$source,[string]$installed){
  $srcFiles=Get-ChildItem $source -Recurse -File
  $dstFiles=Get-ChildItem $installed -Recurse -File
  if($srcFiles.Count -ne $dstFiles.Count){throw '安装后文件数量不一致。'}
  foreach($sf in $srcFiles){
    $rel=$sf.FullName.Substring($source.Length+1)
    $df=Join-Path $installed $rel
    if(-not(Test-Path -LiteralPath $df)){throw "安装后缺少文件：$rel"}
    if((Get-FileHash $sf.FullName -Algorithm SHA256).Hash -ne (Get-FileHash $df -Algorithm SHA256).Hash){throw "安装后 SHA 不一致：$rel"}
  }
}
function Normalize-Trusted([string]$value){
  if($null -eq $value){return ''}
  return $value.Trim().Trim('"').Replace('/','\').TrimEnd('\').ToUpperInvariant()
}
function Split-Trusted([string]$value){
  if([string]::IsNullOrWhiteSpace($value)){return @()}
  return @($value -split ';' | ForEach-Object {$_.Trim()} | Where-Object {$_})
}
function Ensure-QuickProfileTrust{
  $root='HKCU:\Software\Autodesk\AutoCAD'
  if(-not(Test-Path $root)){throw '未发现当前用户 AutoCAD 配置。请至少启动一次 AutoCAD/Civil 3D 后再执行 Repair。'}
  $found=0;$trustedChanged=0;$autoloadChanged=0
  foreach($release in Get-ChildItem $root -ErrorAction SilentlyContinue | Where-Object {$releaseKeys -contains $_.PSChildName}){
    foreach($product in Get-ChildItem $release.PSPath -ErrorAction SilentlyContinue | Where-Object {$_.PSChildName -like 'ACAD-*'}){
      $profiles=Join-Path $product.PSPath 'Profiles'
      if(-not(Test-Path $profiles)){continue}
      foreach($profile in Get-ChildItem $profiles -ErrorAction SilentlyContinue){
        $variables=Join-Path $profile.PSPath 'Variables'
        if(-not(Test-Path $variables)){New-Item -Path $variables -Force | Out-Null}
        $found++
        $props=Get-ItemProperty -LiteralPath $variables -ErrorAction SilentlyContinue
        $raw=[string]$props.TRUSTEDPATHS
        $expected=Normalize-Trusted $trustedEntry
        $parts=@(Split-Trusted $raw)
        $has=$false
        foreach($p in $parts){if((Normalize-Trusted $p) -eq $expected){$has=$true;break}}
        if(-not $has){
          $parts += ('"'+$trustedEntry+'"')
          New-ItemProperty -LiteralPath $variables -Name TRUSTEDPATHS -PropertyType ExpandString -Value ($parts -join ';') -Force | Out-Null
          $trustedChanged++
        }
        $current=14
        if($null -ne $props.APPAUTOLOAD){$parsed=0;if([int]::TryParse([string]$props.APPAUTOLOAD,[ref]$parsed)){$current=$parsed}}
        $next=$current -bor 2 -bor 8
        if($next -ne $current -or $null -eq $props.APPAUTOLOAD){
          New-ItemProperty -LiteralPath $variables -Name APPAUTOLOAD -PropertyType DWord -Value $next -Force | Out-Null
          $autoloadChanged++
        }
      }
    }
  }
  if($found -eq 0){throw '未发现 AutoCAD 2021–2027 当前用户 Profile。请至少启动一次目标产品后再执行 Repair。'}
  Write-Host "Quick trust PASS: profiles=$found, trusted_changed=$trustedChanged, appautoload_changed=$autoloadChanged"
  Write-Host "Trusted entry: $trustedEntry"
  Write-Host 'SECURELOAD unchanged; other TRUSTEDPATHS preserved.'
}
function Remove-QuickProfileTrust{
  $root='HKCU:\Software\Autodesk\AutoCAD'; if(-not(Test-Path $root)){return}
  $expected=Normalize-Trusted $trustedEntry;$changed=0
  foreach($release in Get-ChildItem $root -ErrorAction SilentlyContinue | Where-Object {$releaseKeys -contains $_.PSChildName}){
    foreach($product in Get-ChildItem $release.PSPath -ErrorAction SilentlyContinue | Where-Object {$_.PSChildName -like 'ACAD-*'}){
      $profiles=Join-Path $product.PSPath 'Profiles';if(-not(Test-Path $profiles)){continue}
      foreach($profile in Get-ChildItem $profiles -ErrorAction SilentlyContinue){
        $variables=Join-Path $profile.PSPath 'Variables';if(-not(Test-Path $variables)){continue}
        $props=Get-ItemProperty -LiteralPath $variables -ErrorAction SilentlyContinue
        $raw=[string]$props.TRUSTEDPATHS
        $parts=@(Split-Trusted $raw)
        $kept=@($parts | Where-Object {(Normalize-Trusted $_) -ne $expected})
        if($kept.Count -ne $parts.Count){
          New-ItemProperty -LiteralPath $variables -Name TRUSTEDPATHS -PropertyType ExpandString -Value ($kept -join ';') -Force | Out-Null
          $changed++
        }
      }
    }
  }
  Write-Host "Removed YangCAD TRUSTEDPATHS from profiles=$changed; APPAUTOLOAD left enabled; other paths preserved."
}
function Ensure-CurrentUserPublisherTrust{
  if($mode -ne 'signed-user'){return}
  if(-not(Test-Path $cer)){throw "缺少公开发布者证书：$cer"}
  $cert=New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($cer)
  foreach($name in @('TrustedPublisher','Root')){
    if($name -eq 'Root' -and $cert.Subject -ne $cert.Issuer){continue}
    $store=New-Object System.Security.Cryptography.X509Certificates.X509Store($name,'CurrentUser')
    $store.Open('ReadWrite');try{if($store.Certificates.Find('FindByThumbprint',$cert.Thumbprint,$false).Count -eq 0){$store.Add($cert)}}finally{$store.Close()}
  }
}

if($Action -eq 'Status'){
  Write-Host "Mode: $mode"
  if(Test-Path $dst){Write-Host "Installed: $dst";Write-Host "Version: $(Bundle-Version $dst)"}else{Write-Host 'YangCADAgent.bundle not installed.'}
  Write-Host "Quick trusted entry: $trustedEntry"
  exit 0
}
Assert-HostStopped
if($Action -eq 'Uninstall'){
  if(Test-Path $dst){$trash=$dst+'.__remove.'+[Guid]::NewGuid().ToString('N');Move-Item $dst $trash;Remove-Item $trash -Recurse -Force;Write-Host "Uninstalled: $dst"}else{Write-Host 'Nothing to uninstall.'}
  if($mode -eq 'quick-trusted-path'){Remove-QuickProfileTrust}
  if($PurgeUserData){$data=Join-Path $env:LOCALAPPDATA 'YangCADAgent';if(Test-Path $data){Remove-Item $data -Recurse -Force;Write-Warning "Purged user data: $data"}}
  exit 0
}

if(-not(Test-Path $bundle)){throw "PackageRoot 缺少 YangCADAgent.bundle：$PackageRoot"}
& (Join-Path $PackageRoot 'Verify-Release.ps1') -PackageRoot $PackageRoot
Ensure-CurrentUserPublisherTrust
$sourceVersion=[Version](Bundle-Version $bundle)
$installedVersion=$null
if(Test-Path $dst){$iv=Bundle-Version $dst;if($iv){$installedVersion=[Version]$iv}}
if($Action -eq 'Install' -and $installedVersion){throw "已安装 $installedVersion；请使用 Update 或 Repair。"}
if($Action -eq 'Update'){
  if(-not $installedVersion){throw '当前未安装，不能 Update；使用 Install。'}
  if($sourceVersion -le $installedVersion){throw "Update 要求 source version > installed version；source=$sourceVersion installed=$installedVersion"}
}
if($Action -eq 'Repair'){
  if(-not $installedVersion){throw '当前未安装，不能 Repair；使用 Install。'}
  if($sourceVersion -ne $installedVersion){throw "Repair 要求 source version == installed version；source=$sourceVersion installed=$installedVersion"}
}

New-Item -ItemType Directory -Force -Path $base|Out-Null
$stage=Join-Path $base ('YangCADAgent.bundle.__stage.'+[Guid]::NewGuid().ToString('N'))
$backup=$null;$installedNew=$false
try{
  Copy-Item $bundle $stage -Recurse -Force
  Get-ChildItem $stage -Recurse -File | ForEach-Object {try{Unblock-File -LiteralPath $_.FullName -ErrorAction Stop}catch{}}
  if(Test-Path $dst){$backup=Join-Path $base ('YangCADAgent.bundle.__backup.'+(Get-Date -Format 'yyyyMMddHHmmss'));Move-Item $dst $backup}
  Move-Item $stage $dst;$installedNew=$true
  if((Bundle-Version $dst) -ne $sourceVersion.ToString()){throw '安装后版本校验失败。'}
  Assert-BundleCopyMatches $bundle $dst
  if($mode -eq 'quick-trusted-path'){Ensure-QuickProfileTrust}
  if($backup -and (Test-Path $backup)){Remove-Item $backup -Recurse -Force}
  Write-Host "$Action PASS: $dst (version $sourceVersion, scope $Scope, mode $mode)"
}catch{
  if(Test-Path $stage){Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue}
  if((Test-Path $dst) -and $backup -and (Test-Path $backup)){Remove-Item $dst -Recurse -Force -ErrorAction SilentlyContinue;Move-Item $backup $dst}
  elseif($backup -and (Test-Path $backup) -and -not(Test-Path $dst)){Move-Item $backup $dst}
  elseif($installedNew -and (Test-Path $dst)){Remove-Item $dst -Recurse -Force -ErrorAction SilentlyContinue}
  throw
}
