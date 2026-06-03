"""Central error code constants."""

UNKNOWN_ERROR = "UNKNOWN_ERROR"
VALIDATION_FAILED = "VALIDATION_FAILED"
VERIFY_FAILED = "VERIFY_FAILED"
USER_CONFIRMATION_REQUIRED = "USER_CONFIRMATION_REQUIRED"

MCP_TOOL_MISSING = "MCP_TOOL_MISSING"
MCP_SERVER_UNAVAILABLE = "MCP_SERVER_UNAVAILABLE"
MCP_TOOL_FAILED = "MCP_TOOL_FAILED"

ACAD_COM_UNAVAILABLE = "ACAD_COM_UNAVAILABLE"
ACAD_COM_DEPENDENCY_MISSING = "ACAD_COM_DEPENDENCY_MISSING"
ACAD_DOC_UNAVAILABLE = "ACAD_DOC_UNAVAILABLE"
ACAD_COMMAND_TIMEOUT = "ACAD_COMMAND_TIMEOUT"
ACAD_RPC_FAILED = "ACAD_RPC_FAILED"

LISP_VALIDATE_FAILED = "LISP_VALIDATE_FAILED"
LISP_LOAD_FAILED = "LISP_LOAD_FAILED"
LISP_RUNTIME_FAILED = "LISP_RUNTIME_FAILED"
LISP_INTERACTIVE_FORBIDDEN = "LISP_INTERACTIVE_FORBIDDEN"

ACCORE_NOT_FOUND = "ACCORE_NOT_FOUND"
ACCORE_NONZERO_EXIT = "ACCORE_NONZERO_EXIT"
ACCORE_TIMEOUT = "ACCORE_TIMEOUT"
ACCORE_UNSUPPORTED_SCRIPT = "ACCORE_UNSUPPORTED_SCRIPT"
ACCORE_CONFIG_LOCKED = "ACCORE_CONFIG_LOCKED"

DWG_LOCKED = "DWG_LOCKED"
FILE_NOT_FOUND = "FILE_NOT_FOUND"
BACKUP_FAILED = "BACKUP_FAILED"
ROLLBACK_FAILED = "ROLLBACK_FAILED"


