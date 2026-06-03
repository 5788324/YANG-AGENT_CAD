from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

from yang_cad_agent.task_query import error_detail, recent_failures


class TaskQueryTests(unittest.TestCase):
    def test_recent_failures_returns_error_records(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            task_dir = root / ".agent" / "tasks"
            task_dir.mkdir(parents=True)
            good = {
                "task_id": "ok-1",
                "status": "completed",
                "error_code": None,
                "user_goal": "ok",
            }
            failed = {
                "task_id": "fail-1",
                "status": "failed",
                "error_code": "ACCORE_CONFIG_LOCKED",
                "track": "C",
                "risk": "batch_modify",
                "user_goal": "bad",
                "script_path": "script.lsp",
                "files": ["a.dwg"],
                "rollback_available": False,
                "started_at": "2026-05-24T01:00:00",
                "finished_at": "2026-05-24T01:01:00",
            }
            (task_dir / "ok-1.json").write_text(json.dumps(good), encoding="utf-8")
            (task_dir / "fail-1.json").write_text(json.dumps(failed), encoding="utf-8")

            result = recent_failures(root)

        self.assertTrue(result["ok"])
        self.assertEqual(result["count"], 1)
        self.assertEqual(result["failures"][0]["task_id"], "fail-1")
        self.assertEqual(result["failures"][0]["error_code"], "ACCORE_CONFIG_LOCKED")

    def test_error_detail_returns_task_and_log_tail(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            task_dir = root / ".agent" / "tasks"
            task_dir.mkdir(parents=True)
            log_dir = root / ".agent" / "logs" / "fail-1"
            log_dir.mkdir(parents=True)
            task = {
                "task_id": "fail-1",
                "status": "failed",
                "error_code": "LISP_LOAD_FAILED",
                "rollback_available": False,
            }
            (task_dir / "fail-1.json").write_text(json.dumps(task), encoding="utf-8")
            (log_dir / "drawing.log").write_text("line1\nline2\nline3", encoding="utf-8")

            result = error_detail(root, "fail-1", log_tail_chars=6)

        self.assertTrue(result["ok"])
        self.assertEqual(result["task"]["task_id"], "fail-1")
        self.assertEqual(result["error_code"], "LISP_LOAD_FAILED")
        self.assertEqual(len(result["log_paths"]), 1)
        self.assertEqual(result["log_tails"][0]["tail"], "\nline3")


if __name__ == "__main__":
    unittest.main()
