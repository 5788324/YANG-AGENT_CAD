"""Current drawing LISP feed scaffold."""

from __future__ import annotations

import json
import subprocess
import time
from datetime import datetime
from pathlib import Path

from . import error_codes
from .ledger import create_task_record, update_task_record
from .lisp_validator import validate_lisp_file


AUTOCAD_PROG_IDS = [f"AutoCAD.Application.{v}" for v in ["26", "25", "24", "23", "22", "21", "20"]]
AUTOCAD_PROG_IDS.append("AutoCAD.Application")


def _lisp_path(path: Path) -> str:
    return str(path.resolve()).replace("\\", "/")


def build_load_command(script_path: Path) -> str:
    resolved = str(script_path.resolve()).replace("\\", "\\\\")
    return f'(load "{resolved}")\n'


def _lisp_string_literal(value: str) -> str:
    return '"' + value.replace("\\", "\\\\").replace('"', '\\"') + '"'


def build_wrapper_lisp(original_script: Path, result_path: Path, task_id: str) -> str:
    """Build a wrapper that records whether AutoCAD reached the end of the load."""
    original = _lisp_path(original_script)
    result = _lisp_path(result_path)
    task_id_line = f'  "task_id": {json.dumps(task_id, ensure_ascii=False)},'
    return "\n".join(
        [
            "(defun yang-agent-current-write-result (status message / fh)",
            f'  (setq fh (open "{result}" "w"))',
            "  (if fh",
            "    (progn",
            '      (write-line "{" fh)',
            f"      (write-line {_lisp_string_literal(task_id_line)} fh)",
            '      (write-line "  \\"track\\": \\"B\\"," fh)',
            '      (write-line "  \\"mode\\": \\"current_lisp\\"," fh)',
            '      (write-line (strcat "  \\"status\\": \\"" status "\\",") fh)',
            '      (write-line (strcat "  \\"message\\": \\"" message "\\"") fh)',
            '      (write-line "}" fh)',
            "      (close fh)",
            "    )",
            "  )",
            "  (princ)",
            ")",
            "",
            "(setq yang-agent-current-result",
            f'  (vl-catch-all-apply (function load) (list "{original}"))',
            ")",
            "(if (vl-catch-all-error-p yang-agent-current-result)",
            '  (yang-agent-current-write-result "failed" "LISP load or runtime failed; inspect AutoCAD command line.")',
            '  (yang-agent-current-write-result "completed" "LISP finished and wrote completion marker.")',
            ")",
            '(princ "\\nYANG AGENT CAD: current LISP wrapper finished.")',
            "(princ)",
            "",
        ]
    )


def prepare_current_lisp_run(project_root: Path, task_id: str, script_path: Path) -> dict:
    current_dir = project_root / ".agent" / "current" / task_id
    current_dir.mkdir(parents=True, exist_ok=True)
    wrapper_path = current_dir / "wrapper.lsp"
    result_path = current_dir / "result.json"
    wrapper_path.write_text(
        build_wrapper_lisp(script_path, result_path, task_id),
        encoding="utf-8",
    )
    command = build_load_command(wrapper_path)
    return {
        "wrapper_path": str(wrapper_path),
        "result_path": str(result_path),
        "command": command,
    }


def read_completion_marker(result_path: Path) -> dict:
    if not result_path.exists():
        return {
            "confirmed": False,
            "status": "not_found",
            "path": str(result_path),
        }
    try:
        marker = json.loads(result_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        return {
            "confirmed": True,
            "status": "unreadable",
            "path": str(result_path),
            "error_code": error_codes.VERIFY_FAILED,
        }
    return {
        "confirmed": True,
        "status": marker.get("status", "unknown"),
        "path": str(result_path),
        "marker": marker,
    }


def wait_for_completion_marker(result_path: Path, timeout_seconds: float = 5.0) -> dict:
    deadline = time.monotonic() + timeout_seconds
    while time.monotonic() < deadline:
        marker = read_completion_marker(result_path)
        if marker["confirmed"]:
            return marker
        time.sleep(0.25)
    return read_completion_marker(result_path)


def _acad_process_state() -> dict:
    try:
        result = subprocess.run(
            ["tasklist", "/FI", "IMAGENAME eq acad.exe", "/FO", "CSV", "/NH"],
            capture_output=True,
            text=True,
            timeout=5,
            check=False,
        )
    except Exception:
        result = None
    if result is not None and result.returncode == 0:
        return {"running": "acad.exe" in result.stdout.lower(), "method": "tasklist"}

    try:
        ps_result = subprocess.run(
            [
                "powershell",
                "-NoProfile",
                "-Command",
                "if (Get-Process acad -ErrorAction SilentlyContinue) { 'running' } else { 'missing' }",
            ],
            capture_output=True,
            text=True,
            timeout=5,
            check=False,
        )
    except Exception as exc:
        return {"running": None, "method": "powershell", "error": str(exc)}
    if ps_result.returncode == 0:
        return {"running": "running" in ps_result.stdout.lower(), "method": "powershell"}
    return {
        "running": None,
        "method": "tasklist+powershell",
        "error": (ps_result.stderr or ps_result.stdout or "").strip(),
    }


def _is_acad_process_running() -> bool | None:
    return _acad_process_state()["running"]


def _send_command_to_autocad(command: str) -> dict:
    try:
        import win32com.client  # type: ignore
    except ImportError:
        return {
            "ok": False,
            "error_code": error_codes.ACAD_COM_DEPENDENCY_MISSING,
            "message": "pywin32 is not installed in this Python environment.",
        }

    last_error = ""
    for prog_id in AUTOCAD_PROG_IDS:
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
        "tried_prog_ids": AUTOCAD_PROG_IDS,
        "acad_process": _acad_process_state(),
    }


