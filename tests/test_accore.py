import tempfile
import unittest
from pathlib import Path

from yang_cad_agent.accore import (
    analyze_accore_output,
    collect_dwgs,
    decode_process_output,
    write_accore_script_wrapper,
)
from yang_cad_agent.doctor import accore_install_issue


class AccoreTests(unittest.TestCase):
    def test_collect_dwgs_nonrecursive(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "a.dwg").write_text("a", encoding="utf-8")
            nested = root / "nested"
            nested.mkdir()
            (nested / "b.dwg").write_text("b", encoding="utf-8")

            result = collect_dwgs(root)

            self.assertEqual(result, [root / "a.dwg"])

    def test_decode_utf16_output(self):
        output = decode_process_output("MessageBox".encode("utf-16-le"), b"")
        self.assertEqual(output, "MessageBox")

    def test_analyze_config_locked(self):
        result = analyze_accore_output(
            "acad2027.cfg 配置文件可能被其他进程锁定，或者已设置为只读",
            returncode=1,
        )
        self.assertEqual(result["error_code"], "ACCORE_CONFIG_LOCKED")

    def test_analyze_lisp_load_failed_even_with_zero_returncode(self):
        result = analyze_accore_output(
            '"main.lsp.scr": 未找到文件。',
            returncode=0,
        )
        self.assertEqual(result["error_code"], "LISP_LOAD_FAILED")

    def test_analyze_ignores_acad2027_startup_load_noise(self):
        result = analyze_accore_output(
            '; 错误: LOAD 失败: "acad2027"',
            returncode=0,
        )
        self.assertIsNone(result["error_code"])

    def test_write_accore_script_wrapper_loads_lisp(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            script = root / "toolbox" / "plugins" / "demo" / "main.lsp"
            script.parent.mkdir(parents=True)
            script.write_text("(princ)", encoding="utf-8")

            wrapper = write_accore_script_wrapper(root, "task-1", script)

            self.assertEqual(wrapper.suffix, ".scr")
            text = wrapper.read_text(encoding="utf-8")
            self.assertIn('(setvar "SECURELOAD" 0)', text)
            self.assertIn('(load "', text)
            self.assertIn("main.lsp", text)

    def test_accore_install_issue_when_cfg_missing_and_dir_unwritable(self):
        issue = accore_install_issue(
            {
                "cfg_exists": False,
                "cfg_read_only": False,
                "install_dir_writable": False,
                "expected_cfg": "C:/Program Files/Autodesk/AutoCAD 2027/acad2027.cfg",
            }
        )
        self.assertEqual(issue["error_code"], "ACCORE_CONFIG_LOCKED")

    def test_accore_install_issue_reports_user_cfg_copy_path(self):
        issue = accore_install_issue(
            {
                "cfg_exists": False,
                "cfg_read_only": False,
                "install_dir_writable": False,
                "expected_cfg": "C:/Program Files/Autodesk/AutoCAD 2027/acad2027.cfg",
                "user_cfg": "C:/Users/YANG/AppData/Local/Autodesk/AutoCAD 2027/R26.0/chs/acad2027.cfg",
                "user_cfg_exists": True,
            }
        )
        self.assertEqual(issue["error_code"], "ACCORE_CONFIG_LOCKED")
        self.assertIn("User config exists", issue["message"])
        self.assertIn("admin rights", issue["message"])

    def test_accore_install_issue_reports_user_cfg_check_error(self):
        issue = accore_install_issue(
            {
                "cfg_exists": False,
                "cfg_read_only": False,
                "install_dir_writable": False,
                "expected_cfg": "C:/Program Files/Autodesk/AutoCAD 2027/acad2027.cfg",
                "user_cfg": "C:/Users/YANG/AppData/Local/Autodesk/AutoCAD 2027/R26.0/chs/acad2027.cfg",
                "user_cfg_exists": False,
                "user_cfg_check_error": "Access denied",
            }
        )
        self.assertEqual(issue["error_code"], "ACCORE_CONFIG_LOCKED")
        self.assertIn("could not be checked", issue["message"])
        self.assertIn("fix-acad-cfg.cmd", issue["message"])


if __name__ == "__main__":
    unittest.main()
