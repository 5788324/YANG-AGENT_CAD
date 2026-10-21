from pathlib import Path
import ast
import re

ROOT = Path(__file__).resolve().parents[1]
SERVICE = (ROOT / "src/YangCAD.AutoCAD/Services/Cad/ScaleConversionService.cs").read_text(encoding="utf-8-sig")
COMMAND = (ROOT / "src/YangCAD.AutoCAD/Commands/ScaleConvertCommands.cs").read_text(encoding="utf-8-sig")
WINDOW = (ROOT / "src/YangCAD.AutoCAD/UI/ScaleConvertWindow.xaml").read_text(encoding="utf-8-sig")
WINDOW_CODE = (ROOT / "src/YangCAD.AutoCAD/UI/ScaleConvertWindow.xaml.cs").read_text(encoding="utf-8-sig")
SHARED_STYLES = (ROOT / "src/YangCAD.AutoCAD/UI/Themes/SharedStyles.xaml").read_text(encoding="utf-8-sig")
RIBBON = (ROOT / "src/YangCAD.AutoCAD/UI/RibbonBootstrapper.cs").read_text(encoding="utf-8-sig")
MCP_CS = (ROOT / "src/YangCAD.AutoCAD/Mcp/Tools/CadScaleTools.cs").read_text(encoding="utf-8-sig")
RUNTIME = (ROOT / "src/YangCAD.AutoCAD/RuntimeHost.cs").read_text(encoding="utf-8-sig")
SERVER = (ROOT / "mcp/yangcad_mcp/server.py").read_text(encoding="utf-8-sig")
GATEWAY = (ROOT / "mcp/yangcad_mcp/gateway.py").read_text(encoding="utf-8-sig")

checks=[]
def ok(name, cond): checks.append((name,bool(cond)))

ok("ribbon exposes scale converter", 'Button("比例转换", "YGSCALE "' in RIBBON)
ok("YGSCALE command exists", '[CommandMethod("YGSCALE", CommandFlags.Modal | CommandFlags.UsePickSet)]' in COMMAND)
ok("command uses selection before modal options", COMMAND.find('GetSelection(options)') < COMMAND.find('new ScaleConvertWindow'))
ok("scaled mode prompts base point", '请选择缩放基点' in COMMAND and 'SelectedMode == ScaleConversionService.DrawingMode.ScaledDrawing' in COMMAND)
ok("GUI routes through shared scale service", 'ScaleConversionService.Convert(context, request, dryRun: false)' in COMMAND)
ok("GUI write uses one runner transaction", COMMAND.count('CadTransactionRunner.WriteContext') == 1)

ok("window has standard 1to1 mode", '标准模式（模型空间按 1:1 实尺寸绘制）' in WINDOW)
ok("window has scaled drawing mode", '缩尺绘图模式（模型空间不是按真实 1:1 绘制）' in WINDOW)
ok("radio buttons use compatible style", 'Style="{StaticResource ElegantRadioButton}"' in WINDOW and 'x:Key="ElegantRadioButton" TargetType="RadioButton"' in SHARED_STYLES)
ok("window keeps simple paper-preservation options", all(x in WINDOW for x in ['文字大小','尺寸标注文字 / 箭头 / 间距','Leader / MLeader','Hatch 图案比例','线型显示比例']))
ok("window supports common presets and manual input", all(x in WINDOW_CODE for x in ['"1:1"','"1:5"','"1:10"','"1:500"','IsEditable']) if 'IsEditable' in WINDOW_CODE else all(x in WINDOW for x in ['IsEditable="True"']))
ok("standard factor is new over old", 'var annotation = newScale / oldScale;' in WINDOW_CODE and 'request.NewScale / request.OldScale' in SERVICE)
ok("same ratio is rejected", "当前比例与目标比例相同，不需要转换" in SERVICE and "当前比例与目标比例相同，不需要转换" in WINDOW_CODE)
ok("scaled geometry factor is old over new", 'var geometry = oldScale / newScale;' in WINDOW_CODE and 'request.OldScale / request.NewScale' in SERVICE)

