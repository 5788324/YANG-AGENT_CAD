# Document Authority

更新时间：2026-08-31

## 当前权威顺序

1. `README.md`
2. `PROJECT.md`
3. `docs/CURRENT_STATE.md`
4. `docs/DECISIONS.md`
5. `docs/AI_FEATURE_DEVELOPMENT_STANDARD.md`
6. `docs/AGENT_RULES.md`
7. `docs/COMPATIBILITY.md`
8. `docs/TOOLBOX_ARCHITECTURE.md`
9. `docs/RVT_EXCHANGE_V1.md`（仅当当前/后续任务涉及 RVT Exchange）
10. `docs/YANGTOOLS_3.1.36_RVT_INTEROP_20260901.md`（CAD→Revit 原生重建对接字段与 fixture）
11. `tasks/CURRENT_TASK.md`
12. `docs/HANDOFF.md`
13. `WORKLOG.md`

## 单主线规则

旧 YANG-AGENT_CAD 历史、旧 archive/backup 分支、旧 handoff、旧 project-state 文档**不属于当前事实体系**，不得恢复后混入当前项目。

新对话如果遇到旧 SHA / 旧文档链接：
- 忽略；
- 不据此回滚当前状态；
- 不把旧代码当候选实现。

当前事实只跟随新 `main`。
