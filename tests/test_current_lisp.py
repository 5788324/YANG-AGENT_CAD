import tempfile
import unittest
from pathlib import Path

from yang_cad_agent.current_lisp import build_load_command, feed_current_lisp
from yang_cad_agent.ledger import load_task_record


class CurrentLispTests(unittest.TestCase):
    def test_build_load_command_escapes_path(self):
        command = build_load_command(Path("C:/Temp/test.lsp"))
        self.assertTrue(command.startswith('(load "'))
        self.assertTrue(command.endswith('")\n'))

    def test_feed_current_lisp_dry_run(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            script = root / "current.lsp"
            script.write_text("(princ)", encoding="utf-8")

            result = feed_current_lisp(root, script, execute=False)

            self.assertTrue(result["ok"])
            self.assertEqual(result["mode"], "dry_run")
            record = load_task_record(root, result["task_id"])
            self.assertEqual(record["status"], "dry_run")


if __name__ == "__main__":
    unittest.main()

