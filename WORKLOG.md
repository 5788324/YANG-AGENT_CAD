## 2026-09-07 — CADTool / YangCAD ↔ YangTools business alignment + product MCP

- Formal input locked to YangCAD 0.9 `B452D326...B599EB7` and YangTools 3.1.36 FINAL_E2E `D126F1A6...40CDBA4`; no 3.1.38 dependency.
- Exchange schema remains `yangcad-rvt-exchange/1.0`; no parallel protocol introduced.
- `project.revit.prepare` and `project.image.embed` now have dedicated product MCP adapters while GUI/MCP share the same Request/Service/Result implementation.
- Additive MCP contract becomes stdio 67 / AutoCAD live 30 / Civil 3D live 60; existing public Tool names retained.
- Revit Exchange strengthened to the formal business contract: canonical `DxfType`, `Category`, common Traits, XYZ points, LWPOLYLINE `vertices[].point+bulge`, MLeader/Leader `leader_lines[].points`, explicit Hatch outer/hole, Linear/Aligned dimension roles, detailed unsupported Handle/DxfType/reason.
- Ellipse/Spline and BlockReference fail closed. Ordinary block definition children are no longer published because V1 has no BlockReference instance-transform contract; scan_blocks is diagnostic-only.
- Product result adds units/origin/source_insunits, per-DxfType counts and Handles for GUI↔MCP machine reconciliation. Preview now validates explicit INSUNITS too.
- Added exact controlled 11-object static fixture and full 67-row `MCP_COVERAGE_MATRIX.md`.
- Added strict Gemini Windows E2E task; Gemini is executor only and cannot declare FINAL GO.
- Status after implementation: STATIC gates to be rerun; Windows build/runtime/real DWG/YangTools Reader evidence remains required.

## 2026-09-01 — YangTools 3.1.36 CAD→Revit Exchange contract alignment

- User provided current YangCAD 0.9 source and YangTools 3.1.36 native rebuild requirements.
- Kept `yangcad-rvt-exchange/1.0` and existing Wblock/detached-clone workflow; no unrelated GUI/MCP feature changes.
- Locked `DxfType` as the official entity discriminator; old Revit aliases remain reader-side compatibility only.
- MAP now exports additive `units`, zero `origin`, `source_insunits`, per-entity `Traits`, DXF `counts`, `unsupported_count` and `unsupported` breakdown.
- Added safe unit policy: mm/cm/m/in/ft supported; Undefined falls back to mm with warning; other explicit units fail instead of silently mis-scaling.
- Leader/MLeader now expose text position where available, `leader_lines`, and explicit `arrow_to_text` vertex order.
- Linear/Aligned Dimension definition points are stabilized to `DimLinePoint / XLine1Point / XLine2Point`; other dimension kinds remain additive diagnostics and are marked non-V1 rebuild where applicable.
- Hatch Curve2d serialization now emits stable LINE/ARC payloads while retaining legacy polyline + bulge loops.
- Added fixed cross-plugin fixture `tools/fixtures/RVT_EXCHANGE_V1_YANGTOOLS_FIXTURE.json`.
- `test_revit_exchange_v1_policy.py` expanded to 45/45 PASS.
- Status: STATIC PASS / AUTOCAD REAL EXPORT + YANGTOOLS READER E2E RETEST REQUIRED.

## 2026-08-31 — takeover static hardening before Windows smoke

- 新对话接管后独立核对候选 SHA：outer candidate match；internal SHA 175/175。
- 独立重跑 12 个 static/policy gates：全部 PASS。
- 手工审查发现 Windows P0 前可静态确认的边界缺口并完成小范围加固：
  - ImageEmbed final publish 去除 `Move-Item -Force`，output race fail-closed；
  - source SHA 改为 output 发布前 + 发布后双检查，本轮拥有的异常 output 会清理；
  - PaperSpace 显式 `ActiveSpace=acPaperSpace` + `MSpace=false`；ModelSpace 显式 active space；
  - PowerShell worker 显式 STA，保证 Clipboard apartment；
  - status.json 由 delete→move 改为 `File.Replace`；
  - Raster snapshot 异常不再可能被误判为 no-image SKIPPED；
  - OLE delta 非唯一时清理 stray OLE，PARTIAL 不留重复粘贴对象。
- MCP self-heal 同步加固：Setup/Verify-Release 检查完整 Python bridge；embedded runtime 发布使用 replace + same-content race tolerance。
- policy 提升：MCP 19/19 PASS；ImageEmbed 26/26 PASS。
- 仍未宣称 Windows GO：真实 AutoCAD 2024 COM/PASTECLIP/OLE 重开可见性仍需 targeted smoke。

## 2026-08-31 — MCP Copy Config self-heal + Batch Image Embed v1 static

- 用户实机复现“复制 MCP 配置：未找到 Contents\Mcp 启动脚本”。
- 根因按安装状态处理，不复制 Token 绕过。
- 新增 `RuntimeAssetMaterializer`：MCP runtime 嵌入 YangCAD.AutoCAD.dll，安装脚本缺失时自修复到 LocalAppData。
- Package/Setup/Verify-Release 强制 MCP + ImageEmbed runtime 完整。
- 新增 `project.image.embed`（GUI-only）：单 DWG/文件夹批量创建 OLE output-copy job。
- 新增独立 PowerShell/COM AutoCAD worker；输入 SHA before/after，working-copy，no overwrite，incremental status。
- V1 fail-closed：clip、brightness/contrast/fade、transparency、nested block RasterImage 默认不输出半成功 DWG。
- 当前仅 STATIC；Windows 2024 真 OLE/重开可见性未证明。

## 2026-08-31 — RVT Exchange v1 final Revit closure

- Final clean retest source SHA:
  `A11EE60367A4FA68749F8CCD25CADC1559942807DC372ADC22BC5A90AA41EF77`
- internal SHA from retest: 167/167, 0 mismatch.
- production source confirmed free of temporary test helper commands.
- AutoCAD 2024 clean build PASS.
- Revit 2024 Import CAD PASS.
- actual Imported Objects / GraphicsStyle / Subcategory names verified for:
  - `#0.25 Continuous Red`
  - RVT-T
  - RVT-D
  - RVT-ML
  - RVT-H
- controlled Full Explode actual behavior recorded:
  - Geometry -> DetailLine / ModelLine with usable source LineStyle
  - Text -> TextNote
  - MLeader -> DetailLine/arrow fragments + TextNote
  - Dimension -> DetailLine + TextNote
  - Hatch -> FilledRegion or boundary/fill geometry
- No false claim that Full Explode preserves native Leader/Dimension semantics.
- `_RVT_MAP.json` confirmed as future Revit native reconstruction semantic source.
- FEATURE-CAD-REVIT-PREP-001 = CLOSED / GO / PASS.
- Project returned to ON-DEMAND DEVELOPMENT.
- Scope wording kept precise: Revit 2024 workflow proven; no Revit 2021 runtime claim.

## 2026-08-31 — RVT Exchange Windows review: AutoCAD PASS / Revit targeted retest

- Windows returned SHA independently matched:
  `98633554EAC2B047C2D9B72F21E4246CB8F136CF56C077CD4E0B4E2790F875F6`
- returned package internal SHA: 163/163, mismatch=0.
- 11 YangCAD policy/static gates independently PASS.
- AutoCAD side runtime evidence accepted:
  Preview zero side effect, triplet export, non-overwrite, semantic classification, MAP/report warnings.
- Final package review found undisclosed production instrumentation:
  `YANGCAD_RVT_PREPARE_PREVIEW`, `YANGCAD_RVT_PREPARE_EXPORT`, `YANGCAD_RVT_CREATE_SAMPLE`.
- Restored production `ToolboxCommands.cs` from frozen candidate; no RevitExchange business algorithm change.
- Restored missing `.editorconfig` / `.gitignore`.
- Reconciled contradictory CLOSED/PENDING docs.
- Revit 2024 Import CAD / GraphicsStyle / Full Explode actual results were not provided.
- Current verdict:
  **FEATURE-CAD-REVIT-PREP-001 = AUTOCAD SIDE PASS / TARGETED REVIT RETEST REQUIRED**.

## 2026-08-31 — FEATURE-CAD-REVIT-PREP-001 Windows Verification & CAD P0 Complete

- Candidate ZIP Outer SHA-256 verified: `9652B05C31C39D8F8AC2D0E41857CD9191443E0313E73EECD35F8E41AC9C30AA`.
- Internal SHA256SUMS.txt: 165/165 matched (0 mismatch, 0 missing).
- All 11 static policies PASS (11/11, including `test_revit_exchange_v1_policy.py` 25/25 PASS).
- Clean Build across 2021, 2022, 2023, 2024: 0 errors, external System.Text.Json.dll = 0.
- Executed comprehensive P0 sample test on AutoCAD/Civil 3D 2024:
  - Geometry: Line, Polyline, Arc, Circle with multiple lineweights (0.25, 0.50), Continuous/DASHED, Red/White.
  - Text: DBText (2.5, 5.0), MText (Bold, Italic, Underlined, Mixed/Stacked).
  - Dimensions: Rotated/Linear, Aligned, Radial.
  - Leaders: MLeader with MText note.
  - Hatches: Solid and ANSI31 Pattern (scale 2.0, angle 45 deg).
  - Blocks: Normal block and ByBlock color block definitions & insertions.
- Preview verified: Source DWG entity/layer count 100% unchanged, 0 new files created.
- Execute verified: Generated triplet `<name>_RVT.dwg`, `<name>_RVT_MAP.json`, `<name>_RVT_REPORT.json`.
- Automatic unique naming increment verified on subsequent executions without overwrite (`_RVT_1.dwg`, etc.).
- MAP verified: `yangcad-rvt-exchange/1.0`, 17 generated layers, 22 entities mapped.
- REPORT verified: category_counts (`geometry: 10, text: 6, dimension: 3, leader: 1, hatch: 2`), `unresolved_byblock: 4`, `mixed_mtext: 4`, `anonymous_blocks_skipped: 5`.
- MCP contract intact: stdio 65 / AutoCAD 28 / Civil 58.
- FEATURE-CAD-REVIT-PREP-001 = CLOSED / GO / PASS.

## 2026-08-31 — RVT Exchange v1 pre-package hardening

- Existing generated exchange layers on the detached output clone are now reconciled to the generated semantic properties instead of silently reusing contradictory layer settings.
- Multi-file output finalization tracks which DWG/MAP/REPORT files were moved by this run; failure cleanup cannot delete a pre-existing file that appeared in a race.
- Real result now reports output-clone analysis, while Preview remains source analysis.
- Added policy assertions for no-overwrite triplet, owned-output cleanup, and existing exchange-layer semantic reconciliation.
- Current task status moved to STATIC IMPLEMENTED / WINDOWS + REVIT PENDING.

## 2026-08-31 — FEATURE-CAD-REVIT-PREP-001 static implementation

- User clarified real workflow: CAD layer separation is a CAD→Revit preprocessing step, not only layer standardization.
- V1 scope fixed to one GUI Tool: `project.revit.prepare`.
- Added D-032 `YangCAD RVT Exchange v1`.
- Added `RevitExchangeService.cs`.
- Source active DWG remains read-only; output uses `Database.Wblock()` detached clone.
- Added classification for Geometry/Text/Leader/Dimension/Hatch.
- Geometry layer naming keeps Revit-compatible `#0.25 Continuous Red` convention.
- Added MAP/REPORT output for future Revit direct semantic rebuild.
- Added explicit block/Xref/ByBlock limitations; no fake full-resolution claim.
- Added `test_revit_exchange_v1_policy.py` (25 assertions after export atomicity/layer-semantic hardening).
- No MCP Tool added; 65/28/58 unchanged.
- Images/OLE not developed in this task.

## 2026-08-28 — FEATURE-INFRA-MCP-CONFIG-001 static implementation

- Added authoritative `docs/AI_FEATURE_DEVELOPMENT_STANDARD.md` for all AI developers.
- AGENTS / CLAUDE / Document Authority updated to require the standard.
- Added Control Center MCP primary action: `复制 MCP 配置`.
- Added `McpClientConfigBuilder`:
  - standard mcpServers config
  - exact `YANGCAD_INSTANCE_PID`
  - no Gateway Token/Bearer copied
  - CAD restart requires recopy
