"""Read-only task ledger query helpers."""

from __future__ import annotations

from pathlib import Path

from .ledger import list_task_records


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
