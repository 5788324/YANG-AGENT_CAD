from __future__ import annotations

import builtins
import sys
import types
import unittest
from unittest.mock import patch

from yang_cad_agent.acad_com_diagnose import diagnose_acad_com


class AcadComDiagnoseTests(unittest.TestCase):
    def test_reports_missing_pywin32(self) -> None:
        real_import = builtins.__import__

        def fake_import(name, *args, **kwargs):
            if name == "win32com.client":
                raise ImportError("missing")
            return real_import(name, *args, **kwargs)

        with patch("builtins.__import__", side_effect=fake_import):
            result = diagnose_acad_com()

        self.assertTrue(result["ok"])
        self.assertFalse(result["pywin32"]["available"])
        self.assertEqual(result["diagnostics"][0]["rule_id"], "pywin32_missing")

    def test_reports_running_acad_without_com(self) -> None:
        fake_client = types.ModuleType("win32com.client")
        fake_client.GetActiveObject = lambda prog_id: (_ for _ in ()).throw(RuntimeError("missing"))
        fake_win32com = types.ModuleType("win32com")
        fake_win32com.client = fake_client
        with patch("yang_cad_agent.acad_com_diagnose._check_pywin32", return_value={"available": True}):
            with patch(
                "yang_cad_agent.acad_com_diagnose._acad_process_state",
                return_value={"running": True, "method": "test"},
            ):
                with patch.dict(sys.modules, {"win32com": fake_win32com, "win32com.client": fake_client}):
                    result = diagnose_acad_com()

        self.assertFalse(result["attachable"])
        self.assertEqual(result["diagnostics"][0]["rule_id"], "acad_process_without_com")
        self.assertEqual(result["prog_id_checks"][0]["prog_id"], "AutoCAD.Application.26")


if __name__ == "__main__":
    unittest.main()
