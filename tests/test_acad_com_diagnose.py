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
        self.assertIn("registered_prog_ids", result)
        self.assertIn("acad_process_details", result)
        self.assertIn("window_probe", result)

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
                with patch(
                    "yang_cad_agent.acad_com_diagnose._acad_process_details",
                    return_value={"ok": True, "processes": [{"Id": 1, "MainWindowTitle": "AutoCAD"}]},
                ):
                    with patch(
                        "yang_cad_agent.acad_com_diagnose._collect_acad_windows",
                        return_value={
                            "available": True,
                            "window_count": 1,
                            "visible_window_count": 1,
                            "windows": [{"visible": True, "title": ""}],
                        },
                    ):
                        with patch(
                            "yang_cad_agent.acad_com_diagnose._check_registered_prog_ids",
                            return_value=[{"prog_id": "AutoCAD.Application.26", "registered": True, "clsid": "{demo}", "error": ""}],
                        ):
                            with patch(
                                "yang_cad_agent.acad_com_diagnose._collect_rot_entries",
                                return_value={"available": True, "entry_count": 0, "filtered_entries": [], "error": ""},
                            ):
                                with patch.dict(sys.modules, {"win32com": fake_win32com, "win32com.client": fake_client}):
                                    result = diagnose_acad_com()

        self.assertFalse(result["attachable"])
        self.assertEqual(result["diagnostics"][0]["rule_id"], "acad_visible_window_title_empty")
        self.assertEqual(result["diagnostics"][1]["rule_id"], "acad_not_in_running_object_table")
        self.assertEqual(result["diagnostics"][2]["rule_id"], "acad_process_without_com")
        self.assertEqual(result["prog_id_checks"][0]["prog_id"], "AutoCAD.Application.26")
        self.assertTrue(result["registered_prog_ids"][0]["registered"])
        self.assertEqual(result["acad_process_details"]["processes"][0]["MainWindowTitle"], "AutoCAD")
        self.assertEqual(result["window_probe"]["visible_window_count"], 1)

    def test_reports_attachable_when_get_active_object_succeeds(self) -> None:
        fake_doc = types.SimpleNamespace(Name="demo.dwg")
        fake_acad = types.SimpleNamespace(ActiveDocument=fake_doc)
        fake_client = types.ModuleType("win32com.client")
        fake_client.GetActiveObject = lambda prog_id: fake_acad
        fake_win32com = types.ModuleType("win32com")
        fake_win32com.client = fake_client
        with patch("yang_cad_agent.acad_com_diagnose._check_pywin32", return_value={"available": True}):
            with patch("yang_cad_agent.acad_com_diagnose._acad_process_state", return_value={"running": True, "method": "test"}):
                with patch("yang_cad_agent.acad_com_diagnose._acad_process_details", return_value={"ok": True, "processes": []}):
                    with patch("yang_cad_agent.acad_com_diagnose._collect_acad_windows", return_value={"available": True, "windows": []}):
                        with patch("yang_cad_agent.acad_com_diagnose._check_registered_prog_ids", return_value=[]):
                            with patch("yang_cad_agent.acad_com_diagnose._collect_rot_entries", return_value={"available": True, "filtered_entries": ["AutoCAD"], "error": ""}):
                                with patch.dict(sys.modules, {"win32com": fake_win32com, "win32com.client": fake_client}):
                                    result = diagnose_acad_com()

        self.assertTrue(result["attachable"])
        self.assertEqual(result["active_document"], "demo.dwg")
        self.assertEqual(result["diagnostics"][0]["rule_id"], "acad_com_attachable")

    def test_reports_running_acad_with_no_top_level_window(self) -> None:
        fake_client = types.ModuleType("win32com.client")
        fake_client.GetActiveObject = lambda prog_id: (_ for _ in ()).throw(RuntimeError("missing"))
        fake_win32com = types.ModuleType("win32com")
        fake_win32com.client = fake_client
        with patch("yang_cad_agent.acad_com_diagnose._check_pywin32", return_value={"available": True}):
            with patch(
                "yang_cad_agent.acad_com_diagnose._acad_process_state",
                return_value={"running": True, "method": "test"},
            ):
                with patch(
                    "yang_cad_agent.acad_com_diagnose._acad_process_details",
                    return_value={"ok": True, "processes": [{"Id": 1, "MainWindowTitle": ""}]},
                ):
                    with patch(
                        "yang_cad_agent.acad_com_diagnose._collect_acad_windows",
                        return_value={"available": True, "window_count": 0, "visible_window_count": 0, "windows": []},
                    ):
                        with patch("yang_cad_agent.acad_com_diagnose._check_registered_prog_ids", return_value=[]):
                            with patch(
                                "yang_cad_agent.acad_com_diagnose._collect_rot_entries",
                                return_value={"available": True, "entry_count": 0, "filtered_entries": [], "error": ""},
                            ):
                                with patch.dict(sys.modules, {"win32com": fake_win32com, "win32com.client": fake_client}):
                                    result = diagnose_acad_com()

        self.assertEqual(result["diagnostics"][0]["rule_id"], "acad_process_without_top_level_window")


if __name__ == "__main__":
    unittest.main()
