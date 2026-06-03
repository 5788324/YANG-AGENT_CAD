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

## 回滚预演 MCP 工具

`rollback_dry_run` 用于通过 MCP 预览某个任务会回滚哪些文件。它固定只做 dry-run，不会恢复或覆盖文件。即使调用参数里传入 `dry_run:false`，MCP 层也会固定按 `dry_run=true` 调用底层回滚流程。

调用示例：

```json
{"action":"call_tool","name":"rollback_dry_run","params":{"root":".","task_id":"任务ID"}}
```

当前工具清单还包括：

- `rollback_dry_run`

## 最近失败任务 MCP 工具

`task_recent_failures` 用于通过 MCP 查看最近失败任务和错误码，方便 AI 快速定位排障入口。该工具只读 `.agent\tasks` 任务记录，不修改 DWG、不启动 AutoCAD、不写入任务记录。

调用示例：

```json
{"action":"call_tool","name":"task_recent_failures","params":{"root":".","limit":3,"scan_limit":100}}
```

返回字段包括：

- `task_id`
- `status`
- `error_code`
- `track`
- `risk`
- `user_goal`
- `script_path`
- `files`
- `rollback_available`
- `ledger_path`

当前工具清单还包括：

- `task_recent_failures`

## MCP 失败任务排障包工具

`task_error_detail` 用于通过 MCP 查看单个失败任务的排障信息。它只读取任务记录、accoreconsole 日志和回滚预演结果，不修改 DWG、不启动 AutoCAD、不写入新的任务记录。

调用示例：
```json
{"action":"call_tool","name":"task_error_detail","params":{"root":".","task_id":"任务ID","log_tail_chars":500}}
```

返回字段包括：
- `task`：完整任务记录
- `error_code`：错误码
- `error`：结构化错误解释，包含 `code`、`meaning`、`suggestion`、`severity`
- `status`：任务状态
- `rollback_available`：是否有可回滚备份
- `rollback_dry_run`：只读回滚预演结果
- `log_paths`：相关日志路径
- `log_tails`：日志尾部内容，方便 AI 快速定位失败原因

安全说明：该工具固定为只读排障入口。即使任务有可回滚备份，也只返回 `rollback_dry_run`，不会执行真实回滚。

## MCP 错误码解释

`task_error_detail` 会把原始 `error_code` 同步转换为结构化 `error` 字段，方便 Codex/Antigravity 或其他 AI 不翻文档也能给出下一步动作。

示例：
```json
{
  "error_code": "LISP_LOAD_FAILED",
  "error": {
    "code": "LISP_LOAD_FAILED",
    "meaning": "AutoCAD failed to load the LISP file.",
    "suggestion": "Check the script path, file encoding, secure load settings, and trusted paths.",
    "severity": "error"
  }
}
```

未知错误码会保留原始 `code`，但 `meaning` 会回退为未分类错误说明，避免调用方丢失错误码。
