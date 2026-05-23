"""Command line entry point for YANG AGENT CAD."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from .accore import run_accore_batch
from .batch_workflow import run_batch_workflow
from .backup import backup_files, rollback_task
from .doctor import run_doctor
from .ledger import create_task_record
from .lisp_validator import validate_lisp_file
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

    parser.error(f"Unknown command: {args.command}")
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
