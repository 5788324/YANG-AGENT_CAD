from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import subprocess
import time
import uuid
from typing import Any, Iterable

SUPPORTED_YEARS = tuple(range(2021, 2028))
_MAX_FILES = 200
_PRODUCTS = {"autocad": "AutoCAD", "civil3d": "Civil3D"}


def _program_files() -> Path:
    value = os.environ.get("ProgramFiles") or os.environ.get("PROGRAMFILES")
    if not value:
        raise RuntimeError("ProgramFiles 环境变量不存在；Batch DWG 仅支持 Windows Autodesk 宿主。")
    return Path(value)


def _local_app_data() -> Path:
    value = os.environ.get("LOCALAPPDATA")
    if not value:
        raise RuntimeError("LOCALAPPDATA 环境变量不存在。")
    return Path(value)


def _validate_year(year: int) -> int:
    if year not in SUPPORTED_YEARS:
        raise ValueError("year 必须是 2021~2027。")
    return year



def _validate_product(product: str) -> str:
    key = str(product).strip().lower().replace(" ", "")
    if key not in _PRODUCTS:
        raise ValueError("product 必须是 AutoCAD 或 Civil3D。")
    return _PRODUCTS[key]


def _sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest().upper()


def _sha256_after_run(path: Path) -> str | None:
    try:
        return _sha256(path)
    except OSError:
        return None


def _resolve_accoreconsole(year: int) -> Path:
    override = os.environ.get("YANGCAD_ACCORECONSOLE", "").strip()
    path = Path(override) if override else _program_files() / "Autodesk" / f"AutoCAD {year}" / "AcCoreConsole.exe"
    return path


def _civil_marker(year: int) -> Path:
    return _program_files() / "Autodesk" / f"AutoCAD {year}" / "C3D" / "AeccDbMgd.dll"


def _resolve_worker(year: int) -> Path:
    override = os.environ.get("YANGCAD_BATCH_WORKER_DLL", "").strip()
    if override:
        return Path(override)
    machine = _program_files() / "Autodesk" / "ApplicationPlugins" / "YangCADAgent.bundle" / "Contents" / str(year) / "YangCAD.BatchWorker.dll"
    appdata = os.environ.get("APPDATA", "").strip()
    user = Path(appdata) / "Autodesk" / "ApplicationPlugins" / "YangCADAgent.bundle" / "Contents" / str(year) / "YangCAD.BatchWorker.dll" if appdata else None
    if machine.is_file():
        return machine
    if user is not None and user.is_file():
        return user
    return machine


def status(year: int = 2024, product: str = "AutoCAD") -> dict[str, Any]:
    year = _validate_year(year)
    product = _validate_product(product)
    console = _resolve_accoreconsole(year)
    worker = _resolve_worker(year)
    civil_marker = _civil_marker(year)
    product_available = product == "AutoCAD" or civil_marker.is_file()
    return {
        "available": console.is_file() and worker.is_file() and product_available,
        "year": year,
        "product": product,
        "architecture": "independent_accoreconsole_process",
        "uses_live_gateway": False,
        "accoreconsole_path": str(console),
        "accoreconsole_exists": console.is_file(),
        "worker_path": str(worker),
        "worker_exists": worker.is_file(),
        "civil3d_runtime_exists": civil_marker.is_file(),
        "product_available": product_available,
        "write_policy": "dry_run default; real hygiene writes output copies only; in-place overwrite forbidden",
    }


def _normalize_path(raw: str) -> Path:
    if not isinstance(raw, str) or not raw.strip():
        raise ValueError("DWG path 必须是非空字符串。")
    path = Path(raw.strip()).expanduser()
    if not path.is_absolute():
        raise ValueError(f"Batch DWG 只接受绝对路径：{raw}")
    return path


def _is_same_or_descendant(path: Path, parent: Path) -> bool:
    child_text = os.path.normcase(str(path.resolve(strict=False)))
    parent_text = os.path.normcase(str(parent.resolve(strict=False)))
    try:
        return os.path.commonpath([child_text, parent_text]) == parent_text
    except ValueError:
        return False


