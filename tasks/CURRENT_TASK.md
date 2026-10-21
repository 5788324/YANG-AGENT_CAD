# CURRENT TASK

更新时间：2026-09-07

## Formal stable baseline

- YangCAD: `YangCAD_0.9_RVT_EXCHANGE_YANGTOOLS_3136_ALIGN_STATIC_20260901.zip`
- SHA-256: `B452D326510DE664094A7B0C7170D4F07C88DAD9B59503D077DC008A3B599EB7`
- YangTools Reader baseline: 3.1.36 FINAL_E2E / `D126F1A65F0DD4A74A1F97AD6F1CD3302309F77212E30BD7752666DE740CDBA4`
- Exchange schema: `yangcad-rvt-exchange/1.0`（不升级）

## Current candidate

### YANGCAD-0.9-SCALE-CONVERTER-MCP-ALIGN-20260907

当前候选在正式 YangCAD 0.9 基线上合并两组尚未实机封版的增量：

1. CADTool ↔ YangTools 业务对齐：
   - `project.revit.prepare` / `project.image.embed` 增加产品级 MCP；
   - GUI/MCP 共用 Request/Service/Result；
   - Exchange 1.0 保留 `DxfType`、LWPOLYLINE `point+bulge`、MLeader/Hatch/Dimension 语义；
   - controlled 11-object contract fixture。
2. 急用 `图纸比例转换 / YGSCALE / cad.scale.convert`：
   - `standard_1to1`：真实几何不变，注释倍率 `New/Old`；
   - `scaled_drawing`：几何倍率 `Old/New`，纸面文字/标注/引线/Hatch/线型尽量保持；
   - Annotative/Xref/Underlay/Civil/AEC/Proxy fail-closed。

当前 additive MCP contract：

- stdio: **68**
- AutoCAD live: **31**
- Civil 3D live: **61**

状态：`IMPLEMENTED / STATIC PASS / WINDOWS BUILD+RUNTIME REQUIRED`。

## Windows execution order

1. clean build / deploy / loaded SHA；
2. YGSCALE `1:5 → 1:10` 与 `1:10 → 1:5`；
3. standard_1to1 几何不变 / annotation scale；
4. 一次 Ctrl+Z 完整恢复；
5. `cad.scale.convert` dry-run + real；
6. MCP 68/31/61 对账；
7. MCP self-heal；
8. Image Embed OLE；
9. Revit Exchange GUI↔MCP + YangTools Reader 11/11 E2E。

Windows 任务：
- `tasks/SCALE_CONVERTER_V1_WINDOWS_SMOKE_20260907.md`
- `tasks/CADTOOL_YANGTOOLS_ALIGNMENT_20260907_WINDOWS_E2E.md`

Gemini 只负责执行并回传原始证据，不能自行宣布 FINAL GO。
