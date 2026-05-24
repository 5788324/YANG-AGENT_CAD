import tempfile
import unittest
from pathlib import Path

from yang_cad_agent.report_summary import summarize_reports


class ReportSummaryTests(unittest.TestCase):
    def test_summarize_reports_writes_markdown(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "demo.dwg.layer-report.csv").write_text(
                "\n".join(
                    [
                        "dwg,layer,color,locked,frozen,entity_count",
                        "demo.dwg,A-WALL,7,no,no,12",
                        "demo.dwg,A-TEXT,2,no,no,3",
                        "demo.dwg,__TOTAL__,0,no,no,15",
                    ]
                )
                + "\n",
                encoding="utf-8",
            )
            (root / "demo.dwg.block-report.csv").write_text(
                "\n".join(
                    [
                        "dwg,block,is_xref,is_layout,insert_count",
                        "demo.dwg,TITLE,no,no,1",
                        "demo.dwg,__TOTAL__,no,no,1",
                    ]
                )
                + "\n",
                encoding="utf-8",
            )
            (root / "demo.dwg.annotation-report.csv").write_text(
                "\n".join(
                    [
                        "dwg,entity_type,label,count",
                        "demo.dwg,MTEXT,multi_line_text,3",
                        "demo.dwg,__TOTAL__,annotation_total,3",
                    ]
                )
                + "\n",
                encoding="utf-8",
            )

            result = summarize_reports(root)

            self.assertTrue(result["ok"])
            self.assertEqual(result["summary"]["layers"], 2)
            self.assertEqual(result["summary"]["entities"], 15)
            self.assertEqual(result["summary"]["blocks"], 1)
            self.assertEqual(result["summary"]["annotations"], 3)
            text = (root / "CAD_REPORT_SUMMARY.md").read_text(encoding="utf-8")
            self.assertIn("CAD 图纸体检总报告", text)
            self.assertIn("A-WALL", text)
            self.assertIn("TITLE", text)
            self.assertIn("MTEXT", text)


if __name__ == "__main__":
    unittest.main()
