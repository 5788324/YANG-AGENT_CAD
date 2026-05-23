"""Command line entry point for YANG AGENT CAD."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from .doctor import run_doctor
from .ledger import create_task_record


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

    parser.error(f"Unknown command: {args.command}")
    return 2


if __name__ == "__main__":
    raise SystemExit(main())

