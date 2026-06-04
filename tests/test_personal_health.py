from __future__ import annotations

import shutil
import unittest
from pathlib import Path
from unittest.mock import patch

from yang_cad_agent.personal_health import run_personal_health, write_personal_health_report


TMP_ROOT = Path("tmp-tests") / "personal-health"


class PersonalHealthTests(unittest.TestCase):
    def setUp(self) -> None:
        shutil.rmtree(TMP_ROOT, ignore_errors=True)
        TMP_ROOT.mkdir(parents=True, exist_ok=True)

    def tearDown(self) -> None:
        shutil.rmtree(TMP_ROOT, ignore_errors=True)

    def test_write_dry_run_report(self) -> None:
        output = TMP_ROOT / "plan.md"
        result = {
            "ok": True,
            "mode": "dry_run",
            "steps": [
                {"plugin": "batch.layer_report", "result": {"ok": True, "task_id": "t1", "file_count": 2}},
                {"plugin": "batch.block_report", "result": {"ok": True, "task_id": "t2", "file_count": 2}},
            ],
        }

        report = write_personal_health_report(
            result=result,
            output_path=output,
            folder=Path("sample"),
            pattern="*.dwg",
            recursive=False,
        )

        self.assertTrue(report["ok"])
        text = output.read_text(encoding="utf-8")
        self.assertIn("安全预演 dry-run", text)
        self.assertIn("batch.layer_report", text)
        self.assertIn("匹配 DWG 数量：2", text)

    def test_run_personal_health_defaults_to_sample_and_writes_report(self) -> None:
        root = TMP_ROOT / "project"
        root.mkdir()
        output = root / "report.md"
        with patch("yang_cad_agent.personal_health.run_health_check") as run_health_check:
            run_health_check.return_value = {
                "ok": True,
                "mode": "dry_run",
                "steps": [
                    {"plugin": "batch.layer_report", "result": {"ok": True, "task_id": "t1", "file_count": 1}},
                ],
            }

            result = run_personal_health(project_root=root, output=output)

        self.assertTrue(result["ok"])
        self.assertEqual(Path(result["folder"]), root.resolve() / "sample")
        self.assertTrue(Path(result["report"]["output"]).exists())
        self.assertFalse(run_health_check.call_args.kwargs["execute"])


if __name__ == "__main__":
    unittest.main()
