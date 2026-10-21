# CLAUDE.md

先读 `AGENTS.md`。

YangCAD 当前已完成 Toolbox Core Foundation，项目处于：

> **ON-DEMAND DEVELOPMENT**

不要恢复旧 YANG-AGENT_CAD 历史，不要自动开始 Round 4/5，也不要从 Backlog 自选功能。

## 强制规范

所有功能开发必须遵循：

`docs/AI_FEATURE_DEVELOPMENT_STANDARD.md`

关键点：

- 默认每次 1–3 个 Tool；
- 真实需求驱动；
- GUI/MCP 共享 Service，不互相调用；
- Service 不取 `MdiActiveDocument`；
- Ordinary Write 不增加确认；
- High-Destructive 只用 Preview/dry_run 或最多一次确认；
- 不做 DI/CQRS/EventBus/RBAC 等无收益平台化；
- 每次正式包更新事实文档与交接；
- 65/28/58 非必要不变；
- 不擅自推送 Git。

事实源：

- `docs/CURRENT_STATE.md`
- `docs/AI_FEATURE_DEVELOPMENT_STANDARD.md`
- `docs/DECISIONS.md`
- `tasks/CURRENT_TASK.md`
- `docs/HANDOFF.md`
