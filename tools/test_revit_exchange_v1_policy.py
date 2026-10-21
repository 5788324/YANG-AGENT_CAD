from pathlib import Path
import json
import re

ROOT = Path(__file__).resolve().parents[1]
SERVICE = (ROOT / "src/YangCAD.AutoCAD/Services/Cad/RevitExchangeService.cs").read_text(encoding="utf-8-sig")
REGISTRY = (ROOT / "src/YangCAD.AutoCAD/Toolbox/ToolboxRegistry.cs").read_text(encoding="utf-8-sig")
DECISIONS = (ROOT / "docs/DECISIONS.md").read_text(encoding="utf-8-sig")
SPEC = (ROOT / "docs/RVT_EXCHANGE_V1.md").read_text(encoding="utf-8-sig") if (ROOT / "docs/RVT_EXCHANGE_V1.md").exists() else ""
FIXTURE_PATH = ROOT / "tools/fixtures/RVT_EXCHANGE_V1_YANGTOOLS_FIXTURE.json"
FIXTURE = json.loads(FIXTURE_PATH.read_text(encoding="utf-8-sig")) if FIXTURE_PATH.exists() else {}

checks = []
def ok(name, cond):
    checks.append((name, bool(cond)))

ok("single gui tool id", 'Id = "project.revit.prepare"' in REGISTRY)
ok("revit tool status is available", 'DeclaredStatus = ToolboxDeclaredStatus.Available' in REGISTRY)
ok("tool is write preview capable", 'Id = "project.revit.prepare"' in REGISTRY and 'IsWrite = true' in REGISTRY and 'SupportsDryRun = true' in REGISTRY)
ok("tool has product MCP coverage", 'SupportsMcp = true' in REGISTRY and 'project.revit.prepare' in (ROOT / 'mcp/yangcad_mcp/server.py').read_text(encoding='utf-8-sig'))
ok("preview and export use runner-owned read context", REGISTRY.count('RevitExchangeService.Preview') == 1 and REGISTRY.count('RevitExchangeService.Export') == 1 and REGISTRY.count('CadTransactionRunner.ReadContext(doc') >= 2)
ok("service does not acquire active document", 'MdiActiveDocument' not in SERVICE and 'DocumentManager' not in SERVICE)
ok("source clone uses Wblock", '.Wblock()' in SERVICE)
ok("detached clone saveas", 'outputDb.SaveAs' in SERVICE and 'sourceDb.SaveAs' not in SERVICE)
ok("output names never overwrite existing triplet", 'GetAvailablePath' in SERVICE and 'File.Exists(stemPath + "_MAP.json")' in SERVICE and 'File.Exists(stemPath + "_REPORT.json")' in SERVICE)
ok("partial output cleanup deletes only files moved by this run", all(x in SERVICE for x in ['movedDwg', 'movedMap', 'movedReport', 'if (movedDwg) DeleteIfExists(paths.Dwg)']))
ok("three exchange outputs", '_MAP.json' in SERVICE and '_REPORT.json' in SERVICE and '.dwg' in SERVICE)
ok("schema is explicit v1", 'yangcad-rvt-exchange/1.0' in SERVICE)
ok("official V1 geometry supported and ellipse/spline fail closed", all(x in SERVICE for x in ['entity is Line', 'entity is Polyline', 'entity is Arc', 'entity is Circle']) and 'entity is Ellipse || entity is Spline' in SERVICE and 'outside yangcad-rvt-exchange/1.0 official V1 type set' in SERVICE)
ok("text supported", 'entity is DBText' in SERVICE and 'entity is MText' in SERVICE)
ok("leader supported", 'entity is MLeader' in SERVICE and 'entity is Leader' in SERVICE)
ok("dimension supported", 'entity is Dimension' in SERVICE and 'DimensionKind' in SERVICE and 'definition_points' in SERVICE)
ok("hatch supported", 'entity is Hatch' in SERVICE and 'PatternName' in SERVICE and 'Gradient' in SERVICE)
ok("hatch boundary semantics exported", 'GetHatchLoops' in SERVICE and 'boundary_loops' in SERVICE and 'BulgeVertex' in SERVICE and 'Curve2d' in SERVICE and 'is_outer = isOuter' in SERVICE and 'is_hole = !isOuter' in SERVICE)
ok("legacy-compatible revit geometry layer naming", '"#" + FormatLineWeight' in SERVICE and 'Red' in SERVICE and 'White' in SERVICE)
ok("text classification includes requested features", all(x in SERVICE for x in ['Bold', 'Italic', 'Underline', 'Superscript', 'Subscript', 'Stacked', 'MixedFormat']))
ok("geometry only normalized to bylayer", 'category == "geometry"' in SERVICE and 'NormalizeGeometry(entity)' in SERVICE and 'LineWeight.ByLayer' in SERVICE)
ok("existing exchange layer is reconciled to generated semantics", 'tr.GetObject(id, OpenMode.ForWrite)' in SERVICE and 'make its properties match that name' in SERVICE and 'layer.IsOff = false' in SERVICE and 'layer.IsFrozen = false' in SERVICE and 'layer.IsLocked = false' in SERVICE and 'layer.IsPlottable = true' in SERVICE)
ok("block/xref boundaries are explicit", 'SkippedAnonymousBlocks' in SERVICE and 'SkippedXrefBlocks' in SERVICE and 'UnresolvedByBlock' in SERVICE and 'DynamicBlockReferences' in SERVICE and 'blockReference.IsDynamicBlock' in SERVICE)
ok("block definition children are diagnostic-only in v1", 'Publishing definition children would place them at definition-space coordinates in YangTools' in SERVICE and 'analysis.SkippedBlockDefinitionEntities++' in SERVICE and '普通块定义仅做诊断，不发布其子实体' in SERVICE)
ok("streamed map avoids whole-file json size coupling", 'using (var writer = new StreamWriter' in SERVICE and 'writer.Write(JsonCodec.Serialize(entity))' in SERVICE)

