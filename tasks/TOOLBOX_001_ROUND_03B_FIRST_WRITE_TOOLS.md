# TOOLBOX-001 Round 3B — First Real Write Tools

状态：**READY / NEXT**
目标版本：`0.9.0-dev`

## 目标

接入第一批真实 GUI 写工具，同时继续证明：

```text
GUI Toolbox ─┐
             ├→ Shared Write Service → Autodesk API
MCP Tool ────┘
```

固定两个工具：

1. `cad.polyline.clean`
2. `cad.layer.cleanup`

不得重新实现一份 GUI-only 业务逻辑。

## P0 — Shared Write Services

从现有 `CadProductionTools.cs` 写逻辑中提取：

- `Services/Cad/PolylineCleanupService.cs`
- `Services/Cad/LayerCleanupService.cs`

要求：

- Service 不读取 `MdiActiveDocument`；
- Service 不自行创建 DocumentLock；
- Service 不自行创建外层 Transaction；
- Database / Transaction 由 Runner / Context 提供；
- MCP Tool 和 GUI Adapter 必须调用完全相同的 Service；
- MCP schema / tool name / response semantics 保持兼容；
- stdio 65 / AutoCAD 28 / Civil 58 不变。

## cad.polyline.clean

GUI 参数：

- `handle` — ObjectSelection / Handle-first
- `tolerance` — Number
- `remove_duplicates` — Bool，默认 true
- `remove_collinear` — Bool，默认 false

保持现有保护：

- remove_duplicates / remove_collinear 至少一个为 true；
- 有 bulge 拒绝；
- 非零 segment width 拒绝；
- 不得清到低于最小顶点数；
- straight/zero-width path semantics 保持。

安全级别：**D-029 B 普通单对象写**

- 支持 Preview / dry_run；
- Real 直接执行；
- 不弹确认；
- Native AutoCAD Undo 必须恢复 baseline。

Preview 与 Real 必须使用同一 Service：
- Preview：`WriteContext(doc, true, ...)`
- Real：`WriteContext(doc, false, ...)`

## cad.layer.cleanup

GUI 参数保持现有 MCP 语义：

- names：图层名称集合
- include_defpoints：Bool，默认 false

若通用 ParameterShape 暂无 string-array：
- 第一版允许 Text 输入逗号/分号分隔图层名；
- Adapter 解析成 string[]；
- 不因此扩复杂 MultiSelect 框架。

必须保留：

- Layer `0` 永不删；
- 默认保护 `Defpoints`；
- 当前层不删；
- xref-dependent 不删；
- locked 不删；
- `Database.Purge` 最终判定可清理性；
- 只删除 AutoCAD 自己判定 purgable 的候选。

安全级别：**D-029 C 高破坏**

- 必须支持 Preview / dry_run；
- 用户明确按“执行”才 Real；
- 最多一次确认；
- **禁止二次确认 / 连续弹窗 / 审批式流程**；
- Preview 清楚列出 `would_delete`；
- Real 后 Native Undo 必须恢复 baseline。

如果 Preview 已足够清楚且“执行”是明确动作，可以不额外弹确认。

## Generic Toolbox Write Interaction

对 `SupportsDryRun=true` 的 GUI Tool 增加轻量 Preview：

- Preview button
- Execute button
- Preview result/error 使用现有 Palette result area
- Preview 不写 Recent
- Real success 才写 Recent
- Real failure 不写 Recent

不要引入：
- workflow engine
- command bus
- approval state machine
- global permission table

## Transaction / Undo 实机证据

### polyline.clean

```text
baseline
→ GUI Preview
→ state unchanged
→ GUI Real
→ state changed
→ native AutoCAD _.U
→ state == baseline
```

### layer.cleanup

```text
baseline
→ GUI Preview
→ no layer deleted
→ GUI Real
→ purgable layer deleted
→ native AutoCAD _.U
→ layer restored
```

Real 后到 `_.U` 之间不要插入 YangCAD 写 RPC。

## GUI / MCP semantic parity

同 DWG、同输入：

### polyline.clean
比较：
- preview removed indices
- real changed
- before / after关键字段
- Undo baseline

### layer.cleanup
比较：
- would_delete
- deleted
- skipped reasons

不要求 JSON 逐字相同，只要求业务语义一致。

## Static Policy

新增：

`tools/test_toolbox_round3b_write_cad_policy.py`

至少锁：

- 两个 shared write Service 存在；
- GUI/MCP 都调用 shared Service；
- GUI 不调 HTTP/Gateway/MCP Tool；
- 两工具 IsWrite=true；
- SupportsDryRun=true；
- Preview 使用 runner-owned dry-run；
- Real 使用 runner-owned write；
- layer protections 保留；
- polyline bulge/width protections 保留；
- stdio 65 不变。

## Windows Build

2021–2024：
- AutoCAD clean build
- Batch clean build
- Civil 2022–2024 clean build
- external System.Text.Json.dll = 0

## Runtime 2024 — FULL P0

必须：

- polyline Preview unchanged
- polyline Real changed
- native Undo baseline
- layer cleanup Preview unchanged
- layer cleanup Real 仅删除 purgable target
- native Undo baseline
- GUI/MCP semantic parity
- Favorites/Recent 只记录 real success
- 0 unhandled exception
- 0 token leak
- 65/28/58 unchanged

## 2021 / 2022 / 2023 minimum

至少：

- Toolbox opens
- polyline.clean Preview PASS
- 一个 real + native Undo PASS（可优先 2021）
- 2022/2023 至少 Preview/availability smoke
- 0 unhandled exception

不要重复 Round 1/2/3A 全矩阵。

## 禁止

- 不复制 GUI cleanup 算法；
- 不让 Service 自己取 ActiveDocument；
- 不新增 GUI → MCP/Gateway 回环；
- 不增加 RBAC / allow_xxx 给普通 GUI；
- 不做二次确认；
- 不扩展大而全 Parameter metadata；
- 不在本轮开发更多写工具；
- 不改 65/28/58 contract。

## 交付

- full source ZIP
- outer SHA-256
- internal SHA256SUMS
- production diff
- static/policy results
- build matrix
- Windows Preview/Real/Undo evidence
- GUI/MCP semantic parity evidence
- runtime/token scan
- CURRENT_STATE / HANDOFF / WORKLOG / CURRENT_TASK 更新
