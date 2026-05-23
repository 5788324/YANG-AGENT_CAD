import tempfile
import unittest
from pathlib import Path

from yang_cad_agent.accore import collect_dwgs, decode_process_output


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


if __name__ == "__main__":
    unittest.main()
