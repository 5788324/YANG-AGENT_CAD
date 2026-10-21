# YangCAD AI Feature Development Standard

状态：**AUTHORITATIVE / REQUIRED**  
适用对象：ChatGPT、Claude、Codex、DeepSeek、其他本地/远程 AI 开发者  
适用阶段：Toolbox Core GO 后的全部按需功能开发

## 0. 基本原则

YangCAD 是个人工作插件箱，不是企业级平台。

所有 AI 开发必须优先：

1. 解决真实工作问题；
2. 单轮默认 1–3 个 Tool；
3. 尽量复用现有 Service / Tool / Generic MCP；
4. 小改动、低耦合、容易维护；
5. 快速静态检查 + 风险匹配的最小 Windows 实机；
6. 每次正式打包都同步文档和交接。

禁止为了“架构漂亮”“分类完整”“以后可能有用”而批量加功能。

---

## 1. 开发前必须先判断

收到新需求后，AI 必须按顺序判断：

1. **已有 Tool 是否已经能完成？**
2. **已有 Service 是否可以直接复用？**
3. **Generic MCP 是否足以临时完成？**
   - object/property
   - api describe/invoke
   - command/extension only as controlled escape hatch
4. 是否真的值得新增 GUI Tool？
5. 是否真的需要同时新增 MCP Tool？

### 默认决策

- 高频、确定性、人工点击方便 → GUI Toolbox。
- AI/批处理/组合工作流价值明显 → MCP。
- 同一个能力同时需要 GUI + MCP → **共享同一 Service**。
- 仅 GUI 有价值 → 不为“对称”强行增加 MCP schema。
- 仅 MCP 有价值 → 不为“有按钮”强行加 GUI。

---

## 2. 单任务规模

默认：

> **1–3 个 Tool / task**

规则：

- 可以只做 1 个。
- 只有天然属于同一业务场景的功能才合并。
- 不允许“顺手再加几个”。
- Backlog 不代表 NEXT，不允许 AI 自动从 Backlog 继续取任务。

---

## 3. 架构边界

标准路径：

```text
GUI Toolbox ─┐
             ├→ Shared Service → Autodesk API
MCP / AI ────┘
```

### 强制

- GUI 不得调用 MCP Tool。
- GUI 不得 HTTP/Gateway 回环调用自己。
- MCP 不得复制一份 GUI 业务逻辑。
- Service 不得直接读取 `MdiActiveDocument`。
- Autodesk 上下文必须由 Runner / Context 提供。
- 新 Toolbox Service 使用 `CadExecutionContext`。
- 现有稳定 MCP `Read/Write(Func<Transaction,T>)` 不因新 GUI Tool 被重构。
- 新 Context 路径保持 `ReadContext/WriteContext` 明确命名。

### 禁止无收益抽象

除非真实需求证明必要，不引入：

- DI Container
- CQRS
- Mediator
- EventBus
- Repository Pattern
- Workflow Engine
- Plugin Loader（固定静态 Toolbox）
- RBAC / 审批系统
- 大一统 GUI/MCP metadata
- 模糊搜索引擎
- 数据库保存 Favorites/Recent

---

## 4. Request / Parameter 规则

- `ParameterShape` 只描述 UI 形状和基础校验。
- Runtime values 不放进 Descriptor/Shape。
- Service 优先使用强类型 Request。
- ObjectSelection 优先 Handle First。
- Point 明确坐标系；默认 WCS，特殊情况必须说明。
- 角度 MCP 对外优先 degrees，Service/API 内转换 radians。
- 批量输入必须有限制或明确范围。
- 复杂工具可以使用 custom ParameterView，不扩一套万能表单引擎。

当前通用 ParameterShape：
- Text
- Number
- Bool
- Choice
- Layer
- ObjectSelection
- Point
- File

---

## 5. Autodesk API / Transaction 规则

### Read Tool

- Runner-owned read context。
- 不在读取路径隐式修改对象。
- Civil 读取不得偷偷 Rebuild。

### Ordinary Write Tool

GUI：
- Document affinity
- Transaction
- rollback on failure
- Native Undo
- 默认不弹确认
- dry_run 只有实际有价值才加

MCP typed write：
- 保持既有 dry_run / transaction / operation log contract

### High-Destructive Tool

包括但不限于：
- 批量删除
- Purge
- 覆盖
- 大范围自动修复
- 批量输出覆盖

要求：
- Preview/dry_run 或最多一次确认
- 明确展示影响范围
- rollback / Undo
- 禁止二次确认、审批流、RBAC

### Output-copy / Exchange Tool

当 Tool 的目的就是生成独立 DWG/交换副本时：

- 活动 Document/Database 仍由 Runner/Context 提供，只读；
- 可以通过 `Database.Wblock()` 或独立 side Database 创建 detached clone；
- detached/output Database 可以由 Service 自己管理其 Transaction，因为它不属于活动 Document 的 Undo 栈；
- 禁止对 source Database 调 `SaveAs` 冒充“输出副本”；
- 默认不覆盖已有输出；
- 输出失败不得污染当前 DWG。

