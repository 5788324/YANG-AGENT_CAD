from __future__ import annotations
import ast
import pathlib
import re
import sys
import xml.etree.ElementTree as ET

ROOT = pathlib.Path(__file__).resolve().parents[1]
errors: list[str] = []

REQUIRED = [
    "README.md", "PROJECT.md", "AGENTS.md", "CLAUDE.md",
    "src/YangCAD.Core/YangCAD.Core.csproj",
    "src/YangCAD.AutoCAD/YangCAD.AutoCAD.csproj",
    "src/YangCAD.AutoCAD/Infrastructure/CadExecutionContext.cs",
    "src/YangCAD.AutoCAD/Toolbox/ToolboxContracts.cs",
    "src/YangCAD.AutoCAD/Toolbox/ToolboxRegistry.cs",
    "src/YangCAD.AutoCAD/UI/ToolboxView.xaml",
    "src/YangCAD.AutoCAD/UI/ScaleConvertWindow.xaml",
    "src/YangCAD.AutoCAD/Services/Cad/ScaleConversionService.cs",
    "src/YangCAD.AutoCAD/Mcp/Tools/CadScaleTools.cs",
    "src/YangCAD.AutoCAD/UI/ToolboxPaletteManager.cs",
    "src/YangCAD.BatchWorker/YangCAD.BatchWorker.csproj",
    "src/YangCAD.Civil3D/YangCAD.Civil3D.csproj",
    "src/YangCAD.Setup/YangCAD.Setup.csproj",
    "mcp/pyproject.toml", "mcp/yangcad_mcp/server.py", "mcp/yangcad_mcp/batch.py", "mcp/yangcad_mcp/extensions.py",
    "docs/TOOL_INDEX.md", "docs/DECISIONS.md", "docs/CURRENT_STATE.md", "docs/HANDOFF.md",
    "docs/TOOLBOX_ARCHITECTURE.md", "docs/DEVELOPMENT_ROUNDS.md",
    "tasks/CURRENT_TASK.md", "tasks/TOOLBOX_001_ROUND_01_WINDOWS_UI_SMOKE.md",
]
for rel in REQUIRED:
    if not (ROOT / rel).exists():
        errors.append("missing: " + rel)

xmls = list(ROOT.rglob("*.xaml")) + list(ROOT.rglob("*.csproj")) + list(ROOT.rglob("*.xml"))
for path in xmls:
    try:
        ET.parse(path)
    except Exception as exc:
        errors.append(f"xml {path.relative_to(ROOT)}: {exc}")

pys = list(ROOT.rglob("*.py"))
for path in pys:
    try:
        ast.parse(path.read_text(encoding="utf-8-sig"), filename=str(path))
    except Exception as exc:
        errors.append(f"python {path.relative_to(ROOT)}: {exc}")

# TASK-002 theme contract: only the three curated themes remain.
theme_dir = ROOT / "src/YangCAD.AutoCAD/UI/Themes"
themes = sorted(p.stem for p in theme_dir.glob("*.xaml") if p.name != "SharedStyles.xaml")
if themes != ["Mist", "Obsidian", "Porcelain"]:
    errors.append(f"theme set mismatch: {themes}")

gateway_tools = [
    "project.revit.prepare", "project.image.embed", "cad.scale.convert", "cad.leader.rebuild",
    "cad.status", "cad.document.info", "cad.selection.read", "cad.entity.get", "cad.entity.query",
    "cad.entity.update", "cad.entity.transform", "cad.entity.erase", "cad.layer.list", "cad.layer.ensure",
    "cad.line.create", "cad.polyline.create", "cad.text.create", "cad.block.list", "cad.block.insert",
    "cad.polyline.inspect", "cad.polyline.clean", "cad.layer.audit", "cad.layer.cleanup", "cad.drawing.audit",
    "cad.object.describe", "cad.property.get", "cad.property.set", "cad.api.describe", "cad.api.invoke",
    "cad.command.run", "cad.extension.list", "cad.extension.invoke",
    "civil.status", "civil.surface.list", "civil.alignment.list", "civil.alignment.station_offset",
    "civil.structure.list", "civil.structure.get", "civil.structure.update",
    "civil.pipe.list", "civil.pipe.get", "civil.pipe.update",
    "civil.surface.get", "civil.surface.elevation", "civil.surface.boundaries",
    "civil.alignment.point_location",
    "civil.profile.list", "civil.profile.get", "civil.profile.elevation", "civil.profile.pvi.list",
    "civil.cogo_point.list", "civil.cogo_point.get",
    "civil.pressure_network.list", "civil.pressure_network.get",
    "civil.feature_line.list", "civil.feature_line.get", "civil.feature_line.points", "civil.feature_line.diagnose",
    "civil.corridor.list", "civil.corridor.get", "civil.corridor.baselines", "civil.corridor.diagnose",
]
batch_tools = ["batch.status", "batch.dwg.audit", "batch.dwg.hygiene"]
extension_workflow_tools = ["extension.status", "extension.build", "extension.install", "extension.artifact.list"]
tools = gateway_tools + batch_tools + extension_workflow_tools
server_path = ROOT / "mcp/yangcad_mcp/server.py"
server = server_path.read_text(encoding="utf-8")
try:
    tree = ast.parse(server)
    exposed: list[str] = []
    for node in tree.body:
        if not isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef)):
            continue
        for dec in node.decorator_list:
            if isinstance(dec, ast.Call) and isinstance(dec.func, ast.Attribute) and dec.func.attr == "tool":
                for kw in dec.keywords:
                    if kw.arg == "name" and isinstance(kw.value, ast.Constant) and isinstance(kw.value.value, str):
                        exposed.append(kw.value.value)
    if len(exposed) != len(set(exposed)):
        errors.append("duplicate MCP exposed tool name")
    if set(exposed) != set(tools):
        errors.append(f"MCP exposed tool set differs from 69-tool product contract: {sorted(set(exposed)^set(tools))}")
