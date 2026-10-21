# YangCAD TOOLBOX-001 Round 2 — Static Implementation Review

日期：2026-08-28

## Verdict

> **STATIC IMPLEMENTED / WINDOWS PENDING**

Round 2 不能写 CLOSED / PASS，直到 Windows SOP 完成。

## Baseline

Input:
`YangCAD_FULL_REPO_ROUND2_START_20260828.zip`

Round 2 candidate:
`YangCAD_FULL_REPO_ROUND2_STATIC_CANDIDATE_20260828.zip`

Outer SHA-256:
`见包外 YangCAD_FULL_REPO_ROUND2_STATIC_CANDIDATE_20260828_SHA256.txt`

Internal SHA256SUMS:
`140/140 matched`

## Static Gates

- cs=51 xaml=7 python=12 themes=3 tools=65 xml=13 errors=0
- release source checks: errors=0
- extension policy regression: 12/12 PASS
- quick install policy regression: 10/10 PASS
- civil api compatibility regression: 8/8 PASS
- PASS: old transaction runner signatures preserved | PASS: explicit context runner names | PASS: context constructor internal | PASS: context stays four-field | PASS: descriptor adapter split | PASS: parameter shape limited | PASS: planned tools not fake executable | PASS: WPF palette uses AddVisual | PASS: ribbon exposes toolbox | PASS: control center three sections | toolbox foundation policy: 10/10 PASS
- PASS: parameter panel is separate WPF control | PASS: all eight parameter kinds handled | PASS: shape validation required and range | PASS: choice validates allowed options | PASS: layer uses runner-owned context | PASS: object handles and point parsing | PASS: file parameter uses browse dialog | PASS: favorites persist tool ids | PASS: recent only after successful execute | PASS: recent dedupe and bounded | PASS: runtime unavailable is visible and execute disabled | PASS: execution result and error stay in palette | PASS: search still uses SearchKeywords | PASS: round2 validation tools are non-destructive experimental | PASS: document activation refreshes availability | PASS: settings normalize ids without database | toolbox round2 interaction policy: 16/16 PASS

- XAML parse: PASS
- forbidden artifacts: 0
- MCP contract: 65 / 28 / 58 unchanged

## Production Delta

Added:
- src/YangCAD.AutoCAD/UI/ToolboxParameterPanel.xaml
- src/YangCAD.AutoCAD/UI/ToolboxParameterPanel.xaml.cs
- tools/test_toolbox_round2_interaction_policy.py

Changed:
- src/YangCAD.AutoCAD/ExtensionApplication.cs
- src/YangCAD.AutoCAD/Infrastructure/UserSettings.cs
- src/YangCAD.AutoCAD/Toolbox/ToolboxRegistry.cs
- src/YangCAD.AutoCAD/UI/Themes/SharedStyles.xaml
- src/YangCAD.AutoCAD/UI/ToolboxPaletteManager.cs
- src/YangCAD.AutoCAD/UI/ToolboxView.xaml
- src/YangCAD.AutoCAD/UI/ToolboxView.xaml.cs

Removed:
- none

## Implemented

- independent ToolboxParameterPanel
- 8 ParameterShape types
- validation
- Favorites
- Recent success-only / dedupe / max 10
- settings persistence
- runtime availability
- execution result/error
- SearchKeywords
- document activation refresh
- non-destructive experimental interaction tools

## Deliberate Boundaries

- ObjectSelection remains Handle-first in the generic panel.
- No modeless generic CAD picking engine.
- No DB for Favorites/Recent.
- No fuzzy search.
- No GUI/MCP metadata unification.
- No new MCP tools.
- Packaging/MCP version remains 0.8.0 while development track is 0.9.0-dev; version migration is intentionally separate.

## Next

Execute:
`tasks/TOOLBOX_001_ROUND_02_WINDOWS_SMOKE.md`