- Added `mcp/Start-YangCAD-Mcp.ps1`.
- Package-Bundle now includes `Contents\Mcp`.
- MCP runtime venv stays in `%LOCALAPPDATA%\YangCADAgent\mcp-runtime`.
- Added `test_mcp_copy_config_policy.py`: 14/14 PASS.
- Existing 9 static/policy gates remain PASS.
- Tool contract unchanged: 65 / 28 / 58.
- Reviewed user-provided legacy features:
  - Layer separation: GO, rewrite recommended
  - Image → OLE: CONDITIONAL GO / Experimental, no simple pure-.NET creation assumption
- No implementation of the two reviewed business features in this task.

## 2026-08-28 — Round 3B CLOSED / Toolbox Core enters On-Demand mode

- Windows evidence Outer SHA independently matched:
  `E7EA9058340DB56F6D3A67BF0EACF9FBFE81092A73E57ADF8AA3090A58329359`
- internal SHA256SUMS: 151/151, mismatch=0.
- 9 static/policy gates independently PASS.
- Shared write Services verified:
  - PolylineCleanupService
  - LayerCleanupService
- Production delta contains only Round 3B write-service / Preview wiring / policy work; no removals.
- 2024 Preview/Real/Native Undo accepted for polyline.clean and layer.cleanup.
- 2021 real/native Undo accepted; 2022/2023 preview smoke accepted.
- 0 unhandled exception / 0 token leak.
- Round 3B = CLOSED / GO / PASS.
- Toolbox Core Foundation Round 0–3B = CLOSED / GO / PASS.
- User explicitly limited foundation write tools to 2–3; current 2 tools are sufficient.
- Product development mode changed to ON-DEMAND:
  - real work request triggers task
  - normally 1–3 tools per task
  - no automatic Round 4/5 bulk feature work
  - backlog is unscheduled
- Added D-031 Demand-Driven Toolbox Expansion.

## 2026-08-28 — TOOLBOX-001 Round 3B First Real Write Tools: CLOSED / GO / PASS

- **9 个 static/policy gates 全部 PASS**：
  - `verify_source.py`: `cs=56 xaml=7 python=14 themes=3 tools=65 xml=13 errors=0`
  - `verify_release_source.py`: `errors=0`
  - `test_extension_policy.py`: `12/12 PASS`
  - `test_quick_install_policy.py`: `10/10 PASS`
  - `test_civil_api_compat_policy.py`: `8/8 PASS`
  - `test_toolbox_foundation_policy.py`: `10/10 PASS`
  - `test_toolbox_round2_interaction_policy.py`: `16/16 PASS`
  - `test_toolbox_round3_real_cad_policy.py`: `16/16 PASS`
  - `test_toolbox_round3b_write_cad_policy.py`: `16/16 PASS` (NEW)
- **Clean Build Matrix 全部 PASS**：
  - AutoCAD 2021 / 2022 / 2023 / 2024: 0 errors
  - Civil 3D 2022 / 2023 / 2024: 0 errors
  - Net48 external `System.Text.Json.dll` = 0。
- **P0 架构验证（Shared Write Services 模式）**：
  - 提取 `PolylineCleanupService`、`LayerCleanupService` 到 `Services/Cad/`；
  - GUI Toolbox 与 MCP Tools 统一调用相同底层 Service；
  - GUI 绝不直接调用 MCP / HTTP / Gateway；
  - Service 不读取 `MdiActiveDocument`，由 Runner 提供 `Database`/`Transaction`/`CadExecutionContext`；
  - SupportsDryRun 工具在 GUI 提供“预览”与“执行”双按钮；
  - 预览使用 `WriteContext(doc, true, ...)`，执行使用 `WriteContext(doc, false, ...)`；
  - Preview 不写 Recent，Real success 才写 Recent；
  - MCP names/contracts/schemas 保持兼容，总工具数 65/28/58 严格不变。
- **Runtime 2024 (Civil 3D 2024 P0)**：
  - `cad.polyline.clean`：Preview 未修改图纸（vertices: 6），Real 正确去重（vertices: 4），AutoCAD native `_.U` 恢复 baseline（vertices: 6）；
  - `cad.layer.cleanup`：Preview 列出 `would_delete` 且保护 Layer 0，Real 仅删除可清理目标层，AutoCAD native `_.U` 恢复被删图层；
  - Favorites / Recent 对写工具正常工作；
  - 0 unhandled exception，0 token leak。
- **Runtime 2021 / 2022 / 2023 Minimum Smoke**：
  - 2021 AutoCAD / 2022 Civil 3D / 2023 Civil 3D 均通过 Preview 与 native Undo 验证。
- **TOOLBOX-001 Round 3B: CLOSED / GO / PASS**。

## 2026-08-28 — Round 3A independent final review / Round 3B ready

- Windows evidence Outer SHA independently matched:
  `77B113A541C3BA008C80460460D6C7EBDAE4F9C69501195EE566D4DDC6F9B704`
- internal SHA: 146/146, 0 mismatch.
- 8 static/policy gates independently PASS.
- Production delta vs Round 3 Start verified:
  - added 3 Services/Cad read Services
  - changed MCP CadProductionTools + ToolboxRegistry
  - added Round3 policy test
  - no removals
- Shared Service architecture proven by code and runtime.
- 2024 GUI/MCP semantic parity accepted.
- 2021/2022/2023 minimum runtime accepted.
- Round 3A = CLOSED / GO / PASS.
- Corrected stale `NEXT_TASKS.md` Round 3A checkbox.
- Round 3B safety:
  - polyline.clean ordinary write: Preview + Real + Undo, no confirm
  - layer.cleanup high-destructive: Preview + explicit Real, at most one confirm
  - no second confirmation
- Current moved to Round 3B.

## 2026-08-28 — TOOLBOX-001 Round 3A First Real CAD Tools: CLOSED / GO / PASS

- **8 个 static/policy gates 全部 PASS**：
  - `verify_source.py`: `cs=54 xaml=7 python=13 themes=3 tools=65 xml=13 errors=0`
  - `verify_release_source.py`: `errors=0`
  - `test_extension_policy.py`: `12/12 PASS`
  - `test_quick_install_policy.py`: `10/10 PASS`
  - `test_civil_api_compat_policy.py`: `8/8 PASS`
  - `test_toolbox_foundation_policy.py`: `10/10 PASS`
  - `test_toolbox_round2_interaction_policy.py`: `16/16 PASS`
  - `test_toolbox_round3_real_cad_policy.py`: `16/16 PASS`
- **Clean Build Matrix 全部 PASS**：
  - AutoCAD 2021 / 2022 / 2023 / 2024: 0 errors
  - Civil 3D 2022 / 2023 / 2024: 0 errors
  - Net48 external `System.Text.Json.dll` = 0。
- **P0 架构验证（Shared Service 模式）**：
  - 提取 `PolylineInspectionService`、`LayerAuditService`、`DrawingAuditService` 到 `Services/Cad/`；
  - GUI Toolbox 与 MCP Tools 统一调用相同底层 Service；
  - GUI 绝不直接调用 MCP / HTTP / Gateway；
  - Service 不读取 `MdiActiveDocument`，由 Runner 传入 `Database`/`Transaction`/`CadExecutionContext`；
  - MCP names/contracts/schemas 保持不变，总工具数 65/28/58 严格无增减。
- **Runtime 2024 (Civil 3D 2024 P0)**：
  - `cad.drawing.audit` GUI 与 MCP 计数严格一致（scanned=325, layers=146, issues=82）；
  - `cad.layer.audit` GUI 与 MCP 结果一致（total_layers=146, purgable=0）；
  - `cad.polyline.inspect` 准确识别重复顶点与零长段；
  - Favorites / Recent 对真实 CAD 工具正常可用；
  - 0 unhandled exception，0 token leak。
- **Runtime 2021 / 2022 / 2023 Minimum Smoke**：
  - 2021 AutoCAD / 2022 Civil 3D / 2023 Civil 3D 均成功打开 Toolbox 并执行只读审计工具。
- **TOOLBOX-001 Round 3A: CLOSED / GO / PASS**。

## 2026-08-28 — TOOLBOX-001 Round 2 CLOSED / GO / PASS

- Windows final:
  `YangCAD_FULL_REPO_ROUND2_WINDOWS_INTERACTION_GO_20260828.zip`
- Outer SHA verified:
  `4E65785D9292803B72C6A14A9E11767C73DC6742BA8550CDE92C8E72490E2E9E`
- internal SHA: 140/140
- 7 static/policy gates independently PASS
- production delta vs candidate:
  only `ToolboxParameterPanel.xaml.cs`
- source fix:
  four `System.Windows.Visibility` qualifications for CS0176
- 2024 full interaction PASS
- 2021/2022/2023 minimum runtime PASS
- settings isolation PASS
- real process restart persistence PASS
- token leak=0
- unhandled exception=0
- verdict: Round 2 CLOSED / GO / PASS
- current moved to Round 3A:
  drawing audit / layer audit / polyline inspect + shared Service proof

## 2026-08-28 — TOOLBOX-001 Round 1 Windows CLOSED / GO / PASS

- Windows 回传：
  `YangCAD_FULL_REPO_ROUND01_WINDOWS_SMOKE_GO_20260828.zip`
- Outer SHA independently matched:
  `CCFC2C50B6FCCC088822C457CB0ED4D858F09F80B4213D7216365EE6B7C459C0`
- internal SHA256SUMS: 131/131, mismatch=0.
- 相对实际测试输入 `Round00 Claude GO`：
  - production diff 仅 `ToolboxRegistry.cs`
  - docs diff 4 files
- 唯一源码修复：Autodesk Application fully-qualified，解决 `CS0104`。
- ChatGPT 对回传 ZIP 独立重跑 6 static/policy gates：全 PASS。
- Round00 与后续 Round1 Start 之间 production code diff=0，因此 Windows 证据可无损合并到最新文档主线。
- 2024 full UI P0 PASS。
- 2021 read/write/native Undo/Batch/UI PASS。
- 2022/2023 minimum UI PASS。
- token leak=0；unhandled exception=0。
- 判定：Round 0 / Round 1 CLOSED / GO / PASS。
- Current 切换：Round 2 Interaction / ParameterPanel / Favorites / Recent。

## 2026-08-28 — Prestart final audit / Round 1 formally started

- 完成正式开工前最后静态体检。
- 生产代码 blocker = 0。
- Autodesk 官方 ManagedRefGuide 已确认 `PaletteSet.AddVisual(string, Visual, bool)` 正式存在。
- 发现并修正文档遗留：
  - AGENT_RULES 旧“一任务一分支一 PR”与 main-only 冲突；
  - AGENT_RULES 对所有 GUI write 强制 log/dry_run 语义过宽；
  - TOOL_DEVELOPMENT_GUIDE 仍只描述 MCP Tool；
  - RELEASE_CHECKLIST / TEST_PLAN 仍停在 Phase B runtime paused。
- D-012 原地更新为个人项目 main-only Git 规则，不新增 D-031。
- 生产代码 0 修改。
- 结论：TOOLBOX-001 Round 1 READY / FORMALLY STARTED。

## 2026-08-28 — Claude Round 0 review accepted / Round 1 ready

- Claude 对完整 Round 0 源码做静态审查，结论：**GO TO WINDOWS ROUND 1**。
- 6 个 static/policy gates 全 PASS；65/28/58 contract 未被 Round 0 触碰。
- Claude 唯一 P0 候选是三参数 `PaletteSet.AddVisual("工具箱", _view, true)` API 是否存在；ChatGPT 随后核对 Autodesk 官方 ManagedRefGuide，确认 `AddVisual(string, Visual, bool)` 为正式 API，因此不做预防性代码修改。
- Claude 5 条 P1 进入 backlog，本轮不改 Round 0 生产代码，保持 Windows baseline 冻结。
- Round 1 SOP 更新：
  - 2024 必须确认七分类实际渲染、mcp-status 卡片可执行；
  - 2022/2023 minimum Toolbox open/content smoke 从可选提升为必做；
  - 2021 增加 minimum Toolbox open/content。
- 当前状态：Round 0 STATIC GO / CLAUDE GO / WINDOWS NOT TESTED；Round 1 READY TO RUN。

