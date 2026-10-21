from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
installer = (ROOT / "src/YangCAD.Setup/InstallerService.cs").read_text(encoding="utf-8-sig")
manifest = (ROOT / "src/YangCAD.Setup/ReleaseManifest.cs").read_text(encoding="utf-8-sig")
package_bundle = (ROOT / "tools/Package-Bundle.ps1").read_text(encoding="utf-8-sig")
package_release = (ROOT / "tools/Package-Release.ps1").read_text(encoding="utf-8-sig")

checks = [
    ("manifest carries target years", "public int[] years { get; set; }" in manifest),
    ("release package writes target years", "years=$years" in package_release),
    ("manifest validates target years", "release-manifest.json years 无效" in installer),
    ("no hardcoded 2021 process exemption", 'IndexOf("2021"' not in installer),
    ("process blocker is target-year aware", "DetectSupportedYear" in installer and "_manifest.years.Contains(knownYear.Value)" in installer),
    ("unknown process year fails closed", "shouldBlock = true; // 进程路径无法识别时 fail-closed。" in installer),
    ("profile writes are target-year scoped", installer.count(".Where(IsTargetReleaseKey)") >= 3),
    ("quick install preserves secureload", "SECURELOAD 未修改" in installer),
    ("Civil platform uses documented value", "Platform='Civil3D'" in package_bundle and "Platform='Civil3D*'" not in package_bundle),
    ("AutoCAD wildcard platform retained", 'Platform=" + $q + "AutoCAD*"' in package_bundle),
]

failed = [name for name, ok in checks if not ok]
if failed:
    for name in failed:
        print("FAIL:", name)
    raise SystemExit(1)

print(f"quick install policy regression: {len(checks)}/{len(checks)} PASS")
