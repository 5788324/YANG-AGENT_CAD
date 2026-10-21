# AGENTS.md — YangCAD

## 强制读取顺序

1. README.md
2. PROJECT.md
3. docs/CURRENT_STATE.md
4. docs/DOCUMENT_AUTHORITY.md
5. docs/DECISIONS.md
6. docs/AI_FEATURE_DEVELOPMENT_STANDARD.md
7. docs/AGENT_RULES.md
8. docs/COMPATIBILITY.md
9. docs/TOOLBOX_ARCHITECTURE.md
10. docs/TOOL_INDEX.md
11. tasks/CURRENT_TASK.md
12. docs/HANDOFF.md
13. WORKLOG.md

## 单主线

- GitHub `main` 是唯一开发线。
- 不恢复旧 YANG-AGENT_CAD history/archive/backup。
- 不从旧 SHA/旧文档推断当前状态。

## 项目原则

- 个人工作插件箱，优先 AI 快速开发、迭代、维护、上线。
- 不直接从 MCP/HTTP background thread 访问 Autodesk API。
- 写操作保持正确 Document/Transaction/rollback/Undo。
- Batch 不破坏输入 DWG。
- 不跨年份复制 Autodesk/YangCAD runtime DLL。
- 不把 Autodesk DLL、token、runtime logs、bin/obj 提交 Git。
- GUI Toolbox 普通写工具不强制 dry_run；高破坏工具按需 Preview/确认。
- TASK-007 动态 Extension 安全机制不扩散到静态 Toolbox。
- GUI 与 MCP 共享 Service，但 GUI 不绕 MCP。
- 不为“架构完整”引入无实际收益的平台化层。
- 每轮保持小范围并更新 CURRENT_STATE/HANDOFF/WORKLOG。

## AI 功能开发规范

- 所有 AI 必须遵循 `docs/AI_FEATURE_DEVELOPMENT_STANDARD.md`。
- 默认每次 1–3 个 Tool；不得自动从 Backlog 继续开发。
- 正式打包必须同步 CURRENT_STATE / HANDOFF / WORKLOG / CURRENT_TASK 与完整源码 SHA。
