# TOOLBOX-001 Round 2 — Interaction / ParameterPanel / Favorites / Recent

状态：**READY / NEXT**
目标版本：`0.9.0-dev`

## 目标

把 Round 0/1 已验证的 PaletteSet “壳”变成可长期使用的交互工具箱框架。

不批量开发真实 CAD/Civil 功能。

## P0 — ParameterPanel

实现 8 个既定 ParameterShape：
- Text
- Number
- Bool
- Choice
- Layer
- ObjectSelection
- Point
- File

要求：
- ParameterShape 不保存 runtime values
- runtime values 进入参数字典 / 强类型 Request
- Required
- DefaultValue
- Min / Max
- Options
- Description
- invalid input 有清楚用户提示
- complex tool 仍保留 custom view escape hatch

本轮不扩：
- Color
- MultiPoint
- Tree
- fuzzy form rules
- conditional form engine

## P0 — Execution State

ToolCard 必须能表现：
- Available
- Experimental
- Planned
- Hidden
- runtime unavailable

规则：
- Planned 不可执行
- unavailable 可见但 disabled / 有原因提示
- Execute success 有轻量结果
- Execute error 有轻量错误提示，不 crash Palette

## P0 — Favorites

- Tool ID add/remove
- favorite category/list
- persisted in existing user settings
- missing/deleted Tool ID 自动忽略，不 crash

## P0 — Recent

- successful execution 后记 Tool ID
- 去重
- newest first
- 固定小数量上限（建议 8–12，按现有 UI 决定）
- persistence
- Planned / failed execution 不写 Recent

## P0 — Search

继续轻量：
- Name
- CategoryPath
- SearchKeywords
- case-insensitive Contains

不做 fuzzy engine。

## 验证 Tool

优先使用：
`system.mcp-status`

如果 ParameterPanel 需要一个有参数的最小验证 Tool，可增加一个**无图纸破坏性的 internal/demo interaction Tool**，或复用已有低风险 Tool；不要为了测试参数面板提前开发复杂 CAD 功能。

若增加 demo Tool，最终正式包可 Hidden，不进入普通用户主列表。

## Windows Smoke

2024 必做：
- open/reopen
- favorites add/remove + restart/reopen persistence
- recent after successful execute
- search keywords
- unavailable/planned disabled
- parameter validation
- execution success/error
- 三主题不回归
- Control Center 不回归
- 65 / 28 / 58 不回归
- 0 unhandled exception
- 0 token leak

2021–2023：
- clean build
- 不需要重复 Round 1 全 UI matrix
- 如 Round 2 引入 net48/WPF API 新依赖，则 2021/2022/2023 各做 minimum open smoke

## Static

必须：
- verify_source
- verify_release_source
- extension 12/12
- quick install 10/10
- Civil compat 8/8
- Toolbox foundation 10/10
- 新增 Round2 interaction policy tests（数量不预设，覆盖真实新边界即可）

## 禁止

- 不动 MCP tool contract 65
- 不重构 Gateway/Broker
- 不做 GUI/MCP metadata 大统一
- 不做 DI/Mediator/EventBus
- 不开发大批 CAD/Civil Tool
- 不为 Favorites/Recent 引数据库

## 交付

- full source ZIP
- outer SHA
- internal SHA
- static results
- Windows results
- production diff
- CURRENT_STATE / HANDOFF / WORKLOG / CURRENT_TASK 更新
