"""Local environment checks."""

from __future__ import annotations

import platform
import shutil
import subprocess
import sys
from pathlib import Path


def _candidate_accore_paths() -> list[Path]:
    base = Path("C:/Program Files/Autodesk")
    return [
        base / f"AutoCAD {year}" / "accoreconsole.exe"
        for year in range(2027, 2020, -1)
    ]


def _check_git() -> dict:
    git = shutil.which("git")
    if not git:
        return {"status": "missing", "path": None}
    try:
        completed = subprocess.run(
            [git, "--version"],
            capture_output=True,
            text=True,
            timeout=10,
            check=False,
        )
    except Exception as exc:  # pragma: no cover - defensive guard
        return {"status": "error", "path": git, "error": str(exc)}
    return {
        "status": "ok" if completed.returncode == 0 else "error",
        "path": git,
        "version": completed.stdout.strip(),
    }


def _safe_exists(path: Path) -> tuple[bool, str | None]:
    try:
        return path.exists(), None
    except OSError as exc:
        return False, str(exc)


def _check_accoreconsole() -> dict:
    checked = _candidate_accore_paths()
    existing = [str(path) for path in checked if path.exists()]
    details = [_check_accore_install(path) for path in checked if path.exists()]
    return {
        "status": "ok" if existing else "missing",
        "existing_paths": existing,
        "checked_paths": [str(path) for path in checked],
        "install_details": details,
    }


def _check_accore_install(accore_path: Path) -> dict:
    install_dir = accore_path.parent
    year = "".join(ch for ch in install_dir.name if ch.isdigit()) or ""
    cfg_path = install_dir / f"acad{year}.cfg" if year else install_dir / "acad.cfg"
    user_cfg_path = (
        Path.home()
        / "AppData"
        / "Local"
        / "Autodesk"
        / f"AutoCAD {year}"
        / "R26.0"
        / "chs"
        / f"acad{year}.cfg"
    ) if year else None
    user_cfg_exists = False
    user_cfg_error = None
    if user_cfg_path is not None:
        user_cfg_exists, user_cfg_error = _safe_exists(user_cfg_path)
    can_write_probe = False
    probe_path = install_dir / ".yang_agent_write_test.tmp"
    try:
        probe_path.write_text("test", encoding="utf-8")
        probe_path.unlink(missing_ok=True)
        can_write_probe = True
    except Exception:
        can_write_probe = False

    return {
        "accoreconsole": str(accore_path),
        "install_dir": str(install_dir),
        "expected_cfg": str(cfg_path),
        "cfg_exists": cfg_path.exists(),
        "cfg_read_only": cfg_path.exists() and not bool(cfg_path.stat().st_mode & 0o200),
        "install_dir_writable": can_write_probe,
        "user_cfg": None if user_cfg_path is None else str(user_cfg_path),
        "user_cfg_exists": user_cfg_exists,
        "user_cfg_check_error": user_cfg_error,
    }


def accore_install_issue(detail: dict) -> dict | None:
    if not detail["cfg_exists"] and not detail["install_dir_writable"]:
        if detail.get("user_cfg_exists"):
            return {
                "error_code": "ACCORE_CONFIG_LOCKED",
                "message": (
                    "AutoCAD install config is missing and install dir is not writable. "
                    f"User config exists and can be copied with admin rights: {detail['user_cfg']} -> "
                    f"{detail['expected_cfg']}"
                ),
            }
        if detail.get("user_cfg_check_error"):
            return {
                "error_code": "ACCORE_CONFIG_LOCKED",
                "message": (
                    "AutoCAD install config is missing and install dir is not writable. "
                    f"User config path could not be checked without higher permissions: {detail['user_cfg']}. "
                    "Run scripts\\fix-acad-cfg.cmd as Administrator if that file exists."
                ),
            }
        return {
            "error_code": "ACCORE_CONFIG_LOCKED",
            "message": f"AutoCAD config is missing and install dir is not writable: {detail['expected_cfg']}",
        }
    if detail["cfg_read_only"]:
        return {
            "error_code": "ACCORE_CONFIG_LOCKED",
            "message": f"AutoCAD config is read-only: {detail['expected_cfg']}",
        }
    return None


def _check_running_autocad_processes() -> dict:
    try:
        completed = subprocess.run(
            [
                "powershell",
                "-NoProfile",
                "-Command",
                "Get-Process | Where-Object { $_.ProcessName -match 'acad|accoreconsole' } | "
                "Select-Object ProcessName,Id,Path | ConvertTo-Json -Compress",
            ],
            capture_output=True,
            text=True,
            timeout=10,
            check=False,
        )
    except Exception as exc:  # pragma: no cover - defensive platform guard
        return {"status": "error", "error": str(exc), "processes": []}

    output = completed.stdout.strip()
    if not output:
        return {"status": "ok", "processes": []}
    return {
        "status": "running",
        "raw": output,
        "processes": output,
    }


def run_doctor() -> dict:
    """Return a machine-readable local environment report."""
    git = _check_git()
    accore = _check_accoreconsole()
    processes = _check_running_autocad_processes()
    result = {
        "ok": git["status"] == "ok",
        "python": {
            "version": sys.version.split()[0],
            "executable": sys.executable,
            "platform": platform.platform(),
        },
        "git": git,
        "accoreconsole": accore,
        "autocad_processes": processes,
        "notes": [],
    }
    if accore["status"] == "missing":
        result["notes"].append(
            "未找到 accoreconsole。若本机已安装 AutoCAD，请后续补充实际安装路径。"
        )
    for detail in accore.get("install_details", []):
        issue = accore_install_issue(detail)
        if issue:
            result["notes"].append(issue["message"])
    if processes["status"] == "running":
        result["notes"].append("检测到 AutoCAD/accoreconsole 进程正在运行，批量执行前建议关闭残留进程。")
    return result
