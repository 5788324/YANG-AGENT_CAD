from __future__ import annotations
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
reg_src=(ROOT/'src/YangCAD.AutoCAD/Toolbox/ToolboxRegistry.cs').read_text(encoding='utf-8')
mcp_src=(ROOT/'src/YangCAD.AutoCAD/Mcp/Tools/CadProductionTools.cs').read_text(encoding='utf-8')
server_src=(ROOT/'mcp/yangcad_mcp/server.py').read_text(encoding='utf-8')
poly=(ROOT/'src/YangCAD.AutoCAD/Services/Cad/PolylineCleanupService.cs').read_text(encoding='utf-8')
layer=(ROOT/'src/YangCAD.AutoCAD/Services/Cad/LayerCleanupService.cs').read_text(encoding='utf-8')
failures=[]
def check(c,m):
    if c: print('PASS: '+m)
    else: failures.append(m)

for tool in ['cad.polyline.clean','cad.layer.cleanup']:
    check(tool not in reg_src, f'{tool} retired from Toolbox GUI')
    check(f'"{tool}"' in server_src, f'{tool} preserved in MCP contract')
check('PolylineCleanupService.Clean' in mcp_src, 'PolylineCleanupService remains MCP backend')
check('LayerCleanupService.Cleanup' in mcp_src, 'LayerCleanupService remains MCP backend')
check('p.HasBulges' in poly and 'HasNonZeroSegmentWidths' in poly, 'Polyline fail-closed protections preserved')
check(all(x in layer for x in ['protected-layer-0','protected-defpoints','current-layer','xref-dependent','locked','db.Purge']), 'Layer cleanup protections preserved')
check('HttpClient' not in reg_src and 'http://' not in reg_src and 'https://' not in reg_src, 'remaining Toolbox GUI stays service-direct')
if failures:
    print(f"\nlegacy write GUI retirement policy: {len(failures)} FAILURES")
    for f in failures: print('  - '+f)
    sys.exit(1)
print('\nlegacy write GUI retirement policy: ALL PASS')
