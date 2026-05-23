"""File backup and rollback primitives."""

from __future__ import annotations

import hashlib
import json
import shutil
from datetime import datetime
from pathlib import Path

from . import error_codes


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _manifest_path(project_root: Path, task_id: str) -> Path:
    return project_root / ".agent" / "backups" / task_id / "manifest.json"


def backup_files(project_root: Path, task_id: str, file_paths: list[Path]) -> dict:
    """Copy files into a task backup directory and write a manifest."""
    backup_root = project_root / ".agent" / "backups" / task_id
    backup_root.mkdir(parents=True, exist_ok=True)
    entries = []
    errors = []

    for raw_path in file_paths:
        source = raw_path.resolve()
        if not source.exists():
            errors.append(
                {
                    "file": str(source),
                    "error_code": error_codes.FILE_NOT_FOUND,
                    "message": "File does not exist.",
                }
            )
            continue
        if not source.is_file():
            errors.append(
                {
                    "file": str(source),
                    "error_code": error_codes.VALIDATION_FAILED,
                    "message": "Only regular files can be backed up.",
                }
            )
            continue

        target = backup_root / source.drive.replace(":", "") / source.relative_to(source.anchor)
        target.parent.mkdir(parents=True, exist_ok=True)
        try:
            shutil.copy2(source, target)
            entries.append(
                {
                    "original": str(source),
                    "backup": str(target),
                    "sha256_before": sha256_file(source),
                    "sha256_backup": sha256_file(target),
                }
            )
        except Exception as exc:  # pragma: no cover - defensive filesystem guard
            errors.append(
                {
                    "file": str(source),
                    "error_code": error_codes.BACKUP_FAILED,
                    "message": str(exc),
                }
            )

    manifest = {
        "task_id": task_id,
        "created_at": datetime.now().isoformat(timespec="seconds"),
        "backup_dir": str(backup_root),
        "files": entries,
        "errors": errors,
    }
    _manifest_path(project_root, task_id).write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    return {
        "ok": not errors,
        "task_id": task_id,
        "backup_dir": str(backup_root),
        "manifest_path": str(_manifest_path(project_root, task_id)),
        "files_backed_up": len(entries),
        "errors": errors,
    }


def load_manifest(project_root: Path, task_id: str) -> dict:
    path = _manifest_path(project_root, task_id)
    if not path.exists():
        return {
            "ok": False,
            "error_code": error_codes.FILE_NOT_FOUND,
            "message": f"Backup manifest not found for task {task_id}.",
            "manifest_path": str(path),
        }
    return {"ok": True, "manifest": json.loads(path.read_text(encoding="utf-8"))}


def rollback_task(project_root: Path, task_id: str, dry_run: bool = True) -> dict:
    """Restore files from a task backup manifest."""
    loaded = load_manifest(project_root, task_id)
    if not loaded["ok"]:
        return loaded

    manifest = loaded["manifest"]
    actions = []
    errors = []
    rollback_safety_dir = (
        project_root / ".agent" / "backups" / f"{task_id}_pre_rollback_{datetime.now():%Y%m%d_%H%M%S}"
    )

    for entry in manifest.get("files", []):
        original = Path(entry["original"])
        backup = Path(entry["backup"])
        action = {
            "original": str(original),
            "backup": str(backup),
            "would_restore": True,
        }
        if not backup.exists():
            action["would_restore"] = False
            errors.append(
                {
                    "file": str(original),
                    "error_code": error_codes.ROLLBACK_FAILED,
                    "message": "Backup file is missing.",
                }
            )
        elif not dry_run:
            try:
                if original.exists():
                    safety_target = (
                        rollback_safety_dir
                        / original.drive.replace(":", "")
                        / original.relative_to(original.anchor)
                    )
                    safety_target.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(original, safety_target)
                    action["pre_rollback_copy"] = str(safety_target)
                original.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(backup, original)
                action["sha256_after"] = sha256_file(original)
            except Exception as exc:  # pragma: no cover - defensive filesystem guard
                action["would_restore"] = False
                errors.append(
                    {
                        "file": str(original),
                        "error_code": error_codes.ROLLBACK_FAILED,
                        "message": str(exc),
                    }
                )
        actions.append(action)

    return {
        "ok": not errors,
        "task_id": task_id,
        "dry_run": dry_run,
        "actions": actions,
        "errors": errors,
    }