def _collect_paths(paths: list[str] | None, folder: str | None, recursive: bool, file_limit: int) -> list[Path]:
    if file_limit < 1 or file_limit > _MAX_FILES:
        raise ValueError(f"file_limit 必须在 1~{_MAX_FILES}。")
    if bool(paths) == bool(folder):
        raise ValueError("paths 与 folder 必须且只能提供一个。")

    candidates: Iterable[Path]
    if paths:
        candidates = (_normalize_path(item) for item in paths)
    else:
        root = _normalize_path(folder or "")
        if not root.is_dir():
            raise FileNotFoundError(f"folder 不存在：{root}")
        candidates = root.rglob("*.dwg") if recursive else root.glob("*.dwg")

    seen: set[str] = set()
    result: list[Path] = []
    for path in candidates:
        full = path.resolve(strict=False)
        key = os.path.normcase(str(full))
        if key in seen:
            continue
        seen.add(key)
        if not full.is_file():
            raise FileNotFoundError(f"DWG 不存在：{full}")
        if full.suffix.lower() != ".dwg":
            raise ValueError(f"仅支持 .dwg：{full}")
        result.append(full)
        if len(result) > file_limit:
            raise ValueError(f"匹配 DWG 超过 file_limit={file_limit}；请缩小范围或提高限制。")

    if not result:
        raise RuntimeError("没有找到可处理的 DWG。")
    return result


def _console_script(worker: Path) -> str:
    # FILEDIA=0 ensures NETLOAD accepts the following path as command-line input.
    # The AcCoreConsole QUIT prompt reads "discard all changes to this drawing? <N>":
    # answering N means "do NOT discard", so Core Console saves the dirty document
    # back onto its original input path at exit. The worker already saved the hygiene
    # copy via Database.SaveAs(output_path), so the script must answer Y (discard)
    # to keep the source DWG byte-identical (TASK-006C P0: never touch the input).
    dll = worker.as_posix().replace('"', '""')
    return "\n".join([
        "_.FILEDIA",
        "0",
        "_.NETLOAD",
        f'"{dll}"',
        "YANGCAD_BATCH_RUN",
        "_.QUIT",
        "_Y",
        "",
    ])


def _tail(text: str, limit: int = 4000) -> str:
    return text if len(text) <= limit else text[-limit:]


def _decode_process_output(value: Any) -> str:
    if value is None:
        return ""
    if isinstance(value, bytes):
        return value.decode("utf-8", errors="replace")
    return str(value)


def _remove_partial_output(output_path: Path | None) -> bool:
    if output_path is None or not output_path.exists():
        return False
    try:
        output_path.unlink()
        return True
    except OSError:
        return False


def _plan_outputs(files: list[Path], output_root: Path) -> dict[Path, Path]:
    # Preflight the entire real-write batch before launching any process. This prevents
    # a later collision/existing output from leaving earlier files already written.
    input_keys = {os.path.normcase(str(p.resolve(strict=False))) for p in files}
    used: set[str] = set()
    plan: dict[Path, Path] = {}
    existing: list[Path] = []

    for input_path in files:
        candidate = output_root / input_path.name
        seed = hashlib.sha256(str(input_path).encode("utf-8")).hexdigest()[:12]
        attempt = 0
        while True:
            key = os.path.normcase(str(candidate.resolve(strict=False)))
            if key not in used and key not in input_keys:
                break
            attempt += 1
            extra = "" if attempt == 1 else f".{attempt}"
            candidate = output_root / f"{input_path.stem}.{seed}{extra}{input_path.suffix}"
        used.add(key)
        if candidate.exists():
            existing.append(candidate)
        plan[input_path] = candidate

    if existing:
        preview = "; ".join(str(p) for p in existing[:5])
        extra = "" if len(existing) <= 5 else f"；另有 {len(existing) - 5} 个"
        raise FileExistsError(f"输出文件已存在，首版拒绝覆盖（整批启动前预检）：{preview}{extra}")
    return plan


