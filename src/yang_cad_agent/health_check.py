"""One-command CAD health check workflow."""

from __future__ import annotations

from pathlib import Path

from . import error_codes
from .batch_workflow import run_batch_workflow
from .report_summary import summarize_reports


REPORT_PLUGINS = [
    {
        "id": "batch.layer_report",
        "script": Path("toolbox/plugins/batch_layer_report/main.lsp"),
    },
    {
        "id": "batch.block_report",
        "script": Path("toolbox/plugins/batch_block_report/main.lsp"),
    },
    {
        "id": "batch.annotation_report",
        "script": Path("toolbox/plugins/batch_annotation_report/main.lsp"),
    },
]


def health_plugin_scripts(project_root: Path) -> list[dict]:
    return [
        {
            "id": item["id"],
            "script": str((project_root / item["script"]).resolve()),
            "exists": (project_root / item["script"]).exists(),
        }
        for item in REPORT_PLUGINS
    ]


def run_health_check(
    project_root: Path,
    folder: Path,
    execute: bool = False,
    pattern: str = "*.dwg",
    recursive: bool = False,
) -> dict:
    project_root = project_root.resolve()
    folder = folder if folder.is_absolute() else project_root / folder
    folder = folder.resolve()
    scripts = health_plugin_scripts(project_root)
    missing = [item for item in scripts if not item["exists"]]
    if missing:
        return {
            "ok": False,
            "error_code": error_codes.FILE_NOT_FOUND,
            "message": "One or more health-check plugins are missing.",
            "missing": missing,
        }

    steps = []
    for item in REPORT_PLUGINS:
        result = run_batch_workflow(
            project_root=project_root,
            folder=folder,
            script_path=project_root / item["script"],
            pattern=pattern,
            recursive=recursive,
            execute=execute,
        )
        steps.append({"plugin": item["id"], "result": result})
        if not result["ok"]:
            return {
                "ok": False,
                "mode": "execute" if execute else "dry_run",
                "error_code": result.get("error_code"),
                "failed_plugin": item["id"],
                "steps": steps,
            }

    summary = None
    if execute:
        summary = summarize_reports(folder)
        if not summary["ok"]:
            return {
                "ok": False,
                "mode": "execute",
                "error_code": summary.get("error_code"),
                "message": "Health-check plugins ran, but summary generation failed.",
                "steps": steps,
                "summary": summary,
            }

    return {
        "ok": True,
        "mode": "execute" if execute else "dry_run",
        "requires_confirmation": not execute,
        "folder": str(folder),
        "pattern": pattern,
        "recursive": recursive,
        "steps": steps,
        "summary": summary,
    }
