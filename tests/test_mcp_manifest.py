import unittest

from yang_cad_agent.mcp_manifest import build_manifest
from yang_cad_agent.mcp_stdio import TOOLS


class McpManifestTests(unittest.TestCase):
    def test_manifest_describes_all_stdio_tools(self):
        manifest = build_manifest(TOOLS)

        self.assertTrue(manifest["ok"])
        self.assertEqual(manifest["name"], "yang-cad-agent")
        self.assertEqual(manifest["transport"], "stdio-json-lines")
        self.assertEqual(manifest["tool_count"], len(TOOLS))
        self.assertEqual(set(manifest["tools"]), set(TOOLS))

    def test_manifest_declares_current_safety_boundaries(self):
        manifest = build_manifest(TOOLS)

        self.assertFalse(manifest["protocol"]["official_mcp_sdk"])
        self.assertTrue(manifest["safety"]["no_general_shell"])
        self.assertTrue(manifest["safety"]["health_check_forces_dry_run"])
        self.assertTrue(manifest["safety"]["rollback_is_dry_run_only"])
        self.assertTrue(manifest["safety"]["batch_execute_requires_cli_confirmation"])


if __name__ == "__main__":
    unittest.main()
