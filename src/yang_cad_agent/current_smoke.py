"""Beginner-friendly current drawing smoke test workflow."""

from __future__ import annotations

from pathlib import Path

from . import error_codes
from .acad_com_diagnose import diagnose_acad_com
from .current_lisp import feed_current_lisp


def current_smoke_script(project_root: Path) -> Path:
    return project_root / "toolbox" / "plugins" / "current_smoke" / "main.lsp"


def _blocked_by_diagnosis(diagnosis: dict) -> dict:
    error_code = error_codes.ACAD_COM_UNAVAILABLE
    if not diagnosis.get("pywin32", {}).get("available", False):
        error_code = error_codes.ACAD_COM_DEPENDENCY_MISSING
    return {
        "ok": False,
        "mode": "execute",
        "status": "blocked",
        "error_code": error_code,
        "diagnosis": diagnosis,
        "next_step": (
            "Open AutoCAD 2027 normally, open a test DWG, wait until the command line is ready, "
            "then run scripts\\current-smoke-test.cmd --execute again."
        ),
    }


def run_current_smoke(project_root: Path, execute: bool = False) -> dict:
    """Run dry-run or guarded execute for the built-in current_smoke plugin."""
    project_root = project_root.resolve()
    script_path = current_smoke_script(project_root)
    if not execute:
        return feed_current_lisp(project_root=project_root, script_path=script_path, execute=False)

    diagnosis = diagnose_acad_com(project_root)
    if not diagnosis.get("attachable", False):
        return _blocked_by_diagnosis(diagnosis)
    result = feed_current_lisp(project_root=project_root, script_path=script_path, execute=True)
    result["preflight"] = diagnosis
    return result
