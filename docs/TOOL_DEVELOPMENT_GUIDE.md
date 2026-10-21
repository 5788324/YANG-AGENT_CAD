# Tool Development Guide

目标：个人工作插件箱，AI 新增功能尽量低摩擦。

## A. 新 Toolbox GUI Tool — 默认最小组成

1. 强类型 Request（需要参数时）；
2. Service；
3. Toolbox Descriptor；
4. Runtime Adapter；
5. ParameterShape（需要通用参数面板时）；
6. 必要的 Windows smoke；
7. 更新 backlog / CURRENT_STATE / WORKLOG。

普通 GUI Tool **不要求**：
- 新增 MCP schema；
- 新增 Ribbon 按钮；
- operation log；
- dry_run；
- allow_xxx；
- PR/独立分支。

普通写工具默认：
`UI → Runner → Transaction → Service → Ctrl+Z`

只有批量删除/覆盖/Purge/大规模自动修复等高破坏操作，才按需加一次确认或 Preview/dry_run。

## B. 新 MCP / AI typed Tool — 最小组成

1. C# `ICadTool` 或对应 Civil typed Tool；
2. MCP ToolDescriptor：Name/Description/IsWrite/SupportsDryRun/Owner；
3. RuntimeHost 或 Civil extension 注册；
4. Python MCP v2 对应 schema；
5. TOOL_INDEX；
6. transaction / rollback / operation log；
7. 适用写 Tool 的 dry_run；
8. Windows 实机证据。

## 参数设计

- 使用明确字段，不以一句自然语言代替结构化参数；
- 实体用 Handle；
- 坐标明确 WCS/UCS；首版默认 WCS；
- 角度 MCP 层用 degrees，C# 转 radians；
- 大查询必须有 limit；
- Toolbox `ObjectSelection` 优先在 UI 层拿 Handle，再交 Service；
- ParameterShape 只描述形状，运行值进入强类型 Request。

## 返回

MCP 推荐：
`count/items/changed/skipped/failed/warnings/dry_run`

Toolbox GUI Service 可返回最简单的强类型 Result；不要求所有 GUI Tool 伪装成 MCP 返回格式。

## 是否真的需要新 MCP Tool

当 AI 遇到未覆盖对象：

1. 先 `cad.object.describe`；
2. 简单 public property 用 `cad.property.*`；
3. 简单 public method 用 `cad.api.*`；
4. 一次性/低频特殊能力可做 versioned extension；
5. 高频、参数语义复杂、需要对象关系/规则协调的操作，沉淀为强类型 Service/Tool。

不要因为 generic layer 存在就把业务逻辑塞进 `cad.api.invoke`。

## Batch-local exception

`batch.*` 继续是 live RuntimeHost 之外的独立路径：

Python MCP schema → batch orchestrator → independent AcCoreConsole → BatchWorker command。

Batch tools 不进入 live `/health`；real output 保持 copy-only。
