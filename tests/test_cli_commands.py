from __future__ import annotations

import contextlib
import io
import json
import tempfile
import unittest
from pathlib import Path

from yang_cad_agent.cli import main


class CliCommandTests(unittest.TestCase):
    def test_task_error_detail_aliases(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
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


if __name__ == "__main__":
    unittest.main()