except Exception as exc:
    errors.append("MCP decorator parse failed: " + str(exc))

all_cs = "\n".join(p.read_text(encoding="utf-8") for p in ROOT.rglob("*.cs"))
for tool in gateway_tools:
    if f'Name="{tool}"' not in all_cs and f'Name = "{tool}"' not in all_cs:
        errors.append("missing C# tool: " + tool)

# No generated payloads in source handoff.
for ext in ("*.dll", "*.pdb", "*.exe", "*.binlog", "*.pyc"):
    if list(ROOT.rglob(ext)):
        errors.append("generated/binary present: " + ext)
if list(ROOT.rglob("__pycache__")):
    errors.append("generated cache directory present: __pycache__")

for rel in ("README.md", "tools/Build.ps1", "tools/Setup-Mcp.ps1"):
    data = (ROOT / rel).read_bytes()
    bad = [b for b in data if b < 32 and b not in (9, 10, 13)]
    if bad:
        errors.append(f"control char present: {rel}")

# MCP/runtime hardening invariants preserved from TASK-001.
gateway_py = (ROOT / "mcp/yangcad_mcp/gateway.py").read_text(encoding="utf-8")
local_gateway = (ROOT / "src/YangCAD.AutoCAD/Mcp/LocalGateway.cs").read_text(encoding="utf-8")
broker = (ROOT / "src/YangCAD.AutoCAD/Mcp/CadRequestBroker.cs").read_text(encoding="utf-8")
rpc = (ROOT / "src/YangCAD.Core/Rpc/RpcContracts.cs").read_text(encoding="utf-8")
settings = (ROOT / "src/YangCAD.AutoCAD/Infrastructure/UserSettings.cs").read_text(encoding="utf-8")
if "new HttpListener" in local_gateway:
    errors.append("LocalGateway must not depend on HttpListener/HTTP.sys URL ACL")
for needle, label in [
    ("new TcpListener(IPAddress.Loopback", "loopback TcpListener"),
    ("/health", "health endpoint"),
    ("ExpectedDocument", "RPC document affinity"),
    ("document_id", "health document session identity"),
]:
    if needle not in local_gateway + rpc:
        errors.append("missing MCP hardening: " + label)
for needle, label in [
    ("WRITE_TOOLS", "write binding"),
    ("required_tool=tool", "tool-aware instance routing"),
    ("ProxyHandler({})", "loopback proxy bypass"),
    ("timeout=None", "no ambiguous client write timeout"),
    ("expectedDocument", "document-bound RPC envelope"),
    ("expectedDocumentId", "document-session-bound RPC envelope"),
]:
    if needle not in gateway_py:
        errors.append("missing Python gateway hardening: " + label)
# Start-page crash fix: preserve the dispatcher already proven in TASK-001, but only after a real DWG exists.
if "ExecuteInCommandContextAsync" in broker:
    errors.append("Broker must not dispatch through unverified ExecuteInCommandContextAsync path")
if "new System.Windows.Forms.Timer" not in broker or "Application.Idle += OnIdle" not in broker:
    errors.append("Broker must preserve TASK-001 Timer + Idle dispatcher")
if "MdiActiveDocument == null" not in broker or "Timer unavailable; falling back to Idle-only" not in broker:
    errors.append("Broker missing active-document gate / Timer failure isolation")
if "ExpectedDocumentId" not in broker or "ExpectedDocumentId" not in rpc:
    errors.append("Broker/RPC missing document-session affinity")

# Permissions: destructive/escape-hatch abilities must remain opt-in.
for needle in [
    "AllowErase { get; set; } = false",
    "AllowCommandExecution { get; set; } = false",
    "AllowExtensionInvoke { get; set; } = false",
]:
    if needle not in settings:
        errors.append("permission default missing: " + needle)