ERROR_DETAILS = {
    UNKNOWN_ERROR: {
        "meaning": "Unclassified error.",
        "suggestion": "Inspect the task record and logs before retrying.",
        "severity": "error",
    },
    VALIDATION_FAILED: {
        "meaning": "Pre-run validation failed.",
        "suggestion": "Fix the parameters or script, then run a dry-run again.",
        "severity": "error",
    },
    VERIFY_FAILED: {
        "meaning": "Post-run verification failed.",
        "suggestion": "Inspect drawing state and consider rollback dry-run.",
        "severity": "error",
    },
    USER_CONFIRMATION_REQUIRED: {
        "meaning": "User confirmation is required before continuing.",
        "suggestion": "Wait for explicit user confirmation.",
        "severity": "warning",
    },
    MCP_TOOL_MISSING: {
        "meaning": "No MCP tool is available for this task.",
        "suggestion": "Use a temporary LISP path or add a new MCP tool.",
        "severity": "warning",
    },
    MCP_SERVER_UNAVAILABLE: {
        "meaning": "MCP server is unavailable.",
        "suggestion": "Restart or reconnect the MCP server.",
        "severity": "error",
    },
    MCP_TOOL_FAILED: {
        "meaning": "MCP tool execution failed.",
        "suggestion": "Inspect MCP tool output and task logs.",
        "severity": "error",
    },
    ACAD_COM_UNAVAILABLE: {
        "meaning": "AutoCAD COM automation is unavailable.",
        "suggestion": "Open AutoCAD and retry, or use the batch track.",
        "severity": "error",
    },
    ACAD_COM_DEPENDENCY_MISSING: {
        "meaning": "Python COM dependency is missing.",
        "suggestion": "Install pywin32 in the active Python environment.",
        "severity": "error",
    },
    ACAD_DOC_UNAVAILABLE: {
        "meaning": "No active AutoCAD document is available.",
        "suggestion": "Open a DWG in AutoCAD and retry.",
        "severity": "error",
    },
    ACAD_COMMAND_TIMEOUT: {
        "meaning": "AutoCAD command execution timed out.",
        "suggestion": "Check whether the command is waiting for interactive input.",
        "severity": "error",
    },
    ACAD_RPC_FAILED: {
        "meaning": "AutoCAD RPC call failed.",
        "suggestion": "Restart AutoCAD and retry after saving work.",
        "severity": "error",
    },
    LISP_VALIDATE_FAILED: {
        "meaning": "LISP static validation failed.",
        "suggestion": "Remove forbidden or incompatible LISP forms, then validate again.",
        "severity": "error",
    },
    LISP_LOAD_FAILED: {
        "meaning": "AutoCAD failed to load the LISP file.",
        "suggestion": "Check the script path, file encoding, secure load settings, and trusted paths.",
        "severity": "error",
    },
    LISP_RUNTIME_FAILED: {
        "meaning": "LISP failed while running.",
        "suggestion": "Inspect AutoCAD command output and task logs.",
        "severity": "error",
    },
    LISP_INTERACTIVE_FORBIDDEN: {
        "meaning": "Batch LISP contains interactive input.",
        "suggestion": "Rewrite the script so accoreconsole can run it without prompts or dialogs.",
        "severity": "error",
    },
    ACCORE_NOT_FOUND: {
        "meaning": "accoreconsole was not found.",
        "suggestion": "Check the AutoCAD installation path.",
        "severity": "error",
    },
    ACCORE_NONZERO_EXIT: {
        "meaning": "accoreconsole returned a non-zero exit code.",
        "suggestion": "Inspect stdout, stderr, and generated accoreconsole logs.",
        "severity": "error",
    },
    ACCORE_TIMEOUT: {
        "meaning": "accoreconsole execution timed out.",
        "suggestion": "Check for long-running scripts, dead loops, or blocked input.",
        "severity": "error",
    },
    ACCORE_UNSUPPORTED_SCRIPT: {
        "meaning": "The script is not suitable for headless accoreconsole execution.",
        "suggestion": "Rewrite it for batch mode or run it through the current drawing track.",
        "severity": "error",
    },
    ACCORE_CONFIG_LOCKED: {
        "meaning": "AutoCAD configuration is locked, missing, or not writable.",
        "suggestion": "Close AutoCAD processes and check acad2027.cfg permissions before retrying.",
        "severity": "error",
    },
    DWG_LOCKED: {
        "meaning": "DWG file is locked or in use.",
        "suggestion": "Close the drawing in AutoCAD and retry.",
        "severity": "error",
    },
    FILE_NOT_FOUND: {
        "meaning": "Required file was not found.",
        "suggestion": "Check the path and run a dry-run before executing.",
        "severity": "error",
    },
    BACKUP_FAILED: {
        "meaning": "Backup failed.",
        "suggestion": "Stop execution and check file permissions and disk space.",
        "severity": "error",
    },
    ROLLBACK_FAILED: {
        "meaning": "Rollback failed.",
        "suggestion": "Use the backup directory to restore files manually.",
        "severity": "error",
    },
}


def explain_error_code(code: str | None) -> dict:
    if not code:
        return {
            "code": None,
            "meaning": "No error code was recorded.",
            "suggestion": "No error-specific action is required.",
            "severity": "info",
        }
    detail = ERROR_DETAILS.get(code)
    if detail is None:
        detail = ERROR_DETAILS[UNKNOWN_ERROR]
    return {
        "code": code,
        "meaning": detail["meaning"],
        "suggestion": detail["suggestion"],
        "severity": detail["severity"],
    }
