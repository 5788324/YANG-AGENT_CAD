# TOOLBOX ARCHITECTURE

状态：**ChatGPT + Claude 合并后正式设计基线**
日期：2026-08-28
目标版本：`0.9.0-dev / TOOLBOX-001`

## 0. 设计目标

最高优先级不是“做一个通用平台”，而是：

> **让 AI 能快速新增、修改、维护 CAD / Civil 3D 小工具，并尽快上线辅助个人工作。**

架构只解决会长期造成返工的问题，不做企业级复杂度或为未来假设提前抽象。

## 1. 产品入口

YangCAD 有两类并列入口：

### GUI Toolbox
用于：
- 高频；
- 确定性；
- 单步/小步骤；
- 日常工程操作。

### MCP / AI
用于：
- 自然语言；
- 多步骤；
- 批量；
- QA；
- Agent 编排。

两者共享业务 Service，但不互相调用。

## 2. UI

### 保留现有
`ControlCenterWindow`
- MCP
- 设置
- 诊断
- 低频管理

不为了 Toolbox 重写。

### 新增
`ToolboxPaletteSet`
- WPF
- AutoCAD PaletteSet
- 左侧分类
- 顶部搜索
- 主工具区 / 参数区
- 收藏 / 最近

Ribbon 只保留少量入口：
- 工具箱
- 检查中心
- AI
- 最近
- 设置

### 验证节奏
- P0：先验证 2024 PaletteSet 壳子、Start Page、Document 切换、现有 Ribbon/MCP 不回归。
- 2021–2023 UI 兼容证据跟随真正功能填充节奏补，不提前做完整矩阵。
- HiDPI / 多显示器属于后续打磨，不阻塞框架上线。

## 3. 最小架构

```text
GUI Toolbox ─┐
             ├─ ToolRuntimeAdapter ── Service ── Autodesk API
MCP Adapter ─┘                       ▲
                                     │
                            CadExecutionContext
                                     ▲
                            CadTransactionRunner
```

不要引入：
- Mediator
- CQRS
- Event Bus
- DI Container
- Repository Pattern
- Workflow Engine
- Plugin Loader（静态 Toolbox）
- Domain Aggregate

除非后续有真实问题证明需要。

## 4. ToolDescriptor

Descriptor 是静态、可序列化数据：

```text
Id
Name
CategoryPath
Description
IconKey
Host
SupportedYears
DeclaredStatus
IsWrite
SupportsDryRun
SupportsGui
SupportsMcp
SupportsBatch
SearchKeywords
ParameterShape
HelpId
```

`DeclaredStatus`：
- Available
- Experimental
- Planned
- Hidden

注意：
- `DeclaredStatus` 是作者配置态。
- 当前宿主/文档是否真正可执行，由 Runtime Adapter 的 `IsAvailable(context)` 判断。
- Planned 默认不进入主工具列表，可进入 Backlog/计划页。

## 5. ToolRuntimeAdapter

只做胶水：

```text
IsAvailable(context)
Execute(...)
```

职责：
- 把 GUI 交互输入或 MCP 参数转成强类型 Request；
- 调用 Runner；
- 调用 Service；
- 返回统一结果/错误。

不得承载真正 CAD/Civil 业务判断。

## 6. Service Boundary

Service 负责“图元应该怎么读/改”。

禁止：
- Service 自己读取 `MdiActiveDocument`；
- Service 自己决定当前宿主；
- GUI 业务逻辑复制到 MCP；
- MCP 业务逻辑复制到 GUI。

### CadExecutionContext

只允许：

```text
Document
Database
Editor
Transaction
```

不放：
- DryRun
- AllowErase
- 权限
- UI 状态
- 用户设置
- ToolDescriptor
- 任意大杂烩状态

Context **只能由 `CadTransactionRunner` 构造**。

### Runner 兼容策略

现有稳定接口保持不变：

```csharp
Read<T>(Document doc, Func<Transaction,T> body)
Write<T>(Document doc, bool dryRun, Func<Transaction,T> body)
```

TOOLBOX-001 若需要 Context，采用纯加法的明确新名字，例如：

