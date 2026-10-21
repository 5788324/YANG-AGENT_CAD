# Personal Local Security Boundary

这是个人本机插件，不做企业级安全平台，但 MCP 具备真实 DWG 写权限，因此保留以下必要边界：

- Gateway 仅 `127.0.0.1`；per-process random Bearer Token；
- Token 不进入 runtime/operation log，也不进入 UI“复制诊断”；
- write 总开关；erase 单独开关；
- `cad.command.run` 单独开关，默认 false；
- `cad.extension.invoke` 单独开关，默认 false；
- generic property/API write 默认 dry_run；
- process/document session affinity；
- Transaction rollback + operation JSONL；
- MCP stdout 仅协议。

## 通用层不是任意 eval

不存在 C#/Python `eval`、shell 或任意 assembly/method 全开放执行器。

- property.set 只允许受支持 public Autodesk setter；
- api.invoke 只允许策略通过的 public Autodesk method；
- command.run 只接受单个命令 token + 类型化参数，不允许换行、PauseToken、`^C` 拼接；
- extension 只扫描本机固定目录中的 DLL，并要求实现 YangCAD extension contract；Host 控制 Transaction。

正式对外 Release 仍需要解决程序集代码签名/可信加载策略；个人开发阶段不以 `SECURELOAD=0` 作为长期方案。


## TASK-006C Batch file-write boundary

Batch real writes do not mutate the input DWG in v1. `batch.dwg.hygiene` defaults to dry-run and requires explicit `allow_write=true` for real execution. Real output must target a new `output_dir`; existing outputs and in-place paths are rejected. The Python orchestrator hashes each input before/after the AcCoreConsole process and treats any source-byte change as P0. Batch job/result/summary files must never contain Gateway Bearer Tokens.

- Batch real 输出优先保持源 `OriginalFileVersion`；检测到 ProxyEntity 时默认拒绝 real save，除非显式 `allow_proxy_save=true`。

### TASK-006C v1 batch execution details

- Real multi-file calls are **per-DWG isolated, not globally atomic**. Successful output copies may remain if a later worker fails; source DWGs remain immutable. Deterministic output-name/existing-file conflicts are preflighted for the entire batch before any process starts.
- Dry-run launches Core Console with `/readonly` in addition to transaction abort semantics.
- Missing/corrupt worker result or failed process triggers best-effort partial output deletion.
- Source missing/unreadable after execution is a P0 integrity failure, the same as a SHA mismatch.
- For real `folder=...` mode, `output_dir` must be outside the input folder tree, preventing a later recursive run from ingesting prior outputs as new inputs.

## TASK-007 Agent Extension boundary

TASK-007 不把任意 C# / shell / assembly eval 作为默认 Tool。

- `extension.build` 只接受 Execute method body，由 builder 固定生成 Contract/Descriptor/csproj；
- conservative source-policy 拒绝 filesystem/network/process/reflection/PInvoke/unsafe/registry/background thread、extension-owned Lock/Transaction、command execution、SaveAs 等；同时做 method-body brace/scope 校验，禁止 body 跳出固定 wrapper 注入新 type；
- source-policy 是 guard，不是 .NET sandbox；手工 legacy DLL 不应被描述为受 TASK-007 builder policy 保护；
- `extension.install` 必须 `allow_install=true`；versioned DLL 不覆盖；manifest 先于 DLL 原子可见，避免 Host 在安装窗口把 managed DLL 当 legacy 加载；
- managed Host load 校验 DLL SHA、target year/product、当前 Host DLL SHA、Descriptor capability；
- `cad.extension.invoke` 仍默认关闭，并受 AllowWrites / document affinity / operation log；
- `RequiresErase=true` 还必须 AllowErase=true；
- write dry-run 由 Host Transaction Abort；异常不 commit；real 仍单 Host Transaction / 一次 Undo；
- `RecordChange` ledger 只允许 JSON-safe string evidence，最多 500 条；它不是独立防篡改 diff。
