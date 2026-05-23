"""Current drawing LISP feed scaffold."""

from __future__ import annotations

from datetime import datetime
from pathlib import Path

from . import error_codes
from .ledger import create_task_record, update_task_record
from .lisp_validator import validate_lisp_file


def build_load_command(script_path: Path) -> str:
    resolved = str(script_path.resolve()).replace("\\", "\\\\")
    return f'(load "{resolved}")\n'


def _send_command_to_autocad(command: str) -> dict:
    try:
        import win32com.client  # type: ignore
    except ImportError:
        return {
            "ok": False,
            "error_code": error_codes.ACAD_COM_DEPENDENCY_MISSING,
            "message": "pywin32 is not installed in this Python environment.",
        }

    prog_ids = [f"AutoCAD.Application.{v}" for v in ["25", "24", "23", "22", "21", "20"]]
    prog_ids.append("AutoCAD.Application")
    last_error = ""
    for prog_id in prog_ids:
        try:
            acad = win32com.client.GetActiveObject(prog_id)
            doc = acad.ActiveDocument
            if doc is None:
                return {
                    "ok": False,
                    "error_code": error_codes.ACAD_DOC_UNAVAILABLE,
                    "message": "AutoCAD is running, but no active document was found.",
                    "prog_id": prog_id,
                }
            doc.SendCommand(command)
            return {
                "ok": True,
                "prog_id": prog_id,
                "document": getattr(doc, "Name", ""),
            }
        except Exception as exc:  # pragma: no cover - depends on local AutoCAD COM
            last_error = str(exc)
            continue

    return {
        "ok": False,
        "error_code": error_codes.ACAD_COM_UNAVAILABLE,
        "message": "Could not connect to a running AutoCAD COM instance.",
        "last_error": last_error,
    }


def feed_current_lisp(project_root: Path, script_path: Path, execute: bool = False) -> dict:
    """Validate a LISP file and dry-run or feed it to the current AutoCAD drawing."""
    task = create_task_record(
        project_root=project_root,
        user_goal=f"current drawing LISP: {script_path}",
        risk="current_light",
        track="B",
    )
    task_id = task["task_id"]
    validation = validate_lisp_file(script_path, target_track="B")
    command = build_load_command(script_path)
    update_task_record(
        project_root,
        task_id,
        status="dry_run" if not execute else "running",
        script_path=str(script_path),
        params={"execute": execute, "command": command},
        started_at=datetime.now().isoformat(timespec="seconds"),
    )

    if not validation["ok"]:
        update_task_record(
            project_root,
            task_id,
            status="failed",
            error_code=validation["error_code"],
            finished_at=datetime.now().isoformat(timespec="seconds"),
        )
        return {
            "ok": False,
            "task_id": task_id,
            "error_code": validation["error_code"],
            "validation": validation,
        }

    if not execute:
        return {
            "ok": True,
            "task_id": task_id,
            "mode": "dry_run",
            "requires_confirmation": True,
            "next_step": "Review the command, then re-run with --execute while AutoCAD is open.",
            "command": command,
            "validation": validation,
        }

    sent = _send_command_to_autocad(command)
    update_task_record(
        project_root,
        task_id,
        status="sent" if sent["ok"] else "failed",
        error_code=None if sent["ok"] else sent.get("error_code", error_codes.UNKNOWN_ERROR),
        finished_at=datetime.now().isoformat(timespec="seconds"),
    )
    return {
        "ok": sent["ok"],
        "task_id": task_id,
        "mode": "execute",
        "command": command,
        "validation": validation,
        "send_result": sent,
    }

