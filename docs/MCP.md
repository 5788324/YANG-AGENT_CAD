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

## 一键图纸体检 MCP 工具

`health_check` 用于让 Codex/Antigravity 通过 MCP 做图纸体检预演。它当前只暴露 dry-run，不接受真实执行。即使调用参数里传入 `execute: true`，MCP 层也会固定按 `execute=false` 调用底层流程。

调用示例：

```json
{"action":"call_tool","name":"health_check","params":{"root":".","folder":".agent/tmp/sample-run","pattern":"*.dwg","recursive":false}}
```

当前工具清单包括：

- `doctor`
- `toolbox_list`
- `task_list`
- `task_show`
- `health_check`

真实 accoreconsole 执行仍需要走 CLI，并由用户/AI 明确确认后运行 `health-check --execute`。

## 报告汇总 MCP 工具

`summarize_reports` 用于让 Codex/Antigravity 通过 MCP 汇总已有 CSV 报告。它不启动 accoreconsole，不修改 DWG，只读取 `*.layer-report.csv`、`*.block-report.csv`、`*.annotation-report.csv` 并写出 Markdown 总报告。

调用示例：

```json
{"action":"call_tool","name":"summarize_reports","params":{"root":".","folder":".agent/tmp/sample-run"}}
```

可选自定义输出：

```json
{"action":"call_tool","name":"summarize_reports","params":{"root":".","folder":".agent/tmp/sample-run","output":".agent/tmp/sample-run/CAD_REPORT_SUMMARY.md"}}
```

当前工具清单还包括：

- `summarize_reports`
