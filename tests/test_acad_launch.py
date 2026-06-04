from __future__ import annotations

import shutil
import unittest
from pathlib import Path
from unittest.mock import Mock, patch
from uuid import uuid4

from yang_cad_agent.acad_launch import launch_autocad, prepare_launch_plan


def _temp_root() -> Path:
    root = Path(__file__).resolve().parents[1] / "tmp-tests" / f"acad-launch-{uuid4().hex}"
    root.mkdir(parents=True, exist_ok=True)
    return root


class AcadLaunchTests(unittest.TestCase):
    def test_dry_run_uses_sample_test_copy(self) -> None:
        root = _temp_root()
        try:
            sample = root / "sample"
            sample.mkdir()
            (sample / "S001.dwg").write_bytes(b"dwg")
            with patch("yang_cad_agent.acad_launch.find_acad_exe", return_value=Path("C:/Program Files/Autodesk/AutoCAD 2027/acad.exe")):
                result = launch_autocad(root, execute=False)

            self.assertTrue(result["ok"])
            self.assertEqual(result["mode"], "dry_run")
            self.assertIn(".agent", result["plan"]["target_dwg"])
            self.assertIsNotNone(result["plan"]["copy_action"])
        finally:
            shutil.rmtree(root, ignore_errors=True)

    def test_execute_copies_sample_and_launches(self) -> None:
        root = _temp_root()
        try:
            sample = root / "sample"
            sample.mkdir()
            (sample / "S001.dwg").write_bytes(b"dwg")
            fake_process = Mock(pid=1234)
            with patch("yang_cad_agent.acad_launch.find_acad_exe", return_value=Path("C:/Program Files/Autodesk/AutoCAD 2027/acad.exe")):
                with patch("yang_cad_agent.acad_launch.subprocess.Popen", return_value=fake_process) as popen:
                    result = launch_autocad(root, execute=True)

            self.assertTrue(result["ok"])
            self.assertEqual(result["status"], "launched")
            self.assertTrue((root / ".agent" / "tmp" / "current-open" / "S001-current-test.dwg").exists())
            self.assertEqual(popen.call_count, 1)
        finally:
            shutil.rmtree(root, ignore_errors=True)

    def test_missing_acad_returns_error(self) -> None:
        root = _temp_root()
        try:
            with patch("yang_cad_agent.acad_launch.find_acad_exe", return_value=None):
                result = prepare_launch_plan(root)
                launch = launch_autocad(root, execute=False)

            self.assertIsNone(result["acad_exe"])
            self.assertFalse(launch["ok"])
            self.assertEqual(launch["error_code"], "ACAD_EXE_NOT_FOUND")
        finally:
            shutil.rmtree(root, ignore_errors=True)


if __name__ == "__main__":
    unittest.main()
