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
