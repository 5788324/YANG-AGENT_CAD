import unittest
from datetime import datetime

from pathlib import Path
import tempfile

from yang_cad_agent.ledger import (
    create_task_record,
    list_task_records,
    new_task_id,
    update_task_record,
)


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

    def test_list_task_records(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            create_task_record(root, "first", "read", "MCP")
            create_task_record(root, "second", "read", "MCP")
            records = list_task_records(root, limit=1)
            self.assertEqual(len(records), 1)
            self.assertIn("task_id", records[0])


if __name__ == "__main__":
    unittest.main()
