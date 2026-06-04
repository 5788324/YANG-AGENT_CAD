"""Read-only AutoCAD COM diagnostics."""

from __future__ import annotations

import ctypes
import json
import subprocess
import sys
from pathlib import Path

from .current_lisp import AUTOCAD_PROG_IDS, _acad_process_state


def _is_current_process_elevated() -> bool | None:
    try:
        return bool(ctypes.windll.shell32.IsUserAnAdmin())
    except Exception:
        return None


def _check_pywin32() -> dict:
    try:
        import win32com.client  # type: ignore
    except ImportError:
        return {
            "available": False,
            "error": "pywin32 is not installed in this Python environment.",
        }
    return {
        "available": True,
        "module": getattr(win32com.client, "__name__", "win32com.client"),
    }


def _check_registered_prog_ids() -> list[dict]:
    try:
        import winreg
    except Exception as exc:  # pragma: no cover - Windows only fallback
        return [{"ok": False, "error": str(exc), "prog_id": ""}]

    checks = []
    for prog_id in AUTOCAD_PROG_IDS:
        check = {"prog_id": prog_id, "registered": False, "clsid": "", "error": ""}
        try:
            with winreg.OpenKey(winreg.HKEY_CLASSES_ROOT, prog_id + "\\CLSID") as key:
                check["clsid"] = winreg.QueryValueEx(key, "")[0]
                check["registered"] = True
        except FileNotFoundError:
            check["error"] = "ProgID is not registered."
        except OSError as exc:
            check["error"] = str(exc)
        checks.append(check)
    return checks


def _collect_rot_entries() -> dict:
    try:
        import pythoncom  # type: ignore
    except ImportError:
        return {"available": False, "error": "pythoncom is not installed.", "entries": []}

    entries: list[str] = []
    try:
        pythoncom.CoInitialize()
        rot = pythoncom.GetRunningObjectTable()
        enum = rot.EnumRunning()
        bind_ctx = pythoncom.CreateBindCtx(0)
        while True:
            monikers = enum.Next(1)
            if not monikers:
                break
            try:
                name = monikers[0].GetDisplayName(bind_ctx, None)
            except Exception as exc:  # pragma: no cover - depends on COM server
                name = f"<display-name-error: {exc}>"
            entries.append(name)
    except Exception as exc:  # pragma: no cover - depends on local COM state
        return {"available": True, "error": str(exc), "entries": entries}
    filtered = [
        entry for entry in entries
        if "auto" in entry.lower() or "acad" in entry.lower() or "dwg" in entry.lower()
    ]
    return {
        "available": True,
        "error": "",
        "entry_count": len(entries),
        "filtered_entries": filtered[:50],
        "entries_truncated": len(filtered) > 50,
    }


def _acad_process_details() -> dict:
    command = (
        "Get-Process acad -ErrorAction SilentlyContinue | "
        "Select-Object ProcessName,Id,Path,MainWindowTitle,StartTime | ConvertTo-Json -Compress"
    )
    try:
        completed = subprocess.run(
            ["powershell", "-NoProfile", "-Command", command],
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=10,
            check=False,
        )
    except Exception as exc:  # pragma: no cover - defensive guard
        return {"ok": False, "error": str(exc), "processes": []}
    output = (completed.stdout or "").strip()
    if not output:
        return {"ok": True, "processes": []}
    try:
        parsed = json.loads(output)
    except json.JSONDecodeError:
        return {"ok": False, "error": "Could not parse PowerShell process output.", "raw": output}
    return {"ok": True, "processes": parsed if isinstance(parsed, list) else [parsed]}


def diagnose_acad_com(project_root: Path | None = None) -> dict:
    """Inspect local AutoCAD COM reachability without sending commands."""
    pywin32 = _check_pywin32()
    process = _acad_process_state()
    process_details = _acad_process_details()
    registered_prog_ids = _check_registered_prog_ids()
    result = {
        "ok": True,
        "mode": "read_only",
        "project_root": str(Path(".").resolve() if project_root is None else project_root.resolve()),
        "python": {
            "executable": sys.executable,
            "version": sys.version.split()[0],
            "elevated": _is_current_process_elevated(),
        },
        "pywin32": pywin32,
        "acad_process": process,
        "acad_process_details": process_details,
        "registered_prog_ids": registered_prog_ids,
        "rot": {"available": False, "entries": []},
        "prog_id_checks": [],
        "attachable": False,
        "active_document": None,
        "diagnostics": [],
    }
    if not pywin32["available"]:
        result["diagnostics"].append(
            {
                "rule_id": "pywin32_missing",
                "severity": "error",
                "message": "Python cannot import win32com.client.",
                "suggestion": "Install pywin32 in the Python used by the scripts, then retry.",
            }
        )
        return result

    import win32com.client  # type: ignore
    try:
        import pythoncom  # type: ignore
        pythoncom.CoInitialize()
    except Exception:
        pass

    result["rot"] = _collect_rot_entries()

    for prog_id in AUTOCAD_PROG_IDS:
        check = {"prog_id": prog_id, "ok": False, "error": ""}
        try:
            acad = win32com.client.GetActiveObject(prog_id)
            doc = getattr(acad, "ActiveDocument", None)
            check["ok"] = True
            check["active_document"] = "" if doc is None else getattr(doc, "Name", "")
            result["attachable"] = True
            result["active_document"] = check["active_document"]
        except Exception as exc:  # pragma: no cover - depends on local AutoCAD COM
            check["error"] = str(exc)
        result["prog_id_checks"].append(check)
        if check["ok"]:
            break

    if result["attachable"]:
        result["diagnostics"].append(
            {
                "rule_id": "acad_com_attachable",
                "severity": "info",
                "message": "Python can attach to AutoCAD COM.",
                "suggestion": "It is reasonable to retry scripts\\current-smoke-test.cmd --execute on a test DWG.",
            }
        )
    elif process.get("running") is True:
        rot = result.get("rot", {})
        filtered_entries = rot.get("filtered_entries", []) if isinstance(rot, dict) else []
        if rot.get("available") and not filtered_entries:
            result["diagnostics"].append(
                {
                    "rule_id": "acad_not_in_running_object_table",
                    "severity": "error",
                    "message": "acad.exe is running, but no AutoCAD-like entry was found in the COM Running Object Table.",
                    "suggestion": "Check whether AutoCAD is still on a license/startup dialog. If it is ready, try repairing AutoCAD COM registration.",
                }
            )
        result["diagnostics"].append(
            {
                "rule_id": "acad_process_without_com",
                "severity": "error",
                "message": "acad.exe is running, but AutoCAD COM is not attachable.",
                "suggestion": (
                    "Close AutoCAD, reopen it normally instead of as administrator, open a test DWG, "
                    "wait until the command line is ready, then retry."
                ),
            }
        )
    else:
        result["diagnostics"].append(
            {
                "rule_id": "acad_process_missing",
                "severity": "warning",
                "message": "No running acad.exe process was detected.",
                "suggestion": "Open AutoCAD 2027 and a test DWG before running current-smoke-test --execute.",
            }
        )
    return result
