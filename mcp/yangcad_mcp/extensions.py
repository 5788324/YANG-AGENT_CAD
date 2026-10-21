from __future__ import annotations

import hashlib
import json
import os
import re
import shutil
import subprocess
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Literal
from xml.sax.saxutils import escape as xml_escape

SUPPORTED_YEARS = tuple(range(2021, 2028))
TARGET_PRODUCTS = {"AutoCAD", "Civil3D"}
MAX_BODY_CHARS = 120_000
MANIFEST_SCHEMA = 1

_NAME_RE = re.compile(r"^[A-Za-z][A-Za-z0-9_.-]{0,63}$")
_VERSION_RE = re.compile(r"^[0-9]+(?:\.[0-9]+){0,3}$")

# This is a conservative source-policy guard for AI-generated extension bodies,
# not a security sandbox. Manual DLLs placed in the legacy extension folder remain
# outside this builder policy and are identified as unmanaged by the live host.
_FORBIDDEN_PATTERNS: tuple[tuple[re.Pattern[str], str], ...] = tuple(
    (re.compile(pattern, re.IGNORECASE | re.MULTILINE), label)
    for pattern, label in [
        (r"\bDllImport\b|\bLibraryImport\b|\bMarshal\.", "P/Invoke / interop"),
        (r"\bunsafe\b|\bextern\s+", "unsafe / extern"),
        (r"\bSystem\.IO\b|\bFile\s*\.|\bDirectory\s*\.|\bFileStream\b", "filesystem access"),
        (r"\bSystem\.Net\b|\bHttpClient\b|\bWebClient\b|\bSocket\b", "network access"),
        (r"\bSystem\.Diagnostics\b|\bProcess\s*\.", "process execution"),
        (r"\bSystem\.Reflection\b|\bAssembly\s*\.|\bActivator\.CreateInstance\b", "reflection / dynamic assembly"),
        (r"\bType\s*\.\s*GetType\b|\.\s*GetType\s*\(|\.\s*GetMethod(?:s)?\s*\(|\.\s*GetProperty(?:ies)?\s*\(|\.\s*GetField(?:s)?\s*\(|\.\s*GetConstructor(?:s)?\s*\(|\.\s*InvokeMember\s*\(|\.\s*DynamicInvoke\s*\(|\bMethodInfo\b|\bConstructorInfo\b|\bdynamic\b", "reflection / dynamic invocation"),
        (r"\bSystem\.CodeDom\b|\bMicrosoft\.CSharp\b|\bSystem\.Linq\.Expressions\b", "runtime code generation"),
        (r"\bMicrosoft\.Win32\b|\bRegistry\b", "registry access"),
        (r"\bSystem\.Management\b", "system management"),
        (r"\bSystem\.Threading\b|\bTask\.Run\b|\bThread\b", "background threading"),
        (r"\bEnvironment\s*\.", "environment access"),
        (r"\bAppDomain\s*\.", "AppDomain access"),
        (r"\[\s*CommandMethod\b", "new AutoCAD command registration"),
        (r"\bSendStringToExecute\b|\bEditor\.Command\b|\bExecuteInCommandContextAsync\b", "command execution"),
        (r"\bLockDocument\s*\(|\bStartTransaction\s*\(|\bTransactionManager\b", "extension-owned lock/transaction"),
        (r"\(\s*OpenMode\s*\)\s*[-+]?\d+", "numeric OpenMode cast"),
        (r"\b\.Commit\s*\(|\b\.Abort\s*\(", "extension-owned commit/abort"),
        (r"\bDatabase\.SaveAs\b|\b\.SaveAs\s*\(", "DWG/file save"),
        (r"\bApplication\s*\.|\bAcadApplication\b|\bMdiActiveDocument\b", "global AutoCAD application access"),
        (r"\bDisableUndoRecording\s*\(|\bStartUndoMark\s*\(|\bEndUndoMark\s*\(", "undo recorder / undo mark control"),
        (r"\bCloseAndSave\s*\(|\bCloseAndDiscard\s*\(|\bcontext\s*\.\s*Document\s*\.\s*Close\s*\(", "document lifecycle control"),
        (r"\bDxfOut\s*\(|\bDwgOut\s*\(|\bReadDwgFile\s*\(", "direct DWG/DXF file I/O"),
        (r"\bSystemObjects\b|\bDynamicLinker\b|\bLoadModule\s*\(", "runtime module loading"),
        (r"\bHostApplicationServices\b", "global host application services"),
        (r"\bcontext\s*\.\s*(?:Transaction|Document|Database)\s*\.\s*Dispose\s*\(", "host-owned object disposal"),
        (r"^\s*#\s*(?:if|define|undef|line|pragma|warning|error|nullable|region|endregion)\b", "preprocessor directive"),
        (r"\\u[0-9a-fA-F]{4}|\\U[0-9a-fA-F]{8}", "unicode escape obfuscation"),
    ]
)


