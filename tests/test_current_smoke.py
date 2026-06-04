from __future__ import annotations

import shutil
import unittest
from contextlib import contextmanager
from pathlib import Path
from unittest.mock import patch
from uuid import uuid4

from yang_cad_agent.current_smoke import current_smoke_script, run_current_smoke


@contextmanager
def _temp_dir():
    root = Path(__file__).resolve().parents[1] / "tmp-tests"
    root.mkdir(parents=True, exist_ok=True)
    path = root / f"current-smoke-{uuid4().hex}"
    path.mkdir(parents=True, exist_ok=True)
    try:
        yield path
    finally:
        shutil.rmtree(path, ignore_errors=True)


class CurrentSmokeTests(unittest.TestCase):
    def test_current_smoke_script_path(self) -> None:
        root = Path("D:/demo")
        self.assertEqual(
            current_smoke_script(root),
            Path("D:/demo/toolbox/plugins/current_smoke/main.lsp"),
        )

    def test_execute_blocks_when_com_not_attachable(self) -> None:
        with _temp_dir() as root:
            with patch("yang_cad_agent.current_smoke.diagnose_acad_com") as diagnose:
                diagnose.return_value = {
                    "ok": True,
                    "pywin32": {"available": True},
                    "attachable": False,
                    "acad_process": {"running": False},
                }
                with patch("yang_cad_agent.current_smoke.feed_current_lisp") as feed:
                    result = run_current_smoke(root, execute=True)

        self.assertFalse(result["ok"])
        self.assertEqual(result["status"], "blocked")
        self.assertEqual(result["error_code"], "ACAD_COM_UNAVAILABLE")
        self.assertEqual(feed.call_count, 0)

    def test_execute_reports_missing_pywin32(self) -> None:
        with _temp_dir() as root:
            with patch("yang_cad_agent.current_smoke.diagnose_acad_com") as diagnose:
                diagnose.return_value = {
                    "ok": True,
                    "pywin32": {"available": False},
                    "attachable": False,
                }
                result = run_current_smoke(root, execute=True)

        self.assertEqual(result["error_code"], "ACAD_COM_DEPENDENCY_MISSING")

    def test_execute_runs_feed_after_attachable_preflight(self) -> None:
        with _temp_dir() as root:
            with patch("yang_cad_agent.current_smoke.diagnose_acad_com") as diagnose:
                diagnose.return_value = {"ok": True, "attachable": True}
                with patch("yang_cad_agent.current_smoke.feed_current_lisp") as feed:
                    feed.return_value = {"ok": True, "status": "completed"}
                    result = run_current_smoke(root, execute=True)

        self.assertTrue(result["ok"])
        self.assertEqual(result["preflight"]["attachable"], True)
        self.assertEqual(feed.call_count, 1)


if __name__ == "__main__":
    unittest.main()
