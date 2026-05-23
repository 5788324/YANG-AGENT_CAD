import unittest

from yang_cad_agent.doctor import run_doctor


class DoctorTests(unittest.TestCase):
    def test_doctor_returns_expected_sections(self):
        result = run_doctor()
        self.assertIn("python", result)
        self.assertIn("git", result)
        self.assertIn("accoreconsole", result)


if __name__ == "__main__":
    unittest.main()

