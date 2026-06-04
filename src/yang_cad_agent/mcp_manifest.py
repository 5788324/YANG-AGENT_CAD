"""Machine-readable MCP stdio compatibility descriptor."""

from __future__ import annotations

from importlib.metadata import PackageNotFoundError, version
from typing import Mapping


SERVER_NAME = "yang-cad-agent"
TRANSPORT = "stdio-json-lines"


def package_version() -> str:
    try:
        return version("yang-cad-agent")
    except PackageNotFoundError:
        return "0.1.0"


def build_manifest(tools: Mapping[str, dict]) -> dict:
    """Describe the current dependency-free stdio server for AI hosts."""
    return {
        "ok": True,
        "name": SERVER_NAME,
        "version": package_version(),
        "transport": TRANSPORT,
        "protocol": {
            "status": "compatibility_descriptor",
            "message_framing": "one_json_object_per_line",
            "request_actions": ["server_info", "manifest", "list_tools", "call_tool"],
            "official_mcp_sdk": False,
        },
        "entrypoints": {
            "module": "python -m yang_cad_agent.mcp_stdio",
            "script": "yang-cad-mcp-stdio",
        },
        "tool_count": len(tools),
        "tools": dict(tools),
        "safety": {
            "no_general_shell": True,
            "mcp_read_only_by_default": True,
            "health_check_forces_dry_run": True,
            "rollback_is_dry_run_only": True,
            "task_error_detail_is_read_only": True,
            "acad_com_diagnose_is_read_only": True,
            "batch_execute_requires_cli_confirmation": True,
            "do_not_commit_dwg_or_agent_runtime_files": True,
        },
        "workflow_order": ["mcp_or_toolbox", "temporary_lisp", "accoreconsole"],
        "notes": [
            "This descriptor exists because the local environment has no bundled official MCP SDK dependency.",
            "Hosts such as Codex or Antigravity can use this to inspect tools and safety boundaries before calling stdio actions.",
            "Real batch execution is intentionally not exposed through this stdio layer.",
        ],
    }