```csharp
ReadContext<T>(...)
WriteContext<T>(...)
```

不要增加同名 `Func<CadExecutionContext,T>` overload，以避免现有 lambda 调用产生 overload ambiguity。

现有 65 个 MCP Tool 不要求迁移。

## 7. 参数系统

第一版仅：

- Text
- Number
- Bool
- Choice
- Layer
- ObjectSelection
- Point
- File

通用属性保持少量：

```text
Key
Label
Required
DefaultValue
Min
Max
Options
Description
```

### ParameterShape
只负责参数形状。

### Request
Service 使用强类型 Request，例如：

```text
LayerMergeRequest
TextNumericOffsetRequest
PipeSlopeUpdateRequest
```

ParameterShape 不保存实际参数值。

### CAD-specific 参数
- ObjectSelection → GUI 交互后转 `Handle[]`
- Point → 转坐标值
- Layer → 转 layer name / id 语义

Service 不关心参数来自 GUI 点选还是 MCP JSON。

### 校验
- shape/range/required：共用边界校验；
- 需要 DWG/Civil 上下文的业务校验：Service。

复杂工具可使用自定义 ParameterView，不强迫通用表单覆盖所有场景。

## 8. 搜索 / 收藏 / 最近

### 搜索
第一版线性过滤：
- Name
- CategoryPath
- SearchKeywords

使用普通 `Contains`。

暂不做：
- SearchIndex
- fuzzy search
- 拼音引擎

若需要拼音/别名，直接填 `SearchKeywords`。

### 收藏 / 最近
只保存 Tool ID，不保存 UI 对象。

## 9. GUI / MCP Metadata

第一阶段不全量统一。

共享：
- Service
- Request
- 必要时 `Id + ParameterShape`

不强制共享：
- GUI Name/Icon/Category 文案
- MCP name/description
- MCP schema 的 AI-facing 说明

原因：两类受众不同，过早统一会增加开发摩擦。

## 10. Toolbox 安全

### A. 普通只读工具
点 → 执行。

### B. 普通写工具
点 → Transaction → 执行 → Ctrl+Z 可撤销。

不弹确认。

### C. 高破坏工具
仅批量删除、覆盖、Purge、大规模自动修复等：

- 一次确认，或
- Preview/dry_run

按需选择。

`SupportsDryRun` 不是所有写工具的义务。

### TASK-007 防线不扩散
以下只属于动态 AI Extension 风险：
- source-policy
- 动态 DLL manifest/SHA
- Host DLL SHA trust
- RequiresErase/allow_xxx 动态代码门禁

固定 Toolbox 代码不使用这一套。

## 11. AI 新增一个普通工具的理想成本

应尽量只需要：

1. Service；
2. Request；
3. Descriptor；
4. Adapter；
5. 参数定义；
6. Icon（需要时）；
7. MCP adapter（只有需要暴露 MCP 时）。

不应该要求每个新功能同时修改大量 Ribbon、权限表、策略表、manifest、schema 平台代码。

## 12. TOOLBOX-001 实施顺序

### P0
- `D-023 ~ D-030` 决策锁定
- PaletteSet 壳
- ToolDescriptor
- ToolRegistry
- ToolRuntimeAdapter
- CadExecutionContext / Runner 新入口
- ToolCard
- 分类
- 搜索
- Available/Experimental/Planned/Hidden
- 统一执行入口

### P1
- 收藏
- 最近使用
- ParameterPanel
- Help
- 设置持久化
- 第一批现有工具接入验证

### P2
- AI / 检查中心入口优化
- 快捷键
- HiDPI / 多屏细节
- 更复杂搜索
- 必要时再讨论 GUI/MCP metadata 更深共享


### Round 2 implementation note

通用 `ObjectSelection` 第一版按 D-006 Handle First 落地：ParameterPanel 接收 Handle 列表。

不在通用参数框架中提前实现 modeless screen-pick engine。具体 Tool 需要 AutoCAD 交互点选时，由 Runtime Adapter 或 custom ParameterView 按需实现并转换成 Handle，再交 Service。

这是低摩擦边界，不是功能缺口。