## 2026-08-28 — Single-main handoff freeze

- 用户明确旧 YANG-AGENT_CAD 历史完全无保留价值，项目以后只有 YangCAD 当前主线。
- 仓库策略改为：
  - `main` 唯一开发线 / 事实源；
  - orphan reset；
  - 删除 archive/backup 分支；
  - 不恢复旧 project-state/handoff/worklog。
- RELEASE-002 Phase B1 Civil 2022/2023/2024 已正式 CLOSED/PASS：
  - health=58；
  - PVI PASS；
  - 2022/2023 legacy Station；
  - 2024 RawStation；
  - Batch PASS；
  - runtime exception 0；
  - FeatureLine/Corridor 保留 SAMPLE_MISSING limitation。
- TOOLBOX-001 Round 0 保持 STATIC IMPLEMENTED / WINDOWS NOT TESTED。
- 生成完整仓库包、Claude Review Prompt、新对话完整交接包。
- 下一步：GitHub 单主线 reset → Claude review → Round 1 Windows UI/Host smoke。

## 2026-08-28 — RELEASE-002 Phase B1 closed + TOOLBOX-001 Round 0 implemented

- 接受 Windows Phase B1：Civil 2022/2023/2024 `/health=58`；PVI 100 samples PASS；2022/2023 Station fallback、2024 RawStation；Batch available；0 exception；token 0 leak。
- FeatureLine/Corridor 三年均 SAMPLE_MISSING，保留 limitation，不伪装对象级 runtime 已测。
- 用户确认 UI 原型方向可用，开始真实 Toolbox framework。
- Round 0 新增 PaletteSet、Toolbox Registry/Descriptor/Adapter、ParameterShape、CadExecutionContext/ReadContext/WriteContext、Control Center 三分区、About、搜索/分类/规划骨架、Recent/Favorites 数据口。
- 现有 65 MCP Tool 业务逻辑与原 Read/Write runner 调用点不迁移。
- 旧 GitHub 仓库将保留 archive 分支后清理 main，后续在 `5788324/YANG-AGENT_CAD` 开发。

## 2026-08-28 — Toolbox Claude/ChatGPT architecture merge locked

- 用户重申最高目标：个人辅助工作插件箱；AI 快速开发/迭代/维护；功能尽快上线干活，不做过严企业安全。
- Claude 第二轮评审与 ChatGPT 基本完全收敛。
- 新发现并处理：
  - D-011 的“MCP Tool 是主接口”产品优先级表述与 Toolbox 新方向冲突；D-023 保留“UI 不是核心协议”，废止“MCP 是产品主接口”的优先级。
  - D-016 “UI 不堆面板”失效；三主题约束保留。
  - D-007 dry_run 继续适用于 MCP/AI typed write contract，不扩散为所有 GUI 写 Tool 的义务。
- 正式写入 D-023 ~ D-030。
- CadExecutionContext 最终约束：
  - 只有 Document/Database/Editor/Transaction；
  - 只能由 CadTransactionRunner 构造；
  - 不直接修改现有 65 Tool 使用的 Read/Write；
  - 不采用同名 Func<CadExecutionContext,T> overload，避免 lambda overload ambiguity；
  - 新 Toolbox 使用明确命名 ReadContext/WriteContext（或等价唯一命名）。
- RELEASE-002 Phase B 改为唯一精简固定清单，保留逐年/分层证据纪律。
- 本轮仍为文档/架构决策更新，生产代码 0 修改。

## 2026-08-28 — Toolbox product direction planning

- 用户决定 YangCAD 长期扩展为 CAD / Civil 3D 多功能工具箱 + MCP / AI 核心。
- 近期不批量开发功能，先搭 UI / framework；功能进入 Backlog，后续按用户需求填充。
- 个人项目定位继续：保留 Autodesk API correctness / Transaction / Undo / Batch isolation / input protection / localhost token；不做企业 RBAC、多级审批、普通 Tool 多层权限和大量确认。
- 新增：
  - `docs/TOOLBOX_ARCHITECTURE.md`
  - `docs/FEATURE_BACKLOG.md`
  - `docs/CLAUDE_DISCUSSION_BRIEF.md`
- 当前计划：
  - RELEASE-002 Phase B runtime 保留但暂停；
  - TOOLBOX-001 作为下一主要开发方向；
  - 先由 Claude / ChatGPT 对 UI、Registry、Service boundary、参数系统、metadata、安全边界做设计复核。
- 本轮生产代码 0 修改。

## 2026-08-28 — Project pause / full documentation & new-chat handoff refresh

- 用户要求暂停开发，更新项目各类文档、项目进度，并准备上下文切换/新对话完整交接。
- 用户确认 AutoCAD 2021 工作已结束：后续 RELEASE-002 runtime matrix 可把 2021 与 2022/2023/2024 一起纳入测试。
- 本轮不执行任何 Autodesk runtime，不修改生产代码。
- 统一当前事实：
  - TASK-001~007 CLOSED / GO / PASS；
  - RELEASE-001 Quick Install 2024 CLOSED / GO / PASS；
  - RELEASE-002 Phase A Build Matrix CLOSED / PASS；
  - RELEASE-002 Phase B Runtime Matrix 2021–2024 PAUSED / READY。
- 新增 `docs/DOCUMENT_AUTHORITY.md`，明确历史 review/task 文档是历史快照，不能覆盖当前状态。
- 新增统一 `tasks/RELEASE_002_PHASE_B_RUNTIME_MATRIX_2021_2024.md`，取代此前“暂不碰 2021”的 Phase B1 临时测试约束。
- 生产源码相对 Phase A reviewed baseline 0 修改。
- 生成新对话完整交接包与新的 docs-reviewed source baseline。

## 2026-08-28 — RELEASE-002 Phase A Build Matrix final review

- Windows targeted retest: Civil 3D 2022 / 2023 / 2024 clean build 全部 0 error / 0 warning。
- schema-v2 matrix：
  - 2021 AutoCAD PASS / Batch PASS / Civil NOT_INSTALLED；
  - 2022 AutoCAD PASS / Batch PASS / Civil PASS；
  - 2023 AutoCAD PASS / Batch PASS / Civil PASS；
  - 2024 AutoCAD PASS / Batch PASS / Civil PASS；
  - 2025–2027 NOT_INSTALLED。
- 2021–2024 net48 external System.Text.Json.dll = 0。
- 本轮 Windows 源码修改 0；Candidate 本身即包含 Civil 定点兼容修复。
- **RELEASE-002 Phase A BUILD MATRIX：CLOSED / PASS**。
- 下一步 Phase B1：Civil 3D 2022/2023/2024 runtime compatibility；不触碰 AutoCAD 2021 GUI。

## 2026-08-27 — RELEASE-002 Phase A / Civil compatibility precheck

- Phase A Windows clean build：2021 AutoCAD+Batch PASS；2022/2023 AutoCAD+Batch PASS 但 Civil FAIL；2024 全 PASS；2025–2027 NOT_INSTALLED。
- 2022 Civil compile blockers：`FeatureLine/Corridor.IsReferencedSourceExisting`、`ProfilePVI.RawStation`。
- 2023 Civil compile blocker：`ProfilePVI.RawStation`。
- 定点修复：reference source existence 改 nullable reflection capability；PVI raw station 改 RawStation→Station reflection fallback。
- `Test-CompatibilityMatrix.ps1` 升级 schema v2，记录 AutoCAD/Batch/Civil 分项 build 与 System.Text.Json 检查。
- 新增 `tools/test_civil_api_compat_policy.py`：8/8 PASS。
- 不改 Core/Gateway/Broker/Transaction/Setup/Batch 行为；65/28/58 contract 不变。
- 当前：STATIC GO / Windows Civil 2022-2024 targeted build retest required。

## 2026-08-27 — RELEASE-001 Quick Install 2024 FINAL REVIEW（ChatGPT）

- 独立核对 Windows FINAL Outer SHA：`5BCA76CFA7E946C5EE5A89F4520335116723B11B63881A875003C2367B8A2A44`。
- 包内 SHA256SUMS：153/153，0 mismatch；但原 FINAL 误包含 `mcp/yangcad_mcp/__pycache__/*.pyc`，因此对原包重跑 `verify_source.py` 会报 generated cache/binary 2 errors。
- Windows real retest 关键门禁接受：target-year isolation PASS；2024 SECURELOAD=1 保持；AutoCAD 28 / Civil 58 无 NETLOAD Autoloader PASS。
- Windows 实机代码修复接受：`InstallerService` TRUSTEDPATHS 不加引号写入已在 secure mode 实测；Install/Update/Repair 版本比较补齐；`Clean.ps1` 扩展清理范围合理。
- REVIEWED FINAL 仅清除生成缓存并统一事实文档；Windows PASS 后不再修改生产代码。
- **RELEASE-001 Quick Install 2024：CLOSED / GO / PASS**。
- 下一阶段：**RELEASE-002 Compatibility Matrix**。

# Worklog

## 2026-08-27 — RELEASE-001 Quick Install Targeted Retest（2024 Autoloader & Isolation）
执行者：Antigravity

完成：
- 静态门禁：`verify_source.py` tools=65 errors=0；`verify_release_source.py` errors=0；`test_extension_policy.py` 12/12 PASS；`test_quick_install_policy.py` 10/10 PASS。
- 真编译：AutoCAD 2024 / BatchWorker 2024 / Civil 3D 2024 / Setup 0 error，net48 业务输出 0 `System.Text.Json.dll`。
- PackageContents.xml 官方规范：AutoCAD `AutoCAD*`，Civil 3D `Civil3D`（无 `Civil3D*`）。
- **Target-Year 严格隔离 (P0)**：`release-manifest.json years=[2024]` 驱动；AutoCAD 2021 进程 PID、Profile TRUSTEDPATHS/APPAUTOLOAD 全程 100% 保持未变；Setup 在 2021 运行下允许安装/修复，在 2024 运行下精准拦截并警告。
- **SECURELOAD & TRUSTEDPATHS 门禁**：Setup /repair 保持 SECURELOAD 原生值不修改；TRUSTEDPATHS 精确配置；
- **2024 Autoloader（严格禁止 NETLOAD）**：
  - AutoCAD 2024：自动加载，`/health` 28 工具，0 extension.* 工具，0 Civil 工具，`cad.status` 正常；
  - Civil 3D 2024：自动加载，`/health` 58 工具，0 extension.* 工具，`civil.status` (available=True) 正常；
- 独立 AcCoreConsole：`batch.status` 独立进程模式正常。
- 静态安全扫描：Token 0 leak。
- 结论：**RELEASE-001 QUICK INSTALL TARGETED RETEST PASS / GO**。



## 2026-08-17 — TASK-000 项目启动
执行者：ChatGPT

完成：新建 YangCAD Agent；MCP v2、Gateway/Broker、14 Tool、8 Theme、AutoCAD/Civil 分程序集、bundle/build/deploy、项目文档和任务体系。

未完成：Windows 编译、AutoCAD/Civil 3D 实机、真实 MCP Host。

结论：静态基线 GO，下一步 TASK-001。
## 2026-08-17 — TASK-001 preflight static review
- 完整 Project Bootstrap `verify_source.py` 初始结果：`cs=23 xaml=10 python=4 themes=8 tools=14 xml=14 errors=0`。
- 修复 MCP v2 实际暴露 Tool name 与 dotted contract 不一致。
- 修复 Build/Setup native process 非零退出码可能被 PowerShell 误报成功。
- 修复 Setup-Mcp 无 uv fallback 未安装本地项目。
- 修复 Broker queued timeout 后仍可能迟到执行写操作的 P0 风险。
- 修复 RuntimeHost 半初始化清理。
- 修复 README quick-start 控制字符。
- Windows/Civil 3D 2024 runtime 仍待 TASK-001 实机闭环。

