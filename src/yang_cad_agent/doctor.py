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


def _check_accoreconsole() -> dict:
    checked = _candidate_accore_paths()
    existing = [str(path) for path in checked if path.exists()]
    return {
        "status": "ok" if existing else "missing",
        "existing_paths": existing,
        "checked_paths": [str(path) for path in checked],
    }


def run_doctor() -> dict:
    """Return a machine-readable local environment report."""
    git = _check_git()
    accore = _check_accoreconsole()
    result = {
        "ok": git["status"] == "ok",
        "python": {
            "version": sys.version.split()[0],
            "executable": sys.executable,
            "platform": platform.platform(),
        },
        "git": git,
        "accoreconsole": accore,
        "notes": [],
    }
    if accore["status"] == "missing":
        result["notes"].append(
            "未找到 accoreconsole。若本机已安装 AutoCAD，请后续补充实际安装路径。"
        )
    return result