@dataclass(frozen=True)
class BuildEnvironment:
    year: int
    target_product: str
    dotnet: str
    acad_root: Path
    civil_root: Path | None
    host_dll: Path
    target_framework: str


def _local_appdata() -> Path:
    value = os.environ.get("LOCALAPPDATA", "").strip()
    if not value:
        raise RuntimeError("LOCALAPPDATA 不存在，无法定位 Extension 工作区。")
    return Path(value) / "YangCADAgent"


def workspace_root() -> Path:
    return _local_appdata() / "ExtensionWorkspace"


def install_root() -> Path:
    return _local_appdata() / "Extensions"


def _program_files() -> Path:
    value = os.environ.get("ProgramFiles") or os.environ.get("PROGRAMFILES")
    if not value:
        raise RuntimeError("ProgramFiles 不存在，无法定位 AutoCAD SDK/runtime。")
    return Path(value)


def _appdata() -> Path | None:
    value = os.environ.get("APPDATA", "").strip()
    return Path(value) if value else None


def _dotnet() -> str:
    override = os.environ.get("YANGCAD_DOTNET", "").strip()
    if override:
        path = Path(override)
        if not path.exists():
            raise RuntimeError(f"YANGCAD_DOTNET 指向不存在文件：{path}")
        return str(path)
    found = shutil.which("dotnet")
    if not found:
        raise RuntimeError("未找到 dotnet CLI；无法编译 Extension。")
    return found


def _target_framework(year: int) -> str:
    if year <= 2024:
        return "net48"
    if year <= 2026:
        return "net8.0-windows"
    return "net10.0-windows"


def _resolve_host_dll(year: int) -> Path:
    override = os.environ.get("YANGCAD_EXTENSION_HOST_DLL", "").strip()
    candidates: list[Path] = []
    if override:
        candidates.append(Path(override))

    program_files = _program_files()
    candidates.append(program_files / "Autodesk" / "ApplicationPlugins" / "YangCADAgent.bundle" / "Contents" / str(year) / "YangCAD.AutoCAD.dll")
    appdata = _appdata()
    if appdata is not None:
        candidates.append(appdata / "Autodesk" / "ApplicationPlugins" / "YangCADAgent.bundle" / "Contents" / str(year) / "YangCAD.AutoCAD.dll")

    repo = os.environ.get("YANGCAD_REPO_ROOT", "").strip()
    if repo:
        candidates.append(Path(repo) / "artifacts" / "bin" / str(year) / "AutoCAD" / "YangCAD.AutoCAD.dll")

    for candidate in candidates:
        if candidate.is_file():
            return candidate.resolve()
    joined = "\n  - ".join(str(item) for item in candidates)
    raise RuntimeError("未找到 YangCAD.AutoCAD.dll。请先部署对应年份 YangCAD 或设置 YANGCAD_EXTENSION_HOST_DLL。候选：\n  - " + joined)