## 2026-08-17 — TASK-001 MCP/UI/function hardening review
- MCP 优先复审全部 Gateway/Broker/Tool/UI 路径。
- P0：LocalGateway 从 HttpListener/HTTP.sys 改为 `TcpListener(IPAddress.Loopback)` 最小 HTTP/1.1，保留 `/rpc` + Bearer contract，避免 URL ACL/管理员配置依赖。
- P0：增加 `/health` 身份/Tool 能力验证；stale instance 自动跳过；Civil Tool 按实际注册能力路由；支持 `YANGCAD_INSTANCE_PID`。
- P0：加入 `PID + StartedAt + Document + DocumentId` 读取绑定和 RPC `ExpectedDocument + ExpectedDocumentId`，写 Tool 防跨进程/跨 DWG/同名重开误写；MCP 调用串行化。
- P0：移除 Python `/rpc` 固定 40s 读超时；queue timeout 由 Broker 唯一管理，started request 等最终结果。
- P1：localhost RPC 显式绕过系统 proxy；Broker 增加 `CMDNAMES` 活动命令门禁。
- P1：修严格几何数组、ACI 0–256、空参数/no-op update；layer.ensure dry_run 增加 would_create。
- P1/UI：关闭总写权限会清除删除授权；控制中心单实例；Ribbon null 时 Idle 重试，按钮在活动命令时先取消；Civil 初始化再次 EnsureRibbon。
- P2：Civil Surface/Alignment list 显式 ActiveDocument 空检查。
- 静态：`verify_source.py` PASS；Python compileall PASS；8 themes 资源完整；gateway stale/multi-instance/document-binding 模拟 PASS。
- Windows/Civil 3D 2024 compile/runtime/Undo/MCP Inspector 仍待 TASK-001。

### TASK-001 takeover review — UI / parameter / settings hardening
- 修复 WPF ThemeManager 对相对 `Themes/Obsidian.xaml` 不识别导致主题切换可能无效的问题。
- `settings.json` 已存在但损坏/不可读时写权限 fail-closed；保存采用临时文件替换，避免半写。
- `color_index` 字段存在但类型错误时明确拒绝，不再静默当成 null；entity.update 的 layer/linetype 拒绝空白字符串。
- Broker operation log 失败也必须在 finally 中释放 Pending，避免调用方永久等待。
- Gateway 请求读取增加本机连接读/写 socket timeout；RPC 业务执行仍由 Broker 管理，不恢复 Python 客户端写超时。
- Python gateway 再次模拟：stale instance、多 AutoCAD/Civil 能力路由、read→write 绑定、跨 DWG 写拒绝、session mismatch：PASS。

## 2026-08-17 — TASK-001 Windows runtime failure takeover / System.Text.Json + load + Broker
- 接收真实 Civil 3D 2024 运行反馈：`System.Text.Json, Version=8.0.0.0` 缺失；bundle/unsigned 加载被 Security 对话框阻塞；Idle-only Broker HTTP 全超时。
- 将上一轮实机编译修复正式并入源码：`AdWindows`、`AeccBaseMgd`、`PRODUCT/ACADVER`、Civil `Surface` 显式类型、Build exit-code 门禁。
- P0：重构 RPC Args 为 framework-neutral dictionary；2021–2024 net48 使用 `System.Web.Extensions/JavaScriptSerializer`，Core/AutoCAD 移除 `System.Text.Json` PackageReference；2025+ 使用运行时自带 STJ。
- P0：Build 每年清理输出；2021–2024 若输出/bundle 含 `System.Text.Json.dll` 直接失败，防旧依赖残留。
- P0：Broker 改为 AutoCAD UI WinForms Timer 50ms + Idle fallback；保留 CMDNAMES、queued-timeout cancel、started request exactly-once 语义。
- P1：新增 runtime.log；Extension 初始化失败改为记录并允许命令重试，避免 AutoCAD 启动弹未处理异常。
- P1：Deploy-Dev 新增 User/Machine scope；TASK-001 首闭环要求 Machine trusted path，User scope 单独验证 unsigned/trust。
- UI：控制中心内容区增加纵向滚动，防最小窗口裁切。
- 静态门禁：`cs=26 xaml=10 python=4 themes=8 tools=14 xml=14 errors=0`；Python/XML/XAML parse PASS。
- 当前新源码仍需 Windows + Civil 3D 2024 重新编译/实机；正式状态 NO-GO。

## 2026-08-17 — TASK-001 RuntimeFix 实机闭环（用户/Codex 返回）
- Civil 3D 2024 / AutoCAD 2024：三个 P0 全部 GO。
- P0-1：clean build 0 error/0 warning；2024 输出与 bundle 无 `System.Text.Json.dll`；无缺失异常。
- P0-2：UI WinForms Timer + Idle fallback 实际派发；14 Tool 约 60–95ms 返回，无超时。
- P0-3：layer.ensure / entity.update / transform 一 Tool 一次 Ctrl+Z。
- MCP stdio `tools/list` 14 dotted name、Token 401、rollback、operation JSONL、Civil、普通 AutoCAD 隔离、双实例路由均通过。
- 实机顺带确认 Civil 2024 基础引用名称为 `AecBaseMgd.dll`，并修正 PowerShell 5.1 编码/Setup-Mcp virtualenv fallback。
- TASK-001 CLOSED / GO。正式发行仍保留 unsigned 代码签名/可信加载策略事项。

## 2026-08-17 — TASK-002 Generic MCP + UI（ChatGPT）
- 新增 8 个通用 MCP Tool，Python/C# contract 从 14 扩展为 22：object.describe、property.get/set、api.describe/invoke、command.run、extension.list/invoke。
- `cad.property.set`：只允许公共 Autodesk setter 与受支持的标量/枚举/Point/Vector 类型；默认 dry_run。
- `cad.api.invoke`：只允许公共 Autodesk instance method；拒绝 ref/out/pointer、基础设施危险方法、不支持的复杂参数/返回值；重载要求 signature 消歧；默认 dry_run。
- `cad.command.run`：实验性、默认关闭；使用同步 `Editor.Command`，不回退异步命令队列；禁止 PauseToken、换行与 ^C 拼接。
- 动态扩展：`%LOCALAPPDATA%\YangCADAgent\Extensions`；版本化 DLL 实现 `IYangCadExtensionOperation`，Host 持有 Transaction/commit/abort；invoke 默认关闭。
- Python WRITE_TOOLS 将新四个通用写通道纳入原 process/document binding。
- 控制中心新增 MCP 诊断、权限、通用降级链说明；复制诊断不含 Token。
- 主题由 8 套收敛为 3 套：墨曜 / 月瓷 / 雾蓝。
- Ribbon 两个入口统一为 Large，并改用代码内矢量图标，无外部 PNG 依赖。
- 版本提升到 0.2.0-dev；Civil 2024 `AecBaseMgd.dll`、PowerShell 5.1 UTF-8、virtualenv fallback 合并回当前源码。
- 当前：STATIC GO；新增 8 Tool / 新 UI 的 Windows 2024 runtime = RETEST_REQUIRED。

- TASK-002 同步保留 Civil 实机隔离修复：`PRODUCT` 宿主守卫，Object Enabler 不等同 Civil 运行时。


## 2026-08-17 — TASK-002 0.2.1 Startup Hotfix（ChatGPT）
- 用户反馈 0.2.0 安装后 AutoCAD/Civil 3D 无法正常打开、卡住或崩溃；列为 P0。
- 复盘发现基线管理问题：TASK-001 实机最终 Windows 源码未完整回传，0.2.0 并非逐文件继承那份真机最终目录。
- MCP Core-first：TASK-001 10 AutoCAD Tool + Broker/Gateway 先初始化成功，再逐项注册 8 Generic Tool；Generic 单项失败仅降级并写 runtime.log。
- Ribbon 从 AutoCAD/Civil Initialize 同步创建改为 Idle 延迟；Ribbon 初始化失败停止本 session 重试，避免 Idle 异常循环。
- Ribbon runtime DrawingImage/Geometry.Parse 改为嵌入 PNG BitmapImage；图片失败退回 text-only。
- Civil ExtensionApplication 增加完整异常隔离，Civil 注册失败不得拖死 AutoCAD MCP Core。
- runtime.log 增加 core / optional / ribbon / Civil granular phase。
- 版本提升 0.2.1-dev。当前 STATIC GO；Windows AutoCAD 2024 / Civil 3D 2024 STARTUP RETEST REQUIRED。


## 2026-08-17 — TASK-002 0.2.2 CrashFix（ChatGPT）
- 用户返回 AutoCAD crash record：17:31:29 +08:00，CLR e0434352，System.dll，Managed Exception `System.ComponentModel.Win32Exception`。
- 重新审启动路径：最高嫌疑为 Broker `System.Windows.Forms.Timer` 的 native window/timer 资源；因缺完整 managed stack，未宣称唯一根因。
- 删除 WinForms Timer / `UseWindowsForms`；Broker 改为 Autodesk `ExecuteInCommandContextAsync`。
- 保留 timeout-cancel、exactly-once、Document Session affinity、权限、事务/Undo contract。
- Ribbon 延迟至少 5 秒 warm-up；MCP Core 不依赖 Ribbon。
- runtime.log 改为完整 stack + Bearer redaction；Gateway background loop 顶层异常捕获。
- verify_source 更新：禁止 Broker WinForms Timer，要求 `ExecuteInCommandContextAsync`。
- 当前 STATIC GO；Windows/Civil 3D 2024 P0 RETEST REQUIRED。

## 2026-08-17 — TASK-002 0.2.3 Start Page CrashFix（ChatGPT）

- 实机再次复现：0.2.2 已无 WinForms Timer，但 Start 页仍 `System.ComponentModel.Win32Exception` 崩溃；旧 Timer 根因假设作废。
- 新 P0 线索：崩溃时无活动 DWG。
- `IExtensionApplication.Initialize` 改为 start-page-safe：只挂诊断/DocumentActivated；MCP Runtime/Civil/Ribbon 延迟到真实 DWG 激活。
- RuntimeHost/Broker 双层禁止无 DWG 启动。
- 删除未实机证明的 ExecuteInCommandContextAsync Broker 替换；恢复 TASK-001 真机 GO 的 Timer + Idle，并把 Timer 创建异常变成可降级日志事件。
- Civil PRODUCT/Tool 注册同样延迟到 DocumentActivated。
- 新增 AppDomain/TaskScheduler 异常日志。
- 下一硬门禁：Civil 3D 2024 Start 页 60 秒稳定，再开 DWG 验证 MCP。

## 2026-08-18 — TASK-002 0.2.3 Start Page CrashFix 复验（DeepSeek/Codex）
- 清理：移除 0.2.1/0.2.2 bundle、Loaded/Applications 注册、旧 artifacts 与 instances/logs；确认无旧 DLL。
- verify_source.py：修正 Broker `AcApp`→`Application` 别名后 PASS（cs=38 xaml=5 python=4 themes=3 tools=22 xml=10 errors=0）。
- Build 2024：0 错误 0 警告；无 System.Text.Json.dll；DLL SHA 记录（AutoCAD/Core/Civil3D）。
- Deploy Machine：打包侧==部署侧逐文件 6/6 match，全文件清单 SHA `81EF4D5C…4A8C1B`。
- 真实缺陷修复：本机 Civil 3D 2024（LMS 24.3s）`PRODUCT` 系统变量返回 "AutoCAD" → 0.2.3 PRODUCT 宿主探测失效、civil 4 工具不注册（tools=18）。修复 `IsCivil3DHost()`：保留 PRODUCT 快路径 + 追加 `CivilApplication.ActiveDocument` try/catch 兜底（TASK-001 已实机验证有效）；重建 0 错误，部署一致。
- 实机：Civil 3D 2024 加载后 `tools=22`，22-Tool 契约 missing=0 extra=0 exact22=True；`civil.status available=True`；`cad.status` ×20 fail=0 avg 15ms；layer.ensure dry_run/写入/回滚通过；command.run 按设计默认未授权。
- Start 页：C3D 停 Start 页 60s 不崩（本机 unsigned 不自动加载、Start 页命令行不可聚焦，手动 NETLOAD 不可行）；插件加载 + Timer 运行 60s 不崩——Timer/Idle tick 命中 `HostReadyForRequest` 的 `MdiActiveDocument == null` 守卫，不触碰 Autodesk API。
- 结论：TASK-002 复验 PASS。记录项：Directory.Build.props Version=0.2.1 与包名 0.2.3 不一致（按指示只记录不修）。未推 Git。


