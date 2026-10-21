param(
  [Parameter(Mandatory=$true)][string]$Path,
  [Parameter(Mandatory=$true)][string]$Thumbprint,
  [string]$TimestampUrl="",
  [switch]$SkipVerify
)
$ErrorActionPreference="Stop"
$utf8=New-Object System.Text.UTF8Encoding($false);[Console]::OutputEncoding=$utf8;$OutputEncoding=$utf8

function Find-SignTool{
  $roots=@(
    (Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"),
    (Join-Path ${env:ProgramFiles(x86)} "Windows Kits\11\bin")
  ) | Where-Object { $_ -and (Test-Path $_) }
  $candidates=@()
  foreach($r in $roots){$candidates += Get-ChildItem $r -Filter signtool.exe -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' }}
  $tool=$candidates | Sort-Object FullName -Descending | Select-Object -First 1
  if(-not $tool){throw "未找到 Windows SDK SignTool.exe。请安装 Windows SDK Signing Tools。"}
  return $tool.FullName
}

$cert=Get-Item ("Cert:\CurrentUser\My\"+$Thumbprint) -ErrorAction Stop
if(-not $cert.HasPrivateKey){throw "证书没有私钥：$Thumbprint"}
$signTool=Find-SignTool
$files=Get-ChildItem -LiteralPath $Path -Recurse -File | Where-Object { $_.Extension -ieq '.dll' -or $_.Extension -ieq '.exe' }
if(-not $files){throw "未找到待签名 DLL/EXE：$Path"}
foreach($f in $files){
  $args=@('sign','/sha1',$Thumbprint,'/fd','SHA256')
  if($TimestampUrl){$args += @('/tr',$TimestampUrl,'/td','SHA256')}
  $args += $f.FullName
  & $signTool @args
  if($LASTEXITCODE -ne 0){throw "SignTool 签名失败：$($f.FullName)"}
}
if(-not $SkipVerify){
  foreach($f in $files){
    & $signTool verify /pa /all /v $f.FullName | Out-Null
    if($LASTEXITCODE -ne 0){throw "SignTool 验证失败：$($f.FullName)"}
  }
}
Write-Host "Signed DLL/EXE files: $($files.Count)"
Write-Host "Signer: $($cert.Subject) [$Thumbprint]"