if 'string.Equals(r.Tool, "cad.layer.cleanup"' not in broker:
    errors.append("Broker missing cad.layer.cleanup destructive permission gate")
if 'string.Equals(r.Tool, "cad.command.run"' not in broker:
    errors.append("Broker missing cad.command.run permission gate")
if 'string.Equals(r.Tool, "cad.extension.invoke"' not in broker:
    errors.append("Broker missing cad.extension.invoke permission gate")


# P0 Start-page startup invariants. No MCP runtime/Civil/Ribbon work may begin before a real DWG exists.
ext_app = (ROOT / "src/YangCAD.AutoCAD/ExtensionApplication.cs").read_text(encoding="utf-8")
runtime_host = (ROOT / "src/YangCAD.AutoCAD/RuntimeHost.cs").read_text(encoding="utf-8")
for needle, label in [
    ("DocumentActivated += OnDocumentActivated", "AutoCAD deferred DocumentActivated start"),
    ("MCP deferred until DocumentActivated", "start-page deferred marker"),
    ("StartForActiveDocument", "active-document start gate"),
]:
    if needle not in ext_app:
        errors.append("start-page gate missing: " + label)
if "MdiActiveDocument == null" not in runtime_host or "Start 页/无活动文档" not in runtime_host:
    errors.append("RuntimeHost must hard-refuse startup without an active DWG")
if "Initialize phase: RuntimeHost.Initialize begin" in ext_app:
    errors.append("IExtensionApplication.Initialize must not start RuntimeHost unconditionally on Start page")

# Product MCP coverage invariants (2026-09-07).
project_tools = (ROOT / "src/YangCAD.AutoCAD/Mcp/Tools/ProjectProductTools.cs").read_text(encoding="utf-8")
toolbox_registry = (ROOT / "src/YangCAD.AutoCAD/Toolbox/ToolboxRegistry.cs").read_text(encoding="utf-8")
for tool_name, service_name in [("project.revit.prepare", "RevitExchangeService"), ("project.image.embed", "ImageEmbedBatchService")]:
    if f'Name = "{tool_name}"' not in project_tools:
        errors.append("product MCP C# adapter missing: " + tool_name)
    if f'name="{tool_name}"' not in server:
        errors.append("product MCP Python adapter missing: " + tool_name)
    if f'RegisterOptional("{tool_name}"' not in runtime_host:
        errors.append("product MCP live registration missing: " + tool_name)
    if tool_name not in gateway_py.split("WRITE_TOOLS =", 1)[1].split("}", 1)[0]:
        errors.append("product write tool missing PID/document binding: " + tool_name)
    if f'Id = "{tool_name}"' not in toolbox_registry or "SupportsMcp = true" not in toolbox_registry:
        errors.append("Toolbox product descriptor missing MCP coverage: " + tool_name)
    if service_name not in project_tools:
        errors.append("product MCP adapter not routed to shared service: " + service_name)
coverage_path = ROOT / "docs/MCP_COVERAGE_MATRIX.md"
if not coverage_path.exists() or "69" not in coverage_path.read_text(encoding="utf-8-sig"):
    errors.append("MCP coverage matrix missing/obsolete")

# Generic layer invariants.
generic_access = (ROOT / "src/YangCAD.AutoCAD/Mcp/Generic/GenericAccess.cs").read_text(encoding="utf-8")
generic_tools = (ROOT / "src/YangCAD.AutoCAD/Mcp/Generic/GenericTools.cs").read_text(encoding="utf-8")
ext_host = (ROOT / "src/YangCAD.AutoCAD/Mcp/Extensions/ExtensionHost.cs").read_text(encoding="utf-8")
for needle, label in [
    ("IsSafeWritableProperty", "property setter policy"),
    ("MethodPolicy", "API invoke policy"),
    ("ref/out/pointer parameter", "ref/out rejection"),
    ("CadTransactionRunner.Write", "generic write transaction"),
]:
    if needle not in generic_access + generic_tools:
        errors.append("generic MCP guard missing: " + label)
if "SendStringToExecute" in generic_tools:
    errors.append("cad.command.run must not use asynchronous SendStringToExecute")
if "Editor.Command" not in generic_tools:
    errors.append("cad.command.run missing synchronous Editor.Command path")
mcp_window_xaml = (ROOT / "src/YangCAD.AutoCAD/UI/McpWindow.xaml").read_text(encoding="utf-8")
if "%LOCALAPPDATA%" not in mcp_window_xaml:
    errors.append("MCP UI missing extension location explanation")
if "CadTransactionRunner.Write" not in ext_host or "Assembly.LoadFrom" not in ext_host:
    errors.append("extension host missing versioned DLL load / host transaction boundary")

