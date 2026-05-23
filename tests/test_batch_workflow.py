import tempfile
import unittest
from pathlib import Path

from yang_cad_agent.batch_workflow import run_batch_workflow
from yang_cad_agent.ledger import load_task_record


class BatchWorkflowTests(unittest.TestCase):
    def test_batch_workflow_dry_run_creates_ledger(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            folder = root / "dwgs"
            folder.mkdir()
            script = root / "safe.lsp"
            script.write_text('(command "_.QSAVE")', encoding="utf-8")
            (folder / "a.dwg").write_text("fake", encoding="utf-8")

            result = run_batch_workflow(root, folder, script, execute=False)

            self.assertTrue(result["ok"])
            self.assertEqual(result["mode"], "dry_run")
            record = load_task_record(root, result["task_id"])
            self.assertEqual(record["status"], "dry_run")
            self.assertEqual(len(record["files"]), 1)

    def test_batch_workflow_fails_when_no_dwgs_match(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            folder = root / "dwgs"
            folder.mkdir()
            script = root / "safe.lsp"
            script.write_text('(command "_.QSAVE")', encoding="utf-8")

            result = run_batch_workflow(root, folder, script, execute=False)

            self.assertFalse(result["ok"])
            self.assertEqual(result["error_code"], "FILE_NOT_FOUND")


if __name__ == "__main__":
    unittest.main()
