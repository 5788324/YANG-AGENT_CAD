"""Read-only task ledger query helpers."""

from __future__ import annotations

from pathlib import Path

from .backup import rollback_task
from .error_codes import explain_error_code
from .ledger import list_task_records
from .ledger import load_task_record


def _log_paths(project_root: Path, task_id: str) -> list[str]:
    log_dir = project_root / ".agent" / "logs" / task_id
    if not log_dir.exists():
        return []
    return [str(path) for path in sorted(log_dir.glob("*.log"))]


def _read_log_tail(path: Path, max_chars: int) -> str:
    try:
        text = path.read_text(encoding="utf-8", errors="replace")
    except TypeError:
        text = path.read_text(encoding="utf-8")
    return text[-max_chars:]


def _log_tails(log_paths: list[str], max_chars: int) -> list[dict]:
    tails = []
    for raw_path in log_paths:
        path = Path(raw_path)
        tails.append(
            {
                "path": raw_path,
                "tail": _read_log_tail(path, max_chars),
            }
        )
    return tails


def _diagnose_log_tails(log_tails: list[dict], error_code: str | None) -> list[dict]:
    diagnostics = []
    for item in log_tails:
        tail = item.get("tail", "")
        path = item.get("path", "")
        tail_lower = tail.lower()
        if "acad2027" in tail_lower and ("load 失败" in tail_lower or "load failed" in tail_lower):
            diagnostics.append(
                {
                    "rule_id": "acad_startup_noise",
                    "severity": "info",
                    "evidence": "acad2027 load message",
                    "message": "AutoCAD startup emitted an acad2027 load message; this is treated as startup noise unless another load error follows.",
                    "suggestion": "Focus on the later LISP load or accoreconsole error in the same log.",
                    "log_path": path,
                }
            )
        if (
            "文件加载已取消" in tail
            or "load canceled" in tail_lower
            or ("load" in tail_lower and "failed" in tail_lower and "acad2027" not in tail_lower)
        ):
            diagnostics.append(
                {
                    "rule_id": "lisp_load_canceled",
                    "severity": "error",
                    "evidence": "LISP load canceled or failed",
                    "message": "AutoCAD canceled or failed the requested LISP load.",
                    "suggestion": "Check the LISP path, file encoding, SECURELOAD/TRUSTEDPATHS behavior, and whether the script can be loaded manually.",
                    "log_path": path,
                }
            )
        if "acad2027.cfg" in tail_lower and ("只读" in tail or "锁定" in tail or "locked" in tail_lower):
            diagnostics.append(
                {
                    "rule_id": "acad_config_locked",
                    "severity": "error",
                    "evidence": "acad2027.cfg locked or read-only",
                    "message": "AutoCAD configuration appears locked or read-only.",
                    "suggestion": "Close AutoCAD and accoreconsole processes, then check acad2027.cfg permissions before retrying.",
                    "log_path": path,
                }
            )
        if "未找到文件" in tail or "not found" in tail_lower:
            diagnostics.append(
                {
                    "rule_id": "referenced_file_missing",
                    "severity": "error",
                    "evidence": "file not found",
                    "message": "The log reports a missing referenced file.",
                    "suggestion": "Check the script path and every file path passed to accoreconsole.",
                    "log_path": path,
                }
            )
    if not diagnostics and error_code:
        diagnostics.append(
            {
                "rule_id": "no_log_rule_match",
                "severity": "warning",
                "evidence": "",
                "message": "No known log keyword rule matched this error.",
                "suggestion": "Inspect the full log and task record before retrying.",
                "log_path": "",
            }
        )
    return diagnostics


def recent_failures(project_root: Path, limit: int = 10, scan_limit: int = 100) -> dict:
    records = list_task_records(project_root, limit=scan_limit)
    failures = []
    for record in records:
        if record.get("status") != "failed" and not record.get("error_code"):
            continue
        failures.append(
            {
                "task_id": record.get("task_id", ""),
                "status": record.get("status", ""),
                "error_code": record.get("error_code"),
                "track": record.get("track", ""),
                "risk": record.get("risk", ""),
                "user_goal": record.get("user_goal", ""),
                "script_path": record.get("script_path", ""),
                "files": record.get("files", []),
                "rollback_available": bool(record.get("rollback_available", False)),
                "started_at": record.get("started_at", ""),
                "finished_at": record.get("finished_at", ""),
                "ledger_path": record.get("ledger_path", ""),
            }
        )
        if len(failures) >= limit:
            break
    return {
        "ok": True,
        "failures": failures,
        "count": len(failures),
        "scanned": len(records),
    }


def error_detail(project_root: Path, task_id: str, log_tail_chars: int = 2000) -> dict:
    record = load_task_record(project_root, task_id)
    log_paths = _log_paths(project_root, task_id)
    log_tails = _log_tails(log_paths, log_tail_chars)
    rollback = None
    if record.get("rollback_available"):
        rollback = rollback_task(project_root, task_id, dry_run=True)
    return {
        "ok": True,
        "task": record,
        "error_code": record.get("error_code"),
        "error": explain_error_code(record.get("error_code")),
        "status": record.get("status"),
        "rollback_available": bool(record.get("rollback_available", False)),
        "rollback_dry_run": rollback,
        "log_paths": log_paths,
        "log_tails": log_tails,
        "diagnostics": _diagnose_log_tails(log_tails, record.get("error_code")),
    }
