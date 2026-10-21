from __future__ import annotations
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
XAML = (ROOT / "src/YangCAD.AutoCAD/UI/McpWindow.xaml").read_text(encoding="utf-8-sig")
CODE = (ROOT / "src/YangCAD.AutoCAD/UI/McpWindow.xaml.cs").read_text(encoding="utf-8-sig")
CONTROL = (ROOT / "src/YangCAD.AutoCAD/UI/ControlCenterWindow.xaml").read_text(encoding="utf-8-sig")
RIBBON = (ROOT / "src/YangCAD.AutoCAD/UI/RibbonBootstrapper.cs").read_text(encoding="utf-8-sig")
COMMANDS = (ROOT / "src/YangCAD.AutoCAD/Commands/ControlCenterCommands.cs").read_text(encoding="utf-8-sig")
GATEWAY = (ROOT / "src/YangCAD.AutoCAD/Mcp/LocalGateway.cs").read_text(encoding="utf-8-sig")
BUILDER = (ROOT / "src/YangCAD.AutoCAD/Mcp/McpClientConfigBuilder.cs").read_text(encoding="utf-8-sig")
PACKAGE = (ROOT / "tools/Package-Bundle.ps1").read_text(encoding="utf-8-sig")
MATERIALIZER = (ROOT / "src/YangCAD.AutoCAD/Infrastructure/RuntimeAssetMaterializer.cs").read_text(encoding="utf-8-sig")
CSPROJ = (ROOT / "src/YangCAD.AutoCAD/YangCAD.AutoCAD.csproj").read_text(encoding="utf-8-sig")
VERIFY_RELEASE = (ROOT / "tools/Verify-Release.ps1").read_text(encoding="utf-8-sig")
INSTALLER = (ROOT / "src/YangCAD.Setup/InstallerService.cs").read_text(encoding="utf-8-sig")
LAUNCHER_PATH = ROOT / "mcp/Start-YangCAD-Mcp.ps1"
LAUNCHER = LAUNCHER_PATH.read_text(encoding="utf-8-sig")
GUIDE = (ROOT / "docs/USER_GUIDE.md").read_text(encoding="utf-8-sig")
STANDARD = (ROOT / "docs/AI_FEATURE_DEVELOPMENT_STANDARD.md").read_text(encoding="utf-8-sig")

