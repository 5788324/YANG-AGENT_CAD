import unittest
from datetime import datetime

from pathlib import Path
import tempfile

from yang_cad_agent.ledger import create_task_record, new_task_id, update_task_record


class LedgerTests(unittest.TestCase):
    def test_new_task_id_has_date_prefix(self):
        task_id = new_task_id(datetime(2026, 5, 23, 9, 8, 7))
        self.assertTrue(task_id.startswith("20260523-090807-"))

    def test_update_task_record(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            record = create_task_record(root, "test", "read", "MCP")
            updated = update_task_record(root, record["task_id"], status="done")
            self.assertEqual(updated["status"], "done")


if __name__ == "__main__":
    unittest.main()
