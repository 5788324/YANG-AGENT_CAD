"""Toolbox plugin manifest helpers."""

from __future__ import annotations

import json
from pathlib import Path

from . import error_codes

REQUIRED_FIELDS = {
    "id": str,
    "name": str,
    "version": str,
    "category": str,
    "tracks": list,
    "risk": str,
    "entry": dict,
}

VALID_TRACKS = {"A", "B", "C", "MCP"}


def validate_plugin_manifest(path: Path) -> dict:
    if not path.exists():
        return {
            "ok": False,
            "error_code": error_codes.FILE_NOT_FOUND,
            "path": str(path),
            "errors": ["plugin.json not found."],
        }
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        return {
            "ok": False,
            "error_code": error_codes.VALIDATION_FAILED,
            "path": str(path),
            "errors": [f"Invalid JSON: {exc}"],
        }

    errors = []
    for field, expected_type in REQUIRED_FIELDS.items():
        if field not in data:
            errors.append(f"Missing required field: {field}")
        elif not isinstance(data[field], expected_type):
            errors.append(f"Field {field} must be {expected_type.__name__}")

    tracks = data.get("tracks", [])
    if isinstance(tracks, list):
        invalid_tracks = [track for track in tracks if track not in VALID_TRACKS]
        if invalid_tracks:
            errors.append(f"Invalid tracks: {', '.join(invalid_tracks)}")

    entry = data.get("entry", {})
    if isinstance(entry, dict):
        if "type" not in entry:
            errors.append("entry.type is required")
        if "path" not in entry:
            errors.append("entry.path is required")
        else:
            entry_path = path.parent / entry["path"]
            if not entry_path.exists():
                errors.append(f"entry.path does not exist: {entry['path']}")

    return {
        "ok": not errors,
        "error_code": None if not errors else error_codes.VALIDATION_FAILED,
        "path": str(path),
        "errors": errors,
        "manifest": data,
    }


def list_plugins(project_root: Path) -> dict:
    plugins_root = project_root / "toolbox" / "plugins"
    results = []
    if not plugins_root.exists():
        return {"ok": True, "plugins": [], "plugins_root": str(plugins_root)}

    for manifest in sorted(plugins_root.glob("*/plugin.json")):
        result = validate_plugin_manifest(manifest)
        item = {
            "ok": result["ok"],
            "path": str(manifest),
            "errors": result["errors"],
        }
        data = result.get("manifest", {})
        if isinstance(data, dict):
            item.update(
                {
                    "id": data.get("id", ""),
                    "name": data.get("name", ""),
                    "version": data.get("version", ""),
                    "category": data.get("category", ""),
                    "tracks": data.get("tracks", []),
                    "risk": data.get("risk", ""),
                }
            )
        results.append(item)

    return {
        "ok": all(item["ok"] for item in results),
        "plugins_root": str(plugins_root),
        "plugins": results,
    }

