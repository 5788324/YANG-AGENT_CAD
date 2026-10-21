# Changed Files — Targeted Retest Candidate 2026-09-08

Base: `YangCAD_0.9_SCALE_CONVERTER_MCP_ALIGN_STATIC_20260907.zip`

## Production code / runtime

- `src/YangCAD.AutoCAD/Commands/ScaleConvertCommands.cs`
  - namespace qualification only (`System.Exception`).
- `src/YangCAD.AutoCAD/Services/Cad/ScaleConversionService.cs`
  - namespace qualification only (`System.Convert` / `System.Exception`).
- `src/YangCAD.AutoCAD/UI/ToolboxView.xaml.cs`
  - explicit GUI preview/execute runtime provenance logging.
- `workers/ImageEmbed/Start-YangCAD-ImageEmbed.ps1`
  - reviewed Windows PowerShell 5.1 / COM reliability fixes; strict fail-closed OLE normalization retained.

## Static gates

- `tools/test_image_embed_batch_policy.py`
  - adds PS5.1 serialization, HashSet return, fail-closed layout, strict OLE normalization checks.
- `tools/test_mcp_product_coverage_policy.py`
  - adds GUI runtime provenance gate.

## Documentation / evidence control

- `PROJECT.md`
- `README.md`
- `WORKLOG.md`
- `docs/CURRENT_STATE.md`
- `docs/HANDOFF.md`
- `docs/CHATGPT_GEMINI_EVIDENCE_AUDIT_20260908.md` (new)
- `docs/TARGETED_RUNTIME_FIX_REVIEW_20260908.md` (new)
- `docs/CHANGED_FILES_TARGETED_FIX_20260908.md` (new)
- `tasks/TARGETED_RETEST_AFTER_GEMINI_AUDIT_20260908.md` (new)
- `artifacts/STATIC_TESTS_TARGETED_RETEST_CANDIDATE_20260908.log` (new)

## Explicitly not carried from Gemini post-fix

- no `Deploy-Dev.ps1` mixed-copy fallback;
- no build DLL/PDB/binlog injected into source archive;
- no silent PaperSpace -> ModelSpace fallback;
- no swallowed OLE Layer/Update/Delete errors.