# Theme/UI invariants.
theme_manager = (ROOT / "src/YangCAD.AutoCAD/UI/ThemeManager.cs").read_text(encoding="utf-8")
window_xaml = (ROOT / "src/YangCAD.AutoCAD/UI/ControlCenterWindow.xaml").read_text(encoding="utf-8")
ribbon = (ROOT / "src/YangCAD.AutoCAD/UI/RibbonBootstrapper.cs").read_text(encoding="utf-8")
if '{ "Obsidian", "Porcelain", "Mist" }' not in theme_manager:
    errors.append("ThemeManager must expose only Obsidian/Porcelain/Mist")
if "ControlCenter32.png" not in ribbon or "Mcp32.png" not in ribbon or ribbon.count("RibbonItemSize.Large") < 1:
    errors.append("Ribbon bitmap icons/aligned large buttons missing")
if "ScheduleEnsureRibbon" not in ribbon or "Application.Idle += OnIdle" not in ribbon or "AddSeconds(5)" not in ribbon:
    errors.append("Ribbon must be deferred until post-startup AutoCAD Idle warm-up")
for tag in ('Tag="Obsidian"', 'Tag="Porcelain"', 'Tag="Mist"'):
    if tag not in window_xaml:
        errors.append("control center missing theme: " + tag)

# Cross-layer version alignment. Keep this generic so Release/version bumps do not require hard-coded verifier edits.
dir_props = (ROOT / "Directory.Build.props").read_text(encoding="utf-8-sig")
mcp_project = (ROOT / "mcp/pyproject.toml").read_text(encoding="utf-8-sig")
mcp_init = (ROOT / "mcp/yangcad_mcp/__init__.py").read_text(encoding="utf-8-sig")
bundle_template = (ROOT / "bundle/PackageContents.template.xml").read_text(encoding="utf-8-sig")
vm = re.search(r"<Version>([^<]+)</Version>", dir_props)
version = vm.group(1) if vm else ""
if not version:
    errors.append("missing .NET version")
else:
    if f'version = "{version}"' not in mcp_project:
        errors.append(f"MCP project version differs from .NET version {version}")
    if f'__version__ = "{version}"' not in mcp_init:
        errors.append(f"MCP package version differs from .NET version {version}")
    if f'AppVersion="{version}"' not in bundle_template:
        errors.append(f"Bundle version differs from .NET version {version}")

# Runtime compatibility carried forward from verified 2024 fixes.
autocad_proj = (ROOT / "src/YangCAD.AutoCAD/YangCAD.AutoCAD.csproj").read_text(encoding="utf-8")
core_proj = (ROOT / "src/YangCAD.Core/YangCAD.Core.csproj").read_text(encoding="utf-8")
civil_proj = (ROOT / "src/YangCAD.Civil3D/YangCAD.Civil3D.csproj").read_text(encoding="utf-8")
civil_bootstrap = (ROOT / "src/YangCAD.Civil3D/CivilExtensionApplication.cs").read_text(encoding="utf-8")
json_codec = (ROOT / "src/YangCAD.AutoCAD/Infrastructure/JsonCodec.cs").read_text(encoding="utf-8")
build_ps1 = (ROOT / "tools/Build.ps1").read_text(encoding="utf-8-sig")
setup_ps1 = (ROOT / "tools/Setup-Mcp.ps1").read_text(encoding="utf-8-sig")
if 'PackageReference Include="System.Text.Json"' in core_proj + autocad_proj:
    errors.append("2021-2024 host must not carry System.Text.Json PackageReference")
if "YANGCAD_NETFRAMEWORK" not in autocad_proj or "JavaScriptSerializer" not in json_codec:
    errors.append("net48 framework-native JSON compatibility path missing")
if 'Reference Include="AdWindows"' not in autocad_proj:
    errors.append("AutoCAD Ribbon reference must use AdWindows.dll")
if "<UseWindowsForms>true</UseWindowsForms>" not in autocad_proj:
    errors.append("AutoCAD host must enable WinForms for the TASK-001 proven Broker Timer")
if 'Reference Include="AecBaseMgd"' not in civil_proj or 'AeccBaseMgd' in civil_proj:
    errors.append("Civil 3D verified base reference must use AecBaseMgd.dll")
if 'Reference Include="AeccPressurePipesMgd"' not in civil_proj:
    errors.append("TASK-005 Pressure Network read-only exploration requires AeccPressurePipesMgd.dll reference")
if 'GetSystemVariable("PRODUCT")' not in civil_bootstrap or 'IndexOf("Civil 3D"' not in civil_bootstrap:
    errors.append("Civil bootstrap missing actual-product guard for Object Enabler/plain AutoCAD isolation")
if "System.Text.Json.dll" not in build_ps1 or "Remove-Item $autoOut" not in build_ps1:
    errors.append("Build.ps1 missing clean-output/System.Text.Json runtime guard")
if "virtualenv fallback" not in setup_ps1 or "-m virtualenv" not in setup_ps1:
    errors.append("Setup-Mcp.ps1 missing Windows virtualenv fallback")

