# YANG AGENT CAD

面向 AutoCAD 的 AI Agent 项目。目标是让代码小白、CAD 新手也能通过自然语言完成查询、绘图修改、批量处理、插件沉淀和错误回退。

## 项目原则

1. 用户只需要描述目标，AI 负责开发、执行、记录和交接。
2. 所有高风险 CAD 修改必须有备份、日志、错误码和回退方案。
3. 所有能力先注册到 MCP/插件箱，再由 AI 调度执行。
4. 执行优先级是：已有 MCP/插件能力优先，缺失时临时生成 LISP，批量任务使用 accoreconsole。
5. 每次开始工作先同步 GitHub，每次结束工作必须提交、推送、更新交接日志。

## 当前阶段

当前仓库处于初始化阶段。第一阶段目标是完成“安全可回滚 MVP”：

- AI 工作规范
- Codex / Antigravity 适配
- MCP 调度协议
- 插件箱规范
- LISP 执行规范
- accoreconsole 批量规范
- 任务日志、错误码、备份、回滚

## 给新对话或新 AI 的入口

请先阅读：

1. [AGENTS.md](AGENTS.md)
2. [docs/WORKPLAN.md](docs/WORKPLAN.md)
3. [handoff/CURRENT_HANDOFF.md](handoff/CURRENT_HANDOFF.md)
4. [docs/PROJECT_STATE.md](docs/PROJECT_STATE.md)

## 快速自检

Windows 用户可直接运行：

```cmd
scripts\doctor.cmd
scripts\test.cmd
```

