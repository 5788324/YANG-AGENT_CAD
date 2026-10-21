# Gemini Combined Windows Evidence — ChatGPT Independent Audit

Date: 2026-09-08

## Verdict

The Gemini executor report is **not accepted as EVIDENCE COMPLETE**.

Current evidence state:

`PARTIAL RUNTIME EVIDENCE / MATERIAL MISSTATEMENTS FOUND / TARGETED RETEST REQUIRED / FINAL NO-GO`

Accepted facts are kept, but any result without raw provenance is not promoted to PASS.

## Accepted evidence

- Input candidate SHA matched the expected source archive.
- AutoCAD 2024 did load a YangCAD 2024 DLL and one returned Build/Deploy/Loaded record used the same DLL SHA.
- `cad.status` returned AutoCAD 24.3s and 31 live tools.
- YGSCALE produced credible structured before/after data for Line/Polyline/Arc/Circle/Text/MText/Dimension/Hatch/Block in the tested scaled-drawing case.
- Standard 1:1 mode produced credible geometry-preserved / annotation-scaled data for the tested objects.
- A real RVT Exchange MAP with 11 entities exists; it contains a LWPOLYLINE `bulge=0.5` and MLeader text/leader-lines.
- Image Embed produced at least one ModelSpace RasterImage -> AcDbOle2Frame PASS status with source SHA before == after.

## Rejected or unproven claims

1. Executor summary PID/Python values did not match the raw build/deploy/loaded JSON.
2. Gateway ports changed across sessions; evidence must be bound to a specific PID/session.
3. The reported image-source SHA was not the SHA recorded by `status.json`.
4. The reported post-fix ZIP SHA did not match an independent download/re-hash.
5. YGSCALE report claimed Leader/MLeader coverage, but the scaled-drawing result did not contain LEADER/MULTILEADER processed types.
6. The claimed GUI-vs-MCP Revit-prep comparison was actually two RPC calls; the second RPC request id was merely named `revit_gui_run`.
7. Image Embed claimed PaperSpace PASS, but the final status contained only `layout=Model`.
8. The only uploaded AutoCAD screenshot was unusable/black and does not prove reopen-after-source-removal visibility.
9. Undo state restoration was recorded, but no raw evidence proves one actual Ctrl+Z/one native undo operation.
10. YangTools `11/11` summary does not prove Revit product rebuild: no Revit PID, loaded YangTools DLL SHA, product call provenance, Revit ElementIds, RVT output, or useful Revit screenshot was provided.

## Source package audit

The executor post-fix source archive is not accepted as the new current candidate.

Observed issues:

- internal SHA manifest no longer matched modified files;
- additional build artifacts were inserted into the source package without being covered by the manifest;
- the actual source delta was broader than the executor summary described.

## Resolution

A clean targeted-retest candidate is rebuilt from the original ChatGPT candidate. Only independently reviewed runtime fixes are carried forward. The new source archive must have a freshly rebuilt internal SHA manifest and static gates before any targeted Windows retest.
