# FEATURE-CAD-REVIT-PREP-001 — Windows / AutoCAD Independent Review

日期：2026-08-31

## Verdict

> **AUTOCAD SIDE = GO / PASS**
>
> **REVIT WORKFLOW GATE = NOT YET PROVEN**
>
> **FEATURE-CAD-REVIT-PREP-001 = TARGETED REVIT RETEST REQUIRED**

不能写 CLOSED，直到 Revit 2024 Import / GraphicsStyle / controlled Full Explode 有真实结果。

## Windows package reviewed

Input candidate:
`YangCAD_FULL_REPO_RVT_EXCHANGE_V1_STATIC_CANDIDATE_20260831.zip`

SHA:
`9652B05C31C39D8F8AC2D0E41857CD9191443E0313E73EECD35F8E41AC9C30AA`

Windows returned package:
`YangCAD_FULL_REPO_RVT_EXCHANGE_V1_GO_20260831.zip`

SHA:
`98633554EAC2B047C2D9B72F21E4246CB8F136CF56C077CD4E0B4E2790F875F6`

Independent checks:
- Outer SHA = MATCH
- returned package internal SHA = 163/163, 0 mismatch
- 11 YangCAD static/policy gates independently PASS

## Accepted AutoCAD evidence

- AutoCAD/Civil 2021–2024 build matrix reported PASS
- Preview keeps active DWG entity/layer counts unchanged
- Preview writes no output files
- Execute creates `_RVT.dwg + _RVT_MAP.json + _RVT_REPORT.json`
- non-overwrite suffix behavior PASS
- geometry layers such as `#0.25 Continuous Red` PASS
- Text / Dimension / Leader / Hatch remain semantic entities
- MAP schema `yangcad-rvt-exchange/1.0`
- report captures ByBlock / mixed MText / skipped anonymous-system blocks as warnings
- no fake PASS behavior observed in the reported sample

## Independent package findings

The Windows returned source was not a clean final source relative to the candidate:

1. `src/YangCAD.AutoCAD/Commands/ToolboxCommands.cs` gained three test-assist commands:
   - `YANGCAD_RVT_PREPARE_PREVIEW`
   - `YANGCAD_RVT_PREPARE_EXPORT`
   - `YANGCAD_RVT_CREATE_SAMPLE`

   These were not disclosed in the delivery report and are not required by the product feature.

2. `.editorconfig` and `.gitignore` were missing.

3. Package documents disagreed:
   - CURRENT_STATE / CURRENT_TASK said CLOSED
   - RELEASE_CHECKLIST / TEST_PLAN / README / PROJECT / STATIC_VALIDATION still said Revit proof pending

4. No Revit runtime evidence artifact was included.
   No actual result was recorded for:
   - Revit 2024 Import CAD
   - generated GraphicsStyle/Subcategory names
   - controlled Full Explode
   - post-explode usability of Geometry/Text/Leader/Dimension/Hatch classifications

## Corrected source

This retest baseline:
- restores the original candidate `ToolboxCommands.cs`
- restores `.editorconfig` and `.gitignore`
- keeps the tested `RevitExchangeService` unchanged
- keeps MCP contract 65 / 28 / 58 unchanged
- reconciles documentation to `AUTOCAD PASS / REVIT PENDING`

No business algorithm change was made.

## Closure rule

Only after the targeted Revit proof passes may the feature be marked:

> **FEATURE-CAD-REVIT-PREP-001 = CLOSED / GO / PASS**
