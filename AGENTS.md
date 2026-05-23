# AGENTS.md

这是 Codex、Antigravity、Claude 或其他 AI 继续开发本项目时必须读取的工作规则。

## 用户背景

用户是代码小白和 CAD 新手。所有开发、判断、命令、测试、错误分析、交接都由 AI 主动完成。不要要求用户理解代码细节；需要用户参与时，只让用户确认高风险动作或提供 AutoCAD/图纸环境信息。

## 每次开始工作

1. 运行 `git status --short --branch`。
2. 运行 `git fetch` 和 `git pull --rebase`。
3. 阅读 `handoff/CURRENT_HANDOFF.md`。
4. 阅读 `docs/PROJECT_STATE.md`。
5. 查看最近一篇 `logs/worklog/YYYY-MM-DD.md`。
6. 如果本地有未提交改动，先判断是否属于用户/其他 AI 的工作，禁止随意覆盖。

## 每次结束工作

1. 运行必要测试或 `doctor` 自检。
2. 更新 `docs/PROJECT_STATE.md`。
3. 更新当天 `logs/worklog/YYYY-MM-DD.md`。
4. 更新 `handoff/CURRENT_HANDOFF.md`。
5. 提交 commit。
6. 推送到 GitHub。
7. 在最终回复里说明完成内容、测试结果、未完成事项。

## 执行原则

优先顺序：

1. 已有 MCP tool 或插件箱能力。
2. 生成临时 LISP，在当前图纸执行。
3. 需要批量时，用 accoreconsole 执行经过校验的 LISP。
4. 用户确认有用后，把临时脚本沉淀为插件箱插件。

## 安全原则

查询类任务可自动执行。当前图纸轻量修改可以自动执行，但必须记录日志。批量修改、删除、覆盖、保存、清理、替换属性、批量导出必须先 dry-run、备份并让用户确认。

任何 AI 生成的 LISP 首次只能作为临时脚本运行，不能直接进入插件箱。插件入箱必须有 manifest、参数 schema、风险等级、适用轨道和用户确认。

## 禁止事项

- 禁止在没有备份的情况下批量修改 DWG。
- 禁止自动删除用户图纸或备份。
- 禁止把客户 DWG、PDF、图纸资料提交到 GitHub。
- 禁止把 shell 暴露为泛用 MCP 能力给 AI 使用。
- 禁止在 accoreconsole 里使用交互函数或对话框。
- 禁止绕过错误码和任务日志直接汇报“完成”。