def _resolve_environment(year: int, target_product: str) -> BuildEnvironment:
    if year not in SUPPORTED_YEARS:
        raise ValueError("year 必须为 2021~2027。")
    if target_product not in TARGET_PRODUCTS:
        raise ValueError("target_product 仅支持 AutoCAD / Civil3D。")

    acad_root = _program_files() / "Autodesk" / f"AutoCAD {year}"
    required = [acad_root / "AcCoreMgd.dll", acad_root / "AcDbMgd.dll", acad_root / "AcMgd.dll"]
    missing = [str(path) for path in required if not path.is_file()]
    if missing:
        raise RuntimeError("AutoCAD Managed API 不完整：" + "; ".join(missing))

    civil_root: Path | None = None
    if target_product == "Civil3D":
        civil_root = acad_root / "C3D"
        if not (civil_root / "AeccDbMgd.dll").is_file():
            raise RuntimeError(f"Civil 3D Managed API 不存在：{civil_root / 'AeccDbMgd.dll'}")
        aec_base = acad_root / "AecBaseMgd.dll"
        if not aec_base.is_file():
            aec_base = civil_root / "AecBaseMgd.dll"
        if not aec_base.is_file():
            raise RuntimeError("Civil 3D AecBaseMgd.dll 不存在。")

    return BuildEnvironment(
        year=year,
        target_product=target_product,
        dotnet=_dotnet(),
        acad_root=acad_root.resolve(),
        civil_root=civil_root.resolve() if civil_root else None,
        host_dll=_resolve_host_dll(year),
        target_framework=_target_framework(year),
    )


def _sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest().upper()


def _sha256_file(path: Path) -> str:
    hasher = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            hasher.update(block)
    return hasher.hexdigest().upper()


def _slug(name: str) -> str:
    if not _NAME_RE.fullmatch(name):
        raise ValueError("name 必须以英文字母开头，仅包含字母/数字/._-，最大 64 字符。")
    return re.sub(r"[^A-Za-z0-9]+", "_", name).strip("_")


def _version(value: str) -> str:
    if not _VERSION_RE.fullmatch(value):
        raise ValueError("version 必须是 1~4 段数字版本，例如 1.0.0。")
    return value


def _csharp_string(value: str) -> str:
    return '"' + value.replace("\\", "\\\\").replace('"', '\\"').replace("\r", "\\r").replace("\n", "\\n") + '"'




def _validate_body_structure(body: str) -> None:
    """Ensure the supplied text remains a method body instead of escaping the generated wrapper."""
    if re.search(r"\b(?:namespace|class|struct|interface|record|enum|delegate)\b", body):
        raise ValueError("Extension source policy rejected: type/namespace declaration is not allowed inside execute_body")
    if re.search(r'(?:\$@|@\$|\$)"', body):
        raise ValueError("Extension source policy rejected: interpolated string literal is not supported in execute_body")
    if '"""' in body:
        raise ValueError("Extension source policy rejected: C# raw string literal is not supported in execute_body")

    depth = 0
    i = 0
    state = "normal"
    while i < len(body):
        ch = body[i]
        nxt = body[i + 1] if i + 1 < len(body) else ""

        if state == "normal":
            if ch == '/' and nxt == '/':
                state = "line_comment"; i += 2; continue
            if ch == '/' and nxt == '*':
                state = "block_comment"; i += 2; continue
            if ch == '@' and nxt == '"':
                state = "verbatim_string"; i += 2; continue
            if ch == '$' and nxt == '"':
                state = "string"; i += 2; continue
            if ch == '$' and nxt == '@' and i + 2 < len(body) and body[i + 2] == '"':
                state = "verbatim_string"; i += 3; continue
            if ch == '@' and nxt == '$' and i + 2 < len(body) and body[i + 2] == '"':
                state = "verbatim_string"; i += 3; continue
            if ch == '"':
                state = "string"; i += 1; continue
            if ch == "'":
                state = "char"; i += 1; continue
            if ch == '{':
                depth += 1
            elif ch == '}':
                depth -= 1
                if depth < 0:
                    raise ValueError("Extension source policy rejected: execute_body escapes the generated method scope")
            i += 1
            continue

        if state == "line_comment":
            if ch in "\r\n": state = "normal"
            i += 1; continue
        if state == "block_comment":
            if ch == '*' and nxt == '/': state = "normal"; i += 2; continue
            i += 1; continue
        if state == "string":
            if ch == '\\': i += 2; continue
            if ch == '"': state = "normal"
            i += 1; continue
        if state == "char":
            if ch == '\\': i += 2; continue
            if ch == "'": state = "normal"
            i += 1; continue
        if state == "verbatim_string":
            if ch == '"' and nxt == '"': i += 2; continue
            if ch == '"': state = "normal"
            i += 1; continue

    if state in {"block_comment", "string", "char", "verbatim_string"}:
        raise ValueError("Extension source policy rejected: unterminated comment/string in execute_body")
    if depth != 0:
        raise ValueError("Extension source policy rejected: execute_body has unbalanced braces")