standard = SERVICE[SERVICE.index('private static void ApplyStandardMode'):SERVICE.index('private static void ApplyScaledDrawingMode')]
scaled = SERVICE[SERVICE.index('private static void ApplyScaledDrawingMode'):SERVICE.index('private sealed class AttributeSnapshot')]
ok("standard mode never transforms geometry", 'TransformBy' not in standard)
ok("scaled mode uses Matrix3d.Scaling", 'Matrix3d.Scaling(geometryFactor, request.BasePoint)' in scaled and 'TransformBy(transform)' in scaled)
ok("scaled text restores model text height", 'dbText.Height = oldHeight' in scaled and 'mText.TextHeight = oldHeight' in scaled)
ok("standard text scales annotation height", 'dbText.Height *= annotationFactor' in standard and 'mText.TextHeight *= annotationFactor' in standard)
ok("dimension keeps standard Dimlfac", 'Standard 1:1 mode must keep Dimlfac unchanged' in standard)
ok("scaled dimension adjusts Dimlfac by inverse geometry factor", 'dimlfac.Value / geometryFactor' in scaled)
ok("dimension visual properties are object-level snapshots", all(x in SERVICE for x in ['"Dimtxt"','"Dimasz"','"Dimgap"','"Dimexe"','"Dimexo"']))
ok("service does not mutate global style tables", all(x not in SERVICE for x in ['DimStyleTableRecord','TextStyleTableRecord','MLeaderStyleTableRecord']))
ok("hatch paper scale handled", 'PatternScale' in SERVICE and 'PreserveHatchPaperScale' in SERVICE)
ok("linetype paper scale handled", 'LinetypeScale' in SERVICE and 'PreserveLinetypePaperScale' in SERVICE)
ok("legacy block transform preserves attribute text height", 'SnapshotBlockAttributes' in SERVICE and 'RestoreBlockAttributes' in SERVICE)
ok("standard mode block ambiguity fails closed", 'standard_mode_block_ambiguous' in SERVICE)
ok("annotative/xref/underlay/civil/aec/proxy fail closed", all(x in SERVICE for x in ['non_model_space','annotative_object','xref','underlay_or_raster','civil3d_object','aec_object','proxy_or_third_party']))
ok("per-object failures are isolated", 'catch (Exception ex)' in SERVICE and 'severity = "fail"' in SERVICE)
ok("result exposes processed skipped failed and undo", all(x in SERVICE for x in ['public int processed','public int skipped','public int failed','single_transaction_one_command_target_one_undo']))
ok("all skipped is not reported as converted", "success = failed == 0 && processed > 0" in SERVICE)

ok("MCP adapter uses same service", 'Name = "cad.scale.convert"' in MCP_CS and 'ScaleConversionService.Convert' in MCP_CS)
ok("MCP defaults dry-run through descriptor", 'SupportsDryRun = true' in MCP_CS)
ok("MCP Python tool exists", 'name="cad.scale.convert"' in SERVER)
ok("MCP write tool is PID/document bound", '"cad.scale.convert"' in GATEWAY.split('WRITE_TOOLS =',1)[1].split('}',1)[0])
ok("live gateway registers scale tool", 'RegisterOptional("cad.scale.convert"' in RUNTIME)

tree=ast.parse(SERVER)
public=[]
for node in tree.body:
    if isinstance(node,(ast.FunctionDef,ast.AsyncFunctionDef)):
        for dec in node.decorator_list:
            if isinstance(dec,ast.Call) and isinstance(dec.func,ast.Attribute) and dec.func.attr=='tool':
                for kw in dec.keywords:
                    if kw.arg=='name' and isinstance(kw.value,ast.Constant): public.append(kw.value.value)
ok("stdio contract is 68 unique", len(public)==68 and len(set(public))==68)

failed=[n for n,v in checks if not v]
for n,v in checks: print(("PASS" if v else "FAIL")+": "+n)
if failed: raise SystemExit("scale converter policy failed: "+", ".join(failed))
print(f"scale converter policy: {len(checks)}/{len(checks)} PASS")