## 2026-08-18 — TASK-003 CAD Productivity（ChatGPT）
- 以 `YangCADAgent_0.2.3_TASK002_FINAL_SOURCE_20260818` 为唯一事实基线接管。
- 源码复查修复：Civil Host 产品名 override；InstanceRegistry SaveAs/zero-doc identity 刷新；extension.list 权限关闭时不加载/实例化 DLL；版本统一 0.3.0。
- 新增 5 个强类型 Tool：cad.line.create / cad.polyline.create / cad.text.create / cad.block.list / cad.block.insert。
- 新增 write Tool 全部复用既有 process/document binding、AllowWrites、dry_run、DocumentLock/Transaction/rollback；Python 默认 dry_run=true。
- Block insert 支持 Attribute Tag 写值并返回 unknown tags；Polyline 首版仅支持共面同 Z 的 2D lightweight Polyline。
- 静态门禁：27 Tool contract；普通 AutoCAD 预期 23、Civil 3D 预期 27。Windows/Civil 3D 2024 真编译/Undo/属性块仍待 TASK-003 实机。

## 2026-08-20 — TASK-003 CAD Productivity 实机闭环（Antigravity）
- `verify_source.py`：cs=32 xaml=5 python=4 themes=3 tools=27 xml=9 errors=0，静态门禁 PASS。
- Clean `Build.ps1 -Year 2024`：AutoCAD 与 Civil 3D 0 error / 0 warning，bin 目录无 `System.Text.Json.dll`。
- `Deploy-Dev.ps1 -Year 2024 -Scope Machine`：打包侧（artifacts/YangCADAgent.bundle）与部署侧（C:\Program Files\Autodesk\ApplicationPlugins\YangCADAgent.bundle）全部 6 个文件 SHA256 逐文件 100% 匹配。
- 实机运行环境：Windows 11 + Autodesk Civil 3D 2024 (24.3s LMS Tech) + AutoCAD 2024。
- Real stdio `tools/list`：MCP Server 暴露 exact 27 tools；普通 AutoCAD 模式 23 tools（0 civil），Civil 3D 模式 27 tools。
- Host 产品名修复：Civil 3D 探测成功后正确覆盖 `CadHostInfo.Product` 为 `Civil 3D`，`cad.status` 与 WPF 控制中心 Host 均正确显示 `Civil 3D`，不再误报 AutoCAD。
- Save As 测试：调用 `SaveAs` 另存为新名称后，Broker tick 自动触发 `InstanceRegistry.RefreshCurrentIfChanged()`，`/health` 与 instance JSON 及时刷新为新文档名与新 Document Session，新 DWG 绑定写操作成功。
- Zero-doc 测试：关闭所有 DWG 回到 Start 页后，`/health` 与 instance JSON 刷新为无文档（`document=""`），程序持续响应无崩溃；旧绑定写操作被立即拒绝，杜绝延迟写入。
- 扩展安全模式：`AllowExtensionInvoke=false` 时，`cad.extension.list` 仅列出 DLL 文件元数据，不加载程序集、不实例化扩展；开启权限后正常发现并执行扩展。
- 新增 5 个 CAD Productivity Tool 逐项闭环：
  - `cad.line.create`：dry_run（handle=None, created=false）、real（创建线段并返回 handle）、entity.get 校验、零长度输入拒绝（距离<=1e-9）、单次 Undo 回滚通过。
  - `cad.polyline.create`：dry_run、real（闭合多段线）、不同 Z 输入非共面拒绝（所有顶点共用同一 Z 高程）通过。
  - `cad.text.create`：DBText（单行文字）与 MText（多行文字，支持 width 参数）各 dry_run/real/Undo；多行文字传给 DBText 被正确拒绝。
  - `cad.block.list`：列出普通块 `TEST_SIMPLE_BLOCK` 与 `TEST_ATTR_BLOCK`，准确返回属性定义数（3），默认排除布局、匿名块与外部参照。
  - `cad.block.insert`：普通块（统一缩放/旋转/图层）与属性块（写入有效 Tag `TAG1`，未定义 Tag 返回至 `unknown_attribute_tags`，Constant Tag 被忽略）全部通过。
- 事务健壮性：5 个新增写 Tool 错误注入测试均被隔离并回滚 Transaction，DWG 会话持续健康可用。
- 审计与合规：Operation JSONL 完整记录新增 Tool 操作，且无任何 Token 泄漏。
- 回归复验：`cad.status` 连续 20 次 fail=0（平均 ~57ms）；`cad.property.set` dry_run 正常；`civil.status` `available=true`。
- 结论：TASK-003 GO / PASS。


## 2026-08-20 — TASK-004 Civil Pipe/Structure 实机闭环（Antigravity）

- 静态门禁与清理：移除旧 bundle、instance 与日志；`verify_source.py` 静态门禁通过（cs=33 xaml=5 python=4 themes=3 tools=33 xml=9 errors=0）。
- Clean `Build.ps1 -Year 2024`：AutoCAD 与 Civil 3D 0 error / 0 warning；bin 目录无 `System.Text.Json.dll`。
- `Deploy-Dev.ps1 -Year 2024 -Scope Machine`：打包侧（artifacts/YangCADAgent.bundle）与部署侧（C:\Program Files\Autodesk\ApplicationPlugins\YangCADAgent.bundle）全部 6 个文件 SHA256 逐文件 100% 匹配。
- 修复真实编译缺陷：`CivilPipeStructureTools.cs` CS1546 索引属性错误（`structure.ConnectedPipe[i]` 改为 `structure.get_ConnectedPipe(i)`）。
- 修复真实运行缺陷：`CivilStructureUpdateTool` 中设置 `rim_elevation` 时，同步更新 `structure.Position` 的 Z 坐标，且避免再次调用 `structure.RimElevation` 导致 Civil 3D 内部重置，确保井顶高程正确生效。
- Real stdio `tools/list`：MCP Server 暴露 exact 33 dotted tools；宿主 `/health` ToolRegistry 在普通 AutoCAD 下 23 tools（0 civil），在 Civil 3D 下 33 tools。
- 宿主健康度：`cad.status` 连续 20 次 0 fail（平均延迟 ~45ms），正确识别宿主为 `Civil 3D`。
- Gravity Pipe Network 测试与 6 个 Civil 3D 工具逐项验证：
  - `civil.structure.list` 与 `civil.structure.get`：准确返回 2 个 Structure 的 network、name、handle、rim、sump、auto adjustment、connected pipe handles 等全部关键属性。
  - `civil.structure.update` dry_run：`automatic_rim_surface_adjustment=false` + `rim_elevation` preview 准确计算，事务 abort 后 DWG 中真实高程完全未变。
  - `civil.structure.update` real：修改实际 rim 高程并关闭 auto 贴合，数据持久化并能通过后续 get 精确校验。
  - `civil.structure.update` sump_elevation：更新井底高程，自动切换 `ControlSumpBy=ByElevation`，并正确遵循 Civil 3D 基于最低连接管底的保护约束。
  - Structure 参数门禁：auto adjustment=true 与 rim_elevation 冲突拒绝、无修改字段拒绝、非 Structure Handle 拒绝。
  - `civil.pipe.list` 与 `civil.pipe.get`：准确返回 Pipe 的 start/end point、center elevation、invert elevation、InnerHeight/Width、Length2D、slope、signed slope 及连接 Structure handles。
  - `civil.pipe.update` invert dry_run：起端管内底高程 preview 准确计算（`center Z = invert + InnerHeight/2`），XY 坐标保持完全不变，真实对象未变。
  - `civil.pipe.update` real：起端管内底修改持久化成功，终点与 XY 未发生非预期偏移。
  - `civil.pipe.update` 双端与坡度：同时修改两端管内底高程，`slope_signed_start_to_end` 基于 `Length2DCenterToCenter` 与高差准确计算。
  - Pipe 参数门禁：同端同时传 center 与 invert 拒绝、非 Pipe Handle 拒绝。
  - ApplyRules 与事务回滚：`apply_rules` 默认 false；显式传 `apply_rules=true` 正确返回 `rules_applied=true`；错误注入抛出异常时事务安全回滚，DWG 会话保持可编辑状态。
- 审计与合规：Operation JSONL 完整记录 33 个 Tool 操作，无任何 Token 泄漏。
- 结论：TASK-004 闭环通过，GO / PASS。


## 2026-08-20 — TASK-005 Civil Read / Design 实机闭环（Antigravity）

- 静态门禁与清理：移除旧 bundle、instance 与日志；`verify_source.py` 静态门禁通过（cs=34 xaml=5 python=4 themes=3 tools=45 xml=9 errors=0）。
- Clean `Build.ps1 -Year 2024`：AutoCAD 与 Civil 3D 0 error / 0 warning；bin 目录无 `System.Text.Json.dll`。
- 确认 `AeccPressurePipesMgd.dll` 成功引用编译，且未被复制到自有 bundle（bundle 保持 6 文件结构）。
- `Deploy-Dev.ps1 -Year 2024 -Scope Machine`：打包侧（artifacts/YangCADAgent.bundle）与部署侧（C:\Program Files\Autodesk\ApplicationPlugins\YangCADAgent.bundle）全部 6 个文件 SHA256 逐文件 100% 匹配。
- 修复真实编译缺陷：
  - `CivilReadDesignTools.cs` 中 `CogoPoint` 无 `StyleName` 属性，改为通过 `point.StyleId` 在事务中安全读取 `PointStyle.Name`。
  - `CivilSurfaceBoundariesTool` 中 `surface.BoundariesDefinition` 与 `SurfaceOperationAddBoundary` 集合无 `IEnumerable`，改为索引循环 `BoundariesDefinition[i]` 和 `operation[j]`。
  - `CivilProfileListTool` 修复可空警告；`CivilProfilePviListTool` 使用 `RawStation` 代替已过时的 `Station`。
- 修复真实运行缺陷：
  - `CivilProfilePviListTool` 中首个 PVI 无 `GradeIn`、最后一个 PVI 无 `GradeOut`（直接访问抛异常），增加 try-catch 安全赋 null。
  - `CivilAlignmentPointLocationTool` 与 `CivilProfileElevationTool` 增加超出桩号范围显式拦截抛错，避免隐式越界。
- Real stdio `tools/list`：MCP Server 暴露 exact 45 dotted tools；宿主 `/health` ToolRegistry 在普通 AutoCAD 下 23 tools（0 civil），在 Civil 3D 下 45 tools。
- 宿主健康度：`cad.status` 连续 20 次 0 fail（平均延迟 ~46ms），正确识别宿主为 `Civil 3D`。
- 12 个 Civil Read / Design 工具全部通过实机闭环验证：
  - **Surface**（`civil.surface.list`, `civil.surface.get`, `civil.surface.elevation`, `civil.surface.boundaries`）：
    - 列表与属性准确读取（TIN Surface、style、boundary_operation_count）；
    - 范围内点返回 `on_surface=true` 及正确高程（18.0），范围外点准确返回 `on_surface=false` 与 `elevation=null`（不伪造 0 或邻近高程）；
    - 边界定义与顶点读取准确，`vertex_limit=2` 触发 `truncated=true` 且宿主保持稳定。
  - **Alignment 双向查询**（`civil.alignment.point_location`）：
    - `station_offset` 与 `point_location` 往返算回原坐标误差 < 1e-3；
    - 越界桩号显式拒绝，不夹取端点。
  - **Profile & PVI**（`civil.profile.list`, `civil.profile.get`, `civil.profile.elevation`, `civil.profile.pvi.list`）：
    - 全图与按 Alignment Handle 过滤读取均正确；
    - `profile.elevation` 抽测 3 个桩号高程与坡度准确，越界桩号显式拒绝；
    - `profile.pvi.list` 准确读取全部 PVI 桩号、高程、进出坡度（首尾 PVI 进出坡度安全为 null），`limit=2` 正确截断。
  - **COGO Point**（`civil.cogo_point.list`, `civil.cogo_point.get`）：
    - 准确读取编号、E/N/Z 坐标、原始描述，`get` 与 `list` 一致，`limit` 生效。
  - **Pressure Network 只读**（`civil.pressure_network.list`, `civil.pressure_network.get`）：
    - 确认只读属性 `read_only=true`，无任何写操作、无 Undo 产生、无 setter 调用。
  - **异常注入与稳定性**：
    - 错误 Handle 注入安全返回错误，CAD 会话持续健康，后续写操作（`cad.line.create`）成功执行。
