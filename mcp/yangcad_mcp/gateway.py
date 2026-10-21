from __future__ import annotations

import json
import os
import threading
import urllib.error
import urllib.request
import uuid
from dataclasses import dataclass
from pathlib import Path
from typing import Any

WRITE_TOOLS = {
    "project.revit.prepare",
    "project.image.embed",
    "cad.scale.convert",
    "cad.entity.update",
    "cad.entity.transform",
    "cad.entity.erase",
    "cad.layer.ensure",
    "cad.line.create",
    "cad.polyline.create",
    "cad.text.create",
    "cad.block.insert",
    "cad.polyline.clean",
    "cad.layer.cleanup",
    "cad.property.set",
    "cad.api.invoke",
    "cad.command.run",
    "cad.extension.invoke",
    "civil.structure.update",
    "civil.pipe.update",
}


@dataclass(frozen=True)
class Instance:
    process_id: int
    port: int
    token: str
    product: str
    version: str
    document: str
    document_id: str
    started_at: str
    updated_at: str = ""


@dataclass(frozen=True)
class Health:
    process_id: int
    port: int
    product: str
    version: str
    document: str
    document_id: str
    started_at: str
    updated_at: str
    tools: tuple[str, ...]


@dataclass(frozen=True)
class Binding:
    process_id: int
    document: str
    document_id: str
    started_at: str


_binding_lock = threading.Lock()
_call_lock = threading.Lock()
_binding: Binding | None = None


def _dir() -> Path:
    base = os.environ.get("LOCALAPPDATA")
    if not base:
        raise RuntimeError("LOCALAPPDATA 不存在。")
    return Path(base) / "YangCADAgent" / "instances"


def _instance_from_dict(data: dict[str, Any]) -> Instance:
    process_id = int(data["ProcessId"])
    port = int(data["Port"])
    token = str(data["Token"])
    if process_id <= 0 or not (1 <= port <= 65535) or not token:
        raise ValueError("invalid instance descriptor")
    return Instance(
        process_id=process_id,
        port=port,
        token=token,
        product=str(data.get("Product", "")),
        version=str(data.get("Version", "")),
        document=str(data.get("Document", "")),
        document_id=str(data.get("DocumentId", "")),
        started_at=str(data.get("StartedAt", "")),
        updated_at=str(data.get("UpdatedAt", "")),
    )


def discover_instances() -> list[Instance]:
    folder = _dir()
    if not folder.exists():
        raise RuntimeError("未发现 YangCAD Agent 实例。请先启动 AutoCAD/Civil 3D 并加载插件。")

    target_pid_text = os.environ.get("YANGCAD_INSTANCE_PID", "").strip()
    target_pid: int | None = None
    if target_pid_text:
        try:
            target_pid = int(target_pid_text)
        except ValueError as exc:
            raise RuntimeError("YANGCAD_INSTANCE_PID 必须是整数 PID。") from exc

    rows: list[tuple[float, Instance]] = []
    for path in folder.glob("*.json"):
        try:
            instance = _instance_from_dict(json.loads(path.read_text(encoding="utf-8")))
            if target_pid is not None and instance.process_id != target_pid:
                continue
            rows.append((path.stat().st_mtime, instance))
        except Exception:
            continue

    rows.sort(key=lambda row: row[0], reverse=True)
    if rows:
        return [row[1] for row in rows]
    if target_pid is not None:
        raise RuntimeError(f"未发现 PID={target_pid} 的 YangCAD instance JSON。")
    raise RuntimeError("没有有效 YangCAD instance JSON。")


def discover_instance() -> Instance:
    """兼容旧调用：返回最新、且 /health 可验证的实例。"""
    instance, _ = _select_healthy_instance()
    return instance


def _auth_headers(instance: Instance) -> dict[str, str]:
    return {"Authorization": f"Bearer {instance.token}"}



def _open_direct(request: urllib.request.Request, timeout: float | None):
    # Gateway 固定为 127.0.0.1，明确绕过系统/企业 HTTP 代理，避免本机 RPC 被代理设置劫持或阻断。
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
    return opener.open(request, timeout=timeout)

def _health(instance: Instance) -> Health:
    request = urllib.request.Request(
        f"http://127.0.0.1:{instance.port}/health",
        method="GET",
        headers=_auth_headers(instance),
    )
    try:
        with _open_direct(request, timeout=1.0) as response:
            data = json.loads(response.read().decode("utf-8"))
    except urllib.error.HTTPError as exc:
        raise RuntimeError(f"health HTTP {exc.code}") from exc
    except Exception as exc:
        raise RuntimeError(f"health unavailable: {exc}") from exc

    if not data.get("ok", False):
        raise RuntimeError("health rejected")
    health = Health(
        process_id=int(data.get("process_id", 0)),
        port=int(data.get("port", 0)),
        product=str(data.get("product", "")),
        version=str(data.get("version", "")),
        document=str(data.get("document", "")),
        document_id=str(data.get("document_id", "")),
        started_at=str(data.get("started_at", "")),
        updated_at=str(data.get("updated_at", "")),
        tools=tuple(str(item) for item in data.get("tools", [])),
    )
    if health.process_id != instance.process_id or health.port != instance.port:
        raise RuntimeError("health identity mismatch")
    if not health.started_at:
        raise RuntimeError("health missing process session identity")
    if health.document and not health.document_id:
        raise RuntimeError("health missing document session identity")
    if instance.started_at and instance.started_at != health.started_at:
        raise RuntimeError("health session mismatch")
    return health


