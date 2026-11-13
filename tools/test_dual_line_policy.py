"""YGDL/YGDLW source-only policy: NOT Windows/CAD runtime evidence."""
from pathlib import Path
import xml.etree.ElementTree as ET
root = Path(__file__).resolve().parents[1]
def read(p): return (root / p).read_text(encoding="utf-8-sig")
src = read("src/YangCAD.AutoCAD/Services/Cad/DualLineService.cs")
cmd = read("src/YangCAD.AutoCAD/Commands/DualLineCommands.cs")
user = read("src/YangCAD.AutoCAD/Infrastructure/UserSettings.cs")
ui = read("src/YangCAD.AutoCAD/UI/ToolboxView.xaml")
ui_code = read("src/YangCAD.AutoCAD/UI/ToolboxView.xaml.cs")
aliases = read("src/YangCAD.AutoCAD/Services/Cad/ShortcutAliasService.cs")
doc = read("docs/DUAL_LINE_V1_20261106.md")
task = read("tasks/DUAL_LINE_WINDOWS_SMOKE_20261106.md")
checks = []
def check(name, valid): checks.append((name, bool(valid)))
ET.fromstring(ui)
check("YGDL YGDLW registered", '[CommandMethod("YGDL", CommandFlags.Modal)]' in cmd and '[CommandMethod("YGDLW", CommandFlags.Modal)]' in cmd)
check("first width 1000mm and persist", 'DoubleLineWidthMm { get; set; } = 1000' in user and 'settings.DoubleLineWidthMm = width.Value' in cmd and 'settings.DoubleLineWidthMm = updated.Value' in cmd)
check("persist existing shared settings", "settings.Save();" in cmd and "settings.json" in user)
check("mm conversion", "MillimetersPerDrawingUnit" in src and "db.Insunits" in src and "case 6: return 1000" in src)
check("reject unsupported drawing units", "当前 INSUNITS 不是受支持" in src)
check("WCS Z elevation", "Elevation = first.Value.Z" in cmd and "drawable.Elevation = record.Elevation" in src)
check("tilted UCS explicitly rejected", "ed.CurrentUserCoordinateSystem" in cmd
      and "ucsNormal.GetNormal().DotProduct(Vector3d.ZAxis)" in cmd
      and "当前 UCS 与世界 XY 平面不平行" in cmd)
check("invisible/locked current layer rejected before append",
      "ctx.Database.Clayer" in src and "currentLayer.IsLocked" in src
      and "currentLayer.IsFrozen" in src and "currentLayer.IsOff" in src)
check("refresh after committed draw and edit", cmd.count("ed.Regen();") == 2
      and "CadTransactionRunner.WriteContext(doc, false" in cmd)
check("double pipe centerline", '"Pipe"' in src and '"Double"' in src and 'record.Role = "C"' in src)
check("left center right", all(x in cmd for x in ['"Center", "Left", "Right"', 'Alignment = alignment']) and "choosePlus ? plus : minus" in src)
check("Miter Bevel Round", all(x in src for x in ['corner == "Miter"', 'corner == "Bevel"', 'corner == "Round"', 'Math.Tan(turns[i] * 0.25)']))
check("round inner radius guard", "CheckRadius(path, width)" in src and "内侧弯头半径不足" in src)
check("offset always centered", "GetOffsetCurves" in src and "request.Width * 0.5" in src and "BuildSides(metadata.Center, width, metadata.Elevation)" in src)
check("persist center geometry xdata", all(x in src for x in ["YANGCAD_DUAL_V1", "DxfCode.ExtendedDataRegAppName", "GetXDataForApplication(RegApp)", "record.Center.AddRange(centerPath)"]))
check("stable group identity", "Guid.NewGuid().ToString" in src and "found.ContainsKey(\"L\")" in src and "found.ContainsKey(\"R\")" in src)
check("centerline not erased", "Tag(c, r); // no change to centerline geometry/Handle" in src)
check("center manual edit blocked", "中心线已手动改变" in src)
check("side manual edit blocked", "双线已被手动编辑" in src)
check("duplicate group blocked", "复制后重复的双线组" in src)
check("single transaction", "CadTransactionRunner.WriteContext(doc, false" in cmd and "original.Erase()" in src)
check("old LINE pair midpoint", "MeasureLegacyLinePair" in src and "ChangeLegacyLinePair" in src and "var centerStart = a.StartPoint + offset * .5;" in src and "newWidth * .5" in src)
check("old LINE existing handles unchanged", "a.UpgradeOpen(); b.UpgradeOpen();" in src and "a.StartPoint = centerStart + aNew;" in src)
check("old Polyline not silently paired", "旧图元配对首版仅支持两条 LINE" in src)
check("lock frozen off", "layer.IsLocked" in src and "layer.IsFrozen" in src and "layer.IsOff" in src)
check("path guards", "MaxVertices = 96" in src and "路径自交" in src and "路径不能原路折返" in src)
check("toolbox and native command route", 'Tag="YGDL"' in ui and 'Tag="YGDLW"' in ui and '"YGDL", "YGDLW"' in ui_code)
check("PGP alias supports both", 'new Feature("智能双线/管道", "YGDL")' in aliases and 'new Feature("修正双线宽度", "YGDLW")' in aliases)
check("documents hold 600 to 1000 and native undo test", "600" in doc and "1000" in doc and "_.U" in task and "WINDOWS_NOT_TESTED" in doc)
failed = []
for name, ok in checks:
    print(("PASS" if ok else "FAIL") + ": " + name)
    if not ok: failed.append(name)
print("dual-line static policy: %d/%d PASS (STATIC ONLY)" % (len(checks)-len(failed), len(checks)))
if failed: raise SystemExit(1)
