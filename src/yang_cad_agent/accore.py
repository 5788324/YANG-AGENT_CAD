"""accoreconsole batch runner scaffold."""

from __future__ import annotations

import subprocess
from datetime import datetime
from pathlib import Path

from . import error_codes
from .doctor import _candidate_accore_paths
from .ledger import create_task_record, update_task_record
from .lisp_validator import validate_lisp_file


def find_accoreconsole() -> Path | None:
    for path in _candidate_accore_paths():
        if path.exists():
            return path
    return None


def collect_dwgs(folder: Path, pattern: str = "*.dwg", recursive: bool = False) -> list[Path]:
    if recursive:
        return sorted(path for path in folder.rglob(pattern) if path.is_file())
    return sorted(path for path in folder.glob(pattern) if path.is_file())


def run_accore_batch(
    project_root: Path,
    folder: Path,
    script_path: Path,
    task_id: str | None = None,
    pattern: str = "*.dwg",
    recursive: bool = False,
    dry_run: bool = True,
    timeout: int = 300,
) -> dict:
    """Run or dry-run a batch accoreconsole operation."""
    if not folder.exists():
        return {
            "ok": False,
            "error_code": error_codes.FILE_NOT_FOUND,
            "message": f"Folder not found: {folder}",
        }
    validation = validate_lisp_file(script_path, target_track="C")
    if not validation["ok"]:
        return {
            "ok": False,
            "error_code": validation["error_code"],
            "validation": validation,
        }

    dwgs = collect_dwgs(folder, pattern=pattern, recursive=recursive)
    if not task_id:
        task = create_task_record(
            project_root=project_root,
            user_goal=f"accoreconsole batch: {script_path}",
            risk="batch_modify",
            track="C",
        )
        task_id = task["task_id"]

    accore = find_accoreconsole()
    if not accore:
        return {
            "ok": False,
            "task_id": task_id,
            "error_code": error_codes.ACCORE_NOT_FOUND,
            "message": "accoreconsole.exe was not found.",
        }

    commands = [
        [str(accore), "/i", str(dwg), "/s", str(script_path), "/l", "zh-CN"]
        for dwg in dwgs
    ]
    update_task_record(
        project_root,
        task_id,
        status="dry_run" if dry_run else "running",
        files=[str(path) for path in dwgs],
        script_path=str(script_path),
        params={
            "folder": str(folder),
            "pattern": pattern,
            "recursive": recursive,
            "dry_run": dry_run,
        },
        started_at=datetime.now().isoformat(timespec="seconds"),
    )

    if dry_run:
        return {
            "ok": True,
            "task_id": task_id,
            "dry_run": True,
            "accoreconsole": str(accore),
            "file_count": len(dwgs),
            "files": [str(path) for path in dwgs],
            "commands": commands,
            "validation": validation,
        }

    log_dir = project_root / ".agent" / "logs" / task_id
    log_dir.mkdir(parents=True, exist_ok=True)
    results = []
    for command, dwg in zip(commands, dwgs):
        started = datetime.now()
        try:
            completed = subprocess.run(
                command,
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
                timeout=timeout,
                check=False,
            )
            elapsed = (datetime.now() - started).total_seconds()
            output = completed.stdout + completed.stderr
            log_path = log_dir / f"{dwg.stem}.log"
            log_path.write_text(output, encoding="utf-8")
            results.append(
                {
                    "file": str(dwg),
                    "success": completed.returncode == 0,
                    "returncode": completed.returncode,
                    "elapsed": elapsed,
                    "log_path": str(log_path),
                    "error_code": None
                    if completed.returncode == 0
                    else error_codes.ACCORE_NONZERO_EXIT,
                }
            )
        except subprocess.TimeoutExpired:
            results.append(
                {
                    "file": str(dwg),
                    "success": False,
                    "error_code": error_codes.ACCORE_TIMEOUT,
                    "elapsed": timeout,
                }
            )

    failed = [item for item in results if not item["success"]]
    update_task_record(
        project_root,
        task_id,
        status="failed" if failed else "done",
        finished_at=datetime.now().isoformat(timespec="seconds"),
        error_code=error_codes.ACCORE_NONZERO_EXIT if failed else None,
        rollback_available=False,
    )
    return {
        "ok": not failed,
        "task_id": task_id,
        "dry_run": False,
        "file_count": len(dwgs),
        "success": len(results) - len(failed),
        "failed": len(failed),
        "results": results,
    }