# TASK-003 CAD productivity invariants.
create_tools = (ROOT / "src/YangCAD.AutoCAD/Mcp/Tools/CadCreateTools.cs").read_text(encoding="utf-8")
for needle, label in [
    ('Name = "cad.line.create"', "line create"),
    ('Name = "cad.polyline.create"', "polyline create"),
    ('Name = "cad.text.create"', "text create"),
    ('Name = "cad.block.list"', "block list"),
    ('Name = "cad.block.insert"', "block insert"),
    ('CadTransactionRunner.Write', "create transaction/dry-run boundary"),
    ('AppendEntity', "database append path"),
]:
    if needle not in create_tools:
        errors.append("TASK-003 create invariant missing: " + label)
if 'RefreshCurrentIfChanged' not in broker or 'RefreshCurrentIfChanged' not in (ROOT / "src/YangCAD.AutoCAD/Mcp/InstanceRegistry.cs").read_text(encoding="utf-8"):
    errors.append("instance registry missing zero-doc/SaveAs refresh hardening")
if 'SetProductOverride("Civil 3D")' not in civil_bootstrap:
    errors.append("Civil host identity override missing after runtime probe")
ext_tools = (ROOT / "src/YangCAD.AutoCAD/Mcp/Extensions/ExtensionTools.cs").read_text(encoding="utf-8")
if 'ListFilesOnly' not in ext_host or 'AllowExtensionInvoke ? ExtensionHost.List() : ExtensionHost.ListFilesOnly()' not in ext_tools:
    errors.append("cad.extension.list must not load/instantiate extension DLLs while extension permission is disabled")

# TASK-004 Civil gravity Pipe/Structure invariants.
civil_ps = (ROOT / "src/YangCAD.Civil3D/Tools/CivilPipeStructureTools.cs").read_text(encoding="utf-8")
for needle, label in [
    ('Name = "civil.structure.list"', "structure list"),
    ('Name = "civil.structure.get"', "structure get"),
    ('Name = "civil.structure.update"', "structure update"),
    ('Name = "civil.pipe.list"', "pipe list"),
    ('Name = "civil.pipe.get"', "pipe get"),
    ('Name = "civil.pipe.update"', "pipe update"),
    ('StructureControlSumpType.ByElevation', "sump elevation control"),
    ('AutomaticRimSurfaceAdjustment', "rim surface adjustment"),
    ('StartPoint = start', "pipe start elevation write"),
    ('EndPoint = end', "pipe end elevation write"),
    ('IsReferenceObject', "reference object write rejection"),
    ('CadTransactionRunner.Write', "Civil write transaction/dry-run boundary"),
]:
    if needle not in civil_ps:
        errors.append("TASK-004 Civil Pipe/Structure invariant missing: " + label)
if '"civil.structure.update"' not in gateway_py or '"civil.pipe.update"' not in gateway_py:
    errors.append("TASK-004 Civil write tools missing Python process/document binding")
if "new CivilStructureUpdateTool()" not in civil_bootstrap or "new CivilPipeUpdateTool()" not in civil_bootstrap:
    errors.append("TASK-004 Civil tools missing deferred Civil host registration")

# TASK-005 Civil Read / Design invariants.
civil_read = (ROOT / "src/YangCAD.Civil3D/Tools/CivilReadDesignTools.cs").read_text(encoding="utf-8")
for needle, label in [
    ('Name = "civil.surface.get"', "surface get"),
    ('Name = "civil.surface.elevation"', "surface elevation"),
    ('Name = "civil.surface.boundaries"', "surface boundaries"),
    ('FindElevationAtXY', "surface elevation API"),
    ('BoundariesDefinition', "surface boundary API"),
    ('Name = "civil.alignment.point_location"', "alignment point location"),
    ('PointLocation(station, offset', "alignment station/offset reverse lookup"),
    ('Name = "civil.profile.list"', "profile list"),
    ('Name = "civil.profile.get"', "profile get"),
    ('Name = "civil.profile.elevation"', "profile elevation"),
    ('Name = "civil.profile.pvi.list"', "profile PVI list"),
    ('ElevationAt(station)', "profile elevation API"),
    ('Name = "civil.cogo_point.list"', "COGO point list"),
    ('Name = "civil.cogo_point.get"', "COGO point get"),
    ('Name = "civil.pressure_network.list"', "pressure network list"),
    ('Name = "civil.pressure_network.get"', "pressure network get"),
    ('GetPressurePipeNetworkIds', "pressure network discovery API"),
]:
    if needle not in civil_read:
        errors.append("TASK-005 Civil read invariant missing: " + label)
if "CadTransactionRunner.Write" in civil_read:
    errors.append("TASK-005 Civil Read/Design file must remain read-only")
for tool_name in [
    "civil.surface.get", "civil.surface.elevation", "civil.surface.boundaries",
    "civil.alignment.point_location", "civil.profile.list", "civil.profile.get",
    "civil.profile.elevation", "civil.profile.pvi.list", "civil.cogo_point.list",
    "civil.cogo_point.get", "civil.pressure_network.list", "civil.pressure_network.get",
]:
    if tool_name not in server:
        errors.append("TASK-005 Python MCP tool missing: " + tool_name)