def _run_one(
    input_path: Path,
    *,
    year: int,
    product: str,
    mode: str,
    dry_run: bool,
    output_path: Path | None,
    options: dict[str, Any],
    timeout_seconds: int,
    run_dir: Path,
) -> dict[str, Any]:
    console = _resolve_accoreconsole(year)
    worker = _resolve_worker(year)
    if not console.is_file():
        raise FileNotFoundError(f"AcCoreConsole.exe 不存在：{console}")
    if not worker.is_file():
        raise FileNotFoundError(f"YangCAD.BatchWorker.dll 不存在：{worker}；请先 Build + Deploy 对应年份。")
    if product == "Civil3D" and not _civil_marker(year).is_file():
        raise FileNotFoundError(f"Civil 3D {year} runtime 不存在：{_civil_marker(year)}")

    item_id = uuid.uuid4().hex
    item_dir = run_dir / item_id
    item_dir.mkdir(parents=True, exist_ok=False)
    job_path = item_dir / "job.json"
    result_path = item_dir / "result.json"
    script_path = item_dir / "run.scr"

    job = {
        "input_path": str(input_path),
        "result_path": str(result_path),
        "mode": mode,
        "dry_run": dry_run,
        "output_path": str(output_path) if output_path else None,
        "options": options,
    }
    job_path.write_text(json.dumps(job, ensure_ascii=False), encoding="utf-8")
    script_path.write_text(_console_script(worker), encoding="utf-8")

    env = os.environ.copy()
    env["YANGCAD_BATCH_JOB"] = str(job_path)
    command = [str(console), "/i", str(input_path), "/s", str(script_path)]
    if product == "Civil3D":
        command += ["/product", "C3D"]
    language = os.environ.get("YANGCAD_ACCORE_LANGUAGE", "").strip()
    if language:
        command += ["/l", language]
    if dry_run:
        command += ["/readonly"]
    input_sha_before = _sha256(input_path)
    started = time.monotonic()
    creationflags = getattr(subprocess, "CREATE_NO_WINDOW", 0)
    try:
        completed = subprocess.run(
            command,
            env=env,
            stdin=subprocess.DEVNULL,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=timeout_seconds,
            creationflags=creationflags,
            check=False,
        )
        timed_out = False
    except subprocess.TimeoutExpired as exc:
        output = _decode_process_output(exc.stdout) + _decode_process_output(exc.stderr)
        input_sha_after = _sha256_after_run(input_path)
        partial_removed = _remove_partial_output(output_path)
        return {
            "ok": False,
            "input_path": str(input_path),
            "error": f"AcCoreConsole timeout after {timeout_seconds}s",
            "timed_out": True,
            "partial_output_removed": partial_removed,
            "console_tail": _tail(output),
            "elapsed_ms": int((time.monotonic() - started) * 1000),
            "input_sha256_before": input_sha_before,
            "input_sha256_after": input_sha_after,
            "input_unchanged": input_sha_after is not None and input_sha_before == input_sha_after,
        }
    except OSError as exc:
        input_sha_after = _sha256_after_run(input_path)
        return {
            "ok": False,
            "input_path": str(input_path),
            "error": f"AcCoreConsole launch failed: {exc}",
            "timed_out": False,
            "partial_output_removed": _remove_partial_output(output_path),
            "console_tail": "",
            "elapsed_ms": int((time.monotonic() - started) * 1000),
            "input_sha256_before": input_sha_before,
            "input_sha256_after": input_sha_after,
            "input_unchanged": input_sha_after is not None and input_sha_before == input_sha_after,
        }

    elapsed_ms = int((time.monotonic() - started) * 1000)
    console_output = completed.stdout or ""
    input_sha_after = _sha256_after_run(input_path)
    if not result_path.is_file():
        return {
            "ok": False,
            "input_path": str(input_path),
            "error": "Batch worker 未生成 result.json。",
            "process_exit_code": completed.returncode,
            "timed_out": timed_out,
            "partial_output_removed": _remove_partial_output(output_path),
            "console_tail": _tail(console_output),
            "elapsed_ms": elapsed_ms,
            "input_sha256_before": input_sha_before,
            "input_sha256_after": input_sha_after,
            "input_unchanged": input_sha_after is not None and input_sha_before == input_sha_after,
        }

    try:
        result = json.loads(result_path.read_text(encoding="utf-8-sig"))
    except Exception as exc:
        return {
            "ok": False,
            "input_path": str(input_path),
            "error": f"result.json 无法解析：{exc}",
            "process_exit_code": completed.returncode,
            "partial_output_removed": _remove_partial_output(output_path),
            "console_tail": _tail(console_output),
            "elapsed_ms": elapsed_ms,
            "input_sha256_before": input_sha_before,
            "input_sha256_after": input_sha_after,
            "input_unchanged": input_sha_after is not None and input_sha_before == input_sha_after,
        }

    result["process_exit_code"] = completed.returncode
    result["console_tail"] = _tail(console_output)
    result["orchestrator_elapsed_ms"] = elapsed_ms
    result["product"] = product
    result["input_sha256_before"] = input_sha_before
    result["input_sha256_after"] = input_sha_after
    result["input_unchanged"] = input_sha_after is not None and input_sha_before == input_sha_after
    if input_sha_after is None:
        result["ok"] = False
        result["error"] = "P0: input DWG is missing/unreadable after batch execution; TASK-006C v1 must preserve the source drawing."
    elif input_sha_before != input_sha_after:
        result["ok"] = False
        result["error"] = "P0: input DWG hash changed; TASK-006C v1 must never overwrite the source drawing."
    if completed.returncode != 0:
        result["ok"] = False
        result["error"] = result.get("error") or f"AcCoreConsole exit code {completed.returncode}"
    if result.get("ok") and not dry_run and output_path is not None and not output_path.is_file():
        result["ok"] = False
        result["error"] = "Worker reported success but output DWG is missing."
    if output_path is not None and output_path.is_file():
        if result.get("ok"):
            result["output_sha256"] = _sha256(output_path)
        else:
            result["partial_output_removed"] = _remove_partial_output(output_path)
    return result


