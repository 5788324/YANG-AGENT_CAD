"""Static source-contract gate for easy layer tools and first QG-like detail extraction.
Not a replacement for a real AutoCAD 2024 runtime smoke test.
"""
from pathlib import Path
import ast
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]
def read(p): return (ROOT/p).read_text(encoding='utf-8-sig')
service=read('src/YangCAD.AutoCAD/Services/Cad/LayerQuickService.cs')
commands=read('src/YangCAD.AutoCAD/Commands/LayerQuickCommands.cs')
window=read('src/YangCAD.AutoCAD/UI/LayerQuickWindow.xaml')
detail=read('src/YangCAD.AutoCAD/Services/Cad/DetailExtractService.cs')
detail_command=read('src/YangCAD.AutoCAD/Commands/DetailExtractCommands.cs')
server=read('mcp/yangcad_mcp/server.py')
gateway=read('mcp/yangcad_mcp/gateway.py')
runtime=read('src/YangCAD.AutoCAD/RuntimeHost.cs')
buttons=read('src/YangCAD.AutoCAD/UI/RibbonBootstrapper.cs')
tool=read('src/YangCAD.AutoCAD/Mcp/Tools/CadLayerQuickTools.cs')
checks=[]
def check(name,pred):checks.append((name,bool(pred)))
ET.fromstring(window)
check('XAML parses', True)
check('Ribbon has exactly two simple task entrances', 'Button("图层工具", "YGLAYER "' in buttons and 'Button("提取大样", "YGDETAIL "' in buttons)
check('Layer panel presents four primary operations', all(x in window for x in ['VisibilityClick','FreezeClick','LockClick','MergeClick']))
check('Hidden/frozen layers recoverable in layer list', 'SourceLayer.ItemsSource = names' in read('src/YangCAD.AutoCAD/UI/LayerQuickWindow.xaml.cs'))
check('Nested selection is supported', 'GetNestedEntity' in commands and 'GetContainers()' in commands)
check('Block insertion vs internal layer chooser', '内部对象图层(I)/整个块或参照插入图层(B)' in commands)
check('Xref host dependent layer resolution', '"|" + inner' in commands and 'table.Has(candidate)' in commands)
check('Native layer write properties', all(x in service for x in ['layer.IsOff = value','layer.IsFrozen = value','layer.IsLocked = value']))
check('Source/target validation', 'source.ObjectId == target.ObjectId' in service)
check('No xref definitions edited by merge', 'record.IsFromExternalReference || record.IsFromOverlayReference' in service)
check('No dependent-layer merge', 'source.IsDependent || target.IsDependent' in service)
check('All local block definitions traversed', 'foreach (ObjectId blockId in bt)' in service and 'foreach (ObjectId entityId in record)' in service)
check('Block AttributeReference layers also migrated', 'foreach (ObjectId attributeId in br.AttributeCollection)' in service and 'attr.LayerId == source.ObjectId' in service)
check('No silent source-layer deletion', 'removed_source_layer = false' in service)
check('Single transaction for UI actions', 'CadTransactionRunner.WriteContext(doc, false' in commands)
check('Detail source never erased', '.Erase()' not in detail)
check('Detail line exact Liang-Barsky clipping', 'Step(-dx' in detail and 'Step(dx' in detail and 'Step(-dy' in detail and 'Step(dy' in detail)
check('Arc/circle exact angle clipping', 'TrimArc(' in detail and 'Math.Acos' in detail and 'Math.Asin' in detail)
check('Polyline exploded segments trimmed with bulges retained', 'poly.Explode(exploded)' in detail and 'segArc.StartAngle' in detail)
check('Cross-boundary complex objects skipped', '边界相交但不能无损裁切' in detail)
check('Attributed blocks protected', 'blockRef.AttributeCollection.Count > 0' in detail)
check('Associative hatch protected', 'hatch.Associative' in detail)
check('Dimensions adjust dimlfac on scale', 'prior / factor' in detail)
check('Native simple detail UX', 'GetCorner(secondOpt)' in detail_command and 'GetPoint("\\n在图中点一下' in detail_command)
for name in ['cad.layer.merge','cad.layer.visibility','cad.layer.freeze','cad.layer.lock','cad.detail.extract']:
    check(name+' MCP all routes', f'name="{name}"' in server and f'"{name}"' in gateway and (f'Name = "{name}"' in tool or f'protected override string ToolName => "{name}"' in tool) and f'RegisterOptional("{name}"' in runtime)
check('Write operations default dry run', server.count('dry_run: bool = True') >= 5)
check('MCP docs coverage', all('`'+x+'`' in read('docs/MCP_COVERAGE_MATRIX.md') for x in ['cad.layer.merge','cad.layer.visibility','cad.layer.freeze','cad.layer.lock','cad.detail.extract']))
for name,good in checks: print(('PASS' if good else 'FAIL')+': '+name)
print(f'layer/detail static policy: {sum(good for _,good in checks)}/{len(checks)} PASS')
if not all(good for _,good in checks):raise SystemExit(1)
