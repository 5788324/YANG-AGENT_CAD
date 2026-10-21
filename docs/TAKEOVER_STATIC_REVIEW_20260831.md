# YangCAD — 2026-08-31 Takeover Static Review

## Baseline received

- Candidate: `YangCAD_FULL_REPO_MCP_SELFHEAL_IMAGE_EMBED_STATIC_CANDIDATE_20260831.zip`
- Received SHA-256: `6E8A70018AD7388CCBCFC7DF4620364AE051AB1E8D7D44CD37FAFDA1899FD95D`
- Received internal SHA: `175/175 PASS`
- Existing 12 static/policy gates: independently rerun and PASS before changes.

## Scope preserved

No third feature started.

Only current work remains:

1. `FEATURE-INFRA-MCP-CONFIG-001` — self-heal Windows retest.
2. `FEATURE-PROJECT-IMAGE-EMBED-001` — AutoCAD 2024 Windows smoke.

Contract remains `65 / 28 / 58`.

## Static hardening applied before Windows

### MCP self-heal

- Setup and Verify-Release now require the complete MCP Python bridge, not only the launcher.
- Embedded runtime materialization publishes replacements without a delete-first gap.
- Same-content concurrent materialization is tolerated; conflicting content fails closed.

### Batch Image Embed v1

- Worker is launched with explicit PowerShell STA for Clipboard use.
- Model/Paper active space is set explicitly; paper space also forces `MSpace=false` before `PASTECLIP`.
- `status.json` uses temp + `File.Replace` rather than delete + move.
- Raster snapshot failures are recorded as conversion failures and cannot become false `no_top_level_raster_images` skips.
- Non-unique new OLE deltas are cleaned before returning a per-image failure.
- Source SHA is checked before output publication and again immediately after publication.
- Final output publication does not use `Move-Item -Force`; a concurrent same-name destination cannot be overwritten.
- If an error occurs after this run publishes an output, only the output owned by this run is removed.

## Static gates after hardening

- `verify_source.py`: errors=0
- `verify_release_source.py`: errors=0
- extension policy: 12/12 PASS
- quick install policy: 10/10 PASS
- civil compatibility policy: 8/8 PASS
- toolbox foundation: 10/10 PASS
- toolbox round2: 16/16 PASS
- toolbox round3 read: PASS
- toolbox round3b write: PASS
- MCP copy config: 19/19 PASS
- RVT Exchange v1: 25/25 PASS
- Image Embed v1: 26/26 PASS

## Remaining Windows proof

MCP:
- installed bundle path copy-config;
- embedded self-heal path after removing `Contents\\Mcp`;
- `cad.status`;
- correct PID binding;
- zero token leak.

Image Embed:
- AutoCAD 2024 COM worker starts;
- real `PASTECLIP` creates embedded `AcDbOle2Frame`;
- ModelSpace JPG/PNG;
- PaperSpace target is actual Layout/PaperSpace, not viewport model space;
- move source image away, reopen output, OLE remains visible;
- missing/clipped/no-image behavior;
- source SHA unchanged;
- no overwrite, including optional race sample;
- one failed DWG does not stop later files.

Until those runtime checks pass, both features remain **NOT CLOSED**.
