from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "mcp"))

from yangcad_mcp.extensions import _scan_body  # noqa: E402


def expect_reject(name: str, body: str, *, is_write: bool, requires_erase: bool = False) -> None:
    try:
        _scan_body(body, is_write=is_write, requires_erase=requires_erase)
    except ValueError:
        return
    raise AssertionError(f"{name}: expected rejection")


def expect_accept(name: str, body: str, *, is_write: bool, requires_erase: bool = False) -> None:
    try:
        _scan_body(body, is_write=is_write, requires_erase=requires_erase)
    except Exception as exc:
        raise AssertionError(f"{name}: expected acceptance, got {exc}") from exc


def main() -> int:
    reject_cases = [
        ("comment-only RecordChange",
         '// context.RecordChange("create", "fake");\nreturn new { ok = true };',
         True, False),
        ("numeric OpenMode cast",
         'var obj = context.Open("1A", (OpenMode)1); return obj.Handle.ToString();',
         False, False),
        ("reflection concat filesystem bypass",
         '// context.RecordChange("noop", "noop");\nvar tn = "System." + "IO.File, mscorlib";\nvar t = Type.GetType(tn);\nvar m = t.GetMethod("Del" + "ete", new Type[] { typeof(string) });\nm.Invoke(null, new object[] { @"C:\\\\temp\\\\victim.txt" });\nreturn null;',
         True, False),
        ("reflection erase bypass",
         'context.RecordChange("erase", "probe");\nvar obj = context.Open("1A", OpenMode.ForWrite);\nvar mi = obj.GetType().GetMethod("Er" + "ase", new Type[] { typeof(bool) });\nmi.Invoke(obj, new object[] { true });\nreturn null;',
         True, False),
        ("disable undo recording",
         'context.Database.DisableUndoRecording(true); context.RecordChange("modify", "undo"); return null;',
         True, False),
        ("global Application access",
         'Application.Quit(); context.RecordChange("modify", "app"); return null;',
         True, False),
        ("document close/discard",
         'context.Document.CloseAndDiscard(); context.RecordChange("modify", "doc"); return null;',
         True, False),
        ("direct DXF file output",
         'context.Database.DxfOut(@"C:\\\\temp\\\\x.dxf", 16, DwgVersion.Current); context.RecordChange("export", "x"); return null;',
         True, False),
        ("runtime module load",
         'SystemObjects.DynamicLinker.LoadModule("evil.dll", false, false); context.RecordChange("load", "module"); return null;',
         True, False),
        ("host transaction dispose",
         'context.Transaction.Dispose(); context.RecordChange("modify", "tr"); return null;',
         True, False),
    ]
    for name, body, is_write, requires_erase in reject_cases:
        expect_reject(name, body, is_write=is_write, requires_erase=requires_erase)

    accept_cases = [
        ("normal write",
         'context.RecordChange("create", "ModelSpace", null, "LINE:OK"); return new { ok = true };',
         True, False),
        ("normal read",
         'var bt = (BlockTable)context.Transaction.GetObject(context.Database.BlockTableId, OpenMode.ForRead); return bt.Has(BlockTableRecord.ModelSpace);',
         False, False),
    ]
    for name, body, is_write, requires_erase in accept_cases:
        expect_accept(name, body, is_write=is_write, requires_erase=requires_erase)

    total = len(reject_cases) + len(accept_cases)
    print(f"extension policy regression: {total}/{total} PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