if "new CivilPressureNetworkGetTool()" not in civil_bootstrap or "new CivilSurfaceElevationTool()" not in civil_bootstrap:
    errors.append("TASK-005 Civil tools missing deferred Civil host registration")


# TASK-006A Production Hygiene invariants.
production_tools = (ROOT / "src/YangCAD.AutoCAD/Mcp/Tools/CadProductionTools.cs").read_text(encoding="utf-8")
for needle, label in [
    ('Name = "cad.polyline.inspect"', "polyline inspect"),
    ('Name = "cad.polyline.clean"', "polyline clean"),
    ('Name = "cad.layer.audit"', "layer audit"),
    ('Name = "cad.layer.cleanup"', "layer cleanup"),
    ('Name = "cad.drawing.audit"', "drawing audit"),
    ('Database.Purge', "AutoCAD purge safety filter"),
    ('CadTransactionRunner.Write', "production write transaction/dry-run boundary"),
    ('GetStartWidthAt', "segment width inspection"),
    ('p.HasBulges', "exact non-zero bulge fail-closed guard"),
    ('p.HasWidth', "AutoCAD width semantic guard"),
    ('HasNonZeroSegmentWidths', "exact non-zero segment width fail-closed guard"),
    ('perpendicularDistance > tolerance', "absolute-distance collinearity tolerance"),
]:
    if needle not in production_tools:
        errors.append("TASK-006A Production Hygiene invariant missing: " + label)
for tool_name in [
    "cad.polyline.inspect", "cad.polyline.clean", "cad.layer.audit", "cad.layer.cleanup", "cad.drawing.audit",
]:
    if tool_name not in server:
        errors.append("TASK-006A Python MCP tool missing: " + tool_name)
if '"cad.polyline.clean"' not in gateway_py or '"cad.layer.cleanup"' not in gateway_py:
    errors.append("TASK-006A write tools missing Python process/document binding")
if 'Math.Abs(p.GetBulgeAt(i)) > tolerance' in production_tools:
    errors.append("TASK-006A bulge fail-closed must not use positional tolerance")
if 'Math.Abs(p.GetStartWidthAt(i)) > tolerance' in production_tools or 'Math.Abs(p.GetEndWidthAt(i)) > tolerance' in production_tools:
    errors.append("TASK-006A width fail-closed must not use positional tolerance")
if 'tolerance * scale * scale' in production_tools:
    errors.append("TASK-006A collinearity must use absolute drawing-distance tolerance")
if "new CadPolylineCleanTool()" not in runtime_host or "new CadLayerCleanupTool()" not in runtime_host:
    errors.append("TASK-006A tools missing optional AutoCAD registration")
mcp_window_code = (ROOT / "src/YangCAD.AutoCAD/UI/McpWindow.xaml.cs").read_text(encoding="utf-8")
if "isCivil ? 62 : 32" not in mcp_window_code or "AutoCAD 期望 32 Tools" not in mcp_window_code or "Civil 3D 期望 62 Tools" not in mcp_window_code:
    errors.append("MCP window tool-count self-check must be 32/62 after leader repair MCP addition")

# TASK-006B Civil Production Read / Assist invariants.
civil_prod = (ROOT / "src/YangCAD.Civil3D/Tools/CivilProductionReadAssistTools.cs").read_text(encoding="utf-8")
for needle, label in [
    ('Name = "civil.feature_line.list"', "FeatureLine list"),
    ('Name = "civil.feature_line.get"', "FeatureLine get"),
    ('Name = "civil.feature_line.points"', "FeatureLine points"),
    ('Name = "civil.feature_line.diagnose"', "FeatureLine diagnose"),
    ('GetSitelessFeatureLineIds', "siteless FeatureLine discovery"),
    ('GetFeatureLineIds', "site FeatureLine discovery"),
    ('GetPoints(FeatureLinePointType.AllPoints)', "FeatureLine point API"),
    ('Name = "civil.corridor.list"', "Corridor list"),
    ('Name = "civil.corridor.get"', "Corridor get"),
    ('Name = "civil.corridor.baselines"', "Corridor baselines"),
    ('Name = "civil.corridor.diagnose"', "Corridor diagnose"),
    ('CorridorCollection', "Corridor discovery"),
    ('BaselineRegions', "Corridor region API"),
    ('IsFeatureLineBased()', "feature-line based corridor baseline detection"),
    ('IsOutOfDate', "Corridor rebuild-state diagnostic"),
    ('region_gap', "Corridor region gap diagnostic"),
    ('region_overlap', "Corridor region overlap diagnostic"),
]:
    if needle not in civil_prod:
        errors.append("TASK-006B Civil Production Read/Assist invariant missing: " + label)
