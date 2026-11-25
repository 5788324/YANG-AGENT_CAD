"""Polygon YGDETAIL source-contract gate. STATIC ONLY; never proves Autodesk execution."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
def read(name): return (ROOT / name).read_text(encoding="utf-8-sig")

boundary = read("src/YangCAD.AutoCAD/Services/Cad/PolygonBoundary2d.cs")
service = read("src/YangCAD.AutoCAD/Services/Cad/PolygonDetailExtractService.cs")
cmd = read("src/YangCAD.AutoCAD/Commands/PolygonDetailCommands.cs")
legacy = read("src/YangCAD.AutoCAD/Services/Cad/DetailExtractService.cs")
task = read("tasks/DETAIL_POLYGON_WINDOWS_SMOKE_20261105.md")
checks = []
def check(title, value): checks.append((title, bool(value)))

check("legacy rectangle command remains",
      "DetailExtractService.Extract(ctx, request, false)" in read("src/YangCAD.AutoCAD/Commands/DetailExtractCommands.cs"))
check("legacy validated rectangle service remains", "TrimLine(" in legacy and "TrimArc(" in legacy)
check("dedicated polygon command with select and draw", all(x in cmd for x in ['CommandMethod("YGDETAILPOLY"', 'Keywords.Add("Select")', 'Keywords.Add("Draw")']))
check("WCS XY boundary required", "poly.Normal.GetNormal().DotProduct(Vector3d.ZAxis)" in cmd)
check("no silent bulge polygon flattening", "poly.GetBulgeAt(i)" in cmd and "bulges.Add(poly.GetBulgeAt(i))" in cmd and "boundary.CreatePolyline" in service)
check("no thick boundary acceptance", "poly.HasWidth" in cmd)
check("source polygon owner checks", "poly.OwnerId != ctx.Database.CurrentSpaceId" in cmd)
check("existing boundary id excluded", "id == request.BoundaryId" in service)
check("drawn polygon no source mutation", "DrawBoundary(Editor ed)" in cmd and "new Polyline" not in cmd)
check("simple polygon vertex/count validation", "vertices.Count < 2 || vertices.Count > 512" in boundary)
check("reject zero-length and zero-area", "DistSquared(a, b)" in boundary and "signedArea" in boundary)
check("reject non-adjacent self-intersections", "SegmentsTouch(a, b, c, d)" in boundary)
check("concave ray-casting classification", "inside = !inside" in boundary)
check("complex entity conservative containment", "ContainsBox(" in boundary and "SideTouches(i, corners[k]" in boundary)
check("line exact parameter intersections", "Cross(qx, qy, sx, sy) / den" in boundary and "ClipLine(" in boundary)
check("circle-segment analytical roots", "discriminant = B * B - 4 * A * C" in boundary)
check("arc/circle split by angles", "ClipArc(" in boundary and "new Arc(" in service)
check("source polyline exact explode path", "poly.Explode(exploded)" in service)
check("atomic refusal for unknown polyline segment", "throw new NotSupportedException" in service)
check("xref, dynamic blocks and associative hatch protected; attributed blocks deep cloned", all(x in service for x in
    ["def.IsFromExternalReference", "block.IsDynamicBlock", "ctx.Database.DeepCloneObjects", "hatch.Associative"]))
check("guard frozen and locked layers", "layer.IsLocked || layer.IsFrozen || layer.IsOff" in service)
check("no erase of any source", ".Erase()" not in service and ".Erase()" not in cmd)
check("copy in caller transaction", "CadTransactionRunner.WriteContext(doc, false" in cmd and
      "tr.AddNewlyCreatedDBObject(item, true)" in service)
check("one-transaction semantics not marked proven", "single_transaction_expected" in service and
      "WINDOWS_NOT_TESTED" in read("docs/DETAIL_POLYGON_V1_20261105.md"))
check("bounded scanning and results", "ids.Length > 100000" in service and
      "parts.Count + attributedBlocks.Count + newParts.Count > 20000" in service)
check("Windows instruction includes undo, concave and arc cases", all(x in task for x in
      ["_.U", "凹多边形", "LINE", "ARC", "CIRCLE", "before", "after", "bulge"]))
for title, ok in checks:
    print(("PASS" if ok else "FAIL") + ": " + title)
print(f"polygon detail static source policy: {sum(ok for _,ok in checks)}/{len(checks)} PASS")
if not all(ok for _,ok in checks): raise SystemExit(1)
