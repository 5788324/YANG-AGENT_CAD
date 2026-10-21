# YangCAD MCP Server 0.9.0

当前 stdio MCP Server 暴露 **68** 个 dotted-name Tool：

- **61** 个 live CAD/Civil Tool 通过 localhost Gateway；
- **3** 个 MCP-local Batch Tool：`batch.status`、`batch.dwg.audit`、`batch.dwg.hygiene`；
- **4** 个 MCP-local Extension Workflow Tool：`extension.status`、`extension.build`、`extension.install`、`extension.artifact.list`。

宿主 C# ToolRegistry 保持：plain AutoCAD **31**、Civil 3D **61**。`batch.*` / `extension.*` 不注册到 live `/health`。

0.9 对接补丁新增产品 Tool：`project.revit.prepare`、`project.image.embed`、`cad.scale.convert`；已有 Tool Name 保持兼容。

## Bundle launcher / Copy MCP Config

正式 YangCAD Bundle 会把本目录复制到：

`YangCADAgent.bundle\Contents\Mcp`

Control Center 的“复制 MCP 配置”会生成：
- `powershell.exe`
- `Start-YangCAD-Mcp.ps1`
- `YANGCAD_INSTANCE_PID=<当前 CAD PID>`

配置不包含 Gateway Token。

`Start-YangCAD-Mcp.ps1` 使用 `%LOCALAPPDATA%\YangCADAgent\mcp-runtime` 用户级 venv；首次启动缺依赖时安装 `mcp[cli]>=2.0,<3`，之后复用。

- `cad.scale.convert`：图纸比例转换（标准 1:1 / 缩尺绘图），默认 dry_run。
