"""Task ledger primitives."""

from __future__ import annotations

import json
from datetime import datetime
from pathlib import Path
from uuid import uuid4


def new_task_id(now: datetime | None = None) -> str:
    stamp = (now or datetime.now()).strftime("%Y%m%d-%H%M%S")
    return f"{stamp}-{uuid4().hex[:8]}"


def create_task_record(
    project_root: Path,
    user_goal: str,
    risk: str,
    track: str,
) -> dict:
    task_id = new_task_id()
    task_dir = project_root / ".agent" / "tasks"
    task_dir.mkdir(parents=True, exist_ok=True)
    record = {
        "task_id": task_id,
        "status": "pending",
        "risk": risk,
        "track": track,
        "user_goal": user_goal,
        "files": [],
        "script_path": "",
        "params": {},
        "backup_dir": "",
        "started_at": "",
        "finished_at": "",
        "error_code": None,
        "rollback_available": False,
    }
    path = task_dir / f"{task_id}.json"
    path.write_text(json.dumps(record, ensure_ascii=False, indent=2), encoding="utf-8")
    return {**record, "ledger_path": str(path)}

