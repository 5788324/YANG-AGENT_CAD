import tempfile
import unittest
from pathlib import Path

from yang_cad_agent.accore import analyze_accore_output, collect_dwgs, decode_process_output


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


if __name__ == "__main__":
    unittest.main()
