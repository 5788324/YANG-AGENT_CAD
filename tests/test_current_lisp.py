import shutil
import unittest
from contextlib import contextmanager
from pathlib import Path
from uuid import uuid4
from unittest.mock import patch

from yang_cad_agent.current_lisp import (
    AUTOCAD_PROG_IDS,
    build_load_command,
    build_wrapper_lisp,
    feed_current_lisp,
)
from yang_cad_agent.ledger import load_task_record
from yang_cad_agent.lisp_validator import validate_lisp_file


@contextmanager
def _temp_dir():
    root = Path(__file__).resolve().parents[1] / "tmp-tests"
    root.mkdir(parents=True, exist_ok=True)
    path = root / f"current-lisp-{uuid4().hex}"
    path.mkdir(parents=True, exist_ok=True)
    try:
        yield str(path)
    finally:
        shutil.rmtree(path, ignore_errors=True)


class CurrentLispTests(unittest.TestCase):
    def test_build_load_command_escapes_path(self):
        command = build_load_command(Path("C:/Temp/test.lsp"))
        self.assertTrue(command.startswith('(load "'))
        self.assertTrue(command.endswith('")\n'))

    def test_feed_current_lisp_dry_run(self):
        with _temp_dir() as tmp:
            root = Path(tmp)
            script = root / "current.lsp"
            script.write_text("(princ)", encoding="utf-8")

            result = feed_current_lisp(root, script, execute=False)

            self.assertTrue(result["ok"])
            self.assertEqual(result["mode"], "dry_run")
            self.assertIn("wrapper.lsp", result["wrapper_path"])
            self.assertIn("result.json", result["completion_marker"])
            self.assertFalse(Path(result["completion_marker"]).exists())
            record = load_task_record(root, result["task_id"])
            self.assertEqual(record["status"], "dry_run")
            self.assertEqual(record["params"]["completion_marker"], result["completion_marker"])

    def test_wrapper_lisp_is_valid_for_current_track(self):
        with _temp_dir() as tmp:
            root = Path(tmp)
            script = root / "current.lsp"
            result = root / "result.json"
            wrapper = root / "wrapper.lsp"
            script.write_text("(princ)", encoding="utf-8")
            wrapper.write_text(
                build_wrapper_lisp(script, result, "task-1"),
                encoding="utf-8",
            )

            validation = validate_lisp_file(wrapper, target_track="B")

            self.assertTrue(validation["ok"])
            text = wrapper.read_text(encoding="utf-8")
            self.assertIn('(write-line "  \\"task_id\\": \\"task-1\\"," fh)', text)
            self.assertNotIn('"task-1"," fh)', text)

    def test_execute_success_without_marker_is_reported_as_unconfirmed(self):
        with _temp_dir() as tmp:
            root = Path(tmp)
            script = root / "current.lsp"
            script.write_text("(princ)", encoding="utf-8")

            with patch("yang_cad_agent.current_lisp._send_command_to_autocad") as send:
                send.return_value = {"ok": True, "prog_id": "AutoCAD.Application", "document": "demo.dwg"}
                result = feed_current_lisp(root, script, execute=True)

            self.assertTrue(result["ok"])
            self.assertEqual(result["status"], "sent_unconfirmed")
            self.assertFalse(result["completion"]["confirmed"])
            record = load_task_record(root, result["task_id"])
            self.assertEqual(record["status"], "sent_unconfirmed")
            self.assertIsNone(record["error_code"])
            self.assertEqual(record["params"]["send_result"]["document"], "demo.dwg")

    def test_autocad_2027_prog_id_is_attempted_first(self):
        self.assertEqual(AUTOCAD_PROG_IDS[0], "AutoCAD.Application.26")
        self.assertIn("AutoCAD.Application", AUTOCAD_PROG_IDS)


if __name__ == "__main__":
    unittest.main()
