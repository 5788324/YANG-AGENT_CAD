# YangCAD 0.9｜CAD→Revit MCP 对齐 + 图纸比例转换 V1 静态变更报告

日期：2026-09-07

## 基线

- YangCAD 正式输入：`YangCAD_0.9_RVT_EXCHANGE_YANGTOOLS_3136_ALIGN_STATIC_20260901.zip`
- 输入 SHA-256：`B452D326510DE664094A7B0C7170D4F07C88DAD9B59503D077DC008A3B599EB7`
- YangTools Reader 稳定对账基线：3.1.36 FINAL_E2E，SHA `D126F1A65F0DD4A74A1F97AD6F1CD3302309F77212E30BD7752666DE740CDBA4`
- Exchange：`yangcad-rvt-exchange/1.0`，未升级 schema。

## A. 此前业务对齐（本候选继续保留）

- `project.revit.prepare` GUI/MCP 共用 `RevitExchangeService`；
- `project.image.embed` GUI/MCP 共用 `ImageEmbedBatchService`；
- RVT Exchange 锁定 `DxfType`；
- LWPOLYLINE 保留 `vertices[].point + bulge`；
- MLeader / Hatch / Dimension 语义与 YangTools 3.1.36 Reader 对齐；
- Unsupported 增加可追踪 Handle/DxfType/reason；
- 受控 11 对象合同 fixture；
- 该 E2E 实机测试因急用比例功能暂停，后续与本候选一起测试。

## B. 本轮急用功能

新增 `图纸比例转换 / YGSCALE / cad.scale.convert`：

- `standard_1to1`：真实几何不变，注释倍率 `new/old`；
- `scaled_drawing`：几何倍率 `old/new`，纸面注释恢复；
- Text/MText、Dimension、Leader/MLeader、Hatch、Linetype、普通 Block（scaled only）处理；
- Annotative/Xref/Underlay/Civil/AEC/Proxy fail-closed；
- 不修改全局 DimStyle/TextStyle/MLeaderStyle；
- 每对象 nested transaction 失败隔离 + 外层统一事务；
- MCP 默认 dry-run，real 继续受 PID/document binding；
- 新 WPF UI 使用三主题资源；修复新增窗口 RadioButton Style TargetType 不匹配风险。

## C. MCP 合同

Additive：

- stdio：68
- AutoCAD live：31
- Civil 3D live：61

无旧 Tool 删除/改名。

## D. 当前判定

`IMPLEMENTED / STATIC PASS / WINDOWS BUILD + AUTOCAD RUNTIME + ONE-UNDO + MCP PRODUCT TEST REQUIRED`

不得把本报告作为 Runtime PASS 证据。