def feed_current_lisp(project_root: Path, script_path: Path, execute: bool = False) -> dict:
    """Validate a LISP file and dry-run or feed it to the current AutoCAD drawing."""
    project_root = project_root.resolve()
    script_path = script_path.resolve()
    task = create_task_record(
        project_root=project_root,
        user_goal=f"current drawing LISP: {script_path}",
        risk="current_light",
        track="B",
    )
    task_id = task["task_id"]
    validation = validate_lisp_file(script_path, target_track="B")
    run_files = None
    if validation["ok"]:
        run_files = prepare_current_lisp_run(project_root, task_id, script_path)
        command = run_files["command"]
    else:
        command = build_load_command(script_path)
    update_task_record(
        project_root,
        task_id,
        status="dry_run" if not execute else "running",
        script_path=str(script_path),
        params={
            "execute": execute,
            "command": command,
            "wrapper_path": "" if run_files is None else run_files["wrapper_path"],
            "completion_marker": "" if run_files is None else run_files["result_path"],
        },
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

    assert run_files is not None
    completion = {
        "confirmed": False,
        "status": "not_run",
        "path": run_files["result_path"],
    }
    if not execute:
        return {
            "ok": True,
            "task_id": task_id,
            "mode": "dry_run",
            "requires_confirmation": True,
            "next_step": "Review the command, then re-run with --execute while AutoCAD is open.",
            "command": command,
            "wrapper_path": run_files["wrapper_path"],
            "completion_marker": run_files["result_path"],
            "completion": completion,
            "validation": validation,
        }

    sent = _send_command_to_autocad(command)
    status = "failed"
    error_code = sent.get("error_code", error_codes.UNKNOWN_ERROR)
    next_step = "Run task_error_detail for this task id and inspect AutoCAD command output."
    if sent["ok"]:
        marker = wait_for_completion_marker(Path(run_files["result_path"]))
        completion = marker
        if marker["confirmed"] and marker["status"] == "completed":
            status = "completed"
            error_code = None
            next_step = "Current drawing LISP completed and wrote the result marker."
        elif marker["confirmed"]:
            status = "failed"
            error_code = marker.get("error_code", error_codes.LISP_RUNTIME_FAILED)
            next_step = "AutoCAD wrote a failure marker. Inspect AutoCAD command output and task_error_detail."
        else:
            status = "sent_unconfirmed"
            error_code = None
            next_step = "Command was sent to AutoCAD, but no completion marker was seen yet. Check the marker path and AutoCAD command line."
    update_task_record(
        project_root,
        task_id,
        status=status,
        error_code=error_code,
        params={
            "execute": execute,
            "command": command,
            "wrapper_path": run_files["wrapper_path"],
            "completion_marker": run_files["result_path"],
            "completion": completion,
            "send_result": sent,
        },
        finished_at=datetime.now().isoformat(timespec="seconds"),
    )
    return {
        "ok": sent["ok"],
        "task_id": task_id,
        "mode": "execute",
        "command": command,
        "status": status,
        "error_code": error_code,
        "next_step": next_step,
        "wrapper_path": run_files["wrapper_path"],
        "completion_marker": run_files["result_path"],
        "completion": completion,
        "validation": validation,
        "send_result": sent,
    }