### Batch

- 不破坏输入 DWG
- real 默认 SaveAs/output copy
- 输入 SHA before/after 必须一致
- Batch 使用独立进程，不与 live acad.exe Gateway 生命周期混用

---

## 6. 兼容性规则

当前已有基础证据：
- AutoCAD 2021–2024
- Civil 3D 2022–2024
- 2025–2027 NOT_INSTALLED

新增功能：

### 如果只使用已证明 API
- 主测 2024
- 其他年份做风险匹配 minimum smoke

### 如果引入年份差异 API
- exact-year compile
- exact-year runtime
- 不跨年份复制 Autodesk/YangCAD DLL
- `NOT_INSTALLED` 永远不能写 PASS
- Build PASS 永远不能外推 Runtime PASS

net48 2021–2024：
- 不重新引入 external `System.Text.Json.dll`

---

## 7. UI / UX 规则

- Toolbox 用于“干活”，Control Center 用于配置/诊断。
- 不为每个 Tool 加 Ribbon 按钮。
- ToolCard / 分类 / 搜索 / Favorites / Recent 继续复用现有框架。
- Planned 工具不得伪装成 executable。
- Runtime unavailable 必须可见且不可执行。
- 成功/错误优先显示在 Palette 内。
- 不连续弹 MessageBox。
- 三主题继续保持：墨曜 / 月瓷 / 雾蓝。
- 新 UI 必须考虑窄 PaletteSet 停靠状态。

---

## 8. MCP 规则

- 65 / 28 / 58 contract 非必要不变。
- 新 Tool 是否进入 MCP 必须由真实 AI 使用价值决定。
- MCP 写前继续遵循 DWG / process binding。
- localhost + instance token 机制不改成公网服务。
- Token 不写普通日志、不写诊断、不复制到剪贴板配置。
- Escape hatch (`cad.command.run`, `cad.extension.invoke`) 保持默认受控。

---

## 9. 测试规则

不为每个小功能重跑完整历史矩阵。

### 每次必须

- `verify_source.py`
- `verify_release_source.py`
- 现有 policy regression
- 新功能边界对应的新 policy（确有需要时）

### Read Tool 最小实机

- 正确输出
- 无副作用
- 若 GUI+MCP 双入口：semantic parity

### Ordinary Write Tool 最小实机

```text
baseline
→ real
→ changed
→ native Undo
→ baseline
```

如支持 Preview：

```text
baseline
→ Preview
→ unchanged
→ Real
→ changed
→ Undo
→ baseline
```

### High-Destructive

必须证明：
- Preview/影响范围
- real 只改预期对象
- rollback/Undo
- 无额外破坏

---

## 10. 禁止“假 PASS”

AI 不得：

- 把 Build PASS 写成 Runtime PASS
- 把 SAMPLE_MISSING 写成 PASS
- 把静态推测写成实机结果
- 把未运行年份写成兼容完成
- 只依据文字报告，不核对回传源码/哈希
- 有源码修改却只回传补丁、不回完整 FINAL_SOURCE

---

## 11. 每次交付必须包含

正式候选/最终交付必须有：

1. 完整源码 ZIP
2. Outer SHA-256
3. 包内 `SHA256SUMS.txt`
4. production diff 摘要
5. static/policy 结果
6. Windows 实机结果（需要时）
7. `README.md`
8. `PROJECT.md`
9. `docs/CURRENT_STATE.md`
10. `docs/HANDOFF.md`
11. `WORKLOG.md`
12. `tasks/CURRENT_TASK.md`
13. 下一任务/SOP（如果存在）

每次打包都保持“随时可切新对话”的交接状态。

---

## 12. Git 规则

- `main` 是唯一开发线和唯一事实源。
- 不恢复旧 YANG-AGENT_CAD 历史。
- 个人项目不强制 PR。
- 不逐文件碎提交。
- Git 写入由当前用户指定执行者负责；不要擅自推送。
- 不提交：
  - Autodesk DLL
  - bin/obj
  - DLL/PDB/EXE/binlog
  - token/secret
  - runtime log
  - release ZIP
  - 临时测试 DWG

---

## 13. AI 自检清单

开始编码前，AI 必须能回答：

- 我解决的真实问题是什么？
- 为什么需要新增 Tool？
- 为什么需要/不需要 MCP？
- 是否已有 Service 可复用？
- 是否会复制业务逻辑？
- 写操作属于 Read / Ordinary Write / High-Destructive 哪一级？
- 最小实机证据是什么？
- 是否引入新年份 API 风险？
- 本轮是否超过 1–3 Tool？
- 完成后文档/交接是否同步？

答不清楚时，优先缩小任务，而不是扩大框架。