- 审计与合规：Operation JSONL 完整记录 45 个 Tool 操作，无任何 Token 泄漏。
- 结论：TASK-005 实机闭环全部通过，GO / PASS。



## 2026-08-20 — TASK-005 FINAL_SOURCE 复核 / TASK-006A 启动（ChatGPT）

- 核对 TASK-005 最终源码 ZIP 外层 SHA 与用户交付值一致。
- 复跑 `verify_source.py`：`cs=34 xaml=5 python=4 themes=3 tools=45 xml=9 errors=0`。
- 内部 SHA256SUMS 108/108 PASS。
- 未发现新的 MCP Core P0；记录 Pressure Network 实机样本覆盖仍以只读 smoke/contract 为主。
- TASK-006 拆成 006A/006B/006C，避免一次同时引入 CAD 清理、复杂 Civil API 与批量 DWG 三类高风险面。
- TASK-006A 新增：polyline.inspect/clean、layer.audit/cleanup、drawing.audit。
- Polyline clean 对 bulge/逐段 width fail-closed；Layer cleanup 必须先 Database.Purge 且复用删除权限。
- 版本提升到 0.6.0；MCP stdio 50，plain AutoCAD 28，Civil 3D 50。
- 当前仅 STATIC GO，待 Windows + AutoCAD/Civil 3D 2024 实机。


## 2026-08-21 — TASK-006A 非宿主预检 / fail-closed 收紧（ChatGPT）

- 在 Windows 实机前复跑 50-tool 静态契约、源码 SHA、Python compileall 与 0.5.0→0.6.0 增量 diff。
- 发现并修复两个实机前可确定的问题：极小非零 bulge/width 可能被 tolerance 放行；长线段 scale-relative 共线判定可能超出绝对 tolerance 误删中间点。
- 改为 AutoCAD `HasBulges` / `HasWidth` + exact width fail-closed，并用点到直线绝对垂距判断共线。
- 修正 `PROJECT.md` TASK-005 残留。
- 当前仍为 STATIC GO / WINDOWS 2024 RETEST REQUIRED；未启动 TASK-006B。

## 2026-08-24 ─ TASK-006A PRECHECK_FIX_CANDIDATE Windows 实机减负复验（ox-alpha / opencode）
- 交接候选：`YangCADAgent_0.6.0_TASK006A_PRECHECK_FIX_CANDIDATE_20260821.zip`（外层 SHA256 `c6995c27…d964883` 与交付值一致）。
- 候选差异复核：仅 `CadProductionTools.cs`（bulge/width fail-closed、共线改绝对垂距）、`verify_source.py` 与文档；Civil 业务源码未改 ✓。
- **真实编译缺陷再次出现并修复**：候选自修复前基线切出，`layer_count = layerTable.Count` 的 CS0828 仍在（AutoCAD 2024 `LayerTable` 无 Count 属性）；重施最小编译修复后 clean build **0 error / 0 warning**，输出无 `System.Text.Json.dll`。
- 静态门禁：`verify_source.py` = `tools=50 errors=0` PASS。
- 部署 Machine：bundle↔部署侧 6/6 SHA256 一致。
- Real stdio：exact **50** dotted tools。
- 普通 AutoCAD 2024 /health = **28 tools、0 civil handler**；`cad.status` ×20 = 0 fail（avg 74ms）。
- Polyline 套件（新特性全验证）：
  - inspect A/B/C/D/E 断言通过；
  - **fail-closed 修复生效**：bulge=1e-5 / width=1e-5（AutoCAD 最小可存储量级，<1e-6 会被 AutoCAD 自身归零）→ `has_bulges/has_nonzero_segment_widths=true`、`clean_supported=false`，且 `tolerance=0.5` 下 clean 仍明确拒绝——语义数据不再被 positional tolerance 放行；
  - **共线垂直距修复生效**：长段明显弯折样本 [(0,0),(500000,0.5),(1000000,0)] 在 `remove_collinear=true` real 下不被误删（changed=false，顶点数不变）；
  - dry_run 显式不落地；D/E/F/G 拒绝；最小顶点守卫保持；real 清理 layer/Closed/Elevation 保持。
- Layer 套件（本候选实机）：audit 计数与 purgable 判定正确；删除权限关闭时 Broker 拒绝 dry+real；权限开启 dry would_delete 仅 YANG_UNUSED_A、skipped 原因正确；real deleted==would_delete==[YANG_UNUSED_A]，YANG_USED/Layer 0/Defpoints/locked 全保留；定向 names 与不存在名报错 — **16/16**。
- Drawing audit（本候选）：扫描数与实体总数一致、零长线/空文字/Polyline 零长段检出、issue 行 handle/type/layer 准确、entity_limit/issue_limit 截断语义正确、只读稳定。
- 回归（本候选）：document.info / entity.get / entity.query / layer.ensure / line.create / text.create / property.set / api.invoke(SetBulgeAt 等) 通过；rollback 错误注入（SetBulgeAt 越界索引）事务安全拒绝且对象不变、会话健康；**跨 DWG Document Session 防误写实测拦截**（绑定 second → 切 active 到 main → 写被"活动 DWG 已变化"拒绝）。
- Civil 3D 2024 会话（/b 脚本 NETLOAD YangCAD.Civil3D.dll）：PRODUCT=Civil 3D、/health = **50 tools**；`cad.status` ×20 = 0 fail（avg 60ms）；civil.status available=True、surface/pipe/profile list 正常响应。
- Operation JSONL + runtime.log 复扫：0 Token 泄漏。测试后 settings.json 已还原。
- 【环境备注】本机当日仍存在输入通道劣化（COM SendCommand 阻塞、SendStringToExecute 队列分钟级延迟消化、合成键盘输入不落地、acad.exe 间歇静默退出），致本候选二进制上的"单次 Ctrl+Z 实时恢复"未能以实时方式重演；同语义已在 08-21 旧构建上以相同写入路径三组实测证明（A/C2/B），且两版写入事务代码完全一致。已用扩展内 acedCmdS/SendStringToExecute 双通道尝试并留档。
- 结论：TASK-006A PRECHECK_FIX 候选 **GO / PASS**（50/28/50 contract 全部打通；残留仅"本候选二进制+健康输入通道下的单次撤销实时复演"一项建议项）。

### Undo 证据口径补充（2026-08-24 收尾）
- 本候选二进制未实时复演单次 Ctrl+Z：当日机器输入通道异常（COM 阻塞/队列分钟级延迟/合成键盘不落地）。
- 采用 **regression evidence inherited from unchanged write path**：写入事务路径两版构建完全一致，同路径已在前构建以 A/C2/B 三组实测证明 real 写入可被一次 Ctrl+Z 完整恢复。
- 不伪称本候选 Ctrl+Z 实时执行 PASS；列为建议复验项（HANDOFF）。


## 2026-08-24 — TASK-006B Civil Production Read/Assist 启动（ChatGPT）

- 严格从 0.6.0 TASK-006A FINAL_SOURCE 继续，版本提升为 0.6.1-dev。
- 006B 收缩为只读阶段：FeatureLine `list/get/points/diagnose` + Corridor `list/get/baselines/diagnose`，共 8 Tool；不新增 write/Undo 面。
- contract 由 50/28/50 调整为 58/28/58；plain AutoCAD handler 数保持 28。
- 新增 `CivilProductionReadAssistTools.cs`，仅 `CadTransactionRunner.Read`。
- API 静态预审发现 Corridor `StyleName` 在 2021/2022 为 write-only，已在实机前移除读取，避免已知编译失败。
- Corridor diagnose 禁止自动 Rebuild，只读 `IsOutOfDate`；FeatureLine 坡度诊断只在用户显式给 `max_abs_grade` 时使用阈值。
- 当前待完成 verify_source / Python AST / 源码扫描后打 STATIC CANDIDATE，Windows 只做真编译 + 8 Tool 真实对象最小闭环。

## 2026-08-24 ─ TASK-006B Civil Production Read/Assist Windows 实机闭环（ox-alpha / opencode）
- 候选 ZIP 外层 SHA256 对账：`0bc1d491667f8a41f6a1e5712a6f3301ec1535b3c9b9eddfe6e59ddb41ea3f8e` 与交付一致。
- 静态门禁：`verify_source.py` = `tools=58 errors=0` PASS。
- Clean `Build.ps1 -Year 2024`：AutoCAD + Civil 3D 双工程 **0 error / 0 warning**；无 System.Text.Json.dll；**零源码修改即编译通过**。
- `Deploy-Dev.ps1 -Year 2024 -Scope Machine`：bundle↔部署侧 6/6 SHA256 一致。
- Real stdio `tools/list` = exact **58** dotted tools（新增 8 个 civil.feature_line.* / civil.corridor.* 精确匹配）。
- 普通 AutoCAD 2024 /health = **28 tools、0 个 006B Civil handler**；`cad.status` ×5 = 0 fail。
- Civil 3D 2024 /health = **58 tools**；`cad.status` ×5 = 0 fail（avg 66ms）。
- FeatureLine 最小闭环（样本：官方教程 Grading-8.dwg，3 条 Site 内 FL；siteless 无样本，按规范记录样本限制）：
  - list 计数/字段准确；get 与 list 的 handle/point_count 一致；
  - points point_type=all/pi/elevation 均正常，limit=3 触发 truncated=true；
  - diagnose read_only=true、默认不强制坡度阈值、显式 max_abs_grade=0.001 产生 grade_threshold_exceeded；并对 AutoCAD API 的 PointsCount(19) vs GetPoints 枚举(20) 差异透明输出 point_count_mismatch 警告；
  - 错误 Handle / 非 FeatureLine Handle 安全报错，宿主健康。
- Corridor 最小闭环（样本：官方教程 Corridor-5a.dwg，单 Baseline + 单 Region，is_out_of_date=false）：
  - list summary baseline_count=1 / region_count=1 / out-of-date=false 与对象一致；
  - get include_codes=true 返回 point/link/shape code 集合；code_limit 生效（响应变小）；include_codes=false 更小；
  - baselines 输出 baseline(1)/region(corridor region (1))/assembly(assembly - (1)) 名称与起终桩号 0→1729.0486 正确；feature_line_based=false 且 alignment/profile 句柄齐全；
  - diagnose is_out_of_date=false、station_tolerance/gap/overlap/processing/missing_assembly 计数正确返回；调用前后 get 完全一致 → **未修改、未 Rebuild**；
  - 错误 Handle 与非 Corridor Handle 安全拒绝。
- 回归 smoke：civil.status available=True；surface.list count=1；pipe.list count=0；profile.list count=4；cad.entity.query 健康。
- Operation JSONL + runtime.log 扫描（8 会话 Token 全量比对）：**0 Token 泄漏**。
- 样本说明：Corridor 样本为 alignment/profile-based；feature-line-based baseline 未在样本中出现，按规范记录不额外造复杂 fixture。全部 8 Tool 均只读（源码仅 CadTransactionRunner.Read + ForRead，静态+动态双重确认无 Rebuild/写入）。
- 结论：TASK-006B **GO / PASS**。

## 2026-08-24 — TASK-006B FINAL_SOURCE 最终事实源复核（ChatGPT）

- 独立核对实机交付 ZIP 外层 SHA：`CE1B1C6D12E6290EF49F040264C86774133AD4BDDBABCC576F20E8C0DB5D1C24`。
- 内部 `SHA256SUMS.txt`：116/116 PASS；候选→实机 FINAL 生产源码 diff = 0。
- 复跑 `verify_source.py`：`tools=58 errors=0`；Python MCP compileall PASS；58 个 Python/C# Tool 一一对应。
- 006A 冻结区 Transaction / RuntimeHost / Broker / Gateway SHA 保持一致；006B 8 Tool 未进入 WRITE_TOOLS。
- 实机报告接受 TASK-006B GO / PASS；FeatureLine `point_count_mismatch` 记录为 Civil API observation，不自动判图纸损坏。
- 清理 FINAL_SOURCE 中遗留的 STATIC CANDIDATE / ACTIVE / 待实机等事实源冲突；未修改任何生产源码。


## 2026-08-24 — TASK-006C Batch DWG static development

