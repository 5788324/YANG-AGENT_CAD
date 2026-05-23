"""Dependency-free stdio tool scaffold.

This is intentionally tiny. It exposes the same core functions that the real MCP
server will wrap later, so Codex/Antigravity work can proceed without network
installs or an MCP SDK dependency.
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

from .doctor import run_doctor
from .ledger import list_task_records, load_task_record
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
}


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
    return {"ok": False, "error": f"Unknown tool: {name}"}


def handle_message(message: dict) -> dict:
    action = message.get("action")
    if action == "list_tools":
        return {"ok": True, "tools": TOOLS}
    if action == "call_tool":
        return {
            "ok": True,
            "result": call_tool(message.get("name", ""), message.get("params", {})),
        }
    return {"ok": False, "error": "Expected action=list_tools or action=call_tool"}


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

