"""Command line entry point for YANG AGENT CAD."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from .acad_com_diagnose import diagnose_acad_com
from .acad_launch import launch_autocad
from .accore import run_accore_batch
from .batch_workflow import run_batch_workflow
from .backup import backup_files, rollback_task
from .current_lisp import feed_current_lisp
from .current_smoke import run_current_smoke
from .doctor import run_doctor
from .health_check import run_health_check
from .ledger import create_task_record, list_task_records, load_task_record
from .lisp_validator import validate_lisp_file
from .personal_health import run_personal_health
from .report_summary import summarize_reports
from .task_query import error_detail
from .toolbox import list_plugins, validate_plugin_manifest


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="yang-cad-agent",
        description="AutoCAD AI Agent helper CLI.",
    )
    sub = parser.add_subparsers(dest="command", required=True)

    doctor = sub.add_parser("doctor", help="Check local CAD agent environment.")
    doctor.add_argument("--json", action="store_true", help="Print JSON output.")

    task = sub.add_parser("new-task", help="Create a task ledger record.")
    task.add_argument("--goal", required=True, help="User goal for this task.")
    task.add_argument("--risk", default="read", help="Risk level from runtime contract.")
    task.add_argument("--track", default="MCP", help="Planned execution track.")
    task.add_argument("--root", default=".", help="Project root.")

    task_list = sub.add_parser("task-list", help="List recent task ledger records.")
    task_list.add_argument("--root", default=".", help="Project root.")
    task_list.add_argument("--limit", type=int, default=20, help="Maximum records to show.")
    task_list.add_argument("--json", action="store_true", help="Print JSON output.")

    task_show = sub.add_parser("task-show", help="Show one task ledger record.")
    task_show.add_argument("task_id", help="Task id.")
    task_show.add_argument("--root", default=".", help="Project root.")

    for detail_name in ("task-error-detail", "task_error_detail"):
        task_detail = sub.add_parser(detail_name, help="Show task error detail and diagnostics.")
        task_detail.add_argument("task_id", help="Task id.")
        task_detail.add_argument("--root", default=".", help="Project root.")
        task_detail.add_argument(
            "--log-tail-chars",
            type=int,
            default=2000,
            help="Maximum log tail characters per log file.",
        )

    backup = sub.add_parser("backup", help="Back up files into .agent/backups.")
    backup.add_argument("--task-id", required=True, help="Existing task id.")
    backup.add_argument("--root", default=".", help="Project root.")
    backup.add_argument("files", nargs="+", help="Files to back up.")

    rollback = sub.add_parser("rollback", help="Restore files from a task backup.")
    rollback.add_argument("task_id", help="Task id to roll back.")
    rollback.add_argument("--root", default=".", help="Project root.")
    rollback.add_argument("--dry-run", action="store_true", help="Only show actions.")

    validate = sub.add_parser("validate-lisp", help="Validate a LISP file before execution.")
    validate.add_argument("path", help="LISP file path.")
    validate.add_argument(
        "--track",
        default="B",
        choices=["B", "C"],
        help="Target track. C applies accoreconsole restrictions.",
    )

    current = sub.add_parser("current-lisp", help="Feed LISP to the current AutoCAD drawing.")
    current.add_argument("--script", required=True, help="LISP script path.")
    current.add_argument("--root", default=".", help="Project root.")
    current.add_argument("--execute", action="store_true", help="Actually send to AutoCAD COM.")

    current_smoke = sub.add_parser("current-smoke", help="Run guarded current drawing smoke test.")
    current_smoke.add_argument("--root", default=".", help="Project root.")
    current_smoke.add_argument("--execute", action="store_true", help="Actually send to AutoCAD COM after preflight.")

    acad_com = sub.add_parser("acad-com-diagnose", help="Read-only AutoCAD COM diagnostics.")
    acad_com.add_argument("--root", default=".", help="Project root.")

    acad_open = sub.add_parser("acad-open", help="Plan or launch AutoCAD with a test DWG copy.")
    acad_open.add_argument("--root", default=".", help="Project root.")
    acad_open.add_argument("--dwg", default="", help="Optional DWG path to open. Omit to use a sample test copy.")
    acad_open.add_argument("--no-sample-copy", action="store_true", help="Do not use sample DWG test copy when --dwg is omitted.")
    acad_open.add_argument("--execute", action="store_true", help="Actually launch AutoCAD. Omit for dry-run.")

    accore = sub.add_parser("accore-run", help="Run or dry-run accoreconsole batch.")
    accore.add_argument("--script", required=True, help="LISP script path.")
    accore.add_argument("--root", default=".", help="Project root.")
    accore.add_argument("--task-id", default="", help="Existing task id.")
    accore.add_argument("--pattern", default="*.dwg", help="DWG glob pattern.")
    accore.add_argument("--recursive", action="store_true", help="Search recursively.")
    accore.add_argument("--dry-run", action="store_true", help="Do not execute accoreconsole.")
    accore.add_argument("folder", help="Folder containing DWG files.")

    batch = sub.add_parser("batch-task", help="Safe batch workflow with validation and backup.")
    batch.add_argument("--script", required=True, help="LISP script path.")
    batch.add_argument("--root", default=".", help="Project root.")
    batch.add_argument("--pattern", default="*.dwg", help="DWG glob pattern.")
    batch.add_argument("--recursive", action="store_true", help="Search recursively.")
    batch.add_argument(
        "--execute",
        action="store_true",
        help="Actually run accoreconsole. Omit for dry-run.",
    )
    batch.add_argument("folder", help="Folder containing DWG files.")

    toolbox = sub.add_parser("toolbox-list", help="List toolbox plugins.")
    toolbox.add_argument("--root", default=".", help="Project root.")
    toolbox.add_argument("--json", action="store_true", help="Print JSON output.")

    plugin = sub.add_parser("toolbox-validate", help="Validate a plugin.json manifest.")
    plugin.add_argument("manifest", help="Path to plugin.json.")

    summary = sub.add_parser("summarize-reports", help="Summarize generated CAD CSV reports.")
    summary.add_argument("folder", help="Folder containing generated report CSV files.")
    summary.add_argument("--output", default="", help="Output Markdown path.")

    health = sub.add_parser("health-check", help="Run one-command CAD health report workflow.")
    health.add_argument("folder", help="Folder containing DWG files.")
    health.add_argument("--root", default=".", help="Project root.")
    health.add_argument("--pattern", default="*.dwg", help="DWG glob pattern.")
    health.add_argument("--recursive", action="store_true", help="Search recursively.")
    health.add_argument(
        "--execute",
        action="store_true",
        help="Actually run read-only report plugins. Omit for dry-run.",
    )

    personal = sub.add_parser("personal-health", help="Beginner-friendly one-command CAD health check.")
    personal.add_argument("folder", nargs="?", default="sample", help="Folder containing DWG files.")
    personal.add_argument("--root", default=".", help="Project root.")
    personal.add_argument("--pattern", default="*.dwg", help="DWG glob pattern.")
    personal.add_argument("--recursive", action="store_true", help="Search recursively.")
    personal.add_argument("--output", default="", help="Output Markdown report path.")
    personal.add_argument(
        "--execute",
        action="store_true",
        help="Actually run read-only report plugins. Omit for dry-run.",
    )

    return parser


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)

    if args.command == "doctor":
        result = run_doctor()
        if args.json:
            print(json.dumps(result, ensure_ascii=False, indent=2))
        else:
            print(f"Python: {result['python']['version']}")
            print(f"Git: {result['git']['status']}")
            print(f"accoreconsole: {result['accoreconsole']['status']}")
            print(f"AutoCAD paths checked: {len(result['accoreconsole']['checked_paths'])}")
        return 0 if result["ok"] else 1

    if args.command == "new-task":
        record = create_task_record(
            project_root=Path(args.root),
            user_goal=args.goal,
            risk=args.risk,
            track=args.track,
        )
        print(json.dumps(record, ensure_ascii=False, indent=2))
        return 0

    if args.command == "task-list":
        records = list_task_records(Path(args.root), limit=args.limit)
        if args.json:
            print(json.dumps({"ok": True, "tasks": records}, ensure_ascii=False, indent=2))
        else:
            for record in records:
                print(
                    f"{record['task_id']} [{record['status']}] "
                    f"{record['track']} {record['risk']} {record['user_goal']}"
                )
        return 0

    if args.command == "task-show":
        record = load_task_record(Path(args.root), args.task_id)
        print(json.dumps(record, ensure_ascii=False, indent=2))
        return 0

    if args.command in ("task-error-detail", "task_error_detail"):
        result = error_detail(Path(args.root), args.task_id, log_tail_chars=args.log_tail_chars)
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 0 if result["ok"] else 1

    if args.command == "backup":
        result = backup_files(
            project_root=Path(args.root),
            task_id=args.task_id,
            file_paths=[Path(file) for file in args.files],
        )
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 0 if result["ok"] else 1

    if args.command == "rollback":
        result = rollback_task(
            project_root=Path(args.root),
            task_id=args.task_id,
            dry_run=args.dry_run,
        )
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 0 if result["ok"] else 1

    if args.command == "validate-lisp":
        result = validate_lisp_file(Path(args.path), target_track=args.track)
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 0 if result["ok"] else 1

    if args.command == "current-lisp":
        result = feed_current_lisp(
            project_root=Path(args.root),
            script_path=Path(args.script),
            execute=args.execute,
        )
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 0 if result["ok"] else 1

    if args.command == "current-smoke":
        result = run_current_smoke(project_root=Path(args.root), execute=args.execute)
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 0 if result["ok"] else 1

    if args.command == "acad-com-diagnose":
        result = diagnose_acad_com(Path(args.root))
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 0 if result["ok"] else 1

    if args.command == "acad-open":
        result = launch_autocad(
            project_root=Path(args.root),
            execute=args.execute,
            dwg=Path(args.dwg) if args.dwg else None,
            use_sample_copy=not args.no_sample_copy,
        )
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 0 if result["ok"] else 1

    if args.command == "accore-run":
        result = run_accore_batch(
            project_root=Path(args.root),
            folder=Path(args.folder),
            script_path=Path(args.script),
            task_id=args.task_id or None,
            pattern=args.pattern,
            recursive=args.recursive,
            dry_run=args.dry_run,
        )
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 0 if result["ok"] else 1

    if args.command == "batch-task":
        result = run_batch_workflow(
            project_root=Path(args.root),
            folder=Path(args.folder),
            script_path=Path(args.script),
            pattern=args.pattern,
            recursive=args.recursive,
            execute=args.execute,
        )
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 0 if result["ok"] else 1

    if args.command == "toolbox-list":
        result = list_plugins(Path(args.root))
        if args.json:
            print(json.dumps(result, ensure_ascii=False, indent=2))
        else:
            for item in result["plugins"]:
                status = "ok" if item["ok"] else "invalid"
                print(f"{item.get('id', item['path'])} [{status}] {item.get('name', '')}")
        return 0 if result["ok"] else 1

    if args.command == "toolbox-validate":
        result = validate_plugin_manifest(Path(args.manifest))
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 0 if result["ok"] else 1

    if args.command == "summarize-reports":
        output = Path(args.output) if args.output else None
        result = summarize_reports(Path(args.folder), output=output)
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 0 if result["ok"] else 1

    if args.command == "health-check":
        result = run_health_check(
            project_root=Path(args.root),
            folder=Path(args.folder),
            pattern=args.pattern,
            recursive=args.recursive,
            execute=args.execute,
        )
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 0 if result["ok"] else 1

    if args.command == "personal-health":
        output = Path(args.output) if args.output else None
        result = run_personal_health(
            project_root=Path(args.root),
            folder=Path(args.folder),
            pattern=args.pattern,
            recursive=args.recursive,
            execute=args.execute,
            output=output,
        )
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 0 if result["ok"] else 1

    parser.error(f"Unknown command: {args.command}")
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
