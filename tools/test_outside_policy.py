"""YGOUTSIDE V1 source policy, not Windows/CAD runtime evidence."""
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
def read(path): return (ROOT / path).read_text(encoding="utf-8-sig")
service = read("src/YangCAD.AutoCAD/Services/Cad/OutsideBoundaryService.cs")
command = read("src/YangCAD.AutoCAD/Commands/OutsideBoundaryCommands.cs")
geometry = read("src/YangCAD.AutoCAD/Services/Cad/PolygonBoundary2d.cs")
palette = read("src/YangCAD.AutoCAD/UI/ToolboxView.xaml")
palette_code = read("src/YangCAD.AutoCAD/UI/ToolboxView.xaml.cs")
aliases = read("src/YangCAD.AutoCAD/Services/Cad/ShortcutAliasService.cs")
docs = read("docs/OUTSIDE_V1_20261105.md")
task = read("tasks/OUTSIDE_WINDOWS_SMOKE_20261105.md")
checks = []
def check(name, ok): checks.append((name, bool(ok)))

ET.fromstring(palette)
check("native YGOUTSIDE registered", '[CommandMethod("YGOUTSIDE", CommandFlags.Modal)]' in command)
check("three boundary modes", all(x in command for x in
      ['Keywords.Add("Rectangle")', 'Keywords.Add("Select")', 'Keywords.Add("Draw")']))
check("select current-space closed polyline", 'poly.Closed' in command and
      'poly.OwnerId != ctx.Database.CurrentSpaceId' in command)
check("reject bulge source boundary", "poly.GetBulgeAt(i)" in command and "不作直线化近似" in command)
check("source boundary protected", 'id == request.BoundaryId' in service)
check("preview truly read-only", 'CadTransactionRunner.ReadContext(doc' in command and
      'OutsideBoundaryService.Preview(ctx, request)' in command)
check("confirm defaults NO", 'confirm.Keywords.Default = "No"' in command)
check("write only after explicit Yes", command.index('answer.StringResult, "Yes"') <
      command.index('CadTransactionRunner.WriteContext(doc, false'))
check("pre-execution fingerprint rechecked", 'plan.Summary.fingerprint, previewFingerprint' in service)
check("shared concave polygon geometry", "new PolygonBoundary2d(request.Vertices)" in service and
      "inside = !inside" in geometry)
check("bbox complete-disjoint proof", "DefinitelyOutside" in service and
      "EdgesTouch(a, b, corners[k]" in service and "corners.Any(boundary.Contains)" in service)
check("line clipping returns true fragments", "boundary.ClipLine(" in service)
check("arc/circle analytic splitting", "boundary.ClipArc(" in service)
check("polyline explode for curve segments", "poly.Explode(exploded)" in service)
check("unsupported polyline segment rejects", "!(obj is Line) && !(obj is Arc" in service)
check("protected xrefs and attributes", all(x in service for x in
      ["def.IsFromExternalReference", "def.IsFromOverlayReference",
       "block.AttributeCollection.Count > 0", "block.IsDynamicBlock"]))
check("protected layouts and dependencies", all(x in service for x in
      ["source is Viewport", "source is RasterImage", "source is UnderlayReference",
       "hatch.Associative"]))
check("locked frozen hidden layers protected", all(x in service for x in
      ["layer.IsLocked", "layer.IsFrozen", "layer.IsOff"]))
check("single caller-owned transaction", "tr.AddNewlyCreatedDBObject(part, true)" in service and
      "entity.Erase()" in service and "CadTransactionRunner.WriteContext(doc, false" in command)
check("dispose rejected or preview fragments", "if (!transferred)" in service and
      "if (item.ObjectId.IsNull) item.Dispose()" in service)
check("bounded scan and changes", "MaxObjects = 100000" in service and
      "MaxChanges = 10000" in service and "MaxNewParts = 20000" in service)
check("Chinese toolbox entry and whitelist", 'Tag="YGOUTSIDE"' in palette and
      '"YGOUTSIDE"' in palette_code)
check("PGP alias supported", 'new Feature("删除边界外", "YGOUTSIDE")' in aliases)
check("no false undo/runtime PASS", "WINDOWS_NOT_TESTED" in docs and
      "native_undo_must_be_tested" in service)
check("Windows task contains real DWG and undo", all(x in task for x in
      ["before", "after", "_.U", "Build", "Loaded", "bulge", "凹多边形"]))
for label, passed in checks: print(("PASS" if passed else "FAIL") + ": " + label)
print(f"YGOUTSIDE source policy: {sum(ok for _, ok in checks)}/{len(checks)} PASS, STATIC ONLY")
if not all(ok for _, ok in checks): raise SystemExit(1)
