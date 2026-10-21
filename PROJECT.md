> **2026-09-08 Gemini 实机证据独立复核**：Gemini 的 `EVIDENCE COMPLETE` 不被接受；确认存在 PID/Python/SHA 不一致、未实际 GUI 却以 RPC 冒充 GUI、未测 PaperSpace 却宣称 PASS、Leader/MLeader 覆盖夸大、Undo/Revit E2E 证据不足等问题。当前改为 `PARTIAL RUNTIME EVIDENCE / TARGETED RETEST REQUIRED / FINAL NO-GO`。本候选从 2026-09-07 clean candidate 重建，只吸收经 ChatGPT 独立审查的必要 Windows 修复；Gemini post-fix ZIP 不作为新基线。详见 `docs/CHATGPT_GEMINI_EVIDENCE_AUDIT_20260908.md` 与 `docs/TARGETED_RUNTIME_FIX_REVIEW_20260908.md`。

> **2026-09-07 急用功能：图纸比例转换 V1（STATIC DEVELOPMENT）**：新增 Ribbon `比例转换` / 命令 `YGSCALE` / MCP `cad.scale.convert`。支持 `standard_1to1`（真实 1:1，几何不变、注释倍率 New/Old）与 `scaled_drawing`（非 1:1，几何倍率 Old/New、纸面注释恢复）两种模式。第一版对 Annotative/Xref/Underlay/Civil/AEC/Proxy 安全跳过；标准模式普通块因无法判定几何块/注释块而先跳过。当前 MCP additive contract 变为 stdio **68** / AutoCAD **31** / Civil 3D **61**。尚未 Windows Build/Runtime/Undo 实机证明。

> **2026-09-07 CADTool ↔ YangTools 业务对齐**：`project.revit.prepare` 与 `project.image.embed` 已补产品级 MCP Adapter，GUI/MCP 共用业务 Service/Request/Result；当前静态合同调整为 stdio **67** / AutoCAD live **30** / Civil 3D live **60**。Exchange 继续锁定 `yangcad-rvt-exchange/1.0`，`LWPOLYLINE vertices[].point + bulge` 不回退。Windows Build/Runtime/真实 11/11 E2E 尚需独立验收。

# YangCAD Project

更新时间：2026-09-01  
阶段：`0.9.0-dev / UI + MCP Closeout`

## 当前产品形态

### Ribbon

- **MCP**：独立一级核心入口；
- **工具箱**：仅两个业务功能；
- **比例转换**：YGSCALE 双模式急用生产功能；
- **控制中心**：系统设置 / 关于。

### Toolbox GUI

只保留：

1. `project.revit.prepare` / Revit 图纸预处理；
2. `project.image.embed` / 批量图片内嵌。

已从可见 Toolbox UI 移除：旧 CAD/Civil 审计/清理卡片、System、Planning、当前文档、MCP 状态、参数面板自检、Recent/Favorites/Search 等开发期/重复入口。

这些 GUI 的旧共享 Service/MCP backend 不做破坏性删除；MCP 是大核心，现有 backend 不删除；新增 `cad.scale.convert` 后当前 contract 为 68 / 31 / 61。

## MCP Core

状态：**STATIC CLOSEOUT PASS / WINDOWS TARGETED RETEST REQUIRED**

本轮修复：

- 独立 `McpWindow`；Control Center 不再承载 MCP 页面；
- `LocalGateway.IsRunning`；
- AutoCAD/Civil host-specific 31/61 自检 + 关键 Tool 校验；
- partial/stale MCP runtime 也触发 embedded self-heal；
- runtime asset 逐文件内容一致性检查；
- launcher dependency 检查要求 MCP major version 2；
- copy config 保持 token-free + current-PID-bound。

Windows 硬门禁仍按：`tasks/FEATURE_INFRA_MCP_CONFIG_001_WINDOWS_RETEST.md`。

## FEATURE-CAD-REVIT-PREP-001

历史核心状态：**CLOSED / GO / PASS**

当前 0.9 closeout delta：**STATIC PASS / WINDOWS REGRESSION REQUIRED**

2026-09-01 YangTools 3.1.36 对接增量：**STATIC PASS / AUTOCAD + REVIT E2E RETEST REQUIRED**。保持 `yangcad-rvt-exchange/1.0`，只做 additive schema 加固：`DxfType` 正式化、`units/origin/Traits/counts`、稳定 Dimension/Hatch/Leader 语义与固定 fixture。

本轮只做收尾，不破坏核心交换协议：

- UI status → Available；
- Exchange layer 在 detached clone 中统一恢复为 On / Thawed / Unlocked / Plottable；
- 动态块引用检测与 warning/report；
- GUI 描述不再宣称特殊对象“完整”转换。

## FEATURE-PROJECT-IMAGE-EMBED-001

状态：**STATIC HARDENED / WINDOWS PENDING**

静态策略保持：

- source DWG 不覆盖；
- working-copy → OLE → output；
- output publish no-overwrite；
- source SHA 发布前/后检查；
- PaperSpace 显式上下文；
- status replace；
- unsafe raster / nested raster / partial failure fail-closed。

最终硬门禁：AutoCAD 2024 Windows OLE Smoke。

## 版本

当前统一：

- Product Version：`0.9.0`
- Informational Version：`0.9.0-dev`

## 当前急用开发与下一步

CAD→Revit/MCP 对齐的实机 E2E 暂停，源码改动保留；优先完成 `YGSCALE` 后一起交 Windows 执行器测试。

顺序：

1. 本候选完成比例转换静态开发与封包；
2. Windows AutoCAD 2024 clean build / deploy / loaded；
3. YGSCALE 两模式 + 一次 Undo + MCP `cad.scale.convert`；
4. 同一候选继续完成 MCP self-heal、Image Embed OLE、Revit Exchange GUI↔MCP↔YangTools 11/11；
5. ChatGPT 根据原始证据决定 GO/NO-GO。