if "CadTransactionRunner.Write" in civil_prod:
    errors.append("TASK-006B Civil Production Read/Assist must remain read-only")
for tool_name in [
    "civil.feature_line.list", "civil.feature_line.get", "civil.feature_line.points", "civil.feature_line.diagnose",
    "civil.corridor.list", "civil.corridor.get", "civil.corridor.baselines", "civil.corridor.diagnose",
]:
    if tool_name not in server:
        errors.append("TASK-006B Python MCP tool missing: " + tool_name)
for registration in [
    "new CivilFeatureLineListTool()", "new CivilFeatureLineGetTool()", "new CivilFeatureLinePointsTool()",
    "new CivilFeatureLineDiagnoseTool()", "new CivilCorridorListTool()", "new CivilCorridorGetTool()",
    "new CivilCorridorBaselinesTool()", "new CivilCorridorDiagnoseTool()",
]:
    if registration not in civil_bootstrap:
        errors.append("TASK-006B Civil registration missing: " + registration)
for tool_name in [
    "civil.feature_line.list", "civil.feature_line.get", "civil.feature_line.points", "civil.feature_line.diagnose",
    "civil.corridor.list", "civil.corridor.get", "civil.corridor.baselines", "civil.corridor.diagnose",
]:
    if f'"{tool_name}"' in gateway_py.split("WRITE_TOOLS =", 1)[1].split("}", 1)[0]:
        errors.append("TASK-006B read-only tool must not be in WRITE_TOOLS: " + tool_name)

# TASK-006C Batch DWG invariants.
batch_py = (ROOT / "mcp/yangcad_mcp/batch.py").read_text(encoding="utf-8")
batch_worker = (ROOT / "src/YangCAD.BatchWorker/BatchWorkerCommand.cs").read_text(encoding="utf-8")
batch_ops = (ROOT / "src/YangCAD.BatchWorker/BatchOperations.cs").read_text(encoding="utf-8")
batch_csproj = (ROOT / "src/YangCAD.BatchWorker/YangCAD.BatchWorker.csproj").read_text(encoding="utf-8")
build_ps = (ROOT / "tools/Build.ps1").read_text(encoding="utf-8-sig")
package_ps = (ROOT / "tools/Package-Bundle.ps1").read_text(encoding="utf-8-sig")
for needle, label in [
    ('name="batch.status"', "batch status MCP"),
    ('name="batch.dwg.audit"', "batch audit MCP"),
    ('name="batch.dwg.hygiene"', "batch hygiene MCP"),
    ('uses_live_gateway', "batch independent gateway contract"),
    ('AcCoreConsole.exe', "AcCoreConsole process host"),
    ('YANGCAD_BATCH_JOB', "per-process job handoff"),
    ('one AcCoreConsole process per DWG', "per-DWG process isolation"),
    ('input_unchanged', "source DWG hash guard"),
    ('real hygiene 必须提供 output_dir', "copy-only real write guard"),
    ('_plan_outputs', "whole-batch output preflight"),
    ('_is_same_or_descendant', "folder/output recursion guard"),
    ('_remove_partial_output', "failed partial-output cleanup"),
    ('command += ["/readonly"]', "dry-run Core Console readonly switch"),
    ('YANGCAD_ACCORE_LANGUAGE', "optional language override without forced language pack"),
]:
    if needle not in server + batch_py:
        errors.append("TASK-006C Batch invariant missing: " + label)
for needle, label in [
    ('CommandMethod("YANGCAD_BATCH_RUN"', "BatchWorker command"),
    ('doc.LockDocument()', "BatchWorker DocumentLock"),
    ('BatchOperations.Hygiene', "BatchWorker hygiene"),
    ('db.SaveAs(fullOutput, saveVersion)', "output-copy SaveAs"),
    ('output_path must differ from input_path', "in-place overwrite rejection"),
    ('db.OriginalFileVersion', "preserve source DWG version"),
]:
    if needle not in batch_worker:
        errors.append("TASK-006C BatchWorker invariant missing: " + label)
for needle, label in [
    ('db.Purge', "batch layer purge safety"),
    ('p.HasBulges', "batch bulge fail-closed skip"),
    ('HasNonZeroWidths', "batch width fail-closed skip"),
    ('perpendicularDistance', "batch absolute collinearity distance"),
    ('tr.Abort()', "batch dry-run rollback"),
    ('ProxyEntity', "batch proxy save guard"),
]:
    if needle not in batch_ops:
        errors.append("TASK-006C BatchOperations invariant missing: " + label)
if 'System.Text.Json' in batch_csproj:
    errors.append("TASK-006C BatchWorker csproj must not add System.Text.Json package/reference")
if 'YangCAD.BatchWorker.csproj' not in build_ps or 'build-batch-$Year.binlog' not in build_ps:
    errors.append("TASK-006C Build.ps1 must build BatchWorker separately")