checks: list[tuple[str, bool]] = [
    ("ribbon exposes standalone MCP core button", 'Button("MCP", "YANGCAD_MCP "' in RIBBON and '[CommandMethod("YANGCAD_MCP")]' in COMMANDS),
    ("control center no longer duplicates MCP page", 'Tag="MCP"' not in CONTROL and '复制 MCP 配置' not in CONTROL and '允许 MCP 修改当前 DWG' not in CONTROL),
    ("dedicated MCP window exposes copy/config/permissions", 'Content="复制 MCP 配置"' in XAML and 'Click="CopyMcpConfigClick"' in XAML and '允许 MCP 修改当前 DWG' in XAML),
    ("copy handler uses dedicated builder", "CopyMcpConfigClick" in CODE and "McpClientConfigBuilder.BuildCopyText()" in CODE),
    ("gateway status uses real running state", "public bool IsRunning" in GATEWAY and "gateway.IsRunning" in CODE and "CAD 网关就绪" in XAML),
    ("self-check uses host-specific 31/61 expectation", "isCivil ? 61 : 31" in CODE and "AutoCAD 期望 31 Tools" in CODE and "Civil 3D 期望 61 Tools" in CODE),
    ("self-check verifies critical tools", all(x in CODE for x in ['"cad.status"','"cad.entity.get"','"cad.entity.update"','"project.revit.prepare"','"project.image.embed"','"cad.scale.convert"','"civil.status"','"civil.surface.list"','"civil.pipe.get"'])),
    ("config contains standard mcpServers shape", '"mcpServers"' in BUILDER and '"yangcad"' in BUILDER),
    ("config launches powershell bridge", '"powershell.exe"' in BUILDER and '"Start-YangCAD-Mcp.ps1"' in BUILDER),
    ("config binds exact current process", '"YANGCAD_INSTANCE_PID"' in BUILDER and "snapshot.ProcessId" in BUILDER),
    ("clipboard config does not expose gateway token", "snapshot.Token" not in BUILDER and ".Token" not in BUILDER and "Bearer" not in BUILDER),
    ("config requires live gateway", "RuntimeHost.Gateway.IsRunning" in BUILDER),
    ("config documents recopy after cad restart", "重启后 PID 会变化" in BUILDER),
    ("bundle packages repository independent mcp bridge", '"Contents\\Mcp"' in PACKAGE and '"mcp"' in PACKAGE and "Start-YangCAD-Mcp.ps1" in PACKAGE),
    ("partial installed runtime triggers health fallback", "IsMcpRuntimeHealthy(installedRoot)" in BUILDER and "incomplete or stale" in BUILDER),
    ("runtime health validates all embedded assets by content", "foreach (var asset in McpAssets)" in MATERIALIZER and "SameContent(target, memory.ToArray())" in MATERIALIZER),
    ("dll-only deployment self-heals embedded mcp runtime", "RuntimeAssetMaterializer.EnsureMcpLauncher()" in BUILDER and "EnsureMcpLauncher" in MATERIALIZER),
    ("materialized runtime is revalidated", "IsMcpRuntimeHealthy(root)" in BUILDER and "materialize 后完整性校验失败" in BUILDER),
    ("mcp runtime is embedded in autocad assembly", "YangCAD.Runtime.Mcp.Start-YangCAD-Mcp.ps1" in CSPROJ and "YangCAD.Runtime.Mcp.yangcad_mcp.server.py" in CSPROJ),
    ("release verification requires complete mcp runtime", all(x in VERIFY_RELEASE for x in [r"Contents\Mcp\Start-YangCAD-Mcp.ps1", r"Contents\Mcp\pyproject.toml", r"Contents\Mcp\yangcad_mcp\server.py", r"Contents\Mcp\yangcad_mcp\gateway.py"])),
    ("installer requires complete mcp runtime", all(x in INSTALLER for x in ["pyproject.toml", "__init__.py", "batch.py", "extensions.py", "gateway.py", "server.py", "Start-YangCAD-ImageEmbed.ps1"])),
    ("self-heal runtime publish avoids delete gap", "File.Replace(temp, target, null)" in MATERIALIZER and "File.Delete(target)" not in MATERIALIZER),
    ("launcher exists in mcp source", LAUNCHER_PATH.exists()),
    ("launcher uses user local runtime venv", r'YangCADAgent\mcp-runtime' in LAUNCHER and r'Scripts\python.exe' in LAUNCHER),
    ("launcher requires python 3.10 and MCP major v2", "sys.version_info >= (3,10)" in LAUNCHER and "importlib.metadata" in LAUNCHER and "int(x.group(1)) == 2" in LAUNCHER and '"mcp[cli]>=2.0,<3"' in LAUNCHER),
    ("launcher starts stdio module without printing config secrets", "-m yangcad_mcp.server" in LAUNCHER and "Write-Host" not in LAUNCHER and "Write-Output" not in LAUNCHER),
    ("user guide states token-free pid-bound behavior", "不包含 Gateway Token" in GUIDE and "YANGCAD_INSTANCE_PID" in GUIDE),
    ("all ai development standard is authoritative", "AUTHORITATIVE / REQUIRED" in STANDARD and "1–3 个 Tool" in STANDARD and "GUI 不得调用 MCP Tool" in STANDARD),
]

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(("PASS" if ok else "FAIL") + ": " + name)
if failed:
    raise SystemExit("mcp copy config policy failed: " + ", ".join(failed))
print(f"mcp copy config policy: {len(checks)}/{len(checks)} PASS")
