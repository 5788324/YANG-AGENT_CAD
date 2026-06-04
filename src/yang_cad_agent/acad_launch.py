"""Safe local AutoCAD launch helper."""

from __future__ import annotations

import shutil
import subprocess
from pathlib import Path


def candidate_acad_paths() -> list[Path]:
    base = Path("C:/Program Files/Autodesk")
    return [base / f"AutoCAD {year}" / "acad.exe" for year in range(2027, 2020, -1)]


def find_acad_exe() -> Path | None:
    for path in candidate_acad_paths():
        if path.exists():
            return path
    found = shutil.which("acad.exe")
    return None if found is None else Path(found)


def _first_sample_dwg(project_root: Path) -> Path | None:
    sample = project_root / "sample"
    preferred = sample / "S001.dwg"
    if preferred.exists():
        return preferred
    matches = sorted(sample.glob("*.dwg"))
    return matches[0] if matches else None


def _test_copy_path(project_root: Path, source: Path) -> Path:
    return project_root / ".agent" / "tmp" / "current-open" / f"{source.stem}-current-test{source.suffix}"


def prepare_launch_plan(project_root: Path, dwg: Path | None = None, use_sample_copy: bool = True) -> dict:
    project_root = project_root.resolve()
    acad_exe = find_acad_exe()
    source_dwg = dwg.resolve() if dwg is not None else None
    target_dwg = source_dwg
    copy_action = None

    if source_dwg is None and use_sample_copy:
        source_dwg = _first_sample_dwg(project_root)
        if source_dwg is not None:
            target_dwg = _test_copy_path(project_root, source_dwg)
            copy_action = {
                "source": str(source_dwg),
                "target": str(target_dwg),
                "reason": "Open a local test copy instead of the sample original.",
            }

    command = [] if acad_exe is None else [str(acad_exe)]
    if target_dwg is not None:
        command.append(str(target_dwg))

    return {
        "acad_exe": None if acad_exe is None else str(acad_exe),
        "checked_paths": [str(path) for path in candidate_acad_paths()],
        "source_dwg": None if source_dwg is None else str(source_dwg),
        "target_dwg": None if target_dwg is None else str(target_dwg),
        "copy_action": copy_action,
        "command": command,
    }


def launch_autocad(project_root: Path, execute: bool = False, dwg: Path | None = None, use_sample_copy: bool = True) -> dict:
    """Return a launch plan or start AutoCAD with an optional test DWG."""
    plan = prepare_launch_plan(project_root, dwg=dwg, use_sample_copy=use_sample_copy)
    if plan["acad_exe"] is None:
        return {
            "ok": False,
            "mode": "execute" if execute else "dry_run",
            "status": "failed",
            "error_code": "ACAD_EXE_NOT_FOUND",
            "plan": plan,
            "next_step": "Install AutoCAD or update the AutoCAD path detection list.",
        }

    if not execute:
        return {
            "ok": True,
            "mode": "dry_run",
            "status": "planned",
            "plan": plan,
            "next_step": "Review the plan, then run scripts\\open-autocad-test.cmd --execute if it is safe.",
        }

    copy_action = plan.get("copy_action")
    if copy_action:
        source = Path(copy_action["source"])
        target = Path(copy_action["target"])
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)

    try:
        process = subprocess.Popen(plan["command"], shell=False)
    except Exception as exc:
        return {
            "ok": False,
            "mode": "execute",
            "status": "failed",
            "error_code": "ACAD_LAUNCH_FAILED",
            "plan": plan,
            "error": str(exc),
            "next_step": "Open AutoCAD manually, then run scripts\\current-com-diagnose.cmd.",
        }

    return {
        "ok": True,
        "mode": "execute",
        "status": "launched",
        "pid": process.pid,
        "plan": plan,
        "next_step": "Wait until AutoCAD command line is ready, then run scripts\\current-com-diagnose.cmd.",
    }
