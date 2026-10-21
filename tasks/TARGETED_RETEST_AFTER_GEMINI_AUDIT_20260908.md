# TARGETED RETEST — After Gemini Evidence Audit

Date: 2026-09-08
Executor: Gemini on Windows / AutoCAD 2024; Revit 2024 only for the Revit-specific item.
Final reviewer: ChatGPT.

## Hard role rule

Gemini is an executor only. Do not write `FINAL GO`.

For every PASS, attach raw evidence. A self-authored summary JSON is not sufficient by itself.

Allowed final executor conclusions:

- `EVIDENCE COMPLETE — SUBMIT TO CHATGPT FOR FINAL REVIEW`
- `NO-GO — FAILURE EVIDENCE ATTACHED`
- `INCOMPLETE — UNPROVEN ITEMS REMAIN`

## 0. Candidate identity

Use only the candidate named in `00_CURRENT/CURRENT_CANDIDATE_SHA256.txt`.

Before work:

1. calculate outer ZIP SHA-256;
2. extract fresh;
3. verify internal `SHA256SUMS.txt` for every listed file;
4. record exact Windows version, AutoCAD version, PID/start time, Python version.

If any identity check fails: STOP.

## 1. Build / Deploy / Loaded

Build the fresh source and record:

- build command;
- build DLL path + SHA;
- deploy DLL path + SHA;
- loaded assembly path + SHA;
- AutoCAD PID/start time;
- Gateway port from the same PID/session.

Required invariant:

`Build DLL SHA == Deploy DLL SHA == Loaded DLL SHA`

Do not mix evidence from different AutoCAD sessions without labeling every PID/port.

## 2. YGSCALE targeted gaps only

Do not repeat the already credible Line/Text/Dimension/Hatch basics unless required to build the fixture.

### 2A. Leader + MLeader

Create a small controlled drawing containing:

- one Leader with visible annotation;
- one MLeader with text;
- one reference geometry object.

Run `scaled_drawing 1:5 -> 1:10` through the real product path.

Record before/after:

- leader vertex coordinates;
- MLeader vertex coordinates;
- text/annotation height;
- arrow size where readable;
- processed type counters proving LEADER and MULTILEADER were actually handled.

### 2B. One real native Undo

After one real YGSCALE conversion:

- capture the converted state;
- press **Ctrl+Z exactly once**;
- capture AutoCAD command history showing the undo action;
- read back the same object properties after undo.

A scripted JSON that merely contains restored values without command-history provenance is not enough.

## 3. True GUI vs MCP — `project.revit.prepare`

Use the same DWG and same options.

### GUI run

1. physically open the YangCAD Toolbox;
2. click `Revit 图纸预处理`;
3. execute from the GUI;
4. save screenshot showing the visible Toolbox result;
5. save runtime.log lines containing:
   - `Toolbox GUI execute begin tool=project.revit.prepare`
   - `Toolbox GUI execute completed tool=project.revit.prepare`

### MCP run

Run `project.revit.prepare` through MCP and retain the normal `RPC accepted/completed` provenance.

Then compare MAP/REPORT fields:

- schema_version
- units / origin / source_insunits
- entity count
- DxfType counts
- handles
- unsupported_count
- warnings
- per-handle entity detail

The GUI run must not contain an RPC invocation pretending to be GUI provenance.

## 4. Image Embed — only missing gates

Use a controlled DWG containing:

- one top-level RasterImage in ModelSpace;
- one top-level RasterImage in a named PaperSpace layout.

Requirements:

- both images produce `AcDbOle2Frame`;
- final `status.json` contains at least two image results and includes both `Model` and the actual PaperSpace layout name;
- source DWG SHA before == after;
- output is a different file and no overwrite occurs.

Then:

1. move/rename the original image files so their old paths no longer resolve;
2. close AutoCAD worker/output drawing;
3. reopen the output DWG in a new AutoCAD session;
4. switch to Model and PaperSpace;
5. capture valid screenshots where both embedded images are visibly present;
6. additionally read back both OLE handles/layouts if possible.

Black/blank screenshots are invalid evidence.

## 5. MCP self-heal — raw evidence

Only re-prove the missing provenance quality:

- healthy runtime;
- runtime missing;
- partial runtime with one required Python module deleted.

For each case retain raw command/process output showing:

- input runtime path;
- health result before;
- materialized fallback path;
- health result after;
- actual launcher process success.

Do not submit only a hand-written summary JSON.

## 6. YangTools / Revit claim

Two acceptable options:

### Option A — do not claim Revit product E2E

Mark this item `NOT_TESTED` and keep only the already accepted CAD Exchange MAP 11/11 evidence.

### Option B — actually prove Revit product E2E

If claiming YangTools/Revit rebuild PASS, provide:

- Revit 2024 PID/start time;
- loaded YangTools 3.1.36 DLL path + SHA;
- exact product command/button used;
- input MAP path + SHA;
- product runtime log;
- created Revit ElementIds/counts by category;
- valid Revit screenshot showing reconstructed content;
- readback proving the bulged polyline became an Arc and MLeader became TextNote + Leader.

A 619-byte summary JSON is not sufficient.

## 7. Evidence upload

Create a new sibling folder under `04_EVIDENCE`:

`20260908_TARGETED_RETEST_AFTER_AUDIT/`

Suggested structure:

- `00_IDENTITY`
- `01_BUILD_DEPLOY_LOADED`
- `02_SCALE_LEADER_MLEADER_UNDO`
- `03_TRUE_GUI_MCP`
- `04_IMAGE_MODEL_PAPER_REOPEN`
- `05_MCP_SELF_HEAL_RAW`
- `06_REVIT_IF_TESTED`
- `07_SCREENSHOTS`
- `08_LOGS`
- `09_SOURCE_IF_CHANGED`

If source code is changed during retest, STOP normal testing, package the full changed source with a new SHA and explicitly request ChatGPT review before treating later results as candidate evidence.
