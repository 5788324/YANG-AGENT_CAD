import unittest

from yang_cad_agent.lisp_validator import validate_lisp_text


class LispValidatorTests(unittest.TestCase):
    def test_accore_allows_noninteractive_ssget(self):
        text = '(setq ss (ssget "X" (list (cons 0 "LINE"))))\n(command "_.QSAVE")'
        result = validate_lisp_text(text, target_track="C")
        self.assertTrue(result["ok"])

    def test_accore_rejects_vla(self):
        text = "(vl-load-com)\n(setq obj (vlax-ename->vla-object ent))"
        result = validate_lisp_text(text, target_track="C")
        self.assertFalse(result["ok"])
        self.assertEqual(result["error_code"], "LISP_VALIDATE_FAILED")

    def test_accore_rejects_interactive_ssget(self):
        result = validate_lisp_text("(setq ss (ssget))", target_track="C")
        self.assertFalse(result["ok"])


if __name__ == "__main__":
    unittest.main()

