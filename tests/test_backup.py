import tempfile
import unittest
from pathlib import Path

from yang_cad_agent.backup import backup_files, rollback_task


class BackupTests(unittest.TestCase):
    def test_backup_and_rollback_dry_run(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            source = root / "drawing.dwg"
            source.write_text("before", encoding="utf-8")

            result = backup_files(root, "task-1", [source])

            self.assertTrue(result["ok"])
            self.assertEqual(result["files_backed_up"], 1)

            source.write_text("after", encoding="utf-8")
            dry_run = rollback_task(root, "task-1", dry_run=True)

            self.assertTrue(dry_run["ok"])
            self.assertTrue(dry_run["dry_run"])
            self.assertEqual(source.read_text(encoding="utf-8"), "after")

    def test_backup_and_rollback_restore(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            source = root / "drawing.dwg"
            source.write_text("before", encoding="utf-8")
            backup_files(root, "task-2", [source])

            source.write_text("after", encoding="utf-8")
            result = rollback_task(root, "task-2", dry_run=False)

            self.assertTrue(result["ok"])
            self.assertEqual(source.read_text(encoding="utf-8"), "before")


if __name__ == "__main__":
    unittest.main()

