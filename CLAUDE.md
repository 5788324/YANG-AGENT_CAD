# YangCAD — AI 接手提示

先读 [`AGENTS.md`](AGENTS.md)，再看 [`docs/HANDOFF.md`](docs/HANDOFF.md) 和 [`tasks/CURRENT_TASK.md`](tasks/CURRENT_TASK.md)。

- 项目：个人 AutoCAD / Civil 3D 插件工具箱 + MCP，**0.10.0-dev**。
- Git：`main` 是稳定回退基线；`feature/*` + Draft PR 是当前未验收开发候选，不要只看 `main` 就判断功能缺失。
- 静态、Windows Build、Autodesk Runtime、E2E 是不同阶段；缺证据写 `NOT_TESTED`。
- GUI/Command/MCP 共用 CAD Service；不直接从 Python/Gateway 线程访问 Autodesk API。
- 原生 CAD 对象优先，写操作需考虑 Undo、异常回滚与源图保护。
- UI 中文、少参数、快捷入口；不为了设计复杂度重做架构。
- `references/` 只收录源泉设计/贱人工具箱的合法功能参考，禁止复制第三方保护程序或资产。
- 新需求先定位现有工具；不要擅自从 Backlog 连续开发未授权功能。

验收与 Git 具体规则：`docs/RELEASE_CHECKLIST.md`、`docs/GIT_WORKFLOW.md`。
