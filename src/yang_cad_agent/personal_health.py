"""Beginner-friendly personal CAD health-check wrapper."""

from __future__ import annotations

from datetime import datetime
from pathlib import Path

from .health_check import run_health_check


DEFAULT_OUTPUT = Path(".agent/reports/PERSONAL_HEALTH_PLAN.md")


def _safe_get_step_result(step: dict) -> dict:
    result = step.get("result")
    return result if isinstance(result, dict) else {}


def _extract_file_count(result: dict) -> int:
    counts = []
    for step in result.get("steps", []):
        step_result = _safe_get_step_result(step)
        if "file_count" in step_result:
            counts.append(int(step_result.get("file_count") or 0))
    return max(counts) if counts else 0


def _append_step_table(lines: list[str], result: dict) -> None:
    lines.append("| 插件 | 状态 | 任务ID | 匹配DWG |")
    lines.append("| --- | --- | --- | --- |")
    steps = result.get("steps", [])
    if not steps:
        lines.append("| 无 | 无 | 无 | 0 |")
        return
    for step in steps:
        step_result = _safe_get_step_result(step)
        status = "成功" if step_result.get("ok") else "失败"
        lines.append(
            "| {plugin} | {status} | {task_id} | {file_count} |".format(
                plugin=step.get("plugin", ""),
                status=status,
                task_id=step_result.get("task_id", ""),
                file_count=step_result.get("file_count", 0),
            )
        )


def write_personal_health_report(
    result: dict,
    output_path: Path,
    folder: Path,
    pattern: str,
    recursive: bool,
    execute_command: str = "",
) -> dict:
    output_path.parent.mkdir(parents=True, exist_ok=True)
    mode = result.get("mode", "unknown")
    file_count = _extract_file_count(result)
    summary = result.get("summary") if isinstance(result.get("summary"), dict) else None
    lines = [
        "# 个人版 CAD 图纸体检",
        "",
        f"- 生成时间：{datetime.now().isoformat(timespec='seconds')}",
        f"- 模式：{'真实执行只读报告' if mode == 'execute' else '安全预演 dry-run'}",
        f"- 图纸目录：`{folder}`",
        f"- 匹配规则：`{pattern}`",
        f"- 递归扫描：{'是' if recursive else '否'}",
        f"- 匹配 DWG 数量：{file_count}",
        "",
        "## 当前结果",
        "",
        f"- 状态：{'通过' if result.get('ok') else '失败'}",
    ]
    if result.get("error_code"):
        lines.append(f"- 错误码：`{result.get('error_code')}`")
    if result.get("failed_plugin"):
        lines.append(f"- 失败插件：`{result.get('failed_plugin')}`")
    if result.get("message"):
        lines.append(f"- 提示：{result.get('message')}")

    lines.extend(["", "## 本次插件步骤", ""])
    _append_step_table(lines, result)

    lines.extend(["", "## 下一步", ""])
    if mode == "execute" and result.get("ok"):
        output = summary.get("output") if summary else ""
        lines.append("- 已完成只读报告生成，没有主动修改 DWG。")
        if output:
            lines.append(f"- 查看总报告：`{output}`")
        lines.append("- 如需回滚测试副本，可先运行对应任务的 rollback dry-run。")
    elif result.get("ok"):
        lines.append("- 这次只是预演，没有修改 DWG。")
        lines.append("- 确认匹配图纸无误后，再让 AI 对测试副本运行真实只读报告。")
        lines.append("- 不要直接对客户原图批量 `--execute`，先复制到临时目录。")
        if execute_command:
            lines.append("")
            lines.append("可对测试副本执行的命令：")
            lines.append("")
            lines.append("```cmd")
            lines.append(execute_command)
            lines.append("```")
    else:
        lines.append("- 不要继续执行。")
        lines.append("- 先把错误码交给 AI，用 `task_error_detail` 查看失败任务。")
        for step in result.get("steps", []):
            step_result = _safe_get_step_result(step)
            task_id = step_result.get("task_id")
            if task_id:
                lines.append(f"- 排障命令：`task_error_detail` / 任务ID `{task_id}`")

    lines.extend(
        [
            "",
            "## 安全说明",
            "",
            "- 默认 dry-run 不修改 DWG。",
            "- 真实批量执行前必须确认目录、备份和回滚路径。",
            "- DWG、PDF 和 `.agent` 运行产物不会提交到 GitHub。",
        ]
    )
    output_path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return {"ok": True, "output": str(output_path)}


def run_personal_health(
    project_root: Path,
    folder: Path | None = None,
    execute: bool = False,
    pattern: str = "*.dwg",
    recursive: bool = False,
    output: Path | None = None,
) -> dict:
    project_root = project_root.resolve()
    target_folder = folder or Path("sample")
    target_folder = target_folder if target_folder.is_absolute() else project_root / target_folder
    target_folder = target_folder.resolve()
    output_path = output or DEFAULT_OUTPUT
    output_path = output_path if output_path.is_absolute() else project_root / output_path
    output_path = output_path.resolve()

    result = run_health_check(
        project_root=project_root,
        folder=target_folder,
        execute=execute,
        pattern=pattern,
        recursive=recursive,
    )
    command_folder = str(target_folder)
    execute_command = (
        f"scripts\\personal-health-check.cmd \"{command_folder}\" --execute"
        if not execute
        else ""
    )
    report = write_personal_health_report(
        result=result,
        output_path=output_path,
        folder=target_folder,
        pattern=pattern,
        recursive=recursive,
        execute_command=execute_command,
    )
    return {
        "ok": result.get("ok", False),
        "mode": result.get("mode", "execute" if execute else "dry_run"),
        "folder": str(target_folder),
        "pattern": pattern,
        "recursive": recursive,
        "report": report,
        "health_check": result,
    }
