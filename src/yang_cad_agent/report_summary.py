"""Summarize generated CAD CSV reports into a beginner-friendly Markdown report."""

from __future__ import annotations

import csv
from pathlib import Path

from . import error_codes


def _read_csv(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def _int_value(value: str | None) -> int:
    try:
        return int(value or "0")
    except ValueError:
        return 0


def _find_report_files(folder: Path, suffix: str) -> list[Path]:
    return sorted(folder.glob(f"*{suffix}"))


def _top_rows(rows: list[dict[str, str]], value_key: str, limit: int = 10) -> list[dict[str, str]]:
    return sorted(rows, key=lambda row: _int_value(row.get(value_key)), reverse=True)[:limit]


def _append_table(lines: list[str], headers: list[str], rows: list[list[str]]) -> None:
    lines.append("| " + " | ".join(headers) + " |")
    lines.append("| " + " | ".join("---" for _ in headers) + " |")
    if not rows:
        lines.append("| " + " | ".join("无" for _ in headers) + " |")
        return
    for row in rows:
        lines.append("| " + " | ".join(row) + " |")


def summarize_reports(folder: Path, output: Path | None = None) -> dict:
    if not folder.exists():
        return {
            "ok": False,
            "error_code": error_codes.FILE_NOT_FOUND,
            "message": f"Folder not found: {folder}",
        }

    output_path = output or folder / "CAD_REPORT_SUMMARY.md"
    layer_files = _find_report_files(folder, ".layer-report.csv")
    block_files = _find_report_files(folder, ".block-report.csv")
    annotation_files = _find_report_files(folder, ".annotation-report.csv")
    xref_image_files = _find_report_files(folder, ".xref-image-report.csv")
    title_block_files = _find_report_files(folder, ".title-block-candidate-report.csv")
    report_files = layer_files + block_files + annotation_files + xref_image_files + title_block_files
    if not report_files:
        return {
            "ok": False,
            "error_code": error_codes.FILE_NOT_FOUND,
            "message": f"No CAD report CSV files found in {folder}",
        }

    layer_rows = [row for path in layer_files for row in _read_csv(path)]
    block_rows = [row for path in block_files for row in _read_csv(path)]
    annotation_rows = [row for path in annotation_files for row in _read_csv(path)]
    xref_image_rows = [row for path in xref_image_files for row in _read_csv(path)]
    title_block_rows = [row for path in title_block_files for row in _read_csv(path)]

    total_layers = sum(1 for row in layer_rows if row.get("layer") != "__TOTAL__")
    total_entities = sum(_int_value(row.get("entity_count")) for row in layer_rows if row.get("layer") == "__TOTAL__")
    total_blocks = sum(_int_value(row.get("insert_count")) for row in block_rows if row.get("block") == "__TOTAL__")
    total_annotations = sum(_int_value(row.get("count")) for row in annotation_rows if row.get("entity_type") == "__TOTAL__")
    total_references = sum(_int_value(row.get("count")) for row in xref_image_rows if row.get("ref_type") == "__TOTAL__")
    total_title_candidates = sum(
        _int_value(row.get("insert_count")) for row in title_block_rows if row.get("block") == "__TOTAL__"
    )

    lines = [
        "# CAD 图纸体检总报告",
        "",
        "## 总览",
        "",
    ]
    _append_table(
        lines,
        ["项目", "数量"],
        [
            ["图层数量", str(total_layers)],
            ["图层对象总数", str(total_entities)],
            ["普通块参照总数", str(total_blocks)],
            ["文字/标注对象总数", str(total_annotations)],
            ["外参/图片/底图引用总数", str(total_references)],
            ["图框标题栏候选数", str(total_title_candidates)],
            ["CSV 报告文件数", str(len(report_files))],
        ],
    )

    lines.extend(["", "## 对象最多的图层", ""])
    top_layers = [
        [
            row.get("dwg", ""),
            row.get("layer", ""),
            row.get("entity_count", "0"),
            row.get("locked", ""),
            row.get("frozen", ""),
        ]
        for row in _top_rows([row for row in layer_rows if row.get("layer") != "__TOTAL__"], "entity_count")
    ]
    _append_table(lines, ["DWG", "图层", "对象数", "锁定", "冻结"], top_layers)

    lines.extend(["", "## 块参照统计", ""])
    top_blocks = [
        [
            row.get("dwg", ""),
            row.get("block", ""),
            row.get("insert_count", "0"),
            row.get("is_xref", ""),
        ]
        for row in _top_rows([row for row in block_rows if row.get("block") != "__TOTAL__"], "insert_count")
    ]
    _append_table(lines, ["DWG", "块名", "插入数量", "外部参照"], top_blocks)

    lines.extend(["", "## 文字标注统计", ""])
    annotation_table = [
        [
            row.get("dwg", ""),
            row.get("entity_type", ""),
            row.get("label", ""),
            row.get("count", "0"),
        ]
        for row in annotation_rows
        if row.get("entity_type") != "__TOTAL__"
    ]
    _append_table(lines, ["DWG", "类型", "说明", "数量"], annotation_table)

    lines.extend(["", "## 外参和图片引用", ""])
    reference_table = [
        [
            row.get("dwg", ""),
            row.get("ref_type", ""),
            row.get("name", ""),
            row.get("path_or_source", ""),
            row.get("count", "0"),
        ]
        for row in xref_image_rows
        if row.get("ref_type") != "__TOTAL__"
    ]
    _append_table(lines, ["DWG", "类型", "名称", "路径/来源", "数量"], reference_table)

    lines.extend(["", "## 图框标题栏候选", ""])
    title_table = [
        [
            row.get("dwg", ""),
            row.get("block", ""),
            row.get("insert_count", "0"),
            row.get("has_attributes", ""),
            row.get("reason", ""),
        ]
        for row in title_block_rows
        if row.get("block") != "__TOTAL__"
    ]
    _append_table(lines, ["DWG", "块名", "插入次数", "有属性", "原因"], title_table)

    lines.extend(
        [
            "",
            "## 说明",
            "",
            "- 本报告由只读 CSV 报告汇总生成，不修改 DWG。",
            "- 原始 CSV 文件仍保留在同一目录，方便用 Excel 继续查看。",
        ]
    )
    output_path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return {
        "ok": True,
        "output": str(output_path),
        "report_files": [str(path) for path in report_files],
        "summary": {
            "layers": total_layers,
            "entities": total_entities,
            "blocks": total_blocks,
            "annotations": total_annotations,
            "references": total_references,
            "title_block_candidates": total_title_candidates,
            "csv_files": len(report_files),
        },
    }
