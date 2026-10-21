# YangCAD 0.9｜CADTool ↔ YangTools 业务对齐修改报告

日期：2026-09-07

## 1. 正式输入基线

- YangCAD：`YangCAD_0.9_RVT_EXCHANGE_YANGTOOLS_3136_ALIGN_STATIC_20260901.zip`
- SHA-256：`B452D326510DE664094A7B0C7170D4F07C88DAD9B59503D077DC008A3B599EB7`
- YangTools Reader：`YangTools_Project_3.1.36_Source_CAD_TO_REVIT_FINAL_E2E_20260901.zip`
- SHA-256：`D126F1A65F0DD4A74A1F97AD6F1CD3302309F77212E30BD7752666DE740CDBA4`
- Exchange：继续锁定 `yangcad-rvt-exchange/1.0`，未建立第二套协议。

## 2. 本轮业务对齐

1. `project.revit.prepare`、`project.image.embed` 增加同名产品 MCP Adapter。
2. 两个 Toolbox GUI 与 MCP 均直接复用同一 `Request → Business Service → Result`，GUI 不回调自身 MCP。
3. MCP additive contract：stdio **67** / AutoCAD live **30** / Civil 3D live **60**；既有公开 Tool Name 不删除、不改名。
4. 新增 67 行 `docs/MCP_COVERAGE_MATRIX.md`，每个公开 MCP Tool / 业务动作单独登记。
5. `project.revit.prepare` Result 增加 `units/origin/source_insunits/dxf_type_counts/handles`，用于 GUI↔MCP 机器对账。
6. `project.image.embed` 使用稳定 Request/Result，并显式返回 `warnings`、job/status/output 读回字段。

## 3. Exchange V1 对齐

- `DxfType` 继续作为唯一正式实体类型字段。
- 正式 V1 类型保持：`LINE/LWPOLYLINE/ARC/CIRCLE/TEXT/MTEXT/LEADER/MULTILEADER/HATCH/DIMENSION`。
- LWPOLYLINE 保留已验收的 `vertices[].point + bulge`；非零 bulge 不拉直。
- TEXT/MTEXT 保留 `text/position/rotation_deg/width`，Traits 增加正式 `TextStyle/Font/Height`。
- Entity `Category` 规范化为 `Curve/Text/Fill/Dimension`；几何 Traits 规范化 `Color/LineType/LineWeightMm`，同时保留兼容别名。
- 所有 2D 几何点统一写成 `[x,y,0]`。
- MLeader/Leader 规范输出 `leader_lines[].points`，方向 `arrow_to_text`；缺真实文字或有效 leader line 时 fail-closed。
- HATCH 明确 `boundary_loops` 的 `is_outer/is_hole`，常用曲线保持 LINE/ARC 语义。
- Dimension 只把 Linear/Aligned 作为 V1 可重建维度，并锁定 `DimLinePoint/XLine1Point/XLine2Point`。
- Ellipse/Spline 不再以 extents-only 伪装可重建 Curve，直接 unsupported。
- BlockReference 进入 unsupported；普通 block definition 的子实体不发布。`scan_blocks=true` 仅做诊断，因为 V1 没有 BlockReference 实例 Transform，避免 YangTools 在 definition-space 错坐标重建。
- MAP/REPORT additive 增加 `unsupported_items[] {Handle,DxfType,reason}`。
- Preview 与 Real 都执行单位 fail-closed；Undefined 仍为 mm + warning，显式非 mm/cm/m/in/ft 单位拒绝。

## 4. Controlled 11 静态合同样本

`tools/fixtures/RVT_EXCHANGE_V1_CONTROLLED_11.json` 固定：

- 3 × LWPOLYLINE（含 `bulge=0.5`）
- 1 × LINE / ARC / CIRCLE / TEXT / MTEXT / MULTILEADER / HATCH / DIMENSION
- 总数 11
- MLeader `text = "MLeader E2E Text"` 且使用 `leader_lines[].points`

该文件只是**静态协议 fixture**，不是 AutoCAD 真 DWG / Runtime 证据。

## 5. 静态验证

- `verify_source.py`: `cs=63 xaml=8 python=20 themes=3 tools=67 xml=14 errors=0`
- `verify_release_source.py`: `errors=0`
- `test_revit_exchange_v1_policy.py`: **56/56 PASS**
- `test_mcp_product_coverage_policy.py`: **17/17 PASS**
- `test_mcp_copy_config_policy.py`: **28/28 PASS**
- `test_image_embed_batch_policy.py`: **27/27 PASS**
- Civil API: **8/8 PASS**
- Extension: **12/12 PASS**
- Quick Install: **10/10 PASS**
- Toolbox Foundation: **16/16 PASS**
- Toolbox Interaction: **13/13 PASS**
- UI Surface: **11/11 PASS**
- Theme UI: **13/13 PASS**
- 全部 13 个 `test_*_policy.py` suite PASS
- Python `compileall mcp/yangcad_mcp tools`: PASS
- YangTools 3.1.36 自身 `Test-CadToRevit.py`: **76/76 PASS**（仅确认其正式基线源码静态合同未损坏，不能替代本轮真实 E2E）。

## 6. 未宣称 / 硬门禁

本环境没有 Windows Autodesk SDK/AutoCAD/Civil 3D，因此没有宣称：

- 本候选 C# Windows Build PASS；
- AutoCAD 2024 Deploy/Loaded PASS；
- MCP 67/30/60 Runtime PASS；
- 真 DWG 11→11 Export PASS；
- GUI↔MCP machine comparison PASS；
- YangTools 3.1.36 本轮真实 MAP Reader 6/3/1/1=11 PASS；
- `project.image.embed` 本轮 MCP real OLE PASS。

这些由 `tasks/CADTOOL_YANGTOOLS_ALIGNMENT_20260907_WINDOWS_E2E.md` 交 Gemini 执行，ChatGPT 只根据原始证据做最终 GO/NO-GO。
