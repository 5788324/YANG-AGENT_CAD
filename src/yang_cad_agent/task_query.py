"""Read-only task ledger query helpers."""

from __future__ import annotations

from pathlib import Path

from .backup import rollback_task
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
    rollback = None
    if record.get("rollback_available"):
        rollback = rollback_task(project_root, task_id, dry_run=True)
    return {
        "ok": True,
        "task": record,
        "error_code": record.get("error_code"),
        "status": record.get("status"),
        "rollback_available": bool(record.get("rollback_available", False)),
        "rollback_dry_run": rollback,
        "log_paths": log_paths,
        "log_tails": _log_tails(log_paths, log_tail_chars),
    }
