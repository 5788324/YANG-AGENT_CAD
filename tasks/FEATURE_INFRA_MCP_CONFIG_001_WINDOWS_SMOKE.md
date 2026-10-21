# FEATURE-INFRA-MCP-CONFIG-001 — Windows Smoke

日期：2026-08-28  
状态：**SUPERSEDED BY `FEATURE_INFRA_MCP_CONFIG_001_WINDOWS_RETEST.md`**

## 输入

使用本轮 Static Candidate 完整源码。

## P0 Build

2021–2024：
- AutoCAD clean build
- Civil 2022–2024 clean build
- Batch clean build
- external System.Text.Json.dll = 0

Package Bundle 至少验证 2024，并确认：

`YangCADAgent.bundle\Contents\Mcp\Start-YangCAD-Mcp.ps1`

以及：
- `pyproject.toml`
- `yangcad_mcp\server.py`
- `gateway.py`
存在。

## P0 Install / UI

2024：

1. 安装/Update 最新 Bundle。
2. 打开真实 DWG。
3. 打开 YangCAD Ribbon → MCP。
4. 显示“CAD 网关就绪”。
5. “复制 MCP 配置”按钮可见。
6. 点击后 Clipboard 成功。

## Clipboard Assertions

必须包含：

- `mcpServers`
- `yangcad`
- `powershell.exe`
- `Start-YangCAD-Mcp.ps1`
- `YANGCAD_INSTANCE_PID`
- 当前 `acad.exe` PID

必须不包含：

- instance JSON 的 Token 值
- `Bearer <token>`
- Authorization secret

保存一份**已脱敏** clipboard evidence；不要把真实 Token 写报告。

## Local AI / MCP Client End-to-End

把复制内容原样发给本机 AI。

要求 AI：

1. 根据其客户端格式添加 `yangcad` MCP Server；
2. 使用相同 command / args / env；
3. 启动 server；
4. 调用 `cad.status`；
5. 调用 `cad.document.info`；
6. 确认 PID/Product/Document 与当前 MCP 窗口一致。

如果首次启动需要安装 Python `mcp[cli]`：
- 允许；
- 安装目录只能是 `%LOCALAPPDATA%\YangCADAgent\mcp-runtime`；
- 不得写进 Bundle；
- stdout 不得混入 setup 日志破坏 MCP 协议。

第二次启动必须复用 runtime，不重复安装。

## PID Safety

必须验证：

1. 复制配置时记录 PID=A。
2. 关闭 CAD A。
3. 使用旧配置启动/调用 YangCAD MCP。
4. 必须失败或明确找不到 PID=A，**不得自动切到另一个 CAD 进程**。
5. 重新启动 CAD 得 PID=B。
6. 重新点“复制 MCP 配置”。
7. 新配置 PID=B。
8. `cad.status` PASS。

## Regression

- stdio tools = 65
- AutoCAD `/health=28`
- Civil `/health=58`
- existing Toolbox opens
- Control Center themes/pages normal
- runtime.log 0 unhandled error
- token quick scan 0 leak

## 2021–2023

因为 UI 代码是 net48 共用：
- exact-year clean build 必做
- minimum Control Center open smoke 建议 2021
- 不需要重复 full MCP client end-to-end

## PASS 定义

全部 P0 通过后：

> FEATURE-INFRA-MCP-CONFIG-001 = CLOSED / GO / PASS

## 回传

- full source ZIP
- Outer SHA
- internal SHA
- build/package result
- clipboard assertions
- local AI/MCP client result
- PID safety result
- production diff
- docs updated
