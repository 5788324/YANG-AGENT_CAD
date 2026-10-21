param(
  [string]$Subject="CN=YangCAD Local Code Signing",
  [int]$ValidYears=5,
  [switch]$TrustCurrentUser
)
$ErrorActionPreference="Stop"
$utf8 = New-Object System.Text.UTF8Encoding($false)
[Console]::OutputEncoding=$utf8;$OutputEncoding=$utf8

$existing=Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert | Where-Object { $_.Subject -eq $Subject } | Sort-Object NotAfter -Descending | Select-Object -First 1
if($existing -and $existing.NotAfter -gt (Get-Date).AddMonths(3)){
  $cert=$existing
}else{
  $cert=New-SelfSignedCertificate -Type CodeSigningCert -Subject $Subject -CertStoreLocation Cert:\CurrentUser\My -HashAlgorithm SHA256 -KeyAlgorithm RSA -KeyLength 3072 -NotAfter (Get-Date).AddYears($ValidYears)
}
if(-not $cert){throw "无法创建/找到代码签名证书。"}

if($TrustCurrentUser){
  $tmp=Join-Path $env:TEMP ("yangcad-signing-"+[Guid]::NewGuid().ToString("N")+".cer")
  try{
    Export-Certificate -Cert $cert -FilePath $tmp -Force | Out-Null
    Import-Certificate -FilePath $tmp -CertStoreLocation Cert:\CurrentUser\Root | Out-Null
    Import-Certificate -FilePath $tmp -CertStoreLocation Cert:\CurrentUser\TrustedPublisher | Out-Null
  }finally{ Remove-Item $tmp -Force -ErrorAction SilentlyContinue }
}
Write-Host "YangCAD code-signing certificate ready."
Write-Host "Thumbprint: $($cert.Thumbprint)"
Write-Host "Subject   : $($cert.Subject)"
Write-Host "Expires   : $($cert.NotAfter.ToString('s'))"
if(-not $TrustCurrentUser){Write-Warning "证书尚未自动加入 CurrentUser Root/TrustedPublisher；User-scope release 需要可信发布者或 AutoCAD trust 配置。"}

Write-Output $cert
