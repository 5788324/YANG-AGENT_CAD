# YangCAD 0.10.0 RC1 — AutoCAD 2024 Quick 验收清单（2026-11-05）

这是**当前范围验收**；下方原 2026-08 历史清单不代表本次 RC 中 Civil 3D 或其他年份已经通过。

- [x] GitHub 唯一受测源码 b6ff137 / 修复 d5200c5 可追溯，Release ZIP SHA 对账。
- [x] AutoCAD 2024 Windows 10 Build/Deploy/Loaded DLL SHA 880329CF...。
- [x] Ribbon、Palette、关于、PGP/REINIT、图层、大样、引线、比例、图片 OLE/Xref、MCP、CAD MAP 的**指定样例**运行验证。
- [x] Release Manifest 74/37/67，19/19 SHA，stdio 74 不重复；PS5.1 正向 ExitCode 0 与三组篡改 ExitCode 1。
- [x] G5 ChatGPT 独立审核 AutoCAD 2024 scope PASS（Issue #9）。
- [x] docs-only RC [Draft PR #11](https://github.com/5788324/YANG-AGENT_CAD/pull/11) → main：完整 diff / SHA manifest / GitHub CI PASS，且 Runtime 代码无变化。
- [ ] PR #11 合并审批及后续主分支核对：**未获批准，不擅自合并**。
- [ ] Civil 3D live 67：NOT_TESTED。
- [ ] Revit Native ElementId E2E：NOT_TESTED。
- [ ] 其他 CAD 年份兼容：NOT_TESTED_CURRENT_RC。
- [ ] 如需正式 Stable 或签名发布：单独申请、重新定义兼容矩阵并实测。

已知限制：R06 对象级 Undo 证据不代表整个 DWG 二进制一致；R07 文件夹 batch 为 2 PASS/1 SKIP/1 FAIL（PARTIAL）。RC1 只是管理标签，现有程序集仍为 0.10.0-dev，已验收 ZIP SHA 与 DLL SHA 见 `docs/RC_AUTOCAD2024_0.10.0.md`。

---

# Release / Development Checklist

更新时间：2026-08-28

## RELEASE-001 Quick Install 2024 — CLOSED / PASS

- [x] verify_source / verify_release_source
- [x] Setup 2024 build
- [x] user-scope / asInvoker
- [x] TRUSTEDPATHS / APPAUTOLOAD
- [x] SECURELOAD 保持
- [x] AutoCAD 2024 no-NETLOAD `/health=28`
- [x] Civil 3D 2024 no-NETLOAD `/health=58`
- [x] Repair / Uninstall / reinstall
- [x] SHA / token scan

正式 Authenticode 仍后置到稳定发布。

## RELEASE-002 Phase A Build Matrix — CLOSED / PASS

- [x] 2021 AutoCAD + Batch
- [x] 2022 AutoCAD + Batch + Civil
- [x] 2023 AutoCAD + Batch + Civil
- [x] 2024 AutoCAD + Batch + Civil
- [x] 2021–2024 no external System.Text.Json.dll
- [x] 2025–2027 NOT_INSTALLED 正确记录
- [x] schema-v2 granular matrix

## RELEASE-002 Phase B1 Civil Runtime — CLOSED / PASS

2022 / 2023 / 2024：

- [x] Civil `/health=58`
- [x] `civil.status available=true`
- [x] PVI 100-sample runtime PASS
- [x] 2022/2023 legacy Station fallback PASS
- [x] 2024 RawStation PASS
- [x] 3x cad.status regression
- [x] batch.status available
- [x] runtime exception = 0
- [x] token leak = 0

Limitations：
- FeatureLine = SAMPLE_MISSING
- Corridor = SAMPLE_MISSING

不伪造对象级 PASS，后续真实功能进入时补 smoke。

## TOOLBOX-001 Round 0 — STATIC GO / CLAUDE GO

- [x] verify_source
- [x] verify_release_source
- [x] extension policy 12/12
- [x] quick install policy 10/10
- [x] Civil compat 8/8
- [x] Toolbox foundation 10/10
- [x] Claude static review = GO TO WINDOWS ROUND 1
- [x] Autodesk official docs confirm `PaletteSet.AddVisual(string, Visual, bool)`

## TOOLBOX-001 Round 1 — CLOSED / GO / PASS

- [x] AutoCAD 2021–2024 clean build
- [x] Civil 2022–2024 clean build
- [x] 2021–2024 external System.Text.Json.dll = 0
- [x] 2024 Toolbox lifecycle / content / dock / float
- [x] 2024 三主题
- [x] Control Center MCP / 系统设置 / 关于
- [x] 2021 read + one write/native Undo + Batch
- [x] 2021 Toolbox minimum content
- [x] 2022 Toolbox minimum content
- [x] 2023 Toolbox minimum content
- [x] 65 / 28 / 58 不回归
- [x] token leak = 0
- [x] unhandled exception = 0

唯一 source fix：
`ToolboxRegistry.cs` Autodesk Application fully-qualified，修复 CS0104。

## Current — TOOLBOX-001 Round 2

- [x] ParameterPanel static implementation
- [x] 8 ParameterShape render/validation
- [x] Favorites implementation
- [x] Recent implementation
- [x] persistence implementation
- [x] IsAvailable implementation
- [x] execution result/error implementation
- [x] search interaction implementation
- [x] Round2 interaction policy 16/16
- [ ] Windows full interaction smoke
- [ ] real-process persistence smoke
- [ ] 2021–2023 minimum UI smoke

## 2025–2027

本机未安装：
- 保持 NOT_INSTALLED
- 不凭 source target/framework table 标 runtime PASS


## TOOLBOX-001 Round 2 — CLOSED / GO / PASS

- [x] ParameterPanel 8 types
- [x] required/range validation
- [x] Favorites
- [x] Recent
- [x] real process restart persistence
- [x] runtime unavailable
- [x] SearchKeywords
- [x] result/error
- [x] 2021/2022/2023 minimum runtime
- [x] 2024 full runtime
- [x] settings isolation
- [x] 65/28/58
- [x] 0 exception / 0 token leak

## Current — Round 3A

- [ ] shared Service extraction
- [ ] GUI drawing audit
- [ ] GUI layer audit
- [ ] GUI polyline inspect
- [ ] MCP output semantic regression
- [ ] GUI/MCP comparison on same DWG
- [ ] 2021–2024 clean build
- [ ] 65/28/58 unchanged


## TOOLBOX-001 Round 3A — CLOSED / GO / PASS

- [x] shared read Services
- [x] GUI/MCP shared Service
- [x] no GUI HTTP/Gateway loopback
- [x] 2021–2024 clean build
- [x] 2024 GUI/MCP semantic parity
- [x] 2021–2023 minimum runtime
- [x] 65/28/58
- [x] 0 exception
- [x] 0 token leak

Evidence SHA:
`77B113A541C3BA008C80460460D6C7EBDAE4F9C69501195EE566D4DDC6F9B704`

## Current — Round 3B

- [ ] polyline.clean shared write Service
- [ ] layer.cleanup shared write Service
- [ ] Preview/dry_run
- [ ] Real
- [ ] Native Undo
- [ ] GUI/MCP parity

## TOOLBOX-001 Round 3B — CLOSED / GO / PASS

- [x] PolylineCleanupService shared by GUI/MCP
- [x] LayerCleanupService shared by GUI/MCP
- [x] Preview / dry_run
- [x] Real
- [x] Native Undo
- [x] GUI/MCP write semantics
- [x] 2021–2024 build
- [x] 2024 full P0
- [x] 2021 write/Undo
- [x] 2022/2023 preview smoke
- [x] 65/28/58
- [x] 0 exception
- [x] 0 token leak

Evidence SHA:
`E7EA9058340DB56F6D3A67BF0EACF9FBFE81092A73E57ADF8AA3090A58329359`

## Current mode

No fixed release feature round.

Future tools are on-demand, normally 1–3 per task.

## FEATURE-INFRA-MCP-CONFIG-001 — STATIC IMPLEMENTED / WINDOWS PENDING

- [x] Copy MCP Config button
- [x] PID-bound config
- [x] token-free clipboard policy
- [x] Bundle Contents\Mcp packaging
- [x] user-local launcher
- [x] 14/14 MCP-copy policy
- [x] existing static/policy regression
- [ ] exact-year Windows build
- [ ] Bundle contains Contents\Mcp
- [ ] installed config path resolves
- [ ] local AI / MCP client starts server from copied config
- [ ] `cad.status` PASS
- [ ] CAD restart → old PID config fails safe / recopy new config PASS
- [ ] token leak scan

## FEATURE-CAD-REVIT-PREP-001 — CLOSED / GO / PASS

- [x] one GUI Tool: `project.revit.prepare`
- [x] source active DWG read-only
- [x] detached `Database.Wblock()` output clone
- [x] Geometry/Text/Leader/Dimension/Hatch
- [x] Revit-compatible geometry layer names (`#0.25 Continuous Red`)
- [x] stable `RVT-*` annotation/dimension/hatch layers
- [x] `_RVT.dwg + _RVT_MAP.json + _RVT_REPORT.json`
- [x] normal block definition diagnostic scan only; BlockReference unsupported / Xref+anonymous skip/report
- [x] no CAD-side explode for semantic objects
- [x] no new MCP Tool; 65/28/58 unchanged
- [x] `test_revit_exchange_v1_policy.py` static policy
- [x] AutoCAD 2021–2024 clean build
- [x] Civil 2022–2024 clean build
- [x] 2024 output-copy runtime P0
- [x] Revit 2024 Import/GraphicsStyle/controlled Full Explode proof
- [x] 2021–2023 minimum runtime
- [x] 0 unhandled exception / 0 token leak

Final review：`docs/RVT_EXCHANGE_V1_FINAL_REVIEW.md`


## MCP Copy Config Self-Heal — WINDOWS PENDING

- [x] Bundle packages Contents\Mcp
- [x] DLL embeds MCP runtime fallback
- [x] Setup / Verify-Release fail closed on missing MCP runtime
- [x] token-free PID-bound config retained
- [ ] installed bundle path runtime PASS
- [ ] deleted-Contents\Mcp self-heal PASS
- [ ] local AI cad.status PASS

## Batch Image Embed v1 — WINDOWS PENDING

- [x] GUI-only Tool; no MCP count change
- [x] independent COM worker
- [x] source output-copy / SHA guard
- [x] no overwrite
- [x] unsafe image properties fail closed
- [x] status.json incremental report
- [ ] AutoCAD 2024 real OLE embed
- [ ] reopen output after source image moved
- [ ] PaperSpace image
- [ ] multi-DWG continuation after one FAIL

## 0.10.0-dev 新增质量门禁

发布前核对 `docs/QUALITY_GATES.md` G0–G5、`docs/RUNTIME_TEST_MATRIX.md` R00–R10 实际状态、GitHub PR/Issue 的 SHA 及 Drive 原始证据。GitHub Actions 静态 PASS 不得替代 Windows Build/Loaded 或真 Undo。
