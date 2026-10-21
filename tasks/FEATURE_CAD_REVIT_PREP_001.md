# FEATURE-CAD-REVIT-PREP-001 — Revit 图纸预处理 / RVT Exchange v1

状态：**STATIC IMPLEMENTED / WINDOWS + REVIT PENDING**

## Tool

`project.revit.prepare`

GUI only in V1; no new MCP Tool.

## Scope

一个 Tool：

- Geometry
- Text
- Leader
- Dimension
- Hatch

Outputs:

- `_RVT.dwg`
- `_RVT_MAP.json`
- `_RVT_REPORT.json`

## Hard boundaries

- source active DWG unchanged
- `Database.Wblock()` clone
- no source SaveAs
- no Xref rewrite
- no anonymous/system block rewrite
- no Dimension/MLeader/Hatch explode
- no project-specific Revit mapping in CAD side
- no image embedding in this task
- no new MCP schema

## Static gates

Existing regressions + `test_revit_exchange_v1_policy.py`.

## Windows P0 (next)

2024 real DWG must contain at least:

- geometry with multiple lineweight/linetype/colors
- DBText + MText with formatting
- MLeader and/or Leader
- linear/aligned + at least one non-linear Dimension
- Solid + patterned Hatch
- normal block definition

Verify:

1. Preview changes nothing.
2. Execute creates three files and source DWG/current doc stays unchanged.
3. geometry layers include e.g. `#0.25 Continuous Red` style.
4. text/leader/dimension/hatch get stable `RVT-*` layers.
5. source semantic entities remain semantic entities in output DWG (no CAD-side explode).
6. MAP entity counts/layers match output DWG sampling.
7. REPORT warnings are honest for skipped/unresolved cases.
8. Import output into Revit and Full Explode on a controlled sample; confirm GraphicsStyle/layer names are visible and usable for replacement.
9. 0 unhandled / 0 token leak.

2021–2023: exact-year clean build first; minimum runtime only after 2024 P0.