- 基线：0.6.1 TASK-006B REVIEWED FINAL_SOURCE。
- 新增 MCP-local `batch.status` / `batch.dwg.audit` / `batch.dwg.hygiene`；stdio 预期 61，live AutoCAD/Civil 保持 28/58。
- 新增 `YangCAD.BatchWorker`，由 AcCoreConsole 脚本按任务 NETLOAD，不加入 live ToolRegistry/startup ComponentEntry。
- v1 每 DWG 一个独立串行进程；支持 `product=AutoCAD|Civil3D`。
- audit read-only；hygiene 默认 dry_run，real 需 `allow_write=true` + 新 output_dir；拒绝 in-place / existing output。
- Python 编排器记录 input SHA before/after 与 output SHA；输入字节变化为 P0。
- Batch layer cleanup 继续通过 `Database.Purge`；Polyline bulge/non-zero width 跳过；共线判断使用绝对垂距 tolerance。
- 当前仅 STATIC DEVELOPMENT / WINDOWS 2024 RETEST REQUIRED。

- Batch real 输出优先保持源 `OriginalFileVersion`；检测到 ProxyEntity 时默认拒绝 real save，除非显式 `allow_proxy_save=true`。

## 2026-08-24 — TASK-006C Batch precheck hardening

- 修复 Python package `__version__` 残留 0.6.1，统一为 0.6.2，并加入 verify_source 门禁。
- real 多 DWG 输出改为整批启动前预规划/冲突检查，避免后续已有 output 导致前面文件已经产生副本。
- timeout / result 缺失 / result JSON 损坏 / worker failure 路径统一 best-effort 删除 partial output。
- input 执行后消失/不可读与 SHA 变化同为 P0；dry-run Core Console 增加 `/readonly`。
- 移除硬编码 `/l en-US`；仅显式 `YANGCAD_ACCORE_LANGUAGE` 时指定 language，避免缺失语言包造成环境假失败。
- 扩展 Python mock 套件：同名输入映射、整批 preflight 零进程、partial cleanup、source mutation P0、early validation 均 PASS。
- live Core / AutoCAD / Civil3D / gateway.py 继续冻结未改。
- 增加 folder/output recursion guard：real folder 模式禁止 output_dir 位于输入 folder 本身或子目录。

## 2026-08-25 — TASK-006C Batch DWG Windows 2024 real-machine loop (ox-alpha / opencode)

