from pathlib import Path
import ast

ROOT = Path(__file__).resolve().parents[1]
REGISTRY = (ROOT / "src/YangCAD.AutoCAD/Toolbox/ToolboxRegistry.cs").read_text(encoding="utf-8-sig")
PROJECT = (ROOT / "src/YangCAD.AutoCAD/Mcp/Tools/ProjectProductTools.cs").read_text(encoding="utf-8-sig")
RUNTIME = (ROOT / "src/YangCAD.AutoCAD/RuntimeHost.cs").read_text(encoding="utf-8-sig")
SERVER = (ROOT / "mcp/yangcad_mcp/server.py").read_text(encoding="utf-8-sig")
GATEWAY = (ROOT / "mcp/yangcad_mcp/gateway.py").read_text(encoding="utf-8-sig")
COVERAGE = (ROOT / "docs/MCP_COVERAGE_MATRIX.md").read_text(encoding="utf-8-sig")
REVT = (ROOT / "src/YangCAD.AutoCAD/Services/Cad/RevitExchangeService.cs").read_text(encoding="utf-8-sig")
IMAGE = (ROOT / "src/YangCAD.AutoCAD/Services/Cad/ImageEmbedBatchService.cs").read_text(encoding="utf-8-sig")
SCALE = (ROOT / "src/YangCAD.AutoCAD/Services/Cad/ScaleConversionService.cs").read_text(encoding="utf-8-sig")
SCALE_TOOL = (ROOT / "src/YangCAD.AutoCAD/Mcp/Tools/CadScaleTools.cs").read_text(encoding="utf-8-sig")
RIBBON = (ROOT / "src/YangCAD.AutoCAD/UI/RibbonBootstrapper.cs").read_text(encoding="utf-8-sig")
TOOLBOX_VIEW = (ROOT / "src/YangCAD.AutoCAD/UI/ToolboxView.xaml.cs").read_text(encoding="utf-8-sig")

checks=[]
def ok(name, cond): checks.append((name,bool(cond)))
for tool, service in [("project.revit.prepare","RevitExchangeService"),("project.image.embed","ImageEmbedBatchService")]:
    segment=REGISTRY[REGISTRY.index(f'Id = "{tool}"'):]
    ok(tool+" GUI advertises MCP", "SupportsMcp = true" in segment[:1800])
    ok(tool+" C# adapter exists", f'Name = "{tool}"' in PROJECT and service in PROJECT)
    ok(tool+" live registration exists", f'RegisterOptional("{tool}"' in RUNTIME)
    ok(tool+" Python stdio adapter exists", f'name="{tool}"' in SERVER)
    ok(tool+" PID/document write binding exists", f'"{tool}"' in GATEWAY.split("WRITE_TOOLS =",1)[1].split("}",1)[0])
    ok(tool+" coverage matrix row exists", tool in COVERAGE and service in COVERAGE)

ok("Revit product result contract stable", all(x in REVT for x in ["public sealed class Result","public bool success","public string map_path","public string report_path","public int unsupported_count","public string[] warnings","public Dictionary<string, int> dxf_type_counts","public string[] handles"]))
ok("Image product result contract stable", all(x in IMAGE for x in ["public sealed class Request","public sealed class Result","public string job_id","public string status_path","public int file_count","public string[] warnings"]))
ok("Toolbox GUI execution has runtime provenance", 'Toolbox GUI execute begin tool=' in TOOLBOX_VIEW and 'Toolbox GUI execute completed tool=' in TOOLBOX_VIEW)

ok("scale converter ribbon product entry exists", 'Button("比例转换", "YGSCALE "' in RIBBON)
ok("scale converter C# adapter shares service", 'Name = "cad.scale.convert"' in SCALE_TOOL and 'ScaleConversionService.Convert' in SCALE_TOOL)
ok("scale converter live registration exists", 'RegisterOptional("cad.scale.convert"' in RUNTIME)
ok("scale converter Python adapter exists", 'name="cad.scale.convert"' in SERVER)
ok("scale converter PID/document write binding exists", '"cad.scale.convert"' in GATEWAY.split("WRITE_TOOLS =",1)[1].split("}",1)[0])
ok("scale converter coverage matrix row exists", '`cad.scale.convert`' in COVERAGE and 'ScaleConversionService' in COVERAGE)
ok("scale converter structured result contract exists", all(x in SCALE for x in ['public sealed class Result','public double geometry_factor','public double annotation_factor','public int processed','public int skipped','public int failed','public string undo_semantics']))

# Decorator count is current additive contract: 69 unique stdio tools.
tree=ast.parse(SERVER)
names=[]
for node in tree.body:
    if isinstance(node,(ast.FunctionDef,ast.AsyncFunctionDef)):
        for dec in node.decorator_list:
            if isinstance(dec,ast.Call) and isinstance(dec.func,ast.Attribute) and dec.func.attr=="tool":
                for kw in dec.keywords:
                    if kw.arg=="name" and isinstance(kw.value,ast.Constant): names.append(kw.value.value)
ok("stdio tool contract is 69 unique", len(names)==69 and len(set(names))==69)
ok("coverage matrix declares 69/32/62", all(x in COVERAGE for x in ["**69**","**32**","**62**"]))
ok("coverage matrix has one row for every public MCP tool", all(f"`{name}`" in COVERAGE for name in names) and COVERAGE.count("| PASS |") >= 69)

failed=[n for n,v in checks if not v]
for n,v in checks: print(("PASS" if v else "FAIL")+": "+n)
if failed: raise SystemExit("mcp product coverage policy failed: "+", ".join(failed))
print(f"mcp product coverage policy: {len(checks)}/{len(checks)} PASS")
