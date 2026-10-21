# FEATURE-INFRA-MCP-CONFIG-001 — Self-Heal Windows Retest

状态：**READY / WINDOWS RETEST**

## 背景

用户实机已复现：

> 复制 MCP配置失败：未找到 YangCAD MCP 启动脚本。

根因：当前已加载 DLL 包含复制按钮，但安装 bundle 缺 `Contents\Mcp\Start-YangCAD-Mcp.ps1`。

## 本轮修复

- release bundle 继续强制包含 `Contents\Mcp`；
- Setup/Verify-Release 对 MCP/ImageEmbed runtime 缺失 fail-closed；
- `YangCAD.AutoCAD.dll` 同时嵌入 MCP runtime；
- bundle 缺脚本时按钮自动释放到：

```text
%LOCALAPPDATA%\YangCADAgent\runtime\Mcp
```

再生成配置。

## P0-A 完整安装路径

1. 用本候选生成 Release；
2. Verify-Release 必须确认：
   - `Contents\Mcp\Start-YangCAD-Mcp.ps1`
   - `Contents\ImageEmbed\Start-YangCAD-ImageEmbed.ps1`
3. Setup Repair/Install；
4. 打开 YangCAD Ribbon → MCP，确认“CAD 网关就绪”，点击“复制 MCP 配置”；
5. JSON command 指向安装 bundle 的 `Contents\Mcp`；
6. 本机 AI 添加 MCP；
7. `cad.status` PASS；
8. clipboard 0 Gateway Token/Bearer。

## P0-B Self-Heal 路径

在测试环境中：

1. 保留新版 `YangCAD.AutoCAD.dll`；
2. 分两次测试：A) 临时重命名/移走整个 `Contents\Mcp`；B) 恢复目录后只删除/篡改 `yangcad_mcp\server.py`，但保留 `Start-YangCAD-Mcp.ps1`；
3. 打开 YangCAD Ribbon → MCP，点击“复制 MCP 配置”；
4. 两种损坏都不应继续使用不健康的安装 runtime，也不应出现“未找到启动脚本”；
5. `%LOCALAPPDATA%\YangCADAgent\runtime\Mcp` 自动生成：
   - Start-YangCAD-Mcp.ps1
   - pyproject.toml
   - yangcad_mcp/*.py
6. 复制 JSON command 指向 self-heal launcher；
7. 本机 AI 启动并 `cad.status` PASS；
8. CAD PID 绑定正确；
9. Token 0 leak。

测试后恢复安装 bundle。

## Closure

A + B 均 PASS 后：

> FEATURE-INFRA-MCP-CONFIG-001 = CLOSED / GO / PASS