ok("official entity discriminator is DxfType", 'public string DxfType' in SERVICE and 'DxfType = entity.GetRXClass().DxfName' in SERVICE)
ok("entity carries additive Traits and Data", 'public Dictionary<string, object?> Traits' in SERVICE and 'Traits = entityTraits' in SERVICE and 'Data = data' in SERVICE)
ok("entity category and common traits use canonical exchange names", 'Category = ExchangeCategory(category)' in SERVICE and '["Color"] = appearance.ColorName' in SERVICE and 'traits["LineWeightMm"]' in SERVICE and 'traits["LineType"]' in SERVICE)
ok("text traits expose canonical TextStyle Font Height", 'traits["TextStyle"]' in SERVICE and 'traits["Font"]' in SERVICE and 'traits["Height"]' in SERVICE and 'data["width"] = 0.0' in SERVICE)
ok("2d points are serialized as xyz triples", 'private static object[] Point2(Point2d p) => new object[] { Round(p.X, 8), Round(p.Y, 8), 0.0 }' in SERVICE)
ok("category counts aggregate canonical Curve Text Fill Dimension", 'var exchangeCategory = ExchangeCategory(category)' in SERVICE and 'analysis.CategoryCounts[exchangeCategory]' in SERVICE)
ok("map root exports units and placement origin", 'ExchangeUnits(db.Insunits' in SERVICE and 'JsonCodec.Serialize(analysis.Origin)' in SERVICE and 'source_insunits' in SERVICE)
ok("default model-only without silent model/paper mixing", 'public bool IncludePaperSpace { get; set; } = false;' in SERVICE and 'IncludePaperSpace = BoolValue("include_paperspace", false)' in REGISTRY and 'include_paperspace: bool = False' in (ROOT / 'mcp/yangcad_mcp/server.py').read_text(encoding='utf-8-sig'))
ok("placement origin independent of bylayer normalization", 'public string CoordinateOriginMode' in SERVICE and 'analysis.Origin = new object[]' in SERVICE and 'NormalizeGeometryOverrides && category == "geometry"' in SERVICE)
ok("mixed space warning disables local coordinate placement", 'Exchange 同时包含模型空间和布局空间' in SERVICE and 'analysis.OriginMode = "source";' in SERVICE)
ok("explicit clone DWG vs MAP scope warning", 'MAP仅包含模型空间' in SERVICE)
ok("map reports model/paper counts and origin mode", all(x in SERVICE for x in ['coordinate_origin_mode', 'model_space_entities', 'paper_space_entities','mixed_spaces']))
ok("map exports source units counts and unsupported summary", 'source_insunits' in SERVICE and 'GroupBy(x => x.DxfType' in SERVICE and 'unsupported_count' in SERVICE)
ok("unsupported entities carry count plus Handle/DxfType/reason detail", 'UnsupportedCounts' in SERVICE and 'UnsupportedItems' in SERVICE and 'unsupported_items' in SERVICE and 'public string reason' in SERVICE)
ok("unsupported explicit drawing units fail closed", 'throw new NotSupportedException("当前 CAD INSUNITS="' in SERVICE and '禁止静默按 mm 转换' in SERVICE)
ok("preview validates units before returning a plan", 'var units = ExchangeUnits(db.Insunits, analysis); // Preview must fail closed' in SERVICE and 'BuildResult(true, documentName, paths, analysis, units, db.Insunits.ToString())' in SERVICE)
ok("mleader exports text position and canonical leader_lines points", 'data["position"] = mLeaderTextLocation' in SERVICE and 'leader_vertex_order' in SERVICE and 'arrow_to_text' in SERVICE and 'points = vertices.ToArray()' in SERVICE)
ok("leaders fail closed when reconstructable text or line is missing", 'MULTILEADER has no reconstructable text content' in SERVICE and 'MULTILEADER has no reconstructable leader line' in SERVICE and 'LEADER has no reconstructable text annotation' in SERVICE and 'LEADER has fewer than two vertices' in SERVICE)
ok("legacy leader exports leader_lines and annotation position", 'LeaderAnnotationPosition' in SERVICE and 'data["leader_lines"] = new object[]' in SERVICE)
ok("linear dimensions use stable semantic roles", all(x in SERVICE for x in ['dimension is RotatedDimension', 'dimension is AlignedDimension', 'role = "DimLinePoint"', 'role = "XLine1Point"', 'role = "XLine2Point"']))
ok("dimension exposes Revit rebuild support and display text", 'revit_rebuild_supported' in SERVICE and 'display_text' in SERVICE)
ok("hatch curve boundaries use stable LINE/ARC payloads", all(x in SERVICE for x in ['curve is LineSegment2d', '["Type"] = "LINE"', 'curve is CircularArc2d', '["Type"] = "ARC"', '["StartPoint"]', '["EndPoint"]']))
ok("fixture exists and declares official schema", bool(FIXTURE) and FIXTURE.get('schema_version') == 'yangcad-rvt-exchange/1.0' and FIXTURE.get('units') == 'mm' and FIXTURE.get('origin') == [0.0, 0.0, 0.0])
fixture_entities = FIXTURE.get('entities', []) if isinstance(FIXTURE, dict) else []
fixture_types = {str(x.get('DxfType', '')).upper() for x in fixture_entities if isinstance(x, dict)}
ok("fixture covers V1 YangTools object types", {'LINE','LWPOLYLINE','ARC','CIRCLE','TEXT','MTEXT','LEADER','MULTILEADER','HATCH','DIMENSION'}.issubset(fixture_types))
ok("fixture entities use Handle DxfType ExchangeLayer Traits Data", bool(fixture_entities) and all(all(k in e for k in ['Handle','DxfType','ExchangeLayer','Traits','Data']) for e in fixture_entities))
handles = [e.get('Handle') for e in fixture_entities if isinstance(e, dict)]
ok("fixture handles are unique", len(handles) == len(set(handles)))
dim_fixture = next((e for e in fixture_entities if str(e.get('DxfType','')).upper() == 'DIMENSION'), {})
dim_roles = {p.get('role') for p in dim_fixture.get('Data',{}).get('definition_points',[]) if isinstance(p, dict)}
ok("fixture dimension carries three stable definition roles", {'DimLinePoint','XLine1Point','XLine2Point'}.issubset(dim_roles))
hatch_fixtures = [e for e in fixture_entities if str(e.get('DxfType','')).upper() == 'HATCH']
ok("fixture hatch carries boundary loops", bool(hatch_fixtures) and all(e.get('Data',{}).get('boundary_loops') for e in hatch_fixtures))
leader_fixture = next((e for e in fixture_entities if str(e.get('DxfType','')).upper() == 'MULTILEADER'), {})
ok("fixture mleader locks arrow-to-text vertex order", leader_fixture.get('Data',{}).get('leader_vertex_order') == 'arrow_to_text' and bool(leader_fixture.get('Data',{}).get('leader_lines')))
ok("spec locks DxfType as official YangTools contract", 'DxfType' in SPEC and 'YangTools 3.1.36' in SPEC and 'definition_points' in SPEC and 'arrow_to_text' in SPEC)