def _code_projection(body: str) -> str:
    """Return code with comments/string/char contents masked, preserving positions/newlines."""
    out = list(body)
    i = 0
    state = "normal"
    while i < len(body):
        ch = body[i]
        nxt = body[i + 1] if i + 1 < len(body) else ""
        if state == "normal":
            if ch == '/' and nxt == '/':
                out[i] = out[i + 1] = ' '; state = "line_comment"; i += 2; continue
            if ch == '/' and nxt == '*':
                out[i] = out[i + 1] = ' '; state = "block_comment"; i += 2; continue
            if ch == '@' and nxt == '"':
                out[i] = out[i + 1] = ' '; state = "verbatim_string"; i += 2; continue
            if ch == '"':
                out[i] = ' '; state = "string"; i += 1; continue
            if ch == "'":
                out[i] = ' '; state = "char"; i += 1; continue
            i += 1; continue
        if state == "line_comment":
            if ch in "\r\n": state = "normal"
            else: out[i] = ' '
            i += 1; continue
        if state == "block_comment":
            if ch == '*' and nxt == '/':
                out[i] = out[i + 1] = ' '; state = "normal"; i += 2; continue
            if ch not in "\r\n": out[i] = ' '
            i += 1; continue
        if state == "string":
            if ch == '\\':
                out[i] = ' ';
                if i + 1 < len(body): out[i + 1] = ' '
                i += 2; continue
            if ch == '"': state = "normal"
            out[i] = ' '; i += 1; continue
        if state == "char":
            if ch == '\\':
                out[i] = ' ';
                if i + 1 < len(body): out[i + 1] = ' '
                i += 2; continue
            if ch == "'": state = "normal"
            out[i] = ' '; i += 1; continue
        if state == "verbatim_string":
            if ch == '"' and nxt == '"':
                out[i] = out[i + 1] = ' '; i += 2; continue
            if ch == '"': state = "normal"
            if ch not in "\r\n": out[i] = ' '
            i += 1; continue
    return ''.join(out)


def _scan_body(body: str, *, is_write: bool, requires_erase: bool) -> list[str]:
    if not isinstance(body, str) or not body.strip():
        raise ValueError("execute_body 不能为空。")
    if len(body) > MAX_BODY_CHARS:
        raise ValueError(f"execute_body 过长；最大 {MAX_BODY_CHARS} 字符。")
    if "\x00" in body:
        raise ValueError("execute_body 包含 NUL。")
    _validate_body_structure(body)

    code = _code_projection(body)
    findings: list[str] = []
    for regex, label in _FORBIDDEN_PATTERNS:
        if regex.search(body):
            findings.append(label)

    erase_used = bool(re.search(r"\.\s*Erase\s*\(", code, re.IGNORECASE))
    if erase_used and not requires_erase:
        findings.append("Erase used without requires_erase=true")
    if requires_erase and not is_write:
        findings.append("requires_erase=true requires is_write=true")

    if not is_write:
        if re.search(r"\bOpenMode\.ForWrite\b|\.\s*UpgradeOpen\s*\(|\.\s*Erase\s*\(", code, re.IGNORECASE):
            findings.append("read extension requests write access")
    else:
        if not re.search(r"\bcontext\s*\.\s*RecordChange\s*\(", code):
            findings.append("write extension must call context.RecordChange(...) for preview/diff evidence")

    if findings:
        unique = list(dict.fromkeys(findings))
        raise ValueError("Extension source policy rejected: " + "; ".join(unique))
    return findings


