# TOOLBOX-001 Round 2 — Implementation Notes

更新时间：2026-08-28
状态：**STATIC IMPLEMENTED / WINDOWS PENDING**

## 本轮目标

把 Round 0/1 已经实机证明稳定的 PaletteSet 壳补成真正可长期使用的交互框架，而不是开始堆大量业务功能。

## 已实现

### Tool Detail flow

```text
Tool Card
  → Tool Detail
  → ParameterPanel
  → Validation
  → Execute
  → Result / Error
  → Recent
  → Favorite
```

采用“列表页 / 详情页”切换，不采用窄 Palette 下容易拥挤的左右双栏参数布局。

### ParameterPanel

新增独立 WPF 控件：
- `UI/ToolboxParameterPanel.xaml`
- `UI/ToolboxParameterPanel.xaml.cs`

覆盖既定 8 种 ParameterShape：
- Text
- Number
- Bool
- Choice
- Layer
- ObjectSelection
- Point
- File

支持：
- Required
- DefaultValue
- Min / Max
- Options
- Description
- visible validation error

实现边界：
- Layer：从当前 DWG 通过 `CadTransactionRunner.ReadContext` 读取图层；
- ObjectSelection：第一版按 D-006 Handle-first，通用面板接收 Handle 文本；真实图上点选由具体 Tool Adapter / custom ParameterView 后续按需接；
- Point：`X,Y` / `X,Y,Z`；
- File：WPF OpenFileDialog；
- 不引入 Color/MultiPoint/Tree/conditional-form engine。

### Favorites / Recent

继续使用现有：
`%LOCALAPPDATA%/YangCADAgent/settings.json`

不引数据库。

Favorites：
- Tool ID add/remove
- missing ID 自动忽略
- settings persistence

Recent：
- 仅成功 Execute 后记录
- 去重
- newest first
- 最多 10 项
- failed / Planned 不记录

`UserSettings.Normalize()` 增加 ID 去空/去重/限长。

### Execution state

Tool detail 支持：
- Available
- Experimental
- Planned
- Hidden（不显示）
- runtime unavailable

规则：
- Planned 不可执行；
- runtime unavailable 可查看，但 Execute disabled；
- execution success/error 在 Palette 内显示；
- availability check exception 自动降级不可用并写 RuntimeLogger，不炸 Palette。

### Search

保持最小实现：
- Name
- CategoryPath
- SearchKeywords
- case-insensitive Contains

不做 fuzzy/pinyin engine。

## Round 2 验证工具

### `system.parameter-demo`

Experimental，仅用于证明 8 种 ParameterShape / validation / execution result。

- 不写图纸；
- 不新增 MCP Tool；
- 只回显输入参数。

### `system.document-context`

Experimental，用于证明 runtime availability：
- 有 active DWG → 可执行；
- 无 active DWG → unavailable / Execute disabled。

这两个 Tool 不改变 65 / 28 / 58 contract。

## Context refresh

DocumentActivated 后：
`ToolboxPaletteManager.RefreshContext()`

只在 Palette 已存在时刷新，不会在 Start Page 主动创建 PaletteSet。

## 版本策略

文档开发轨进入 `0.9.0-dev / Round 2`。

但当前 assembly / MCP / bundle / release scripts 暂保持 0.8.0 contract，避免在 UI 验收前把版本升级和交互框架混成一个任务。

Round 2 Windows GO 后再决定是否做独立 VERSION-0.9.0 收口。
