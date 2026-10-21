# Targeted Runtime Fix Review — 2026-09-08

## Source of truth

Base: `YangCAD_0.9_SCALE_CONVERTER_MCP_ALIGN_STATIC_20260907.zip`

Gemini's post-fix ZIP is **not** used as a base.

## Accepted source fixes

### 1. C# compile qualification

Two compile-qualification fixes are retained from the Windows build attempt:

- `Exception` -> `System.Exception` in `ScaleConvertCommands.cs` where namespace ambiguity can occur.
- `Convert.*` -> `System.Convert.*` and `Exception` -> `System.Exception` in reflection helpers of `ScaleConversionService.cs`.

These are namespace-qualification fixes only; no YGSCALE business algorithm is changed.

### 2. Image Embed PowerShell 5.1 compatibility/reliability

The worker is rebuilt from the clean candidate with reviewed changes only:

- preserve HashSet return as a single object (`return ,$result`);
- avoid unstable PowerShell 5.1 generic-list result serialization by returning a `PSCustomObject` and explicit `ToArray()`;
- robust indexed layout lookup and explicit Model/Paper context;
- **fail closed if the requested layout cannot be found** — no silent fallback to ModelSpace;
- bounded COM `PASTECLIP` retry and quiescent wait;
- bounded document-open retry;
- improved failure detail;
- status replace fallback where `File.Replace` is unavailable.

Correctness-critical operations remain strict:

- OLE Layer assignment must succeed;
- OLE Update must succeed;
- source RasterImage Delete must succeed;
- output publication still refuses overwrite;
- source SHA must remain unchanged.

Gemini's broad `try/catch` suppression around OLE normalization/deletion is **not** retained.

### 3. GUI provenance

`ToolboxView` now writes explicit runtime provenance around GUI preview/execute:

- `Toolbox GUI preview begin/completed tool=...`
- `Toolbox GUI execute begin/completed tool=...`

This exists specifically so a future GUI-vs-MCP reconciliation cannot be faked by naming an RPC request `gui`.

## Explicitly rejected executor changes

- `Deploy-Dev.ps1` in-use bundle fallback is not carried forward. Partial copying into a loaded bundle can create a mixed deployment and is not acceptable as a release behavior.
- Silent fallback to ModelSpace when a PaperSpace layout lookup fails is rejected.
- Swallowing OLE Layer/Update/Delete failures is rejected.
- Executor build artifacts/logs are not embedded in the clean source package.

## Static status

This candidate is a **targeted retest candidate**, not a final release.

Required runtime retest is defined in `tasks/TARGETED_RETEST_AFTER_GEMINI_AUDIT_20260908.md`.
