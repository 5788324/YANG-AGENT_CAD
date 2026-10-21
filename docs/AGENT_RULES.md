# Agent 开发规则

所有功能开发的完整规范以 `docs/AI_FEATURE_DEVELOPMENT_STANDARD.md` 为准；本文保留底层边界摘要。

## 强制边界

- MCP/HTTP 后台线程不得直接访问 AutoCAD/Civil API。
- MCP/AI typed write Tool：保持 DocumentLock + Transaction + rollback + operation log；适用时支持 dry_run。
- Toolbox GUI 普通写工具：Transaction + 基础 Undo 即可；不强制 operation log / dry_run。高破坏工具按需 Preview/dry_run/一次确认。
- 跨请求实体 ID 使用 Handle。
- 不默认开放任意命令/eval/shell。
- 主题资源不得注入 AutoCAD 全局 Application Resources。
- 2021–2027 每年独立编译；未安装年份不得外推 runtime PASS。
- Autodesk DLL 不进仓库。
- 新 MCP Tool 必须更新 TOOL_INDEX；纯 GUI Toolbox Tool 若不暴露 MCP，可只登记 Toolbox Registry / backlog。
- 每轮更新 WORKLOG / CURRENT_STATE / HANDOFF / CURRENT_TASK。

## Tool 命名

MCP：
`cad.<domain>.<action>`、`civil.<domain>.<action>`、`batch.<domain>.<action>`、`dev.<domain>.<action>`。

Toolbox GUI 的 Tool ID 优先沿用相同 domain/action 命名，便于未来共享 Service，但不要求第一阶段自动生成 MCP schema。

## Git

- GitHub `main` 是唯一开发线和事实源。
- 个人项目不强制 branch/PR。
- 每轮少量清晰提交；不要逐文件碎提交。
- 旧 YANG-AGENT_CAD history/archive/backup 不恢复。
- Git 写入默认交给用户指定的 Windows/Codex 执行者；用户明确要求时 ChatGPT 才直接写仓库。

## Generic MCP

- `cad.property.set` 只能写 policy 判定的 public Autodesk setter；不可因“反射可见”直接放开。
- `cad.api.invoke` 不允许 ref/out/pointer、未知复杂参数和 DBObject 基础设施方法。
- `cad.command.run` 不得回退 `SendStringToExecute` 来伪造同步成功。
- 动态扩展必须使用 Host 提供的 Transaction；不要在扩展目录复制 YangCAD host DLL。
- 高频/复杂 Civil 操作经过验证后应从 generic fallback 沉淀为强类型 Tool 或共享 Service。

## Batch DWG

- `batch.*` 不得通过 live `gateway.call()` 执行。
- v1 每个 DWG 启动独立 AcCoreConsole，串行处理。
- real hygiene 必须显式 `allow_write=true`，只写新 output 副本；禁止 in-place。
- 输入文件 SHA before/after 不一致直接视为 P0。

## 文档事实规则

当前状态按以下权威顺序读取：

`README / PROJECT / CURRENT_STATE / DOCUMENT_AUTHORITY / DECISIONS / AGENT_RULES / COMPATIBILITY / TOOLBOX_ARCHITECTURE / CURRENT_TASK / HANDOFF / WORKLOG`

旧项目 history/archive/backup/旧 handoff/旧 project-state 不属于当前事实体系，不恢复、不混入。

Windows real fix 后如源码发生修改，必须回传完整 FINAL_SOURCE + Outer SHA，再做事实源收口。

## Compatibility Matrix

- build/runtime 分开判定。
- `NOT_INSTALLED` 永远不能写 PASS。
- 一次只验证一个精确年份宿主，记录 PID/version/product/document。
- 不跨年份复制 Autodesk/YangCAD DLL。
- 2021–2024 net48 不允许重新引入 external `System.Text.Json.dll`。
