"""Dependency-free stdio tool scaffold.

This is intentionally tiny. It exposes the same core functions that the real MCP
server will wrap later, so Codex/Antigravity work can proceed without network
installs or an MCP SDK dependency.
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

from .backup import rollback_task
from .doctor import run_doctor
from .health_check import run_health_check
from .ledger import list_task_records, load_task_record
from .mcp_manifest import build_manifest
from .report_summary import summarize_reports
from .task_query import error_detail, recent_failures
from .toolbox import list_plugins


TOOLS = {
    "doctor": {
        "description": "Check local CAD agent environment.",
        "params": {},
    },
    "toolbox_list": {
        "description": "List toolbox plugins.",
        "params": {"root": "Project root path."},
    },
    "task_list": {
        "description": "List recent task records.",
        "params": {"root": "Project root path.", "limit": "Maximum records."},
    },
    "task_show": {
        "description": "Show one task record.",
        "params": {"root": "Project root path.", "task_id": "Task id."},
    },
    "health_check": {
        "description": "Dry-run the one-command CAD health report workflow.",
        "params": {
            "root": "Project root path.",
            "folder": "Folder containing DWG files.",
            "pattern": "DWG glob pattern.",
            "recursive": "Search recursively.",
        },
    },
    "summarize_reports": {
        "description": "Summarize generated CAD CSV reports into Markdown.",
        "params": {
            "root": "Project root path.",
            "folder": "Folder containing generated report CSV files.",
            "output": "Optional output Markdown path.",
        },
    },
    "rollback_dry_run": {
        "description": "Preview rollback actions for a task without restoring files.",
        "params": {"root": "Project root path.", "task_id": "Task id."},
    },
    "task_recent_failures": {
        "description": "List recent failed task records and error codes.",
        "params": {
            "root": "Project root path.",
            "limit": "Maximum failures to return.",
            "scan_limit": "Maximum recent records to scan.",
        },
    },
    "task_error_detail": {
        "description": "Build a read-only diagnostic bundle for one task.",
        "params": {
            "root": "Project root path.",
            "task_id": "Task id.",
            "log_tail_chars": "Maximum characters to include from each log tail.",
        },
    },
}


def _as_bool(value: object) -> bool:
    if isinstance(value, bool):
        return value
    if isinstance(value, str):
        return value.strip().lower() in {"1", "true", "yes", "y"}
    return bool(value)


def call_tool(name: str, params: dict | None = None) -> dict:
    params = params or {}
    if name == "doctor":
        return run_doctor()
    if name == "toolbox_list":
        return list_plugins(Path(params.get("root", ".")))
    if name == "task_list":
        return {
            "ok": True,
            "tasks": list_task_records(
                Path(params.get("root", ".")),
                limit=int(params.get("limit", 20)),
            ),
        }
    if name == "task_show":
        return load_task_record(Path(params.get("root", ".")), params["task_id"])
    if name == "health_check":
        return run_health_check(
            project_root=Path(params.get("root", ".")),
            folder=Path(params["folder"]),
            pattern=params.get("pattern", "*.dwg"),
            recursive=_as_bool(params.get("recursive", False)),
            execute=False,
        )
    if name == "summarize_reports":
        root = Path(params.get("root", ".")).resolve()
        folder = Path(params["folder"])
        output_param = params.get("output", "")
        output = Path(output_param) if output_param else None
        if not folder.is_absolute():
            folder = root / folder
        if output is not None and not output.is_absolute():
            output = root / output
        return summarize_reports(folder, output=output)
    if name == "rollback_dry_run":
        return rollback_task(
            project_root=Path(params.get("root", ".")),
            task_id=params["task_id"],
            dry_run=True,
        )
    if name == "task_recent_failures":
        return recent_failures(
            project_root=Path(params.get("root", ".")),
            limit=int(params.get("limit", 10)),
            scan_limit=int(params.get("scan_limit", 100)),
        )
    if name == "task_error_detail":
        return error_detail(
            project_root=Path(params.get("root", ".")),
            task_id=params["task_id"],
            log_tail_chars=int(params.get("log_tail_chars", 2000)),
        )
    return {"ok": False, "error": f"Unknown tool: {name}"}


def handle_message(message: dict) -> dict:
    action = message.get("action")
    if action in {"server_info", "manifest"}:
        return build_manifest(TOOLS)
    if action == "list_tools":
        return {"ok": True, "tools": TOOLS}
    if action == "call_tool":
        return {
            "ok": True,
            "result": call_tool(message.get("name", ""), message.get("params", {})),
        }
    return {
        "ok": False,
        "error": "Expected action=server_info, action=manifest, action=list_tools, or action=call_tool",
    }


def main() -> int:
    for line in sys.stdin:
        if not line.strip():
            continue
        try:
            message = json.loads(line)
            response = handle_message(message)
        except Exception as exc:  # pragma: no cover - defensive server loop
            response = {"ok": False, "error": str(exc)}
        print(json.dumps(response, ensure_ascii=False), flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