def _run_batch(
    *,
    paths: list[str] | None,
    folder: str | None,
    recursive: bool,
    file_limit: int,
    year: int,
    product: str,
    mode: str,
    dry_run: bool,
    allow_write: bool,
    output_dir: str | None,
    options: dict[str, Any],
    timeout_seconds: int,
    continue_on_error: bool,
) -> dict[str, Any]:
    year = _validate_year(year)
    product = _validate_product(product)
    if timeout_seconds < 10 or timeout_seconds > 3600:
        raise ValueError("timeout_seconds 必须在 10~3600。")
    files = _collect_paths(paths, folder, recursive, file_limit)

    output_root: Path | None = None
    output_plan: dict[Path, Path] = {}
    if mode == "hygiene" and not dry_run:
        if not allow_write:
            raise ValueError("real hygiene 需要显式 allow_write=true；用于避免 AI 误把 dry_run 切成真实文件写入。")
        if not output_dir:
            raise ValueError("real hygiene 必须提供 output_dir；TASK-006C 首版禁止原地覆盖输入 DWG。")
        output_root = _normalize_path(output_dir)
        if folder:
            source_root = _normalize_path(folder)
            if _is_same_or_descendant(output_root, source_root):
                raise ValueError("folder 模式的 real hygiene 要求 output_dir 位于输入 folder 之外；避免后续递归批处理重新吃入自己的输出。")
        output_root.mkdir(parents=True, exist_ok=True)
        output_plan = _plan_outputs(files, output_root)

    run_id = uuid.uuid4().hex
    run_dir = _local_app_data() / "YangCADAgent" / "batch" / run_id
    run_dir.mkdir(parents=True, exist_ok=False)

    items: list[dict[str, Any]] = []
    for input_path in files:
        output_path = output_plan.get(input_path)

        item = _run_one(
            input_path,
            year=year,
            mode=mode,
            product=product,
            dry_run=dry_run,
            output_path=output_path,
            options=options,
            timeout_seconds=timeout_seconds,
            run_dir=run_dir,
        )
        items.append(item)
        if not item.get("ok") and not continue_on_error:
            break

    failed = sum(1 for item in items if not item.get("ok"))
    result = {
        "ok": failed == 0,
        "run_id": run_id,
        "mode": mode,
        "dry_run": dry_run,
        "allow_write": allow_write,
        "year": year,
        "product": product,
        "uses_live_gateway": False,
        "process_model": "one AcCoreConsole process per DWG; sequential in TASK-006C v1",
        "requested_count": len(files),
        "processed_count": len(items),
        "succeeded": len(items) - failed,
        "failed": failed,
        "stopped_early": len(items) < len(files),
        "run_artifact_dir": str(run_dir),
        "items": items,
    }
    (run_dir / "summary.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    return result


def audit(
    paths: list[str] | None = None,
    folder: str | None = None,
    recursive: bool = False,
    file_limit: int = 50,
    year: int = 2024,
    product: str = "AutoCAD",
    entity_limit: int = 200000,
    issue_limit: int = 20,
    tolerance: float = 1e-6,
    timeout_seconds: int = 180,
    continue_on_error: bool = True,
) -> dict[str, Any]:
    if issue_limit < 0 or issue_limit > 200:
        raise ValueError("issue_limit 必须在 0~200；Batch 响应需保持有界。")
    if entity_limit < 1 or entity_limit > 1000000:
        raise ValueError("entity_limit 必须在 1~1000000。")
    if tolerance <= 0 or tolerance > 1.0:
        raise ValueError("tolerance 必须在 (0, 1.0]。")
    return _run_batch(
        paths=paths,
        folder=folder,
        recursive=recursive,
        file_limit=file_limit,
        year=year,
        product=product,
        mode="audit",
        dry_run=True,
        allow_write=False,
        output_dir=None,
        options={"entity_limit": entity_limit, "issue_limit": issue_limit, "tolerance": tolerance},
        timeout_seconds=timeout_seconds,
        continue_on_error=continue_on_error,
    )


def hygiene(
    paths: list[str] | None = None,
    folder: str | None = None,
    recursive: bool = False,
    file_limit: int = 50,
    year: int = 2024,
    product: str = "AutoCAD",
    cleanup_layers: bool = True,
    cleanup_polylines: bool = True,
    remove_collinear: bool = False,
    protect_defpoints: bool = True,
    allow_proxy_save: bool = False,
    tolerance: float = 1e-6,
    entity_limit: int = 200000,
    dry_run: bool = True,
    allow_write: bool = False,
    output_dir: str | None = None,
    timeout_seconds: int = 180,
    continue_on_error: bool = True,
) -> dict[str, Any]:
    if not cleanup_layers and not cleanup_polylines:
        raise ValueError("cleanup_layers 与 cleanup_polylines 至少启用一个。")
    if entity_limit < 1 or entity_limit > 1000000:
        raise ValueError("entity_limit 必须在 1~1000000。")
    if tolerance <= 0 or tolerance > 1.0:
        raise ValueError("tolerance 必须在 (0, 1.0]。")
    return _run_batch(
        paths=paths,
        folder=folder,
        recursive=recursive,
        file_limit=file_limit,
        year=year,
        product=product,
        mode="hygiene",
        dry_run=dry_run,
        allow_write=allow_write,
        output_dir=output_dir,
        options={
            "cleanup_layers": cleanup_layers,
            "cleanup_polylines": cleanup_polylines,
            "remove_collinear": remove_collinear,
            "protect_defpoints": protect_defpoints,
            "allow_proxy_save": allow_proxy_save,
            "tolerance": tolerance,
            "entity_limit": entity_limit,
        },
        timeout_seconds=timeout_seconds,
        continue_on_error=continue_on_error,
    )
