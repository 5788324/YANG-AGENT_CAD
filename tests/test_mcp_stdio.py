import unittest
from unittest.mock import patch

from yang_cad_agent.mcp_stdio import handle_message


class McpStdioTests(unittest.TestCase):
    def test_server_info_returns_manifest(self):
        result = handle_message({"action": "server_info"})

        self.assertTrue(result["ok"])
        self.assertEqual(result["name"], "yang-cad-agent")
        self.assertIn("doctor", result["tools"])
        self.assertFalse(result["protocol"]["official_mcp_sdk"])
        self.assertTrue(result["safety"]["health_check_forces_dry_run"])

    def test_list_tools(self):
        result = handle_message({"action": "list_tools"})
        self.assertTrue(result["ok"])
        self.assertIn("doctor", result["tools"])
        self.assertIn("health_check", result["tools"])
        self.assertIn("summarize_reports", result["tools"])
        self.assertIn("rollback_dry_run", result["tools"])
        self.assertIn("task_recent_failures", result["tools"])
        self.assertIn("task_error_detail", result["tools"])
        self.assertIn("personal_health", result["tools"])
        self.assertIn("acad_com_diagnose", result["tools"])

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

    def test_rollback_dry_run_tool_cannot_restore_files(self):
        with patch("yang_cad_agent.mcp_stdio.rollback_task") as rollback_task:
            rollback_task.return_value = {"ok": True, "dry_run": True, "actions": []}

            result = handle_message(
                {
                    "action": "call_tool",
                    "name": "rollback_dry_run",
                    "params": {
                        "root": ".",
                        "task_id": "task-1",
                        "dry_run": False,
                    },
                }
            )

        self.assertTrue(result["ok"])
        self.assertTrue(result["result"]["dry_run"])
        self.assertTrue(rollback_task.call_args.kwargs["dry_run"])

    def test_task_recent_failures_tool(self):
        with patch("yang_cad_agent.mcp_stdio.recent_failures") as recent_failures:
            recent_failures.return_value = {"ok": True, "count": 1, "failures": []}

            result = handle_message(
                {
                    "action": "call_tool",
                    "name": "task_recent_failures",
                    "params": {"root": ".", "limit": 5, "scan_limit": 50},
                }
            )

        self.assertTrue(result["ok"])
        self.assertEqual(result["result"]["count"], 1)
        self.assertEqual(recent_failures.call_args.kwargs["limit"], 5)
        self.assertEqual(recent_failures.call_args.kwargs["scan_limit"], 50)

    def test_task_error_detail_tool(self):
        with patch("yang_cad_agent.mcp_stdio.error_detail") as error_detail:
            error_detail.return_value = {"ok": True, "task": {"task_id": "task-1"}}

            result = handle_message(
                {
                    "action": "call_tool",
                    "name": "task_error_detail",
                    "params": {"root": ".", "task_id": "task-1", "log_tail_chars": 123},
                }
            )

        self.assertTrue(result["ok"])
        self.assertEqual(result["result"]["task"]["task_id"], "task-1")
        self.assertEqual(error_detail.call_args.kwargs["task_id"], "task-1")
        self.assertEqual(error_detail.call_args.kwargs["log_tail_chars"], 123)

    def test_personal_health_tool_is_dry_run_only(self):
        with patch("yang_cad_agent.mcp_stdio.run_personal_health") as run_personal_health:
            run_personal_health.return_value = {"ok": True, "mode": "dry_run"}

            result = handle_message(
                {
                    "action": "call_tool",
                    "name": "personal_health",
                    "params": {
                        "root": ".",
                        "folder": "sample",
                        "execute": True,
                        "output": ".agent/reports/test.md",
                    },
                }
            )

        self.assertTrue(result["ok"])
        self.assertEqual(result["result"]["mode"], "dry_run")
        self.assertFalse(run_personal_health.call_args.kwargs["execute"])

    def test_acad_com_diagnose_tool_is_read_only(self):
        with patch("yang_cad_agent.mcp_stdio.diagnose_acad_com") as diagnose:
            diagnose.return_value = {"ok": True, "mode": "read_only"}

            result = handle_message(
                {
                    "action": "call_tool",
                    "name": "acad_com_diagnose",
                    "params": {"root": "."},
                }
            )

        self.assertTrue(result["ok"])
        self.assertEqual(result["result"]["mode"], "read_only")
        self.assertEqual(diagnose.call_count, 1)


if __name__ == "__main__":
    unittest.main()
