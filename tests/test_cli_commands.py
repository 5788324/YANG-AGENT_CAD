from __future__ import annotations

import contextlib
import io
import json
import unittest
import shutil
from contextlib import contextmanager
from pathlib import Path
from uuid import uuid4
from unittest.mock import patch

from yang_cad_agent.cli import main


@contextmanager
def _temp_dir():
    root = Path(__file__).resolve().parents[1] / "tmp-tests"
    root.mkdir(parents=True, exist_ok=True)
    path = root / f"cli-{uuid4().hex}"
    path.mkdir(parents=True, exist_ok=True)
    try:
        yield str(path)
    finally:
        shutil.rmtree(path, ignore_errors=True)


class CliCommandTests(unittest.TestCase):
    def test_task_error_detail_aliases(self) -> None:
        with _temp_dir() as temp_dir:
            root = Path(temp_dir)
            task_dir = root / ".agent" / "tasks"
            task_dir.mkdir(parents=True)
            task = {
                "task_id": "fail-1",
                "status": "failed",
                "error_code": "ACAD_COM_UNAVAILABLE",
                "rollback_available": False,
            }
            (task_dir / "fail-1.json").write_text(json.dumps(task), encoding="utf-8")

            for command in ("task-error-detail", "task_error_detail"):
                output = io.StringIO()
                with contextlib.redirect_stdout(output):
                    exit_code = main([command, "fail-1", "--root", str(root)])

                result = json.loads(output.getvalue())
                self.assertEqual(exit_code, 0)
                self.assertEqual(result["task"]["task_id"], "fail-1")
                self.assertEqual(result["error"]["code"], "ACAD_COM_UNAVAILABLE")

    def test_acad_com_diagnose_command(self) -> None:
        output = io.StringIO()
        with patch("yang_cad_agent.cli.diagnose_acad_com") as diagnose:
            diagnose.return_value = {"ok": True, "mode": "read_only"}
            with contextlib.redirect_stdout(output):
                exit_code = main(["acad-com-diagnose", "--root", "."])

        result = json.loads(output.getvalue())
        self.assertEqual(exit_code, 0)
        self.assertEqual(result["mode"], "read_only")
        self.assertEqual(diagnose.call_count, 1)

    def test_current_smoke_command(self) -> None:
        output = io.StringIO()
        with patch("yang_cad_agent.cli.run_current_smoke") as current_smoke:
            current_smoke.return_value = {"ok": True, "mode": "dry_run"}
            with contextlib.redirect_stdout(output):
                exit_code = main(["current-smoke", "--root", "."])

        result = json.loads(output.getvalue())
        self.assertEqual(exit_code, 0)
        self.assertEqual(result["mode"], "dry_run")
        self.assertFalse(current_smoke.call_args.kwargs["execute"])


if __name__ == "__main__":
    unittest.main()
