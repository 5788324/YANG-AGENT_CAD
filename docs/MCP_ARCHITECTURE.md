> **2026-09-07 current contract override**: two product MCP tools `project.revit.prepare` / `project.image.embed` were added without renaming existing tools. Current contract is stdio **68**, AutoCAD live **31**, Civil 3D live **61** (adds `cad.scale.convert`). Historical TASK-006C/TASK-007 counts below are retained only as milestone history.

# MCP Architecture — v0.2

- Transport：stdio
- Python：3.10+
- Local RPC：`127.0.0.1` + per-process Bearer Token
- AutoCAD dispatcher：Timer + Idle（TASK-001 2024 实机已验证；0.2.3 仅在 active DWG 后启动）

```text
MCP Host
  ↓ stdio
Python yangcad_mcp
  ↓ authenticated localhost RPC
LocalGateway
  ↓ queue
CadRequestBroker
  ↓ AutoCAD UI thread
  ├─ Strong Typed Tool
  ├─ Generic Property/API Tool
  ├─ Command fallback (opt-in)
  └─ Extension fallback (opt-in)
```

## Instance / affinity

instance JSON：`%LOCALAPPDATA%\YangCADAgent\instances\<PID>.json`。

Python 通过 `/health` 校验 PID/port/process session/DocumentId/Tool capability。一次成功读取后写入绑定：

`PID + StartedAt + Document + DocumentId`

新增 generic write tools 同样进入 `WRITE_TOOLS`，不会绕过绑定。

## 通用 Property/API

### Property

`cad.object.describe` 返回对象 runtime type、property metadata、method metadata。只有 `safe_write=true` 的 property 才能进入 `cad.property.set`。

首版写类型：string、bool、数值、enum、Point2d/Point3d、Vector2d/Vector3d。ObjectId 等引用型属性不会通过通用 setter；需要专用 Tool/API/Extension。

### API

`cad.api.describe` 为每个方法返回精确 signature 与 invokable/reason。通用 invoke 拒绝：

- static / generic；
- ref/out/pointer；
- DBObject 基础设施危险方法；
- 非 Autodesk public member；
- 无法安全转换的参数或无法序列化的返回值。

ObjectId/Handle 可作为 API 参数由 Handle 转换，便于调用一部分 Civil API；复杂关系仍优先专用 Tool。

## Command fallback

`cad.command.run` 使用同步 `Editor.Command`，不使用异步命令队列。默认权限关闭，且禁止交互 PauseToken。其模型外命令上下文与 Undo 行为必须在 TASK-002 针对具体命令实测；失败时返回错误，不自动降级为异步执行。

## Dynamic Extension

目录：`%LOCALAPPDATA%\YangCADAgent\Extensions`。

Extension 实现 `IYangCadExtensionOperation`；推荐每次生成新版本文件名，因为已加载 .NET Framework assembly 无法原地卸载。Host 提供 `YangCadExtensionContext` 和唯一 Transaction，负责 commit/abort。不要把 YangCAD host DLL 复制进扩展目录。


## Batch-local MCP tools (TASK-006C)

`batch.status` / `batch.dwg.audit` / `batch.dwg.hygiene` are MCP-local tools and therefore intentionally do not appear in AutoCAD/Civil `/health`. At TASK-006C the milestone contract was stdio **61**, plain AutoCAD **28**, Civil 3D **58**; this is historical, not the current contract.

The batch path launches AcCoreConsole directly; it never discovers an instance JSON and never calls localhost `/rpc`. This prevents folder-scale DWG lifecycle operations from perturbing the current acad.exe Gateway/document affinity.

## Agent Extension Workflow (TASK-007)

```text
AI / MCP Host
  → MCP-local extension.build
  → %LOCALAPPDATA%/YangCADAgent/ExtensionWorkspace/<BuildId>
  → dotnet build against deployed YangCAD.AutoCAD + Autodesk runtime
  → extension.install (explicit allow_install)
  → %LOCALAPPDATA%/YangCADAgent/Extensions/<versioned>.dll + .yangcad.json

then live CAD:
  → cad.extension.list (explicit extension permission)
  → Host manifest/SHA/year/product/Host-SHA validation
  → Assembly.LoadFrom(versioned DLL)
  → cad.extension.invoke
  → Broker affinity/permissions
  → Host-owned Transaction
  → dry-run Abort OR real Commit
```

TASK-007 added 4 MCP-local tools, so that milestone contract was **65 / 28 / 58**. The current 2026-09-07 additive product contract is **68 / 31 / 61** after `cad.scale.convert`. Python builder never imports Autodesk DLL and never loads the generated DLL. Generated Extension must reference Host/Autodesk assemblies with copy-local disabled.

`YangCadExtensionContext.RecordChange` is the preview/diff ledger. It is explicitly extension-reported evidence; the Host does not claim to snapshot every changed Autodesk property independently.
