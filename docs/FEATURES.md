# 功能使用说明

本文档说明当前已经具备的功能、用途、命令和风险等级。

## 功能总览

| 功能 | 命令 | 状态 | 风险 |
| --- | --- | --- | --- |
| 环境自检 | `scripts\doctor.cmd` | 可用 | 只读 |
| 运行测试 | `scripts\test.cmd` | 可用 | 只读 |
| 创建任务 | `new-task` | 可用 | 只读 |
| 查看任务 | `task-list` / `task-show` | 可用 | 只读 |
| 备份文件 | `backup` | 可用 | 只复制 |
| 回滚文件 | `rollback` | 可用 | 会覆盖原文件 |
| LISP 校验 | `validate-lisp` | 可用 | 只读 |
| 当前图 LISP 投喂 | `current-lisp` | dry-run 可用，execute 待实测 | 可能修改当前图 |
| 批量 accore 预演 | `batch-task` | dry-run 可用 | 预演只读 |
| 批量 accore 执行 | `batch-task --execute` | 骨架可用，真实执行待实测 | 会修改 DWG |
| 插件箱列表 | `toolbox-list` | 可用 | 只读 |
| 插件校验 | `toolbox-validate` | 可用 | 只读 |
| MCP stdio | `yang_cad_agent.mcp_stdio` | 骨架可用 | 只暴露安全工具 |
| 批量图层统计报告 | `batch.layer_report` | 可用，已在测试副本验证 | 只读报告 |
| 批量块统计报告 | `batch.block_report` | 可用，已在测试副本验证 | 只读报告 |
| 批量文字标注统计报告 | `batch.annotation_report` | 可用，已在测试副本验证 | 只读报告 |
| 图纸体检总报告 | `summarize-reports` | 可用，已在测试副本验证 | 只读报告 |

## 环境自检

用途：检查 Python、Git、AutoCAD accoreconsole 是否可用。

```cmd
scripts\doctor.cmd
```

成功标准：

- `ok` 是 `true`
- `accoreconsole.status` 是 `ok`

## 任务记录

每次重要操作都会生成任务 ID。任务记录保存在：

```text
.agent\tasks
```

列出最近任务：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli task-list
```

查看单个任务：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli task-show 任务ID
```

## LISP 校验

用途：执行前检查 LISP 是否有明显危险或不兼容。

当前图校验：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli validate-lisp toolbox\plugins\current_smoke\main.lsp --track B
```

批量 accoreconsole 校验：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli validate-lisp 脚本路径 --track C
```

批量模式会禁止：

- `vla-*`
- `vlax-*`
- `vl-load-com`
- `getpoint`
- `getstring`
- `alert`
- 无参数 `(ssget)`

## 当前图 LISP 投喂

用途：把 LISP 发送到当前打开的 AutoCAD 图纸。

预演：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli current-lisp --script toolbox\plugins\current_smoke\main.lsp
```

真实执行：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli current-lisp --script toolbox\plugins\current_smoke\main.lsp --execute
```

风险说明：

- 不加 `--execute` 不会发送给 AutoCAD。
- 加 `--execute` 后会尝试连接 AutoCAD 并发送 `(load "...")`。
- 当前版本还没有完成标记监听，所以发送成功不等于脚本一定完整执行完。

## 批量任务

用途：用 accoreconsole 批量处理 DWG。

预演：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli batch-task --script 脚本路径 sample
```

真实执行：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli batch-task --script 脚本路径 sample --execute
```

执行流程：

1. 校验 LISP。
2. 扫描 DWG。
3. 创建任务记录。
4. `--execute` 时先备份 DWG。
5. 调用 accoreconsole。
6. 写入日志和结果。

风险说明：

- 默认不加 `--execute`，只预演。
- 加 `--execute` 会真实打开并保存 DWG。
- 当前真实执行还未在 sample 图纸上完成验证。

## 插件箱

插件目录：

```text
toolbox\plugins
```

列出插件：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli toolbox-list
```

当前内置插件：

- `current.smoke_test`：当前图 LISP 投喂烟测，只打印命令行文字。
- `batch.smoke_qsave`：批量 accoreconsole 烟测，会 QSAVE，只能用于测试图纸副本。
- `batch.layer_report`：批量图层统计报告，不修改 DWG，会在图纸同目录生成 `*.layer-report.csv`。
- `batch.block_report`：批量块统计报告，不修改 DWG，会在图纸同目录生成 `*.block-report.csv`。
- `batch.annotation_report`：批量文字标注统计报告，不修改 DWG，会在图纸同目录生成 `*.annotation-report.csv`。

图层统计报告示例：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli batch-task --script toolbox\plugins\batch_layer_report\main.lsp .agent\tmp\sample-run
```

