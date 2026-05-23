# MCP 最小工具说明

当前仓库先提供一个无外部依赖的 stdio 工具骨架，后续再接入正式 MCP Python SDK。

## 启动方式

```powershell
$env:PYTHONPATH='src'
python -m yang_cad_agent.mcp_stdio
```

在当前 Codex 环境里可使用：

```powershell
$env:PYTHONPATH='src'
& 'C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' -m yang_cad_agent.mcp_stdio
```

## 消息格式

列出工具：

```json
{"action":"list_tools"}
```

调用 doctor：

```json
{"action":"call_tool","name":"doctor","params":{}}
```

列出插件：

```json
{"action":"call_tool","name":"toolbox_list","params":{"root":"."}}
```

列出任务：

```json
{"action":"call_tool","name":"task_list","params":{"root":".","limit":5}}
```

查询任务：

```json
{"action":"call_tool","name":"task_show","params":{"root":".","task_id":"任务ID"}}
```

## 当前工具

- `doctor`
- `toolbox_list`
- `task_list`
- `task_show`

当前 `toolbox_list` 已可返回内置插件，例如 `current.smoke_test`。

## 后续目标

正式 MCP Server 应包装同一批底层函数，并继续保持：

- 不暴露泛用 shell
- 批量修改默认 dry-run
- 执行前校验 LISP
- 执行前备份
- 所有任务写入 task ledger
