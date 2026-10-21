# TOOLBOX-001 Round 3A — First Real CAD Tools

状态：**READY / NEXT**
目标版本：`0.9.0-dev`

## 目标

第一次把“真正 CAD 工作能力”接入 Toolbox。

本轮最重要的不是工具数量，而是证明长期开发模式：

```text
GUI Toolbox ─┐
             ├→ Shared Service → Autodesk API
MCP Tool ────┘
```

**禁止 GUI 直接调用 MCP Tool / HTTP / Gateway。**

## 本轮固定工具

### 1. 图纸检查
GUI ID：`cad.drawing.audit`

现有 MCP：
`cad.drawing.audit`

功能：
- 模型空间有限扫描
- 实体类型/图层汇总
- 零长 Line
- 空 DBText/MText
- Polyline 零长段

GUI：
- 参数：entity limit / issue limit / tolerance
- 输出摘要卡/文本
- 只读

### 2. 图层审计
GUI ID：`cad.layer.audit`

现有 MCP：
`cad.layer.audit`

功能：
- 图层状态
- modelspace entity count
- current/dependent/locked
- Database.Purge 可清理性

GUI：
- 默认无参数
- 只读
- 结果必须适合人工阅读

### 3. 多段线检查
GUI ID：`cad.polyline.inspect`

现有 MCP：
`cad.polyline.inspect`

功能：
- Lightweight 2D Polyline
- duplicate vertex
- zero-length segment
- collinear redundant vertex
- bulge/width limitations

GUI：
- ObjectSelection：Handle-first
- tolerance
- 只读

## 架构要求 — P0

现有 MCP 实现当前把业务逻辑直接放在 `CadProductionTools.cs`。

本轮应提取**最小 shared Service**，而不是 GUI 复制代码。

推荐最小拆分：

```text
Services/Cad/
├─ DrawingAuditService
├─ LayerAuditService
└─ PolylineInspectionService
```

可根据实际代码合并成更少文件，只要：
- GUI 和 MCP 真正调用同一 Service；
- Service 不读取 MdiActiveDocument；
- Toolbox Service 使用 Runner supplied `CadExecutionContext`；
- MCP adapter 保持原 contract 和输出语义；
- 不为了“Service”引入 DI/Repository/Mediator。

### 允许的现实折中

为了避免一次重构 `CadProductionHelpers`：
- 可把纯 helper 迁到 shared Service/helper；
- 或保留 internal helper 并逐步迁移；
- 但最终 GUI 不允许调用 `CadPolylineInspectTool.Execute()` 这种 MCP Tool 类。

## 结果展示

Round 3A 不做复杂 Dashboard。

优先简单可维护：
- summary
- issue count
- compact rows/list
- 可复制结果

不要为了表格做 DataGrid 平台。

## 静态门禁

现有全部保持：
- verify_source
- verify_release_source
- extension 12/12
- quick install 10/10
- Civil compat 8/8
- Toolbox foundation 10/10
- Round2 interaction 16/16

新增 Round3 policy 必须至少锁：
- three real CAD Toolbox descriptors
- GUI adapters do not invoke MCP/Gateway
- shared service used by GUI and MCP
- MCP names/contracts unchanged
- no new MCP tool count
- no destructive write in Round3A

数量按真实边界决定，不为了凑测试数。

## Windows 验收

### 2024 P0
用真实 DWG：
1. `cad.drawing.audit` GUI 执行
2. 同图纸 MCP `cad.drawing.audit`
3. 核关键计数语义一致

4. `cad.layer.audit` GUI
5. 同图 MCP
6. 核 layer count / purgable semantics

7. `cad.polyline.inspect`
   - 找一条 LWPOLYLINE
   - GUI Handle 输入
   - MCP 同 Handle
   - 核 duplicate/zero-length/collinear 结果一致

### UI
- Favorites / Recent 对真实 Tool 正常
- SearchKeywords
- failure 留在 Palette
- 当前无文档时 runtime unavailable

### Compatibility
- 2021 / 2022 / 2023 clean build
- minimum open + 至少一个只读 audit 执行
- 不重跑 Round 1/2 全套

### Contract
仍必须：
- stdio 65
- AutoCAD 28
- Civil 58

## 本轮不做

- layer cleanup
- polyline clean
- purge/delete
- text numeric offset
- batch write
- Check Center 聚合页
- DataGrid/report export
- Civil tools

这些进入 Round 3B / Round 4。

## Round 3B 预告

Round 3A PASS 后再做第一批写工具，优先：
- `cad.polyline.clean`
- `cad.layer.cleanup`

这两类写工具再验证：
- Transaction
- native Undo
- high-destructive Preview/dry_run/one confirmation
- GUI/MCP shared Service

不把读/写风险混在同一个首轮。
