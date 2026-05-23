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


def task_record_path(project_root: Path, task_id: str) -> Path:
    return project_root / ".agent" / "tasks" / f"{task_id}.json"


def load_task_record(project_root: Path, task_id: str) -> dict:
    path = task_record_path(project_root, task_id)
    record = json.loads(path.read_text(encoding="utf-8"))
    return {**record, "ledger_path": str(path)}


def update_task_record(project_root: Path, task_id: str, **updates) -> dict:
    path = task_record_path(project_root, task_id)
    record = json.loads(path.read_text(encoding="utf-8"))
    record.update(updates)
    path.write_text(json.dumps(record, ensure_ascii=False, indent=2), encoding="utf-8")
    return {**record, "ledger_path": str(path)}


def list_task_records(project_root: Path, limit: int = 20) -> list[dict]:
    task_dir = project_root / ".agent" / "tasks"
    if not task_dir.exists():
        return []
    records = []
    for path in sorted(task_dir.glob("*.json"), key=lambda item: item.stat().st_mtime, reverse=True):
        record = json.loads(path.read_text(encoding="utf-8"))
        records.append({**record, "ledger_path": str(path)})
        if len(records) >= limit:
            break
    return records
