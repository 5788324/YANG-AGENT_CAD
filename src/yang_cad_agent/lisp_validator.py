"""Static checks for AI-generated AutoLISP."""

from __future__ import annotations

import re
from pathlib import Path

from . import error_codes


FORBIDDEN_ALWAYS = {
    "shell": "Shell execution is not allowed in generated LISP.",
    "startapp": "Launching external applications is not allowed.",
    "vl-file-delete": "Deleting files from generated LISP is not allowed.",
}

FORBIDDEN_IN_ACCORE = {
    "vla-": "ActiveX vla-* functions are not available in accoreconsole.",
    "vlax-": "ActiveX vlax-* functions are not available in accoreconsole.",
    "vl-load-com": "COM loading is not allowed in accoreconsole scripts.",
    "getpoint": "Interactive point input is not available in accoreconsole.",
    "getstring": "Interactive string input is not available in accoreconsole.",
    "getint": "Interactive integer input is not available in accoreconsole.",
    "alert": "Dialog alerts are not available in accoreconsole.",
    "messagebox": "Dialogs are not available in accoreconsole.",
}


def _line_number(text: str, index: int) -> int:
    return text.count("\n", 0, index) + 1


def _find_token(text: str, token: str) -> list[dict]:
    matches = []
    for match in re.finditer(re.escape(token), text, flags=re.IGNORECASE):
        matches.append({"token": token, "line": _line_number(text, match.start())})
    return matches


def _find_interactive_ssget(text: str) -> list[dict]:
    findings = []
    for match in re.finditer(r"\(\s*ssget\s*\)", text, flags=re.IGNORECASE):
        findings.append(
            {
                "token": "ssget",
                "line": _line_number(text, match.start()),
                "message": "Interactive (ssget) is not available in accoreconsole.",
            }
        )
    return findings


def validate_lisp_text(text: str, target_track: str = "B") -> dict:
    """Validate LISP text for the target track."""
    findings = []
    lowered = text.lower()
    for token, message in FORBIDDEN_ALWAYS.items():
        for found in _find_token(lowered, token):
            findings.append({**found, "severity": "error", "message": message})

    if target_track.upper() == "C":
        for token, message in FORBIDDEN_IN_ACCORE.items():
            for found in _find_token(lowered, token):
                findings.append({**found, "severity": "error", "message": message})
        for found in _find_interactive_ssget(lowered):
            findings.append({**found, "severity": "error"})

    has_save = any(token in lowered for token in ['"_.qsave"', '"qsave"', '"_.saveas"', '"saveas"'])
    if target_track.upper() == "C" and not has_save:
        findings.append(
            {
                "token": "qsave/saveas",
                "line": None,
                "severity": "warning",
                "message": "Batch scripts should save explicitly with QSAVE or SAVEAS.",
            }
        )

    errors = [item for item in findings if item["severity"] == "error"]
    return {
        "ok": not errors,
        "target_track": target_track.upper(),
        "error_code": None if not errors else error_codes.LISP_VALIDATE_FAILED,
        "findings": findings,
    }


def validate_lisp_file(path: Path, target_track: str = "B") -> dict:
    if not path.exists():
        return {
            "ok": False,
            "target_track": target_track.upper(),
            "error_code": error_codes.FILE_NOT_FOUND,
            "findings": [],
            "message": f"LISP file not found: {path}",
        }
    result = validate_lisp_text(path.read_text(encoding="utf-8"), target_track=target_track)
    return {**result, "path": str(path)}

