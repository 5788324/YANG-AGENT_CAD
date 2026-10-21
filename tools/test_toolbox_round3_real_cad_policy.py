from __future__ import annotations
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
registry_file = ROOT / "src/YangCAD.AutoCAD/Toolbox/ToolboxRegistry.cs"
mcp_prod_file = ROOT / "src/YangCAD.AutoCAD/Mcp/Tools/CadProductionTools.cs"
server_file = ROOT / "mcp/yangcad_mcp/server.py"

failures: list[str] = []
def check(condition: bool, message: str) -> None:
    if not condition: failures.append(message)
    else: print(f"PASS: {message}")

reg_src = registry_file.read_text(encoding="utf-8")
mcp_src = mcp_prod_file.read_text(encoding="utf-8")
server_src = server_file.read_text(encoding="utf-8")

# Product direction: legacy CAD GUI cards were intentionally removed, but their MCP services remain.
for tool in ['cad.drawing.audit','cad.layer.audit','cad.polyline.inspect']:
    check(tool not in reg_src, f"{tool} not exposed as Toolbox GUI card")
    check(f'"{tool}"' in server_src, f"{tool} remains exposed by MCP core")

for service_name in ['PolylineInspectionService.cs','LayerAuditService.cs','DrawingAuditService.cs']:
    check((ROOT / 'src/YangCAD.AutoCAD/Services/Cad' / service_name).exists(), f"{service_name} retained for MCP/shared backend")

check("PolylineInspectionService.Inspect" in mcp_src, "PolylineInspectionService still used by MCP")
check("LayerAuditService.Audit" in mcp_src, "LayerAuditService still used by MCP")
check("DrawingAuditService.Audit" in mcp_src, "DrawingAuditService still used by MCP")
check("HttpClient" not in reg_src and "http://" not in reg_src and "https://" not in reg_src, "remaining Toolbox GUI does not call MCP/HTTP")

if failures:
    print(f"\nlegacy CAD GUI retirement policy: {len(failures)} FAILURES")
    for f in failures: print("  - " + f)
    sys.exit(1)
print("\nlegacy CAD GUI retirement policy: ALL PASS")
