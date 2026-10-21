# User Guide

## YangCAD Ribbon

当前只保留三个一级入口：

- **MCP**
- **工具箱**
- **控制中心**

## MCP

点击 YangCAD Ribbon 的 **MCP**。

MCP 窗口用于：

- 查看 CAD Gateway、当前 PID、DWG Session、Live Tool 数；
- **复制 MCP 配置**，直接交给本机 AI 添加 YangCAD MCP Server；
- 刷新、复制不含 Token 的诊断、自检、打开日志；
- 控制 MCP 写入 / 删除 / `cad.command.run` / `cad.extension.invoke` 权限。

状态文案 **CAD 网关就绪** 只表示 YangCAD localhost Gateway 正在监听；是否已有 AI 客户端连接，应以实际 MCP 调用结果为准。

隐藏诊断命令：`YANGCAD_MCP_STATUS`。

### 复制 MCP 配置

前提：

- 已打开一个真实 DWG；
- Ribbon → MCP 显示 **CAD 网关就绪**。

点击：

> **MCP → 复制 MCP 配置**

剪贴板会得到：

- 当前 CAD/Civil 进程 PID；
- host/document 摘要；
- 标准 `mcpServers.yangcad` JSON；
- `powershell.exe` MCP launcher command/args；
- `YANGCAD_INSTANCE_PID=<当前 PID>` 会话绑定。

把整段内容直接发给本机 AI，让它：

1. 按客户端格式添加 `yangcad` MCP server；
2. 启动 server；
3. 调用 `cad.status` 验证；
4. 写操作前先执行读取 Tool 建立目标 DWG 绑定。

### 安全

复制配置：

- **不包含 Gateway Token**；
- 不包含 Bearer header；
- MCP bridge 从 `%LOCALAPPDATA%\YangCADAgent\instances` 读取当前本机会话描述，并验证 `/health`。

配置绑定当前 PID。AutoCAD/Civil 3D 重启后 PID 会变化，请重新点击“复制 MCP 配置”。

### MCP Runtime / Self-Heal

正式 Bundle 应携带完整：

`%APPDATA%\Autodesk\ApplicationPlugins\YangCADAgent.bundle\Contents\Mcp`

YangCAD 会验证完整 runtime，而不是只检查 `Start-YangCAD-Mcp.ps1`。如果安装目录缺文件、内容陈旧或 DLL-only/dev 部署没有完整 runtime，会从当前 DLL 的嵌入资源恢复到：

`%LOCALAPPDATA%\YangCADAgent\runtime\Mcp`

启动脚本使用：

`%LOCALAPPDATA%\YangCADAgent\mcp-runtime`

维护用户级 Python venv。要求 Python 3.10+，并验证 Python package `mcp` major version = 2；不满足时按 `mcp[cli]>=2.0,<3` 修复。

## 工具箱

点击 YangCAD Ribbon 的 **工具箱**。

当前只保留两个功能：

### Revit 图纸预处理

- 原 DWG 不修改；
- 输出 `_RVT.dwg / _RVT_MAP.json / _RVT_REPORT.json`；
- 支持 Preview；
- 动态块、匿名块、Xref 等边界会在报告中提示。

### 批量图片内嵌

输入可以是单个 DWG 或一个文件夹。Preview 只列计划；Execute 启动独立 AutoCAD COM worker，将顶层外链 RasterImage 转换为 OLE 输出副本。

当前仍需 AutoCAD 2024 Windows 最终 OLE Smoke。大量任务期间不要同时使用 Windows 剪贴板。V1 对 clipped / brightness-contrast-fade / transparency / block-nested RasterImage 采用 fail-closed，默认不会留下半成功 DWG。

## 控制中心

点击 YangCAD Ribbon 的 **控制中心**。

控制中心只负责：

- 系统设置：墨曜 / 月瓷 / 雾蓝主题；
- 关于：版本、运行环境、日志目录。

MCP 功能不再重复放在控制中心。

## AI 遇到没有专用 Tool

推荐顺序：

1. `cad.object.describe(handle)`；
2. `cad.property.get` / `cad.property.set`；
3. `cad.api.describe` / `cad.api.invoke`；
4. 必要时在 Ribbon → MCP 中授权 `cad.command.run` 或 `cad.extension.invoke`。

通用写 Tool 默认 dry_run；先看 before/after 再 real。

## 日志

`%LOCALAPPDATA%\YangCADAgent\logs\runtime.log`

Token 不应出现在普通日志、复制诊断或“复制 MCP 配置”中。
