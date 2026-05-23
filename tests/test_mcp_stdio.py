import unittest

from yang_cad_agent.mcp_stdio import handle_message


class McpStdioTests(unittest.TestCase):
    def test_list_tools(self):
        result = handle_message({"action": "list_tools"})
        self.assertTrue(result["ok"])
        self.assertIn("doctor", result["tools"])


if __name__ == "__main__":
    unittest.main()