if 'YangCAD.BatchWorker.dll' not in package_ps:
    errors.append("TASK-006C bundle packaging must include BatchWorker DLL")
if 'YangCAD.BatchWorker' in bundle_template:
    errors.append("TASK-006C BatchWorker must not be a startup ComponentEntry")
for name in batch_tools:
    if f'Name = "{name}"' in all_cs or f'Name="{name}"' in all_cs:
        errors.append("TASK-006C batch MCP tool must not register in live AutoCAD/Civil ToolRegistry: " + name)
if 'from .gateway import call' in batch_py or 'call(' in batch_py:
    errors.append("TASK-006C batch.py must not route work through live Gateway")
if 'output_path = input_path' in batch_py:
    errors.append("TASK-006C must not support in-place overwrite in v1")

# TASK-007 Agent Extension Workflow invariants.
ext_workflow = (ROOT / "mcp/yangcad_mcp/extensions.py").read_text(encoding="utf-8")
ext_contracts = (ROOT / "src/YangCAD.AutoCAD/Mcp/Extensions/ExtensionContracts.cs").read_text(encoding="utf-8")
ext_host = (ROOT / "src/YangCAD.AutoCAD/Mcp/Extensions/ExtensionHost.cs").read_text(encoding="utf-8")
for tool_name in extension_workflow_tools:
    if f'name="{tool_name}"' not in server:
        errors.append("TASK-007 MCP-local workflow tool missing: " + tool_name)
    if f'Name = "{tool_name}"' in all_cs or f'Name="{tool_name}"' in all_cs:
        errors.append("TASK-007 local workflow tool must not register in live ToolRegistry: " + tool_name)
for needle, label in [
    ('allow_install', "explicit install consent"),
    ('ExtensionWorkspace', "versioned extension workspace"),
    ('DllSha256', "DLL integrity manifest"),
    ('generated-body-v1', "generated source policy marker"),
    ('_validate_body_structure', "method-body scope escape guard"),
    ('context.RecordChange(', "write preview/diff ledger requirement"),
    ('YANGCAD_EXTENSION_HOST_DLL', "host assembly discovery override"),
    ('<Private>false</Private>', "no host/Autodesk DLL copy-local references"),
    ('Extension source policy rejected', "conservative source policy"),
]:
    if needle not in ext_workflow:
        errors.append("TASK-007 extension builder invariant missing: " + label)
for forbidden, label in [
    ('from .gateway import call', "builder must not route build/install through live Gateway"),
    ('Assembly.LoadFrom', "Python builder must never load generated DLL"),
]:
    if forbidden in ext_workflow:
        errors.append("TASK-007 extension builder boundary violation: " + label)
for needle, label in [
    ('RequiresErase', "extension erase capability"),
    ('RecordChange', "extension change ledger"),
    ('MaxChanges = 500', "change ledger bound"),
]:
    if needle not in ext_contracts:
        errors.append("TASK-007 extension contract invariant missing: " + label)
for needle, label in [
    ('ManifestPath', "managed sidecar manifest"),
    ('ComputeSha256', "host DLL integrity verification"),
    ('TargetMatchesHost', "Civil-only extension host guard"),
    ('YearMatchesHost', "per-year extension host guard"),
    ('VerifyHostContractSha', "current Host DLL SHA binding"),
    ('ValidateManifestAgainstDescriptor', "manifest/descriptor capability binding"),
    ('dry_run_host_transaction_abort', "dry-run rollback evidence"),
    ('single_host_transaction', "one Tool / one transaction Undo scope"),
    ('extension_reported_json_safe_evidence_not_independent_host_snapshot', "change ledger semantics"),
    ('descriptor.RequiresErase', "erase gate lookup"),
]:
    if needle not in ext_host:
        errors.append("TASK-007 ExtensionHost invariant missing: " + label)
if 'UserSettings.Load().AllowErase' not in ext_host:
    errors.append("TASK-007 RequiresErase must honor independent AllowErase permission")
if 'Assembly.LoadFrom' not in ext_host or 'CadTransactionRunner.Write' not in ext_host:
    errors.append("TASK-007 must preserve versioned DLL load + host-owned transaction boundary")

# stdio transport stdout is protocol-only.
for path in (ROOT / "mcp/yangcad_mcp").rglob("*.py"):
    try:
        tree = ast.parse(path.read_text(encoding="utf-8-sig"), filename=str(path))
        for node in ast.walk(tree):
            if isinstance(node, ast.Call) and isinstance(node.func, ast.Name) and node.func.id == "print":
                errors.append("stdout print in MCP package: " + str(path.relative_to(ROOT)))
    except Exception:
        pass

print(f"cs={len(list(ROOT.rglob('*.cs')))} xaml={len(list(ROOT.rglob('*.xaml')))} python={len(pys)} themes={len(themes)} tools={len(tools)} xml={len(xmls)} errors={len(errors)}")
for error in errors:
    print("ERROR", error)
sys.exit(1 if errors else 0)
