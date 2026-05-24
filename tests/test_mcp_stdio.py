import unittest
from unittest.mock import patch

from yang_cad_agent.mcp_stdio import handle_message


class McpStdioTests(unittest.TestCase):
    def test_list_tools(self):
        result = handle_message({"action": "list_tools"})
        self.assertTrue(result["ok"])
        self.assertIn("doctor", result["tools"])
        self.assertIn("health_check", result["tools"])
        self.assertIn("summarize_reports", result["tools"])

    def test_health_check_tool_is_dry_run_only(self):
        with patch("yang_cad_agent.mcp_stdio.run_health_check") as run_health_check:
            run_health_check.return_value = {"ok": True, "mode": "dry_run"}

            result = handle_message(
                {
                    "action": "call_tool",
                    "name": "health_check",
                    "params": {
                        "root": ".",
                        "folder": ".agent/tmp/sample-run",
                        "pattern": "*.dwg",
                        "recursive": False,
                        "execute": True,
                    },
                }
            )

        self.assertTrue(result["ok"])
        self.assertEqual(result["result"]["mode"], "dry_run")
        self.assertFalse(run_health_check.call_args.kwargs["execute"])

    def test_summarize_reports_tool(self):
        with patch("yang_cad_agent.mcp_stdio.summarize_reports") as summarize_reports:
            summarize_reports.return_value = {"ok": True, "output": "summary.md"}

            result = handle_message(
                {
                    "action": "call_tool",
                    "name": "summarize_reports",
                    "params": {
                        "root": ".",
                        "folder": ".agent/tmp/sample-run",
                        "output": ".agent/tmp/sample-run/summary.md",
                    },
                }
            )

        self.assertTrue(result["ok"])
        self.assertEqual(result["result"]["output"], "summary.md")
        self.assertEqual(summarize_reports.call_count, 1)


if __name__ == "__main__":
    unittest.main()
