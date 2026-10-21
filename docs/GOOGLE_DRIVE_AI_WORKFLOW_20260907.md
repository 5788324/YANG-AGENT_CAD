# YangCAD Google Drive AI 协作规则

日期：2026-09-07

## 目标

从本候选开始，YangCAD 的 ChatGPT / Gemini / Codex 交接优先通过 Google Drive 完成，减少用户手工下载、上传和转发文件。

## Drive 工作目录

项目工作目录：`YangCAD_YangTools_AI_Workspace/04_YangCAD_Development/`

建议固定结构：

- `00_CURRENT/`：唯一当前状态、AI_START_HERE、当前候选 SHA
- `01_SOURCE/`：当前/历史开发候选源码 ZIP
- `02_DOCS/`：变更报告、功能说明、Coverage、协议说明
- `03_TASKS/`：Gemini/Codex 执行任务
- `04_EVIDENCE/`：Windows Build/Deploy/Runtime 原始证据

正式稳定版仍放在既有 `02_YangCAD/`，只有 ChatGPT 最终验收 GO 后才升级正式稳定基线。

## AI 接手顺序

任何执行 AI 开始前先读：

1. `04_YangCAD_Development/00_CURRENT/AI_START_HERE.md`
2. `04_YangCAD_Development/00_CURRENT/CURRENT_STATUS.md`
3. 当前候选 SHA 文件
4. `02_DOCS/STATIC_CHANGE_REPORT...`
5. 与任务对应的 `03_TASKS/*.md`

禁止仅依据聊天里的旧文件名继续工作。

## 写入规则

- 新源码候选：上传 `01_SOURCE/`，必须带 SHA-256。
- 实机证据：全部写 `04_EVIDENCE/<日期_任务>/`；没有原始证据不得 PASS。
- Gemini 只执行，不得自行 FINAL GO。
- ChatGPT 负责复核证据与升级 `00_CURRENT`。
- Codex 若做 Git，同样以 `00_CURRENT` 指定候选为输入。
- 不覆盖正式稳定源码；开发候选用新文件名。
- 不把 Build PASS 当 Runtime PASS。

## 当前候选

本轮候选同时包含：

- CADTool ↔ YangTools MCP/Exchange 业务对齐；
- `图纸比例转换 / YGSCALE / cad.scale.convert` 双模式 V1。

状态：`IMPLEMENTED / STATIC PASS / WINDOWS RUNTIME REQUIRED`。
