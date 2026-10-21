# Test Plan

更新时间：2026-08-28

## 当前静态门禁

每次正式源码包至少跑：

- `verify_source.py`
- `verify_release_source.py`
- `test_extension_policy.py` = 12/12
- `test_quick_install_policy.py` = 10/10
- `test_civil_api_compat_policy.py` = 8/8
- `test_toolbox_foundation_policy.py` = 10/10
- stdio = 65
- plain live = 28
- Civil live = 58
- no binary/cache payload

## 已完成

TASK-001~007、RELEASE-001、RELEASE-002 Phase A、Phase B1 已关闭。

Phase B1 已实机证明 Civil 2022/2023/2024：
- health=58
- PVI runtime
- Station fallback/RawStation capability
- Batch
- 0 unhandled runtime exception

FeatureLine/Corridor = SAMPLE_MISSING limitation。

## 当前：TOOLBOX-001 Round 1

目标只验证新 UI/Host 骨架，不重复完整历史回归。

### Build
- AutoCAD 2021–2024
- Civil 2022–2024
- no external System.Text.Json.dll

### 2024 full UI smoke
- Start Page 不自动弹 Toolbox
- open / close / reopen
- dock left / right / float
- Document A → B → close A
- 七分类实际渲染
- Tool Card / mcp-status
- search
- Planned 不伪装 executable
- 三主题
- Control Center 三页
- About actions
- 0 unhandled exception / token leak

### 2021 compatibility baseline
- health=28
- read smoke
- one real write
- native Undo
- Batch audit + source SHA unchanged
- Toolbox 能打开且有内容

### 2022 / 2023 minimum UI smoke
- exact-year build
- Toolbox 能打开
- 有分类 + Tool Card
- 关闭后可再次打开
- 0 unhandled exception

不重复 Phase B1 PVI 深测。

## Round 2+

Round 2 只增加 Interaction/ParameterPanel/Favorites/Recent 相关验收。  
Round 3+ 每新增真实 Tool，只加与该 Tool 风险相匹配的最小 smoke；不默认重跑整个历史矩阵。

## 2025–2027

NOT_INSTALLED，不外推 runtime PASS。


## Round 1 Closure

TOOLBOX-001 Round 1 已实机 CLOSED/PASS：
- 2021–2024 exact-year build
- 2024 full UI P0
- 2021 runtime baseline
- 2022/2023 minimum UI
- 65/28/58
- 0 exception / 0 token leak

当前 Test Plan 切换到 Round 2。

## Round 2 Test Focus

只增加 Interaction 框架验收：
- parameter shape → visible controls
- required/range validation
- Favorites add/remove + persistence
- Recent after successful execute
- failed execute 不错误加入 Recent（除非设计明确）
- IsAvailable disabled state
- SearchKeywords filtering
- execution success/error visible result
- theme/control center 不回归
- contract 不回归

不重复完整 Phase B1 / Round 1 矩阵。


## Round 2 Static Implementation

已完成：
- separate ParameterPanel
- 8 kinds
- validation
- Favorites/Recent/persistence
- availability
- result/error
- search
- `test_toolbox_round2_interaction_policy.py` = 16/16 PASS

当前唯一 runtime task：
`tasks/TOOLBOX_001_ROUND_02_WINDOWS_SMOKE.md`

2024 full；2021–2023 minimum。


## Round 2 Closure

Round 2 Windows interaction is CLOSED/PASS.
Do not repeat it for Round 3A unless a regression is observed.

## Round 3A Focus

- shared service correctness
- GUI/MCP same semantic result on same DWG
- three fixed read-only CAD tools
- no write/destructive behavior
- Favorites/Recent/Search still work with real tools
- no document → unavailable
- 2021/2022/2023 minimum execute smoke
- 2024 main evidence
- 65/28/58 unchanged


## Round 3A Closure

PASS:
- drawing.audit GUI/MCP
- layer.audit GUI/MCP
- polyline.inspect GUI/MCP
- shared read Services
- 2024 full P0
- 2021–2023 minimum

## Round 3B Test Focus