确认 dry-run 只匹配测试副本后，再执行：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli batch-task --script toolbox\plugins\batch_layer_report\main.lsp .agent\tmp\sample-run --execute
```

已验证输出：

```text
.agent\tmp\sample-run\S001-test.dwg.layer-report.csv
```

说明：该插件是只读报告插件，不保存 DWG。LISP 静态检查中可能出现 `qsave/saveas` 警告，这是因为通用批处理规则提醒“修改类脚本应显式保存”，对该只读插件不构成失败。

块统计报告示例：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli batch-task --script toolbox\plugins\batch_block_report\main.lsp .agent\tmp\sample-run
```

确认 dry-run 只匹配测试副本后，再执行：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli batch-task --script toolbox\plugins\batch_block_report\main.lsp .agent\tmp\sample-run --execute
```

已验证输出：

```text
.agent\tmp\sample-run\S001-test.dwg.block-report.csv
```

说明：该插件统计普通块参照数量。动态块有效名称、块属性明细等更深信息后续再扩展。

文字标注统计报告示例：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli batch-task --script toolbox\plugins\batch_annotation_report\main.lsp .agent\tmp\sample-run
```

确认 dry-run 只匹配测试副本后，再执行：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli batch-task --script toolbox\plugins\batch_annotation_report\main.lsp .agent\tmp\sample-run --execute
```

已验证输出：

```text
.agent\tmp\sample-run\S001-test.dwg.annotation-report.csv
```

说明：该插件统计 `TEXT`、`MTEXT`、`DIMENSION`、`LEADER`、`MULTILEADER`、`ACAD_TABLE` 的数量，后续可扩展为按图层和文字样式分组。

## 图纸体检总报告

用途：把图层、块、文字标注三类 CSV 报告汇总成一份 Markdown 文件。

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli summarize-reports .agent\tmp\sample-run
```

已验证输出：

```text
.agent\tmp\sample-run\CAD_REPORT_SUMMARY.md
```

当前测试副本汇总结果：

- 图层数量：12
- 图层对象总数：93
- 普通块参照总数：1
- 文字/标注对象总数：24
- CSV 报告文件数：3

## MCP stdio

用途：给未来 MCP/AI 工具调用做准备。

列出工具：

```powershell
$env:PYTHONPATH='src'
'{"action":"list_tools"}' | & 'C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' -m yang_cad_agent.mcp_stdio
```

当前工具：

- `doctor`
- `toolbox_list`
- `task_list`
- `task_show`

## 一键图纸体检

用途：把图层统计、块统计、文字标注统计、总报告汇总串成一个命令。默认 dry-run，只检查会处理哪些 DWG，不修改图纸。

预演：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli health-check .agent\tmp\sample-run
```

真实执行测试副本：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli health-check .agent\tmp\sample-run --execute
```

当前已验证输出：

```text
.agent\tmp\sample-run\S001-test.dwg.layer-report.csv
.agent\tmp\sample-run\S001-test.dwg.block-report.csv
.agent\tmp\sample-run\S001-test.dwg.annotation-report.csv
.agent\tmp\sample-run\CAD_REPORT_SUMMARY.md
```

测试副本汇总结果：图层 12，图层对象 93，普通块参照 1，文字/标注对象 24，CSV 文件 3。

注意：这个命令虽然当前使用的是只读报告插件，但 `--execute` 会启动 accoreconsole。正式对整批图纸执行前，仍然要先 dry-run、确认匹配文件、保留备份记录。

## MCP 一键体检工具

MCP stdio 已暴露 `health_check` 工具，供 Codex/Antigravity 做图纸体检 dry-run。

```powershell
$env:PYTHONPATH='src'
'{"action":"call_tool","name":"health_check","params":{"root":".","folder":".agent/tmp/sample-run","pattern":"*.dwg","recursive":false}}' | & 'C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' -m yang_cad_agent.mcp_stdio
```

安全说明：MCP 层固定 `execute=false`，即使参数传入 `execute:true` 也不会真实启动 accoreconsole 执行。真实执行仍需走 CLI 的 `health-check --execute`。
