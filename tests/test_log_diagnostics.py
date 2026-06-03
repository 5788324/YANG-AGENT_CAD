from __future__ import annotations

import unittest

from yang_cad_agent.log_diagnostics import diagnose_log_tails


class LogDiagnosticsTests(unittest.TestCase):
    def test_detects_startup_noise_and_lisp_load_canceled(self) -> None:
        result = diagnose_log_tails(
            [
                {
                    "path": "demo.log",
                    "tail": (
                        '; 错误: LOAD 失败: "acad2027"\n'
                        '命令: (load "D:/demo/main.lsp")\n'
                        "; 错误: 文件加载已取消:D:/demo/main.lsp\n"
                    ),
                }
            ],
            "LISP_LOAD_FAILED",
        )

        self.assertEqual([item["rule_id"] for item in result], ["acad_startup_noise", "lisp_load_canceled"])

    def test_detects_config_locked(self) -> None:
        result = diagnose_log_tails(
            [{"path": "demo.log", "tail": "C:\\AutoCAD\\acad2027.cfg 已锁定"}],
            "ACCORE_CONFIG_LOCKED",
        )

        self.assertEqual(result[0]["rule_id"], "acad_config_locked")

    def test_detects_missing_file(self) -> None:
        result = diagnose_log_tails(
            [{"path": "demo.log", "tail": "未找到文件 D:/demo/missing.lsp"}],
            "FILE_NOT_FOUND",
        )

        self.assertEqual(result[0]["rule_id"], "referenced_file_missing")

    def test_returns_fallback_when_error_has_no_match(self) -> None:
        result = diagnose_log_tails([{"path": "demo.log", "tail": "plain log"}], "UNKNOWN_ERROR")

        self.assertEqual(result[0]["rule_id"], "no_log_rule_match")
        self.assertEqual(result[0]["severity"], "warning")

    def test_detects_accore_timeout_from_error_code(self) -> None:
        result = diagnose_log_tails([{"path": "demo.log", "tail": "plain log"}], "ACCORE_TIMEOUT")

        self.assertEqual(result[0]["rule_id"], "accore_timeout")
        self.assertIn("waiting for input", result[0]["suggestion"])

    def test_detects_accore_nonzero_exit(self) -> None:
        result = diagnose_log_tails(
            [{"path": "demo.log", "tail": "accoreconsole exited with code 1"}],
            "ACCORE_NONZERO_EXIT",
        )

        self.assertEqual(result[0]["rule_id"], "accore_nonzero_exit")

    def test_detects_secure_load_blocked(self) -> None:
        result = diagnose_log_tails(
            [{"path": "demo.log", "tail": "SECURELOAD blocked path outside TRUSTEDPATHS"}],
            "LISP_LOAD_FAILED",
        )

        self.assertEqual(result[0]["rule_id"], "secure_load_blocked")

    def test_returns_empty_when_no_error_and_no_match(self) -> None:
        self.assertEqual(diagnose_log_tails([{"path": "demo.log", "tail": "plain log"}], None), [])


if __name__ == "__main__":
    unittest.main()
