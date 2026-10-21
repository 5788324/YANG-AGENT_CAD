from pathlib import Path
import sys
ROOT=Path(__file__).resolve().parents[1]
errors=[]
def text(rel): return (ROOT/rel).read_text(encoding='utf-8-sig')
checks={
 'Directory.Build.props':'<Version>0.9.0</Version>',
 'mcp/pyproject.toml':'version = "0.9.0"',
 'mcp/yangcad_mcp/__init__.py':'__version__ = "0.9.0"',
 'bundle/PackageContents.template.xml':'AppVersion="0.9.0"',
}
for rel,needle in checks.items():
    if needle not in text(rel): errors.append(f'version mismatch: {rel}')

for rel in [
 'src/YangCAD.Setup/YangCAD.Setup.csproj','src/YangCAD.Setup/app.manifest',
 'src/YangCAD.Setup/Program.cs','src/YangCAD.Setup/MainForm.cs',
 'src/YangCAD.Setup/InstallerService.cs','src/YangCAD.Setup/ReleaseManifest.cs','src/YangCAD.Setup/WinTrust.cs',
 'tools/Build-Setup.ps1','tools/Package-Release.ps1','tools/Install-Release.ps1',
 'tools/Verify-Release.ps1','tools/Sign-Release.ps1','tools/New-LocalCodeSigningCertificate.ps1',
 'tools/Test-CompatibilityMatrix.ps1'
]:
    if not (ROOT/rel).is_file(): errors.append(f'missing release/setup file: {rel}')

pb=text('tools/Package-Bundle.ps1')
for forbidden in ['LoadOnAutoCADStartup=`"True`"','AppType=`"ManagedPlugin`"']:
    if forbidden in pb: errors.append(f'unsupported bundle attribute retained: {forbidden}')
for y,s in {2021:'R24.0',2022:'R24.1',2023:'R24.2',2024:'R24.3',2025:'R25.0',2026:'R25.1',2027:'R26.0'}.items():
    if f'{y}="{s}"' not in pb: errors.append(f'missing series mapping {y}->{s}')

vs=text('STATIC_VALIDATION.txt') if (ROOT/'STATIC_VALIDATION.txt').exists() else ''
if 'tools=68' not in vs: errors.append('current product contract tools=68 missing')

for rel in ['src/YangCAD.AutoCAD/YangCAD.AutoCAD.csproj','src/YangCAD.Civil3D/YangCAD.Civil3D.csproj','src/YangCAD.BatchWorker/YangCAD.BatchWorker.csproj']:
    s=text(rel)
    for needle in ['2021','2024','net48','2025','2026','net8.0-windows','2027','net10.0-windows']:
        if needle not in s: errors.append(f'framework matrix missing {needle}: {rel}')

setup_proj=text('src/YangCAD.Setup/YangCAD.Setup.csproj')
setup_manifest=text('src/YangCAD.Setup/app.manifest')
setup_service=text('src/YangCAD.Setup/InstallerService.cs')
setup_ui=text('src/YangCAD.Setup/MainForm.cs')
for needle in ['<TargetFramework>net48</TargetFramework>','<OutputType>WinExe</OutputType>','<UseWindowsForms>true</UseWindowsForms>']:
    if needle not in setup_proj: errors.append(f'setup project invariant missing: {needle}')
if 'requestedExecutionLevel level="asInvoker"' not in setup_manifest:
    errors.append('setup must be asInvoker/no-admin by default')
for needle in [
    'Environment.SpecialFolder.ApplicationData','Autodesk", "ApplicationPlugins"',
    'AutoCadUserRoot','TRUSTEDPATHS','APPAUTOLOAD','AppAutoLoadStartupBit','AppAutoLoadDetectedBit',
    'Registry.CurrentUser','R24.3','R26.0','quick-trusted-path','unsigned-quick',
    '.__stage.','.__backup.','VerifyDirectoryMatches','release-manifest.json',
    'SECURELOAD 未修改','RemoveYangCadTrustedPath'
]:
    if needle not in setup_service: errors.append(f'quick setup service invariant missing: {needle}')
for needle in ['"安装"','"更新"','"修复"','"卸载"','彻底卸载','TRUSTEDPATHS','ApplicationPlugins']:
    if needle not in setup_ui: errors.append(f'setup UI invariant missing: {needle}')
for forbidden in ['SECURELOAD=0','SECURELOAD", 0','requestedExecutionLevel level="requireAdministrator"','Verb = "runas"']:
    if forbidden in setup_service + setup_ui + setup_manifest:
        errors.append(f'setup must not weaken security/elevate: {forbidden}')

package=text('tools/Package-Release.ps1')
for needle in [
    'Build-Setup.ps1','YangCADAgent_Setup.exe',"[ValidateSet('Quick','Signed')]", "[string]$Mode='Quick'",
    "mode='unsigned-quick'","installerMode='quick-trusted-path'", "trustStrategy='user-profile-trustedpaths+appautoload'",
    'secureload_change=$false','scope_default=\'User\'',"requires_admin=$false"
]:
    if needle not in package: errors.append(f'package quick invariant missing: {needle}')
# Signed route must remain available for later stable releases.
for needle in ['Sign-Release.ps1','Export-Certificate','YangCADAgent_Publisher.cer',"$Mode -eq 'Signed'"]:
    if needle not in package: errors.append(f'future signed route missing: {needle}')

verify=text('tools/Verify-Release.ps1')
for needle in ['quick-trusted-path','unsigned-quick','secureload_change','user-profile-trustedpaths+appautoload','signed-user']:
    if needle not in verify: errors.append(f'release verifier quick invariant missing: {needle}')
installer=text('tools/Install-Release.ps1')
for needle in ['Get-Process -Name acad,AcCoreConsole','TRUSTEDPATHS','APPAUTOLOAD','quick-trusted-path','Remove-QuickProfileTrust','.__stage.','.__backup.','Assert-BundleCopyMatches']:
    if needle not in installer: errors.append(f'fallback installer quick invariant missing: {needle}')
for forbidden in ['SECURELOAD=0','SetSystemVariable("SECURELOAD"','-AllowUnsigned']:
    if forbidden in installer + package:
        errors.append(f'quick release must not contain legacy security bypass: {forbidden}')

signer=text('tools/Sign-Release.ps1')
for needle in ["'/fd','SHA256'","'/td','SHA256'",'Cert:\\CurrentUser\\My',".Extension -ieq '.exe'"]:
    if needle not in signer: errors.append(f'signing invariant missing: {needle}')

civil_pos=pb.find('YangCAD.Civil3D.$year')
auto_pos=pb.find('YangCAD.AutoCAD.$year')
if civil_pos < 0 or auto_pos < 0 or civil_pos > auto_pos:
    errors.append('bundle component order must be Civil above AutoCAD dependency')

print(f'release source checks: errors={len(errors)}')
for e in errors: print('ERROR:',e)
sys.exit(1 if errors else 0)