def _select_healthy_instance(preferred_pid: int | None = None, required_tool: str | None = None) -> tuple[Instance, Health]:
    errors: list[str] = []
    instances = discover_instances()
    if preferred_pid is not None:
        instances = [item for item in instances if item.process_id == preferred_pid]
        if not instances:
            raise RuntimeError(f"绑定的 AutoCAD/Civil 3D 实例 PID={preferred_pid} 已不存在；请先执行读取 Tool 重新绑定。")

    for instance in instances:
        try:
            health = _health(instance)
            if required_tool is not None and required_tool not in health.tools:
                errors.append(f"PID {instance.process_id}/port {instance.port}: tool {required_tool} not registered")
                continue
            return instance, health
        except Exception as exc:
            errors.append(f"PID {instance.process_id}/port {instance.port}: {exc}")

    detail = "; ".join(errors[:4])
    if preferred_pid is not None:
        raise RuntimeError(f"绑定实例 PID={preferred_pid} 当前不可用；为避免把写操作切到其他 CAD 进程，本次拒绝。{(' ' + detail) if detail else ''}")
    raise RuntimeError(f"找到 instance JSON，但没有可连接且身份匹配的 YangCAD Gateway。{(' ' + detail) if detail else ''}")


def _get_binding() -> Binding | None:
    with _binding_lock:
        return _binding


def _set_binding(instance: Instance, health: Health) -> None:
    global _binding
    with _binding_lock:
        _binding = Binding(instance.process_id, health.document, health.document_id, health.started_at)


def _post(instance: Instance, tool: str, args: dict[str, Any], expected_document: str, expected_document_id: str) -> dict[str, Any]:
    payload = json.dumps(
        {
            "id": uuid.uuid4().hex,
            "tool": tool,
            "expectedDocument": expected_document,
            "expectedDocumentId": expected_document_id,
            "args": args,
        },
        ensure_ascii=False,
    ).encode("utf-8")
    request = urllib.request.Request(
        f"http://127.0.0.1:{instance.port}/rpc",
        data=payload,
        method="POST",
        headers={
            "Content-Type": "application/json; charset=utf-8",
            **_auth_headers(instance),
        },
    )
    try:
        # 不设置客户端读超时。排队超时由 C# Broker 负责；一旦请求已开始执行，
        # 客户端必须等待最终结果，避免“客户端超时后 DWG 又迟到写入”的不确定状态。
        with _open_direct(request, timeout=None) as response:
            result = json.loads(response.read().decode("utf-8"))
    except urllib.error.HTTPError as exc:
        body = exc.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"YangCAD Gateway HTTP {exc.code}: {body}") from exc
    except Exception as exc:
        raise RuntimeError(
            "YangCAD Gateway 连接在请求期间中断。若这是写 Tool，不要自动重试；先重新调用 cad.status / 读取 Tool 确认 DWG 状态。"
        ) from exc

    ok = result.get("Ok", result.get("ok", False))
    if not ok:
        raise RuntimeError(str(result.get("Error", result.get("error", "未知错误"))))
    return result.get("Data", result.get("data"))


def call(tool: str, args: dict[str, Any] | None = None) -> dict[str, Any]:
    # AutoCAD 主线程本身是串行执行模型；MCP 侧也串行化，避免并发读取互相覆盖 DWG/进程绑定。
    with _call_lock:
        return _call_serialized(tool, args)


def _call_serialized(tool: str, args: dict[str, Any] | None = None) -> dict[str, Any]:
    arguments = args or {}
    is_write = tool in WRITE_TOOLS

    if is_write:
        binding = _get_binding()
        if binding is None:
            raise RuntimeError(
                "为避免写错 DWG，MCP 写操作前必须先成功调用 cad.status、cad.document.info、cad.entity.get/query、cad.selection.read 或其他读取 Tool 建立当前图纸绑定。"
            )
        instance, health = _select_healthy_instance(preferred_pid=binding.process_id, required_tool=tool)
        if binding.started_at and health.started_at and binding.started_at != health.started_at:
            raise RuntimeError("绑定的 CAD 进程已重启；本次写操作拒绝。请先重新调用读取 Tool。")
        if (
            not binding.document
            or not binding.document_id
            or not health.document
            or not health.document_id
            or binding.document.lower() != health.document.lower()
            or binding.document_id != health.document_id
        ):
            raise RuntimeError(
                f"活动 DWG 已变化：MCP 绑定 '{binding.document or '<none>'}'，当前为 '{health.document or '<none>'}'；Document session 不匹配。"
                " 为避免写错图，本次写操作拒绝；请先在目标 DWG 上调用读取 Tool 重新绑定。"
            )
        return _post(instance, tool, arguments, binding.document, binding.document_id)

    instance, health = _select_healthy_instance(required_tool=tool)
    if not health.document:
        # Broker 会给出标准“无活动 DWG”错误；这里保留同一请求路径。
        expected_document = ""
    else:
        expected_document = health.document

    try:
        result = _post(instance, tool, arguments, expected_document, health.document_id)
    except RuntimeError as exc:
        # DocumentActivated/SaveAs 与 health -> rpc 之间存在极小竞态。读取 Tool 可安全重试一次；写 Tool 从不自动重试。
        if "活动 DWG 已变化" not in str(exc):
            raise
        instance, health = _select_healthy_instance(preferred_pid=instance.process_id, required_tool=tool)
        result = _post(instance, tool, arguments, health.document, health.document_id)

    _set_binding(instance, health)
    return result
