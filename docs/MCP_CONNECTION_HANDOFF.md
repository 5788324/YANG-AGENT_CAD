# YangCAD MCP Connection Handoff

状态：**ACTIVE**

## 目的

让用户在当前 AutoCAD / Civil 3D 会话中点击：

> YangCAD Ribbon → **MCP** → **复制 MCP 配置**

然后把剪贴板整段内容发给本机 AI。

AI 不需要知道 Gateway Token，也不需要手工找端口。

## 配置结构

复制内容包含标准形态：

```json
{
  "mcpServers": {
    "yangcad": {
      "command": "powershell.exe",
      "args": [
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        "<installed bundle>\\Contents\\Mcp\\Start-YangCAD-Mcp.ps1"
      ],
      "env": {
        "YANGCAD_INSTANCE_PID": "<current acad.exe PID>"
      }
    }
  }
}
```

不同 AI 客户端如果不是 JSON 配置格式，只需要保留相同：

- command
- args
- env

由 AI 自己转换为该客户端的 MCP 配置格式。

## 为什么不复制 Token

YangCAD stdio bridge 会从：

`%LOCALAPPDATA%\YangCADAgent\instances\*.json`

发现指定 PID 的当前实例，并通过 `/health` 校验：
- PID
- port
- process session
- document session
- tool availability

Gateway Token 只存在用户本机 instance descriptor / request header 路径。

Clipboard handoff 不需要、也不允许包含 Token。

## PID Binding

复制时自动加入：

`YANGCAD_INSTANCE_PID=<当前 PID>`

目的：
- 多个 AutoCAD/Civil 进程同时打开时不串实例；
- 写操作不会静默切到另一个 CAD；
- AI 明确连接用户在独立 MCP 窗口中刚刚点击“复制 MCP 配置”的进程。

AutoCAD/Civil 3D 重启后 PID 改变：
- 旧配置应 fail safe；
- 用户重新点击一次复制即可。

## Runtime

正式 Bundle：

`%APPDATA%\Autodesk\ApplicationPlugins\YangCADAgent.bundle\Contents\Mcp`

Launcher：

`Start-YangCAD-Mcp.ps1`

完整 runtime 健康检查覆盖 launcher、pyproject 和 `yangcad_mcp/*.py`。安装目录任一核心文件缺失或与当前 DLL 嵌入版本不一致时，改用 `%LOCALAPPDATA%\YangCADAgent\runtime\Mcp` 的 embedded self-heal 副本。

Python runtime：

`%LOCALAPPDATA%\YangCADAgent\mcp-runtime`

首次缺少 Python MCP 依赖时在用户目录安装：
`mcp[cli]>=2.0,<3`

要求：
Python 3.10+。

## AI 验证顺序

连接后：

1. `cad.status`
2. `cad.document.info`
3. 必要的只读 Tool
4. 写 Tool 前重新确认目标 DWG binding
5. 高风险 Tool 继续 obey YangCAD permission / dry_run contract
