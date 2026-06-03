from __future__ import annotations

import re
import unittest
from pathlib import Path

from yang_cad_agent import error_codes


def _constant_error_codes() -> set[str]:
    return {
        value
        for name, value in vars(error_codes).items()
        if name.isupper() and isinstance(value, str)
    }


def _documented_error_codes() -> set[str]:
    root = Path(__file__).resolve().parents[1]
    text = (root / "docs" / "ERROR_CODES.md").read_text(encoding="utf-8")
    return set(re.findall(r"^\| ([A-Z0-9_]+) \|", text, flags=re.MULTILINE))


class ErrorCodeTests(unittest.TestCase):
    def test_error_details_cover_all_constants(self) -> None:
        self.assertEqual(_constant_error_codes(), set(error_codes.ERROR_DETAILS))

    def test_error_code_docs_cover_all_constants(self) -> None:
        self.assertEqual(_constant_error_codes(), _documented_error_codes())

    def test_error_detail_fields_are_complete(self) -> None:
        for code, detail in error_codes.ERROR_DETAILS.items():
            with self.subTest(code=code):
                self.assertTrue(detail["meaning"])
                self.assertTrue(detail["suggestion"])
                self.assertIn(detail["severity"], {"info", "warning", "error"})


if __name__ == "__main__":
    unittest.main()
