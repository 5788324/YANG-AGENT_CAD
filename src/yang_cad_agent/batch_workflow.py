"""Safe high-level batch workflow."""

from __future__ import annotations

from pathlib import Path

from . import error_codes
from .accore import check_accore_ready, collect_dwgs, find_accoreconsole, run_accore_batch
from .backup import backup_files
from .ledger import create_task_record, update_task_record
from .lisp_validator import validate_lisp_file


def run_batch_workflow(
    project_root: Path,
    folder: Path,
    script_path: Path,
    pattern: str = "*.dwg",
    recursive: bool = False,
    execute: bool = False,
) -> dict:
    """Validate, scan, optionally back up, then dry-run or execute accoreconsole."""
    task = create_task_record(
        project_root=project_root,
        user_goal=f"safe batch task: {script_path}",
        risk="batch_modify",
        track="C",
    )
    task_id = task["task_id"]

    validation = validate_lisp_file(script_path, target_track="C")
    if not validation["ok"]:
        update_task_record(
            project_root,
            task_id,
            status="failed",
            script_path=str(script_path),
            error_code=validation["error_code"],
        )
        return {
            "ok": False,
            "task_id": task_id,
            "error_code": validation["error_code"],
            "validation": validation,
        }

    if not folder.exists():
        update_task_record(
            project_root,
            task_id,
            status="failed",
            error_code=error_codes.FILE_NOT_FOUND,
        )
        return {
            "ok": False,
            "task_id": task_id,
            "error_code": error_codes.FILE_NOT_FOUND,
            "message": f"Folder not found: {folder}",
        }

    dwgs = collect_dwgs(folder, pattern=pattern, recursive=recursive)
    if not dwgs:
        update_task_record(
            project_root,
            task_id,
            status="failed",
            script_path=str(script_path),
            params={
                "folder": str(folder),
                "pattern": pattern,
                "recursive": recursive,
                "execute": execute,
            },
            error_code=error_codes.FILE_NOT_FOUND,
        )
        return {
            "ok": False,
            "task_id": task_id,
            "error_code": error_codes.FILE_NOT_FOUND,
            "message": f"No DWG files matched {pattern} in {folder}.",
            "validation": validation,
        }

    update_task_record(
        project_root,
        task_id,
        status="planned",
        files=[str(path) for path in dwgs],
        script_path=str(script_path),
        params={
            "folder": str(folder),
            "pattern": pattern,
            "recursive": recursive,
            "execute": execute,
        },
    )

    if not execute:
        accore = run_accore_batch(
            project_root=project_root,
            folder=folder,
            script_path=script_path,
            task_id=task_id,
            pattern=pattern,
            recursive=recursive,
            dry_run=True,
        )
        update_task_record(
            project_root,
            task_id,
            status="dry_run",
            rollback_available=False,
        )
        return {
            "ok": accore["ok"],
            "task_id": task_id,
            "mode": "dry_run",
            "requires_confirmation": True,
            "next_step": "Review this result, then re-run with --execute to modify files.",
            "file_count": len(dwgs),
            "validation": validation,
            "accore": accore,
        }

    accore = find_accoreconsole()
    if not accore:
        update_task_record(
            project_root,
            task_id,
            status="failed",
            error_code=error_codes.ACCORE_NOT_FOUND,
        )
        return {
            "ok": False,
            "task_id": task_id,
            "error_code": error_codes.ACCORE_NOT_FOUND,
            "message": "accoreconsole.exe was not found.",
        }
    preflight = check_accore_ready(accore)
    if not preflight["ok"]:
        update_task_record(
            project_root,
            task_id,
            status="failed",
            error_code=preflight["error_code"],
        )
        return {
            "ok": False,
            "task_id": task_id,
            "error_code": preflight["error_code"],
            "message": preflight["message"],
            "preflight": preflight,
        }

    backup = backup_files(project_root=project_root, task_id=task_id, file_paths=dwgs)
    update_task_record(
        project_root,
        task_id,
        backup_dir=backup["backup_dir"],
        rollback_available=backup["ok"],
    )
    if not backup["ok"]:
        update_task_record(
            project_root,
            task_id,
            status="failed",
            error_code=error_codes.BACKUP_FAILED,
        )
        return {
            "ok": False,
            "task_id": task_id,
            "error_code": error_codes.BACKUP_FAILED,
            "backup": backup,
        }

    accore = run_accore_batch(
        project_root=project_root,
        folder=folder,
        script_path=script_path,
        task_id=task_id,
        pattern=pattern,
        recursive=recursive,
        dry_run=False,
    )
    update_task_record(
        project_root,
        task_id,
        rollback_available=True,
        backup_dir=backup["backup_dir"],
    )
    return {
        "ok": accore["ok"],
        "task_id": task_id,
        "mode": "execute",
        "file_count": len(dwgs),
        "validation": validation,
        "backup": backup,
        "accore": accore,
        "rollback_command": f"yang-cad-agent rollback {task_id}",
    }
