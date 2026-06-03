"""Read-only log keyword diagnostics for task troubleshooting."""

from __future__ import annotations


def _diagnostic(
    rule_id: str,
    severity: str,
    evidence: str,
    message: str,
    suggestion: str,
    log_path: str,
) -> dict:
    return {
        "rule_id": rule_id,
        "severity": severity,
        "evidence": evidence,
        "message": message,
        "suggestion": suggestion,
        "log_path": log_path,
    }


def diagnose_log_tails(log_tails: list[dict], error_code: str | None) -> list[dict]:
    diagnostics = []
    for item in log_tails:
        tail = item.get("tail", "")
        path = item.get("path", "")
        tail_lower = tail.lower()
        if "acad2027" in tail_lower and ("load 失败" in tail_lower or "load failed" in tail_lower):
            diagnostics.append(
                _diagnostic(
                    rule_id="acad_startup_noise",
                    severity="info",
                    evidence="acad2027 load message",
                    message=(
                        "AutoCAD startup emitted an acad2027 load message; this is "
                        "treated as startup noise unless another load error follows."
                    ),
                    suggestion="Focus on the later LISP load or accoreconsole error in the same log.",
                    log_path=path,
                )
            )
        if (
            "文件加载已取消" in tail
            or "load canceled" in tail_lower
            or ("load" in tail_lower and "failed" in tail_lower and "acad2027" not in tail_lower)
        ):
            diagnostics.append(
                _diagnostic(
                    rule_id="lisp_load_canceled",
                    severity="error",
                    evidence="LISP load canceled or failed",
                    message="AutoCAD canceled or failed the requested LISP load.",
                    suggestion=(
                        "Check the LISP path, file encoding, SECURELOAD/TRUSTEDPATHS "
                        "behavior, and whether the script can be loaded manually."
                    ),
                    log_path=path,
                )
            )
        if "acad2027.cfg" in tail_lower and ("只读" in tail or "锁定" in tail or "locked" in tail_lower):
            diagnostics.append(
                _diagnostic(
                    rule_id="acad_config_locked",
                    severity="error",
                    evidence="acad2027.cfg locked or read-only",
                    message="AutoCAD configuration appears locked or read-only.",
                    suggestion=(
                        "Close AutoCAD and accoreconsole processes, then check "
                        "acad2027.cfg permissions before retrying."
                    ),
                    log_path=path,
                )
            )
        if "未找到文件" in tail or "not found" in tail_lower:
            diagnostics.append(
                _diagnostic(
                    rule_id="referenced_file_missing",
                    severity="error",
                    evidence="file not found",
                    message="The log reports a missing referenced file.",
                    suggestion="Check the script path and every file path passed to accoreconsole.",
                    log_path=path,
                )
            )
        if error_code == "ACCORE_TIMEOUT" or "timed out" in tail_lower or "超时" in tail:
            diagnostics.append(
                _diagnostic(
                    rule_id="accore_timeout",
                    severity="error",
                    evidence="accoreconsole timeout",
                    message="accoreconsole appears to have timed out.",
                    suggestion=(
                        "Check whether the LISP script is waiting for input, stuck in a loop, "
                        "or processing too many drawings in one run."
                    ),
                    log_path=path,
                )
            )
        if (
            error_code == "ACCORE_NONZERO_EXIT"
            or "exited with code" in tail_lower
            or "returncode" in tail_lower
            or "返回非 0" in tail
        ):
            diagnostics.append(
                _diagnostic(
                    rule_id="accore_nonzero_exit",
                    severity="error",
                    evidence="accoreconsole non-zero exit",
                    message="accoreconsole returned a non-zero exit code.",
                    suggestion="Inspect the full accoreconsole log, stdout/stderr, and the first failed drawing.",
                    log_path=path,
                )
            )
        if (
            "secureload" in tail_lower
            or "trustedpaths" in tail_lower
            or "trusted path" in tail_lower
            or "不受信任" in tail
            or "安全加载" in tail
        ):
            diagnostics.append(
                _diagnostic(
                    rule_id="secure_load_blocked",
                    severity="error",
                    evidence="secure load or trusted path",
                    message="AutoCAD security loading rules may have blocked the script.",
                    suggestion=(
                        "Check SECURELOAD/TRUSTEDPATHS and prefer running scripts from a trusted "
                        "project or plugin directory."
                    ),
                    log_path=path,
                )
            )
    if not diagnostics and error_code:
        diagnostics.append(
            _diagnostic(
                rule_id="no_log_rule_match",
                severity="warning",
                evidence="",
                message="No known log keyword rule matched this error.",
                suggestion="Inspect the full log and task record before retrying.",
                log_path="",
            )
        )
    return diagnostics