CONTROLLED = json.loads((ROOT / "tools/fixtures/RVT_EXCHANGE_V1_CONTROLLED_11.json").read_text(encoding="utf-8-sig"))
controlled_entities = CONTROLLED.get("entities", [])
controlled_types = [str(e.get("DxfType", "")).upper() for e in controlled_entities]
ok("controlled E2E fixture is exactly 11 official objects", len(controlled_entities) == 11 and controlled_types.count("LWPOLYLINE") == 3 and all(controlled_types.count(t) == 1 for t in ["LINE","ARC","CIRCLE","TEXT","MTEXT","MULTILEADER","HATCH","DIMENSION"]))
poly_bulges = [v.get("bulge") for e in controlled_entities if str(e.get("DxfType", "")).upper()=="LWPOLYLINE" for v in e.get("Data",{}).get("vertices",[]) if isinstance(v,dict)]
ok("controlled E2E locks bulge 0.5", 0.5 in poly_bulges)
controlled_ml = next((e for e in controlled_entities if str(e.get("DxfType", "")).upper()=="MULTILEADER"), {})
ml_data = controlled_ml.get("Data", {})
ok("controlled E2E locks MLeader text and points", ml_data.get("text") == "MLeader E2E Text" and bool(ml_data.get("leader_lines")) and bool(ml_data.get("leader_lines",[{}])[0].get("points")))

ok("rvt exchange is long-lived documented contract", 'D-032' in DECISIONS and 'YangCAD RVT Exchange v1' in SPEC)
ok("product result includes GUI/MCP reconciliation fields", all(x in SERVICE for x in ['public string units','public object[] origin','public string source_insunits','public Dictionary<string, int> dxf_type_counts','public string[] handles']))
ok("product MCP uses same RevitExchangeService", 'project.revit.prepare' in (ROOT / 'mcp/yangcad_mcp/server.py').read_text(encoding='utf-8-sig') and (ROOT/'src/YangCAD.AutoCAD/Mcp/Tools/ProjectProductTools.cs').read_text(encoding='utf-8-sig').count('RevitExchangeService.') >= 2)

failed = [name for name, passed in checks if not passed]
for name, passed in checks:
    print(("PASS" if passed else "FAIL") + ": " + name)
if failed:
    raise SystemExit("revit exchange v1 policy failed: " + ", ".join(failed))
print(f"revit exchange v1 policy: {len(checks)}/{len(checks)} PASS")