def _generated_source(
    *,
    name: str,
    version: str,
    operation: str,
    description: str,
    body: str,
    is_write: bool,
    requires_erase: bool,
    target_product: str,
) -> str:
    usings = [
        "using System;",
        "using System.Collections.Generic;",
        "using Autodesk.AutoCAD.ApplicationServices;",
        "using Autodesk.AutoCAD.DatabaseServices;",
        "using Autodesk.AutoCAD.Geometry;",
        "using YangCAD.AutoCADHost.Mcp.Extensions;",
    ]
    if target_product == "Civil3D":
        usings.extend([
            "using Autodesk.Civil.ApplicationServices;",
            "using Autodesk.Civil.DatabaseServices;",
        ])

    indented_body = "\n".join("            " + line for line in body.strip().splitlines())
    return f"""{os.linesep.join(usings)}

namespace YangCAD.GeneratedExtensions
{{
    public sealed class GeneratedOperation : IYangCadExtensionOperation
    {{
        public YangCadExtensionDescriptor Descriptor {{ get; }} = new YangCadExtensionDescriptor
        {{
            Name = {_csharp_string(operation)},
            Version = {_csharp_string(version)},
            Description = {_csharp_string(description)},
            IsWrite = {str(is_write).lower()},
            SupportsDryRun = true,
            RequiresErase = {str(requires_erase).lower()}
        }};

        public object Execute(YangCadExtensionContext context)
        {{
{indented_body}
        }}
    }}
}}
"""


def _xml_path(path: Path) -> str:
    return xml_escape(str(path))


def _csproj(env: BuildEnvironment, assembly_name: str, version: str) -> str:
    refs = [
        ("AcCoreMgd", env.acad_root / "AcCoreMgd.dll"),
        ("AcDbMgd", env.acad_root / "AcDbMgd.dll"),
        ("AcMgd", env.acad_root / "AcMgd.dll"),
        ("YangCAD.AutoCAD", env.host_dll),
    ]
    if env.target_product == "Civil3D":
        assert env.civil_root is not None
        aec_base = env.acad_root / "AecBaseMgd.dll"
        if not aec_base.is_file():
            aec_base = env.civil_root / "AecBaseMgd.dll"
        refs.extend([
            ("AecBaseMgd", aec_base),
            ("AeccDbMgd", env.civil_root / "AeccDbMgd.dll"),
        ])
        pressure = env.civil_root / "AeccPressurePipesMgd.dll"
        if pressure.is_file():
            refs.append(("AeccPressurePipesMgd", pressure))

    ref_xml = "\n".join(
        f'    <Reference Include="{name}"><HintPath>{_xml_path(path)}</HintPath><Private>false</Private></Reference>'
        for name, path in refs
    )
    numeric = version.split(".") + ["0"] * (4 - len(version.split(".")))
    asm_version = ".".join(numeric[:4])
    return f"""<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>{env.target_framework}</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <PlatformTarget>x64</PlatformTarget>
    <Deterministic>true</Deterministic>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <AssemblyName>{xml_escape(assembly_name)}</AssemblyName>
    <RootNamespace>YangCAD.GeneratedExtensions</RootNamespace>
    <Version>{version}</Version>
    <FileVersion>{asm_version}</FileVersion>
    <AssemblyVersion>{asm_version}</AssemblyVersion>
    <OutputPath>out/</OutputPath>
  </PropertyGroup>
  <ItemGroup>
{ref_xml}
  </ItemGroup>
</Project>
"""


def _run_dotnet_build(project: Path, dotnet: str, timeout_seconds: int) -> tuple[str, str]:
    try:
        completed = subprocess.run(
            [dotnet, "build", str(project), "-c", "Release", "--nologo"],
            cwd=str(project.parent),
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=timeout_seconds,
            check=False,
            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
        )
    except subprocess.TimeoutExpired as exc:
        raise RuntimeError(f"Extension 编译超时（{timeout_seconds}s）。") from exc
    if completed.returncode != 0:
        stdout_tail = "\n".join(completed.stdout.splitlines()[-40:])
        stderr_tail = "\n".join(completed.stderr.splitlines()[-40:])
        detail = (stdout_tail + "\n" + stderr_tail).strip()
        raise RuntimeError("Extension 编译失败：\n" + detail[:12_000])
    return completed.stdout, completed.stderr


def _read_manifest(path: Path) -> dict[str, Any]:
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except Exception as exc:
        raise RuntimeError(f"Extension manifest 无效：{path}: {exc}") from exc
    if int(data.get("SchemaVersion", 0)) != MANIFEST_SCHEMA:
        raise RuntimeError(f"不支持的 Extension manifest schema：{data.get('SchemaVersion')}")
    return data