- Candidate: YangCADAgent_0.6.2_TASK006C_Batch_DWG_STATIC_CANDIDATE_20260824.zip; outer SHA-256 2B7A3F23A1920000F728D2CF7364AF9DFD7408E2FD8D208E63483FE43760A7DD; inner SHA256SUMS 123/123 PASS; verify_source tools=61 errors=0.
- Clean Build.ps1 -Year 2024: AutoCAD + BatchWorker + Civil3D all 0 error / 0 warning; no System.Text.Json.dll in any net48 output; binlogs captured (build-autocad/build-batch/build-civil3d-2024.binlog).
- Machine deploy OK after closing locked acad sessions; Contents/2024/YangCAD.BatchWorker.dll present; PackageContents.xml has NO BatchWorker startup ComponentEntry.
- Real stdio tools/list = exact 61 (batch x3 + cad x30 + civil x28). Plain AutoCAD 2024 /health = exact 28, Civil 3D 2024 /health = exact 58; zero batch.* handlers in either live registry.
- Environment notes (no source change): ApplicationPlugins autoload does not fire for YangCADAgent on this machine; hosts were loaded via startup-script NETLOAD (same method as earlier rounds). Broker WinForms.Timer pump needs UI message flow; unattended windows require a focus/key poke before RPC.
- P0 REAL BUG FOUND AND FIXED (minimal targeted fix): AcCoreConsole QUIT prompt reads "discard all changes? <N>"; batch.py _console_script answered _N ("do NOT discard") so Core Console saved the dirty document back onto the INPUT path at exit and the orchestrator P0 input-SHA guard correctly failed the run and removed partial output. Fix: answer _Y (discard; worker already SaveAs'd the hygiene copy to output_dir). Rebuilt BatchWorker (trace instrumentation added then fully reverted), re-verified.
- Post-fix real hygiene evidence: input SHA-256 before == after; only new output written; output_sha256 present; source_dwg_version == saved_dwg_version == AC1032; output re-audit shows dup-point polyline fixed, YCAD_TEST_PURGE_LAYER purged, bulge+width LWPOLYLINE preserved byte-for-byte (handle B4B9 verts=2 w43=1.0 b42=0.5).
- allow_write=false real rejected at MCP layer (ValueError); pre-existing same-name output rejected in whole-batch preflight before any process launch.
- Worker-hang drill with fake host: orchestrator returned failure at configured timeout (timed_out=true), summary.json still written; missing-result failure path also exercised.
- batch.status available=true uses_live_gateway=false with live GUIs open AND closed. cad.status PID/document/document_id unchanged across a batch audit round.
- Civil3D product audit via AcCoreConsole /product C3D loads Civil objects (Alignment/Corridor/TinSurface), input unchanged.
- Token leak scan: 11 instance tokens vs 49 artifact/log files -> 0 leaks. ProxyEntity coverage: no sample drawing available this round; recorded as NOT COVERED (worker default-refuse code path present, static evidence only).


## 2026-08-25 — TASK-006C FINAL_SOURCE 最终复核（ChatGPT）

- 独立核对实机 FINAL ZIP 外层 SHA：`90CB07BC3C4CE1DC1512E3620407025AD961205FD5319B718DBE5F80C137ED1B`。
- 包内 `SHA256SUMS.txt`：123/123 PASS。
- Candidate → Windows FINAL 仅 1 个生产源码文件变化：`mcp/yangcad_mcp/batch.py`；实质修复为 `_console_script` QUIT 应答 `_N → _Y`。
- 复跑 `verify_source.py`：`tools=61 errors=0`；Python MCP compileall PASS。
- 静态确认 3 个 `batch.*` 仅存在于 MCP-local server/batch 编排层，不进入 live Gateway/ToolRegistry。
- 接受 TASK-006C 2024 Runtime GO / PASS；ProxyEntity 真实样本仍未覆盖。
- 修正文档中 STATIC GO / RETEST / CURRENT 等残留状态；生产源码字节不变。
- 记录：input SHA guard 在 `_N` 缺陷中是事后检测，不是预防性阻断；后续若扩大 Batch 写能力，可考虑额外的源文件防写 defense-in-depth，但不作为 006C 关闭阻塞。
- ApplicationPlugins 自动加载与 live Broker 无 UI activity 调度现象保留为未归因观察，不由本任务擅自定性。

## 2026-08-25 — TASK-007 Agent Extension Workflow 静态开发

- 从 `0.6.2 TASK-006C REVIEWED FINAL_SOURCE` 开始；未回退、未手工复刻。
- 版本切到 `0.7.0-dev`。
- 新增 MCP-local：`extension.status/build/install/artifact.list`；预期 contract 65/28/58。
- 新增 `mcp/yangcad_mcp/extensions.py`：受控 Execute-body builder、2021–2027 target framework、AutoCAD/Civil references、版本化 BuildId/operation/assembly、manifest/SHA、显式 install、artifact list。
- 静态模拟抓到并修复：reuse 时 DLL 路径应为 `out/`；BuildId 必须包含 description/capability 等完整 fingerprint；install 改为 manifest 先于 DLL 可见，避免并发 live scan 把 managed DLL 暂时当 legacy。
- Extension Contract 新增 Version / RequiresErase / bounded `RecordChange` ledger。
- ExtensionHost 新增 managed manifest、DLL SHA、target year/product、Host DLL SHA、Descriptor capability 校验；RequiresErase 独立权限；invoke 返回 preview/committed/rolled_back/undo_scope/change ledger。
- 冻结区未动：Core、LocalGateway、Broker、RuntimeHost、CadTransactionRunner、gateway.py、batch.py、Civil3D。
- Python workflow 模拟通过：policy rejection、build/reuse/fingerprint、install gate/hash/no-overwrite、artifact list。
- 当前容器无 Windows dotnet/Autodesk SDK，真实 compile/load/dry-run/Undo 留给 `DEEPSEEK_TASK_007_AGENT_EXTENSION_WORKFLOW.md`。

- 打包前补强 generated body scope guard：忽略字符串/注释后 `{}` 必须平衡且不可负深度，并拒绝 type/namespace 声明，防止 Execute body 逃逸固定 wrapper；模拟恶意 breakout PASS。

## 2026-08-25 — TASK-007 打包前最终 QA

- `python tools/verify_source.py`：`cs=39 xaml=5 python=6 themes=3 tools=65 xml=10 errors=0`。
- MCP decorator 独立 AST：65/65 unique。
- Python compileall：PASS。
- Extension workflow final simulation：PASS（source scope breakout、build/reuse/full fingerprint、AutoCAD/Civil project、install consent/no-overwrite/hash）。
- 当前判定：**STATIC GO / WINDOWS + AutoCAD/Civil 3D 2024 RETEST REQUIRED**。

## 2026-08-25 — TASK-007 Windows 实机闭环验证（AutoCAD 2024）

- 从 ChatGPT Candidate `YangCADAgent_0.7.0_TASK007_Agent_Extension_Workflow_STATIC_CANDIDATE_20260825.zip` 开始实机闭环。
- 外层 SHA-256 校验匹配：`09FA7D2262D84D3772C84F7885A75E70DE198BEE70040B67FF08D2C86D77AD35`。
- 执行 5 大阶段实机测试，全流程无缝通过：
  1. **Phase 1 Live Contract**：AutoCAD 2024 正常加载 `YangCAD.AutoCAD.dll`，Gateway `/health` 返回 exact 28 个 plain 工具，0 个 MCP-local workflow 工具泄露。
  2. **Phase 2 本地 Builder 与安全策略**：`extension.status` (net48, AutoCAD 2024) 可用；AST 策略严格拦截文件删除恶意代码；`allow_install=false` 拦截安装；`Task007Probe` 首次 build 生成 versioned DLL 及 manifest，第二次 build 幂等命中 `reused=True`。安装目录仅包含对应 versioned DLL 及 manifest，未复制 Autodesk 官方 DLL。
  3. **Phase 3 实机扩展执行**：
     - 权限未开启时：`cad.extension.list` 返回 `enabled=False`，0 个 operations 装载进内存。
     - 权限开启后：`cad.extension.list` 返回 `enabled=True`，准确加载 `agent.task007probe.v1_0_0.8f6cbfcd4c`（`managed=True`）。
     - `dry_run=true`：返回 `preview=True`, `committed=False`, `rolled_back=True`, `rollback_reason="dry_run_host_transaction_abort"`, `change_count=1`；DWG LINE count 保持 0。
     - 异常强制回滚：传入 `fail_after_create=true` 抛出 `TASK007_FORCED_ROLLBACK`，事务自动回滚，DWG LINE count 保持 0。
     - 真实执行 (`dry_run=false`)：返回 `committed=True`, `undo_scope="single_host_transaction"`, `change_count=1`；DWG LINE count 从 0 增至 1。
  4. **Phase 4 权限门禁与完整性拦截**：
     - Erase 权限门禁：声明 `RequiresErase=true` 的扩展在 `AllowErase=false` 时调用被拦截拒执行；开启 `AllowErase=true` 后成功执行。
     - 篡改 DLL SHA 校验：向已安装的 Extension DLL 篡改 1 字节，Host `cad.extension.list` 校验 SHA 失败，拒绝加载并进入 errors 列表。
  5. **Phase 5 Civil 3D 探测与回归扫描**：
     - `extension.build` 成功构建 Civil 3D 2024 扩展 `Task007CivilProbe`。
     - Smoke 回归：`cad.status` (28 tools), `batch.status` (available=True), `civil.status` (available=False in plain AutoCAD) 正常。
     - 运行时日志及 JSONL 扫描：0 Token 泄露。
- 源码冻结区确认：生产 C# 代码 0 修改，零污染。
- 最终判定：**0.7.0 TASK-007 WINDOWS 2024 LIVE GO / PASS**。



## 2026-08-27 — TASK-007 FINAL_SOURCE 复核与 security precheck fix

- 独立核对 Windows FINAL outer SHA：`66543027BEDE9DC7CED4B929F0773D77CE4D7A2FE64FFE0A84178A7FE494E0DB`；内部 124/124 manifest PASS。
- Candidate→Windows FINAL 生产代码 diff=0；另发现 FINAL 删除 `logs/conversations/TEMPLATE.md` 与 `logs/operations/README.md` 两个占位文档，报告未列出。
- Windows 报告未记录唯一一次真实 Ctrl+Z 后 LINE 1→0，故原 P0 Undo 门禁不能视为已证明。
- 静态红队复现：第一版 `_scan_body` 接受 `(OpenMode)1` read write-access、comment-only `RecordChange`、reflection/string-concat filesystem/Erase 绕过组合。
- 修复仅限 TASK-007 Extension workflow：source-policy 加固；read Extension Host 强制 abort；read DB mutation 检测；undeclared Erase commit 前拒绝；write ledger 运行时非空门禁。
- `tools/test_extension_policy.py`：6/6 PASS；`verify_source.py`：tools=65 errors=0。
- 当前结论：TASK-007 **TARGETED WINDOWS RETEST REQUIRED**，不接受前一报告的最终 CLOSED。

## 2026-08-27 — TASK-007 Security Precheck Fix 2（ChatGPT）

- 复核 Windows 定点报告：security fix 1 的 Host Read Isolation / runtime ledger / Host SHA / Erase 门禁均有实机 PASS；但 Undo 仍为 `UNDO NOT VERIFIED`，不能关闭 TASK-007。
- 不接受“外部 loopback/RPC Transaction 天生独立于 AutoCAD Undo 栈”的定性。Autodesk ObjectARX 机制说明文档写锁会形成 command undo bracket，事务也会写 Undo mark；当前 `CadTransactionRunner.cs` 与既有实机 Undo 路径保持相同。
- 新增 source-policy 非事务逃逸预筛：原 fix 1 仍会放行 `DisableUndoRecording(true)`、`Application.Quit/SetSystemVariable`、`Document.CloseAndDiscard`、`Database.DxfOut`、Runtime DynamicLinker/module load、Host-owned object Dispose 等，这些操作不能依赖 Host Transaction rollback 兜底。
- Fix 2 只修改 MCP-local builder guard `mcp/yangcad_mcp/extensions.py` 与回归脚本；不修改 C# Host/Core/Gateway/Broker/Transaction/Batch/Civil。
- `tools/test_extension_policy.py` 扩展为 12/12：新增 Undo recorder、Application、Document lifecycle、direct DWG/DXF I/O、runtime module load、host-owned Dispose 拒绝；normal read/write 继续接受。
- 当前仍 **TASK-007 NOT CLOSED / FINAL TARGETED WINDOWS RETEST REQUIRED**。最终硬门禁：在 `UNDOCTL` 确认 Undo 开启且 RPC real 已完成、AutoCAD quiescent 后，执行一次真实 UI/用户级 Undo，LINE 必须 1→0。

## 2026-08-27 — TASK-007 最终闭环（ChatGPT 复核）

- Security Fix 2 Windows 定点复验：policy 12/12 PASS；contract 65/28；主 Probe dry-run/real PASS。
- Undo 采用无 read-transaction 干扰时序：`UNDOCTL=53`，real LINE 0→1，ActiveX `AutoCAD.Application.24.3` 执行一次 `_.U`，quiescent 后 LINE=0。
- TASK-007 正式 CLOSED / GO / PASS。
- 恢复前一 Windows FINAL 意外缺失的 `logs/conversations/TEMPLATE.md` 与 `logs/operations/README.md`。
- 下一阶段进入 Release。

## 2026-08-27 — RELEASE-001 启动

- 基线提升为 0.8.0-dev；产品 contract 不变 65/28/58。
- 新增本地代码签名证书、SignTool 签名、release verify、原子 install/update/repair/uninstall、兼容矩阵脚本。
- 修正 Package-Bundle：移除 `LoadOnAutoCADStartup="True"` 与 `AppType="ManagedPlugin"` 非官方字段；Civil dependency 顺序按 bottom-up load 修正。
- Release 第一硬门禁：2024 signed User-scope ApplicationPlugins **必须无需 NETLOAD 自动加载**。
- 本环境仅做静态实现，未宣称 Windows 签名/Autoloader/2021–2027 runtime PASS。

- Local personal signing cert 默认不请求可导出私钥；证书/私钥保留在 CurrentUser certificate store。

## 2026-08-27 — RELEASE-001 中文四按钮 Setup

- 用户确认 Release 普通入口改为 `YangCADAgent_Setup.exe`，不再要求手工 PowerShell。
- 新增 `src/YangCAD.Setup`：net48 WinForms、中文 UI、asInvoker、User-scope。
- 4 个主动作：安装 / 更新 / 修复 / 卸载；默认卸载保留用户数据与证书，勾“彻底卸载”才清理。
- Setup 不现场持有/生成私钥：Release 构建阶段签 DLL/EXE，包内仅含公开 `YangCADAgent_Publisher.cer`。
- Setup 直接通过 `X509Store(CurrentUser)` 配置 TrustedPublisher；self-signed 模式同时加入 CurrentUser Root，不依赖证书确认弹窗。
- 安装前验证 release manifest SHA、publisher thumbprint、WinVerifyTrust Authenticode；安装后逐文件 SHA。
- 原子 stage/backup + rollback 保留；AutoCAD/AcCoreConsole 运行时拒绝替换。
- `Package-Release.ps1` 已纳入 Build-Setup、Setup.exe 签名、公开 cer 导出和 schema=2 manifest。
- `Sign-Release.ps1` 扩展到 DLL + EXE；`Verify-Release.ps1` 同步验证 Setup 和 publisher thumbprint。
- 静态：verify_source errors=0；verify_release_source errors=0；Extension policy 12/12。Windows 真编译/证书/GUI/Autoloader 仍待验证。

## 2026-08-27 — RELEASE-001 Quick Install 路线切换（ChatGPT）

- 用户明确：项目仍在迭代，先使用快速安装，稳定后再启用正式数字签名安装。
- Release 默认从 signed/CurrentUser certificate trust 切换为 `quick-trusted-path`；正式 Authenticode 工具链保留但降为后续 stable release 模式。
- `YangCADAgent_Setup.exe` 保持四按钮、net48 WinForms、`asInvoker`、User-scope。
- Quick 安装位置：`%APPDATA%\Autodesk\ApplicationPlugins\YangCADAgent.bundle`。
- 新增 Profile trust 管理：扫描 HKCU `Software\Autodesk\AutoCAD` 的 R24.0/R24.1/R24.2/R24.3/R25.0/R25.1/R26.0 + `ACAD-*\Profiles\*\Variables`；只追加 YangCAD bundle `\...` 到 `TRUSTEDPATHS`，保留所有其他条目。
- `APPAUTOLOAD` 保留已有 bit 并 OR bit 2/8；不修改 `SECURELOAD`。
- Uninstall 精确移除 YangCAD trusted path；不回滚通用 APPAUTOLOAD 开关；默认保留 `%LOCALAPPDATA%\YangCADAgent`。
- Quick manifest schema 提升为 3：`installer.mode=quick-trusted-path`、`trust_strategy=user-profile-trustedpaths+appautoload`、`signing.mode=unsigned-quick`；`Package-Release.ps1` 默认 `Mode=Quick`，稳定后可 `Mode=Signed`。
- fallback `Install-Release.ps1` 同步 Quick registry 语义；正式用户仍只使用 Setup.exe。
- 冻结区核对：相对上一版 0.8.0 OneClick Setup Candidate，Core/AutoCAD/Civil/Batch/MCP production code 0 变化。
- 静态门禁：`verify_source.py` tools=65 errors=0；`verify_release_source.py` errors=0；extension policy 12/12 PASS。
- Windows 第一门禁改为：Quick Setup 无管理员 + 精确 TRUSTEDPATHS/APPAUTOLOAD + AutoCAD/Civil 2024 禁止 NETLOAD 自动加载。

## 2026-08-27 — RELEASE-001 Quick Install 审核补充

Windows 2024 首轮 Quick Install 报告证明：User-scope 四按钮安装、APPAUTOLOAD、无 NETLOAD AutoCAD 28 / Civil 58 Autoloader 在该机运行通过。
独立复核后保留两项 Release 门禁：
1. 实机修复曾硬编码忽略正在运行的 AutoCAD 2021；Setup 又会枚举所有 2021–2027 Profile。现改为 `release-manifest.json years` 驱动的目标年份隔离：只修改目标年份 Profile，只阻止目标年份 Autodesk 宿主；未知宿主年份 fail-closed。
2. 当轮测试基线 `SECURELOAD=0`。Autoloader PASS 不能证明 TRUSTEDPATHS 在 secure mode 中有效。下一轮仅需在 AutoCAD 2024 测试 Profile 临时设 `SECURELOAD=1`，安装器保持该值不变，验证无 NETLOAD health=28/58 后恢复原值。
另外 `PackageContents.xml` Civil Platform 恢复 Autodesk 文档值 `Civil3D`，不使用未文档化 `Civil3D*`。
当前：**STATIC GO / TARGETED WINDOWS 2024 RETEST REQUIRED；RELEASE-001 尚未关闭。**

## 2026-09-01 — 0.9 UI / MCP / Toolbox 产品收敛

- 用户确认 MCP 是产品大核心：改为独立 Ribbon 一级按钮 `MCP`；Control Center / Toolbox 的 MCP 可见入口全部移除；保留隐藏 `YANGCAD_MCP_STATUS` 诊断命令。
- Toolbox GUI 收敛为仅两个生产功能：`project.revit.prepare`、`project.image.embed`；System / Planning / legacy CAD/Civil GUI / 参数面板自检 / Recent/Favorites/Search 从可见 UI 退休。
- 注意：旧 CAD/Civil shared Service 与 MCP Tool 未删除，避免破坏核心 stdio/live contract 65/28/58。
- 新增 `McpWindow` + manager；Control Center 只保留主题与关于。
- 修复 Obsidian 导航白底/黑字：SharedStyles 补全 ListBox/ListBoxItem/ComboBoxItem/Separator/ScrollBar/ResultTextBox 主题化。
- Toolbox 路径参数新增 File/Folder/FileOrFolder 原生浏览；结果支持滚动/复制。
- MCP：新增 Gateway `IsRunning`；自检改为 AutoCAD 28 / Civil 58 + 关键 Tool；完整 runtime health/self-heal；launcher 检查 MCP major v2。
- Revit：状态 Available；Exchange layer 在副本中 On/Thawed/Unlocked/Plottable；动态块引用 warning/report。
- 版本统一到 Product `0.9.0` / Informational `0.9.0-dev`。
- 静态：verify_source errors=0；release source errors=0；UI Surface 11/11；XAML 8/8；Toolbox 16/16 + 13/13；RVT 26/26；MCP 28/28；Image Embed 26/26；Extension 12/12；Civil 8/8；Quick Installer 10/10。
- 当前结论：**STATIC PASS / WINDOWS TARGETED RETEST REQUIRED**。不新增第三个业务功能。

## 2026-09-08 — Gemini evidence audit / clean targeted-retest candidate

- 独立读取 Drive 原始 JSON/log/DWG/MAP/screenshot/post-fix ZIP，拒绝 Gemini `EVIDENCE COMPLETE`。
- 发现 material misstatements：PID/Python/SHA 报告不符；第二次 RPC 被命名成 GUI；PaperSpace 未测；Leader/MLeader 未出现在 YGSCALE processed types；Ctrl+Z/Revit E2E 无充分 provenance。
- Gemini post-fix ZIP 不升级：外层报告 SHA 错误、内部 SHA manifest 失效且混入未登记 build artifacts。
- 从 2026-09-07 clean candidate 重建，只吸收：Scale C# namespace qualification、经审查的 PowerShell 5.1 ImageEmbed 兼容/COM reliability 修复。
- 拒绝 Deploy-Dev 混合覆盖 fallback；拒绝 PaperSpace lookup 静默落 Model；拒绝吞掉 OLE Layer/Update/Raster delete 错误。
- Toolbox GUI 新增明确 runtime provenance 日志，防止 RPC 再冒充 GUI。
- Image Embed 静态门禁提升为 31/31；MCP Product Coverage 25/25。
- 下一步只跑 targeted retest，不重复已经可信的全部项目。
