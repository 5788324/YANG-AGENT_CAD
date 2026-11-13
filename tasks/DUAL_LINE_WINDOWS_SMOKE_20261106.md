# Gemini Windows AutoCAD 2024 — YGDL / YGDLW real runtime test
2026-11-06. Status: WINDOWS_NOT_TESTED. G0 full Git HEAD + Windows host + loaded PID + Build logs, DLL/Loaded DLL SHA; G1 CI is not Runtime PASS.

1. INSUNITS 0/4/6/1 and width conversion, unitless warning; unsupported units refused.
2. Clean settings: first width 1000 mm, after draw width 600, next invocation and host restart remember 600.
3. YGDL Double creates exactly two editable native polylines; Pipe creates exactly two side polylines and one centerline. Source units and elevation correct.
4. Center/Left/Right input path alignment correctly preserved.
5. Miter/Bevel/Round, corners incl 90°, acute, obtuse; distances measured along perpendiculars. Bulge round inner radius > half width.
6. Repeat groups in one command, W updates next group's width, Undo removes last picked vertex.
7. YGDLW selected any of left/right/center, 600 mm -> 1000 mm: both sides outward 200, visible centerline geometry/Handle exactly unchanged.
8. 1000 -> 600: both inward 200; cycle 600 ->1000 ->600 detects cumulative drift.
9. Legacy two parallel LINE pairs, matching endpoints including reversed orientation: symmetric width changes preserve original two LINE Handles.
10. Legacy unequal/offset/aligned mismatch, non-parallel, other elevation, POLYLINE and ARC rejected with original DWG untouched.
11. Manual edited left/right or centerline -> reject rather than overwriting; copied duplicate GUID -> reject.
12. Frozen/locked/off layers, cross-layout and Xref interactions rejected as designed.
13. New round width too large / short center segments / self-crossing / reversal -> reject in one CAD transaction.
14. Actual native _.U once after YGDL group draw and once after YGDLW width edit: compare before/after/undo Handle, entity type, bulge, coordinate, center and DXF.
15. Save/close/reopen in fresh process; XData group is still readable and YGDLW can edit after reopening.
16. Toolbox two Chinese buttons and PGP command aliases, no interference with source XD and other add-ins.

Provide at least: 3 DWG fixture before/after/undo, zoomed screenshots, exact object-level before/after/undo JSON, AutoCAD real command logs and any test driver .cs/scripts, Build binlog exit code and loaded DLL path/PID/SHA.
Label each PASS/FAIL/BLOCKED/NOT_TESTED; do not substitute Transaction.Abort for _.U. Keep PR Draft; do not merge.
READY_FOR_CHATGPT_REVIEW — git=<40-hex> — BUILD=<...> LOADED=<...> NATIVE_UNDO=<...> evidence=<url>

## UCS, current layer, view and command collision
- Test WCS and rotated/origin-shifted UCS 30/90 degrees: inspect WCS coordinate readback for exact placement.
- Test tilted UCS: command must reject before writing any entity, with explicit Chinese error, instead of flattening points.
- Test current off/locked/frozen layer: no new objects and explicit failure; visible writeable layer creates objects in correct CurrentSpaceId.
- Confirm that one completed YGDL group and one YGDLW width edit trigger screen refresh; dynamic vertex preview is NOT_IMPLEMENTED, do not claim it as PASS.
- Do not load the uploaded LISP c:YGDL and the .NET YGDL in one AutoCAD process. Isolate them or rename one for comparative tests.
