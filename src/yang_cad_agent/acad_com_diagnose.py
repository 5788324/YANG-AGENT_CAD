"""Read-only AutoCAD COM diagnostics."""

from __future__ import annotations

import ctypes
import sys
from pathlib import Path

from .current_lisp import AUTOCAD_PROG_IDS, _acad_process_state


def _is_current_process_elevated() -> bool | None:
    try:
        return bool(ctypes.windll.shell32.IsUserAnAdmin())
    except Exception:
        return None


def _check_pywin32() -> dict:
    try:
        import win32com.client  # type: ignore
    except ImportError:
        return {
            "available": False,
            "error": "pywin32 is not installed in this Python environment.",
        }
    return {
        "available": True,
        "module": getattr(win32com.client, "__name__", "win32com.client"),
    }


def diagnose_acad_com(project_root: Path | None = None) -> dict:
    """Inspect local AutoCAD COM reachability without sending commands."""
    pywin32 = _check_pywin32()
    process = _acad_process_state()
    result = {
        "ok": True,
        "mode": "read_only",
        "project_root": str(Path(".").resolve() if project_root is None else project_root.resolve()),
        "python": {
            "executable": sys.executable,
            "version": sys.version.split()[0],
            "elevated": _is_current_process_elevated(),
        },
        "pywin32": pywin32,
        "acad_process": process,
        "prog_id_checks": [],
        "attachable": False,
        "active_document": None,
        "diagnostics": [],
    }
    if not pywin32["available"]:
        result["diagnostics"].append(
            {
                "rule_id": "pywin32_missing",
                "severity": "error",
                "message": "Python cannot import win32com.client.",
                "suggestion": "Install pywin32 in the Python used by the scripts, then retry.",
            }
        )
        return result

    import win32com.client  # type: ignore

    for prog_id in AUTOCAD_PROG_IDS:
        check = {"prog_id": prog_id, "ok": False, "error": ""}
        try:
            acad = win32com.client.GetActiveObject(prog_id)
            doc = getattr(acad, "ActiveDocument", None)
            check["ok"] = True
            check["active_document"] = "" if doc is None else getattr(doc, "Name", "")
            result["attachable"] = True
            result["active_document"] = check["active_document"]
        except Exception as exc:  # pragma: no cover - depends on local AutoCAD COM
            check["error"] = str(exc)
        result["prog_id_checks"].append(check)
        if check["ok"]:
            break

    if result["attachable"]:
        result["diagnostics"].append(
            {
                "rule_id": "acad_com_attachable",
                "severity": "info",
                "message": "Python can attach to AutoCAD COM.",
                "suggestion": "It is reasonable to retry scripts\\current-smoke-test.cmd --execute on a test DWG.",
            }
        )
    elif process.get("running") is True:
        result["diagnostics"].append(
            {
                "rule_id": "acad_process_without_com",
                "severity": "error",
                "message": "acad.exe is running, but AutoCAD COM is not attachable.",
                "suggestion": (
                    "Close AutoCAD, reopen it normally instead of as administrator, open a test DWG, "
                    "wait until the command line is ready, then retry."
                ),
            }
        )
    else:
        result["diagnostics"].append(
            {
                "rule_id": "acad_process_missing",
                "severity": "warning",
                "message": "No running acad.exe process was detected.",
                "suggestion": "Open AutoCAD 2027 and a test DWG before running current-smoke-test --execute.",
            }
        )
    return result
