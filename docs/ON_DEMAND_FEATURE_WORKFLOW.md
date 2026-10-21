# YangCAD — On-Demand Feature Workflow

状态：**ACTIVE**

规范性要求以 `docs/AI_FEATURE_DEVELOPMENT_STANDARD.md` 为准；本文是日常简化流程。

## 原则

Toolbox 核心架构已经通过真实读/写 Tool 验证。

以后不再为了“填满工具箱”一次开发大量功能。

默认：

> 工作中遇到需求 → 一次提交 1–3 个 Tool → 快速开发 → 最小实机验收 → 合回 main

## 一个需求的最小流程

1. 用户描述真实工作问题。
2. 先判断：
   - 已有 Tool 能否复用；
   - Generic MCP 能否临时解决；
   - 是否值得做 GUI Tool；
   - 是否需要 GUI + MCP 双入口。
3. 若新增 Tool：
   - 强类型 Request（需要时）
   - shared Service
   - Toolbox Descriptor / Adapter
   - ParameterShape（通用参数足够时）
   - MCP adapter/schema（仅确有 AI 使用价值时）
4. 只增加与该 Tool 风险匹配的 policy/static test。
5. Windows 只做最小必要 smoke。
6. 更新 CURRENT_STATE / HANDOFF / WORKLOG / TOOL_INDEX（若 MCP）。
7. 打完整源码 ZIP + Outer SHA。

## 每轮规模

默认 **1–3 个 Tool**。

可以一次只做 1 个。

只有同一业务场景天然成组，才合并到 2–3 个。

不因为某分类还有空位而顺手开发。

## Read Tool

最低要求：
- shared Service
- GUI 可执行
- 若 MCP 也暴露则 GUI/MCP semantic parity
- exact-year build 若使用新 API
- minimum runtime smoke

## Ordinary Write Tool

最低要求：
- runner-owned Transaction
- basic native Undo
- 不增加确认
- dry_run 仅在有实际价值时提供

## High-Destructive Write Tool

最低要求：
- Preview/dry_run 或一次确认
- 明确展示影响范围
- native Undo / rollback
- 最多一次确认
- 禁止审批流、二次确认、RBAC

## Backlog

`docs/FEATURE_BACKLOG.md` 只用于记录可能需求。

Backlog 条目：
- 不代表 NEXT；
- 不代表必须做；
- 不代表需要一次完成某个分类。

真正 NEXT 永远由当前工作需求决定。

## 兼容性

2021–2024 已有基础兼容证据。

新增功能如果只用已证明 API：
- 主测 2024；
- 其他年份做风险匹配的 minimum smoke。

如果引入年份差异 API：
- 按 D-021/D-022 做 exact-year build/runtime。

2025–2027 仍是 NOT_INSTALLED，不外推 PASS。
