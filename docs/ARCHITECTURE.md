# Architecture

## Layers

1. **MCP Layer**：Python SDK v2 / stdio；负责 Tool schema、instance discovery、process/document binding。
2. **AutoCAD Host**：loopback Gateway、Broker、transaction、强类型 CAD Tool、Generic MCP、UI。
3. **Civil 3D Extension**：只在 Civil 3D 加载，注册 Civil 专业 Tool。
4. **Versioned Extensions**：可选，本地 DLL fallback；不属于 Core 编译产物。

## Request path

```text
AI / MCP Host
  → Python stdio MCP
  → 127.0.0.1 /rpc + Bearer
  → CadRequestBroker queue
  → AutoCAD UI thread (TASK-001 proven Timer + Idle; only after active DWG)
  → Tool
  → DocumentLock / Transaction
  → Autodesk API
```

TASK-001 已在 AutoCAD/Civil 3D 2024 实机证明 Timer + Idle fallback、document affinity、rollback 与基础写 Tool Undo。0.2.2 的 ExecuteInCommandContextAsync 替换未解决 Start 页崩溃，0.2.3 因此回到已验证调度器，并把整个 Runtime 延迟到真实 DWG 激活后。

## Tool layers

```text
Strong Typed Tools
     ↓ missing
Generic Object / Property
     ↓ insufficient
Generic Autodesk API Invoke
     ↓ insufficient
Command fallback / Versioned Extension
```

Command/Extension 是 escape hatch，默认关闭；不能绕过 Broker、权限、document affinity 和 operation log。

## Separate assemblies

`YangCAD.AutoCAD` 不引用 Civil API。`YangCAD.Civil3D` 单独引用 `AeccDbMgd.dll + AecBaseMgd.dll`，保证普通 AutoCAD 不因缺 Civil runtime 依赖而失败。


## TASK-006C Batch DWG path

```text
AI / MCP Host
  → Python stdio MCP `batch.*`
  → local batch orchestrator (NO live Gateway)
  → one AcCoreConsole.exe process per DWG
  → NETLOAD YangCAD.BatchWorker.dll
  → YANGCAD_BATCH_RUN
  → AutoCAD Database API / DocumentLock + Transaction (write only)
  → result.json + summary.json
```

Batch v1 is deliberately separate from the normal `127.0.0.1 /rpc` path. It is sequential, does not switch the current GUI document, and never registers `batch.*` in the live ToolRegistry. Real hygiene is copy-only: the worker saves a new output DWG and the orchestrator verifies the input SHA-256 stayed unchanged.


## TASK-007 Agent Extension path

```text
AI / MCP Host
  → MCP-local extension.build/install
  → versioned DLL + managed manifest
  → existing cad.extension.list/invoke
  → Broker permission/document affinity
  → Host-owned Transaction
```

Builder/local workflow does not add live ToolRegistry handlers; 0.7.0 candidate contract is stdio 65 / live 28/58. Managed load binds DLL to target year/product and current Host DLL SHA.