def status(year: int = 2024, target_product: Literal["AutoCAD", "Civil3D"] = "AutoCAD") -> dict[str, Any]:
    base: dict[str, Any] = {
        "workflow_version": 1,
        "year": year,
        "target_product": target_product,
        "live_load_tool": "cad.extension.list",
        "live_invoke_tool": "cad.extension.invoke",
        "uses_live_gateway_for_build": False,
        "source_policy": "conservative_generated-body_policy_not_a_sandbox",
        "write_preview_contract": "Host Transaction + dry_run Abort + context.RecordChange ledger",
    }
    try:
        base["workspace"] = str(workspace_root())
        base["install_folder"] = str(install_root())
        env = _resolve_environment(year, target_product)
        base.update({
            "available": True,
            "dotnet": env.dotnet,
            "acad_root": str(env.acad_root),
            "civil_root": str(env.civil_root) if env.civil_root else None,
            "host_dll": str(env.host_dll),
            "target_framework": env.target_framework,
        })
    except Exception as exc:
        base.update({"available": False, "error": str(exc)})
    return base


def build(
    *,
    name: str,
    version: str,
    description: str,
    execute_body: str,
    year: int = 2024,
    target_product: Literal["AutoCAD", "Civil3D"] = "AutoCAD",
    is_write: bool = True,
    requires_erase: bool = False,
    timeout_seconds: int = 120,
) -> dict[str, Any]:
    slug = _slug(name)
    version = _version(version)
    if not description or len(description) > 500:
        raise ValueError("description 必须为 1~500 字符。")
    if timeout_seconds < 10 or timeout_seconds > 600:
        raise ValueError("timeout_seconds 必须为 10~600。")
    _scan_body(execute_body, is_write=is_write, requires_erase=requires_erase)
    env = _resolve_environment(year, target_product)

    body_sha = _sha256_bytes(execute_body.encode("utf-8"))
    fingerprint_payload = json.dumps({
        "name": name, "version": version, "description": description, "execute_body": execute_body,
        "year": year, "target_product": target_product, "is_write": is_write, "requires_erase": requires_erase,
    }, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")
    fingerprint_sha = _sha256_bytes(fingerprint_payload)
    short_sha = fingerprint_sha[:10].lower()
    version_token = version.replace(".", "_")
    operation = f"agent.{slug.lower()}.v{version_token}.{short_sha}"
    assembly_name = f"YangCAD.Extension.{slug}.v{version_token}.{short_sha}"
    build_id = f"{slug}-v{version_token}-{short_sha}-y{year}-{target_product.lower()}"

    root = workspace_root()
    root.mkdir(parents=True, exist_ok=True)
    final_dir = root / build_id
    if final_dir.exists():
        manifest_path = final_dir / "manifest.json"
        if manifest_path.is_file():
            manifest = _read_manifest(manifest_path)
            dll = final_dir / "out" / str(manifest.get("DllFile", ""))
            source_existing = final_dir / "GeneratedOperation.cs"
            if (dll.is_file() and source_existing.is_file()
                    and _sha256_file(dll) == str(manifest.get("DllSha256", ""))
                    and _sha256_file(source_existing) == str(manifest.get("SourceSha256", ""))
                    and str(manifest.get("BuildFingerprintSha256", "")) == fingerprint_sha):
                return {
                    "ok": True,
                    "reused": True,
                    "build_id": build_id,
                    "operation": operation,
                    "dll": str(dll),
                    "dll_sha256": manifest["DllSha256"],
                    "source_sha256": manifest["SourceSha256"],
                    "manifest": str(manifest_path),
                    "next": "调用 extension.install(..., allow_install=true)，随后在 CAD 控制中心开启 extension 权限并调用 cad.extension.list/invoke。",
                }
        raise RuntimeError(f"Extension workspace 已存在但不完整/哈希不符：{final_dir}")

    temp_dir = root / (build_id + ".tmp")
    if temp_dir.exists():
        shutil.rmtree(temp_dir, ignore_errors=True)
    temp_dir.mkdir(parents=True)

    try:
        source = _generated_source(
            name=name,
            version=version,
            operation=operation,
            description=description,
            body=execute_body,
            is_write=is_write,
            requires_erase=requires_erase,
            target_product=target_product,
        )
        source_path = temp_dir / "GeneratedOperation.cs"
        project_path = temp_dir / "GeneratedExtension.csproj"
        source_path.write_text(source, encoding="utf-8")
        project_path.write_text(_csproj(env, assembly_name, version), encoding="utf-8")

        _run_dotnet_build(project_path, env.dotnet, timeout_seconds)
        built_dll = temp_dir / "out" / (assembly_name + ".dll")
        if not built_dll.is_file():
            raise RuntimeError(f"dotnet build 返回成功，但未生成 DLL：{built_dll}")

        dll_name = built_dll.name
        source_sha = _sha256_file(source_path)
        dll_sha = _sha256_file(built_dll)
        manifest = {
            "SchemaVersion": MANIFEST_SCHEMA,
            "ManagedBy": "YangCAD Agent TASK-007",
            "BuildId": build_id,
            "Name": name,
            "Version": version,
            "Operation": operation,
            "Description": description,
            "Year": year,
            "TargetProduct": target_product,
            "TargetFramework": env.target_framework,
            "IsWrite": is_write,
            "SupportsDryRun": True,
            "RequiresErase": requires_erase,
            "SourceSha256": source_sha,
            "ExecuteBodySha256": body_sha,
            "BuildFingerprintSha256": fingerprint_sha,
            "DllSha256": dll_sha,
            "DllFile": dll_name,
            "AssemblyName": assembly_name,
            "CreatedAt": datetime.now(timezone.utc).isoformat(),
            "HostDllSha256": _sha256_file(env.host_dll),
            "SourcePolicy": "generated-body-v1",
        }
        (temp_dir / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
        # Keep only deterministic source/build artifacts; remove obj to keep workspace small.
        shutil.rmtree(temp_dir / "obj", ignore_errors=True)
        temp_dir.rename(final_dir)
    except Exception:
        shutil.rmtree(temp_dir, ignore_errors=True)
        raise

    return {
        "ok": True,
        "reused": False,
        "build_id": build_id,
        "operation": operation,
        "year": year,
        "target_product": target_product,
        "is_write": is_write,
        "requires_erase": requires_erase,
        "dll": str(final_dir / "out" / (assembly_name + ".dll")),
        "dll_sha256": dll_sha,
        "source_sha256": source_sha,
        "manifest": str(final_dir / "manifest.json"),
        "next": "调用 extension.install(..., allow_install=true)，随后在 CAD 控制中心开启 extension 权限并调用 cad.extension.list，再用 cad.extension.invoke(dry_run=true) 预览。",
    }


def install(*, build_id: str, allow_install: bool = False) -> dict[str, Any]:
    if not allow_install:
        raise ValueError("安装 Extension 需要显式 allow_install=true。")
    if not build_id or Path(build_id).name != build_id or ".." in build_id:
        raise ValueError("build_id 非法。")

    build_dir = workspace_root() / build_id
    manifest_path = build_dir / "manifest.json"
    if not manifest_path.is_file():
        raise FileNotFoundError(f"未找到 build manifest：{manifest_path}")
    manifest = _read_manifest(manifest_path)
    dll = build_dir / "out" / str(manifest.get("DllFile", ""))
    if not dll.is_file():
        raise FileNotFoundError(f"未找到 build DLL：{dll}")
    actual_sha = _sha256_file(dll)
    expected_sha = str(manifest.get("DllSha256", ""))
    if actual_sha != expected_sha:
        raise RuntimeError("Build DLL SHA-256 与 manifest 不一致，拒绝安装。")

    root = install_root()
    root.mkdir(parents=True, exist_ok=True)
    target_dll = root / dll.name
    target_manifest = root / (dll.name + ".yangcad.json")
    if target_dll.exists() or target_manifest.exists():
        raise FileExistsError(f"目标版本已存在；版本化 Extension 不允许覆盖：{target_dll}")

    installed_manifest = dict(manifest)
    installed_manifest["InstalledAt"] = datetime.now(timezone.utc).isoformat()
    installed_manifest["InstalledDll"] = str(target_dll)

    temp_dll = target_dll.with_suffix(target_dll.suffix + ".tmp")
    temp_manifest = target_manifest.with_suffix(target_manifest.suffix + ".tmp")
    manifest_committed = False
    try:
        shutil.copyfile(dll, temp_dll)
        if _sha256_file(temp_dll) != expected_sha:
            raise RuntimeError("安装临时 DLL SHA-256 校验失败。")
        temp_manifest.write_text(json.dumps(installed_manifest, ensure_ascii=False, indent=2), encoding="utf-8")
        # Commit manifest first. ExtensionHost scans *.dll, so there is never a window
        # where the new DLL is visible without its integrity/capability manifest.
        temp_manifest.replace(target_manifest)
        manifest_committed = True
        if target_dll.exists():
            raise FileExistsError(f"目标版本在安装期间出现，拒绝覆盖：{target_dll}")
        temp_dll.replace(target_dll)
    except Exception:
        if manifest_committed and not target_dll.exists():
            try:
                target_manifest.unlink()
            except Exception:
                pass
        raise
    finally:
        for temp in (temp_dll, temp_manifest):
            try:
                if temp.exists():
                    temp.unlink()
            except Exception:
                pass

    return {
        "ok": True,
        "build_id": build_id,
        "operation": manifest["Operation"],
        "dll": str(target_dll),
        "manifest": str(target_manifest),
        "dll_sha256": expected_sha,
        "load_note": "DLL 已安装但不会由本地 builder 直接 Load。请在目标 CAD 进程显式开启 cad.extension.invoke 权限，然后调用 cad.extension.list 由 Host 校验 manifest/SHA 后加载。",
        "preview_note": "首次调用建议 cad.extension.invoke(..., dry_run=true)；Host 会 Abort 写事务并返回 extension-reported change ledger。",
    }


def artifact_list(limit: int = 200) -> dict[str, Any]:
    if limit < 1 or limit > 2000:
        raise ValueError("limit 必须为 1~2000。")
    work_rows: list[dict[str, Any]] = []
    root = workspace_root()
    if root.exists():
        for manifest_path in sorted(root.glob("*/manifest.json"), key=lambda p: p.stat().st_mtime, reverse=True)[:limit]:
            try:
                data = _read_manifest(manifest_path)
                dll = manifest_path.parent / "out" / str(data.get("DllFile", ""))
                work_rows.append({
                    "build_id": data.get("BuildId"),
                    "operation": data.get("Operation"),
                    "version": data.get("Version"),
                    "year": data.get("Year"),
                    "target_product": data.get("TargetProduct"),
                    "is_write": data.get("IsWrite"),
                    "requires_erase": data.get("RequiresErase"),
                    "dll": str(dll),
                    "dll_exists": dll.is_file(),
                    "dll_sha256_matches": dll.is_file() and _sha256_file(dll) == data.get("DllSha256"),
                    "created_at": data.get("CreatedAt"),
                })
            except Exception as exc:
                work_rows.append({"manifest": str(manifest_path), "error": str(exc)})

    installed_rows: list[dict[str, Any]] = []
    install = install_root()
    if install.exists():
        manifests = sorted(install.glob("*.dll.yangcad.json"), key=lambda p: p.stat().st_mtime, reverse=True)
        for manifest_path in manifests[:limit]:
            try:
                data = _read_manifest(manifest_path)
                dll = install / str(data.get("DllFile", ""))
                installed_rows.append({
                    "build_id": data.get("BuildId"),
                    "operation": data.get("Operation"),
                    "version": data.get("Version"),
                    "year": data.get("Year"),
                    "target_product": data.get("TargetProduct"),
                    "dll": str(dll),
                    "dll_exists": dll.is_file(),
                    "dll_sha256_matches": dll.is_file() and _sha256_file(dll) == data.get("DllSha256"),
                    "installed_at": data.get("InstalledAt"),
                })
            except Exception as exc:
                installed_rows.append({"manifest": str(manifest_path), "error": str(exc)})

    return {
        "workspace": str(root),
        "install_folder": str(install),
        "build_count": len(work_rows),
        "installed_count": len(installed_rows),
        "builds": work_rows,
        "installed": installed_rows,
        "note": "此 Tool 只读 manifest/hash，不加载任何 DLL。live 可加载 operation 请调用 cad.extension.list。",
    }
