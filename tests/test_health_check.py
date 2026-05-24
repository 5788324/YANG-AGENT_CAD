from __future__ import annotations

import unittest
from pathlib import Path

from yang_cad_agent.health_check import health_plugin_scripts


class HealthCheckTests(unittest.TestCase):
    def test_health_check_plugins_exist(self) -> None:
        project_root = Path(__file__).resolve().parents[1]

        scripts = health_plugin_scripts(project_root)

        self.assertEqual(
            [item["id"] for item in scripts],
            ["batch.layer_report", "batch.block_report", "batch.annotation_report"],
        )
        self.assertTrue(all(item["exists"] for item in scripts))


if __name__ == "__main__":
    unittest.main()