- Preview state unchanged
- Real exact intended change
- Native Undo exact baseline
- GUI/MCP semantic parity
- layer cleanup protection rules
- polyline bulge/width/min-vertex protection
- real success only enters Recent
- 0 exception / 0 token leak

## On-Demand Feature Test Rule

Toolbox Core Round 0–3B 已关闭。

以后不默认重跑完整矩阵。

每个新增 Tool 只做与风险匹配的最小验证：

- read：service + output semantics + minimum runtime
- ordinary write：real + native Undo
- destructive write：Preview/dry_run + Real + Undo
- new year-sensitive API：exact-year build/runtime
- MCP exposure：GUI/MCP semantic parity
- no MCP exposure：不为凑一致性新增 schema

保持现有 static policy 回归，但新增测试只锁新边界。

## FEATURE-INFRA-MCP-CONFIG-001 Test Focus

Static：
- copy button wired
- standard mcpServers shape
- exact PID env
- no Token/Bearer in clipboard builder
- Bundle packages MCP bridge
- launcher uses user-local venv
- partial installed runtime (launcher exists but module missing/stale) triggers embedded self-heal
- AutoCAD/Civil self-check expects 28/58 live tools + critical tools
- existing 65/28/58 contract unchanged

Windows 2024 P0：
1. Quick/update install new Bundle.
2. Open DWG, Ribbon → MCP shows CAD Gateway ready.
3. Click “复制 MCP 配置”.
4. Clipboard contains command/args/env and current PID.
5. Clipboard contains no raw token/Bearer.
6. Give copied text to local AI/MCP client.
7. AI adds `yangcad` MCP Server.
8. Server launches using installed `Contents\Mcp\Start-YangCAD-Mcp.ps1`.
9. `cad.status` succeeds and matches current host/document.
10. Read tool succeeds.
11. Optional dry-run write confirms existing permissions/binding.
12. Restart CAD: old PID-bound config must not silently switch to another process; recopy config then reconnect PASS.

## FEATURE-CAD-REVIT-PREP-001 Test Focus

Static：
- one GUI-only Tool; no MCP schema growth
- source active DB never SaveAs / write transaction
- export uses detached `Database.Wblock()` clone
- output triplet never overwrites existing output
- partial-output cleanup only deletes files created/moved by the current run
- five semantic categories
- exact geometry layer naming contract
- text formatting flags
- leader geometry/text metadata
- dimension kind/style/definition points
- hatch pattern + boundary loops
- block/xref/ByBlock limitations reported honestly
- MAP streamed; schema fixed `yangcad-rvt-exchange/1.0`

Windows/Revit 2024 P0：
1. Preview writes no files and changes no current DWG state.
2. Real creates DWG/MAP/REPORT output copy and source/current document remains unchanged.
3. Output DWG retains semantic Dimension/MLeader/Hatch/Text entities; no CAD-side explode.
4. Geometry and `RVT-*` layer names match MAP sampling.
5. Import into Revit 2024; generated CAD subcategory/GraphicsStyle names are visible/usable.
6. Controlled Full Explode proves current layer-replacement workflow remains usable.
7. No Revit rebuild plugin is developed in this task.

2021–2023：clean build first, then risk-matched minimum runtime only.


## RVT Exchange v1 Final Revit Evidence

状态：**PASS**

Revit 2024 targeted proof：
- Import CAD → ImportInstance PASS
- Imported Objects / GraphicsStyle/Subcategory actual names PASS
- Geometry `#...` layer replacement workflow PASS
- Text → TextNote actual behavior recorded
- MLeader explode limitation recorded
- Dimension explode limitation recorded
- Hatch explode behavior recorded
- MAP remains semantic source for future native reconstruction

Compatibility wording:
- 本轮只声明 Revit 2024 workflow PASS
- 不外推 Revit 2021 runtime PASS


## MCP Copy Config Self-Heal

P0：完整 bundle 路径 + 删除 Contents\Mcp 后 embedded fallback 两条路径都必须让本地 AI `cad.status` 成功，且 clipboard 0 token。

## Batch Image Embed v1

P0：Preview no write → independent worker → output AcDbOle2Frame → move original image file away → reopen output image still visible → input SHA unchanged。

Fail-closed cases：missing source / clipped / visual adjustment / transparency / nested block raster。
