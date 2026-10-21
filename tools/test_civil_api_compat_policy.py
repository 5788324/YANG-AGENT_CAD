from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
prod = (ROOT / "src/YangCAD.Civil3D/Tools/CivilProductionReadAssistTools.cs").read_text(encoding="utf-8-sig")
read = (ROOT / "src/YangCAD.Civil3D/Tools/CivilReadDesignTools.cs").read_text(encoding="utf-8-sig")
matrix = (ROOT / "tools/Test-CompatibilityMatrix.ps1").read_text(encoding="utf-8-sig")
checks = {
    "reference helper present": "ReferenceSourceExists(Autodesk.Civil.DatabaseServices.Entity entity)" in prod,
    "no direct source-exists property access": "featureLine.IsReferencedSourceExisting" not in prod and "corridor.IsReferencedSourceExisting" not in prod,
    "unknown source existence is explicit": "reference_source_existence_unavailable" in prod and "return null" in prod,
    "pvi raw station helper present": "ProfilePviRawStation(ProfilePVI pvi)" in read,
    "raw station reflection preferred": 'new[] { "RawStation", "Station" }' in read,
    "no direct RawStation compile dependency": "pvi.RawStation" not in read,
    "matrix has component build fields": all(name in matrix for name in ("autocad_build", "batch_build", "civil_build", "system_text_json")),
    "matrix schema v2": "schema=2" in matrix,
}
failed = [name for name, ok in checks.items() if not ok]
if failed:
    raise SystemExit("civil api compatibility regression failed: " + ", ".join(failed))
print(f"civil api compatibility regression: {len(checks)}/{len(checks)} PASS")
