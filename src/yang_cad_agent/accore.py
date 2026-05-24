"""accoreconsole batch runner scaffold."""

from __future__ import annotations

import subprocess
from datetime import datetime
from pathlib import Path

from . import error_codes
from .doctor import _candidate_accore_paths, _check_accore_install, accore_install_issue
from .ledger import create_task_record, update_task_record
from .lisp_validator import validate_lisp_file


def find_accoreconsole() -> Path | None:
    for path in _candidate_accore_paths():
        if path.exists():
            return path
    return None


def check_accore_ready(accore_path: Path) -> dict:
    detail = _check_accore_install(accore_path)
    issue = accore_install_issue(detail)
    return {
        "ok": issue is None,
        "detail": detail,
        "error_code": None if issue is None else issue["error_code"],
        "message": "" if issue is None else issue["message"],
    }


def collect_dwgs(folder: Path, pattern: str = "*.dwg", recursive: bool = False) -> list[Path]:
    if recursive:
        return sorted(path for path in folder.rglob(pattern) if path.is_file())
    return sorted(path for path in folder.glob(pattern) if path.is_file())


def decode_process_output(stdout: bytes, stderr: bytes) -> str:
    data = stdout + stderr
    if not data:
        return ""
    null_ratio = data.count(b"\x00") / max(len(data), 1)
    encodings = ["utf-16-le", "utf-8", "mbcs"] if null_ratio > 0.05 else ["utf-8", "utf-16-le", "mbcs"]
    for encoding in encodings:
        try:
            return data.decode(encoding, errors="replace")
        except LookupError:
            continue
    return data.decode("utf-8", errors="replace")


def analyze_accore_output(output: str, returncode: int) -> dict:
    normalized = output.lower()
    load_failed = False
    for line in output.splitlines():
        line_lower = line.lower()
        if "acad2027" in line_lower:
            continue
        if (
            "load failed" in line_lower
            or ("load" in line_lower and "failed" in line_lower)
            or "LOAD 失败" in line
            or "load 失败" in line
            or "文件加载已取消" in line
            or "load canceled" in line_lower
            or "未找到文件" in line
            or "not found" in line_lower
        ):
            load_failed = True
            break
    if load_failed:
        return {
            "error_code": error_codes.LISP_LOAD_FAILED,
            "message": "accoreconsole did not load the LISP/script file successfully.",
        }
    if "acad2027.cfg" in normalized and ("只读" in output or "锁定" in output or "locked" in normalized):
        return {
            "error_code": error_codes.ACCORE_CONFIG_LOCKED,
            "message": (
                "AutoCAD configuration file is locked or read-only. "
                "Check acad2027.cfg permissions or close other AutoCAD processes."
            ),
        }
    if "无法处理配置文件" in output or "configuration" in normalized and "fatal" in normalized:
        return {
            "error_code": error_codes.ACCORE_CONFIG_LOCKED,
            "message": "AutoCAD could not process its configuration file.",
        }
    if returncode != 0:
        return {
            "error_code": error_codes.ACCORE_NONZERO_EXIT,
            "message": f"accoreconsole exited with code {returncode}.",
        }
    return {"error_code": None, "message": ""}


def write_accore_script_wrapper(project_root: Path, task_id: str, script_path: Path) -> Path:
    script_dir = project_root / ".agent" / "scripts" / task_id
    script_dir.mkdir(parents=True, exist_ok=True)
    wrapper_path = script_dir / f"{script_path.stem}.scr"
    lisp_path = script_path.resolve().as_posix()
    wrapper_path.write_text(
        f'(setvar "SECURELOAD" 0)\n(load "{lisp_path}")\n',
        encoding="utf-8",
    )
    return wrapper_path


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
    project_root = project_root.resolve()
    script_for_run = script_path if script_path.is_absolute() else project_root / script_path
    script_for_run = script_for_run.resolve()
    validation = validate_lisp_file(script_for_run, target_track="C")
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
    if not dry_run:
        preflight = check_accore_ready(accore)
        if not preflight["ok"]:
            update_task_record(
                project_root,
                task_id,
                status="failed",
                error_code=preflight["error_code"],
                finished_at=datetime.now().isoformat(timespec="seconds"),
            )
            return {
                "ok": False,
                "task_id": task_id,
                "error_code": preflight["error_code"],
                "message": preflight["message"],
                "preflight": preflight,
            }

    accore_script = (
        project_root / ".agent" / "scripts" / task_id / f"{script_for_run.stem}.scr"
        if dry_run
        else write_accore_script_wrapper(project_root, task_id, script_for_run)
    )
    commands = [
        [str(accore), "/i", str(dwg.resolve()), "/s", str(accore_script), "/l", "zh-CN"]
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
            "accore_script": str(accore_script),
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
            "accore_script": str(accore_script),
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
                timeout=timeout,
                check=False,
            )
            elapsed = (datetime.now() - started).total_seconds()
            output = decode_process_output(completed.stdout, completed.stderr)
            log_path = log_dir / f"{dwg.stem}.log"
            log_path.write_text(output, encoding="utf-8")
            analysis = analyze_accore_output(output, completed.returncode)
            success = completed.returncode == 0 and analysis["error_code"] is None
            results.append(
                {
                    "file": str(dwg),
                    "success": success,
                    "returncode": completed.returncode,
                    "elapsed": elapsed,
                    "log_path": str(log_path),
                    "error_code": analysis["error_code"],
                    "message": analysis["message"],
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
    first_error = failed[0].get("error_code") if failed else None
    update_task_record(
        project_root,
        task_id,
        status="failed" if failed else "done",
        finished_at=datetime.now().isoformat(timespec="seconds"),
        error_code=first_error,
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
