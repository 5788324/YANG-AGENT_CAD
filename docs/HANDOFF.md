# 新对话 / 其他 AI 接手（YangCAD）

更新时间：2026-11-03 · 版本：`0.10.0-dev` · **开发预览，Windows 新功能未验收**。

## 接手前先做 4 件事

1. 从 GitHub `5788324/YANG-AGENT_CAD` 获取 `main`、未合并的 `feature/*` 与 Draft PR 的最新 SHA；不要从 ZIP 时间或旧摘要猜版本。
2. 读 `AGENTS.md`、`docs/CURRENT_STATE.md`、`docs/FEATURE_CATALOG.md`、`tasks/CURRENT_TASK.md`。
3. 对照 `src/YangCAD.AutoCAD/RuntimeHost.cs`、`mcp/yangcad_mcp/server.py`、`docs/MCP_COVERAGE_MATRIX.md` 核实真实工具合同：预计 74/37/67。
4. 若没有 Windows + Autodesk 宿主，则只可判静态结果，不可宣称实机成功。

## 现有程序架构

`Ribbon/Palette/Command → Service(Request,Result) → CadTransactionRunner → Autodesk DB`

`Python MCP stdio → local Gateway → CAD host MCP Adapter → 同一 Service`

各部分文件：

- 界面：`src/YangCAD.AutoCAD/UI/`，可停靠 PaletteSet + 控制中心 + 独立 MCP 窗口。
- 工具注册：`src/YangCAD.AutoCAD/Toolbox/ToolboxRegistry.cs`、`RuntimeHost.cs`。
- 业务：`src/YangCAD.AutoCAD/Services/Cad/`。
- CMD：`src/YangCAD.AutoCAD/Commands/`。
- MCP：`src/YangCAD.AutoCAD/Mcp/` 与 `mcp/yangcad_mcp/`。
- Windows 部署：`tools/Build.ps1`、`tools/Deploy-Dev.ps1`、`src/YangCAD.Setup/`。
- 第三方参考：`references/` 仅描述功能、来源、权限，不放保护代码。

## 当前接手任务

看 `tasks/CURRENT_TASK.md`。最近的尚未验证增量包括图片/外参内嵌、图层、QG、大样引线修复、比例转换、快捷键，以及此轮 UI 结构调整。

## 验收证据要求（不要让 AI 自己写 PASS 充数）

- 明确 source SHA、Build DLL SHA、Deploy DLL SHA、Loaded DLL SHA、CAD PID/宿主年款及会话。
- `YGKEYS` 实测 PGP 备份/冲突/REINIT；Ribbon 原生 Tab 保持；Palette 快捷命令真实执行。
- 图层/比例/引线/QG：保存前后对象的 Handle、实体类别、图层、几何/注释数据、真实一次 Undo。
- 图片内嵌：至少 Model/PaperSpace，源图片移开后重新打开 DWG 仍存在内嵌图像。
- CAD→Revit：实际 MAP 和真实 Revit 元素/日志，不能用手写汇总 JSON 替代。
- `NOT_TESTED`、`UNPROVEN` 不要擅自改为 `PASS`。

## 改动交接

推送 feature 分支并开 Draft PR；提交改动说明、静态日志、实际风险、下一轮 Windows 任务；同步 README（有产品变化时）、CURRENT_STATE、HANDOFF、CURRENT_TASK 和 WORKLOG。稳定后才合并 `main`。
