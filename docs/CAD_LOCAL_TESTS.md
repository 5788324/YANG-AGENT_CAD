# AutoCAD 本地实测清单

## 当前图一键烟测脚本

推荐优先使用这个入口验证当前图 LISP 链路。

dry-run：

```cmd
scripts\current-smoke-test.cmd
```

预期：不连接 AutoCAD，不修改 DWG，输出 `task_id`、`wrapper_path`、`completion_marker`。

execute：

```cmd
scripts\current-smoke-test.cmd --execute
```

执行前必须确认 AutoCAD 2027 已打开，并且当前打开的是测试 DWG。该脚本只运行 `current_smoke`，只在 AutoCAD 命令行打印测试文字，不保存 DWG。

结果判断：

- `completed`：AutoCAD 已执行并写入完成标记。
- `sent_unconfirmed`：命令已发送，但暂时没看到完成标记；检查 AutoCAD 命令行和 `completion_marker`。
- `failed`：复制 `task_id`，让 AI 使用 `task_error_detail` 排障。

本文件用于真实 AutoCAD 环境测试。不要使用客户项目图纸。优先使用空白图纸或专门创建的测试 DWG。

如果使用用户提供的 `sample` 目录，请先阅读 [SAMPLE_DWGS.md](SAMPLE_DWGS.md)。

## 准备

1. 打开 AutoCAD 2027。
2. 新建空白 DWG。
3. 保存到一个临时目录，例如 `D:\codex\Yang Agent_CAD\.agent\tmp\cad-test\current-test.dwg`。
4. 不要把测试 DWG 提交到 GitHub，`.gitignore` 已忽略 `*.dwg`。

## 当前图 LISP dry-run

```cmd
scripts\doctor.cmd
```

创建一个测试 LISP：

```lisp
(princ "\nYANG AGENT CAD current drawing smoke test")
(princ)
```

运行 dry-run：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli current-lisp --script .agent\tmp\cad-test\current-smoke.lsp
```

确认输出里有：

- `mode = dry_run`
- `requires_confirmation = true`
- `(load "...current-smoke.lsp")`

## 当前图 LISP execute

确认 AutoCAD 已打开并有活动图纸后再运行：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli current-lisp --script .agent\tmp\cad-test\current-smoke.lsp --execute
```

预期：

- 如果当前 Python 没有 `pywin32`，返回 `ACAD_COM_DEPENDENCY_MISSING`。
- 如果 AutoCAD 未打开，返回 `ACAD_COM_UNAVAILABLE`。

## 2026-06-04 当前图真实执行复测

已完成：

- 默认 Python 已安装并验证 `pywin32`，`win32com.client` 可导入。
- 当前图执行代码已加入 AutoCAD 2027 COM ProgID：`AutoCAD.Application.26`。
- 新增只读诊断入口：`scripts\current-com-diagnose.cmd`。
- `scripts\current-smoke-test.cmd --execute` 已在本机复测。
- 失败任务 `20260604-234553-07493147` 返回 `ACAD_COM_UNAVAILABLE`。
- 该任务的 `send_result.acad_process.running` 为 `true`，说明 `acad.exe` 正在运行，但 COM 附着失败。
- `scripts\current-com-diagnose.cmd` 已验证：
  - `python.elevated = false`
  - `pywin32.available = true`
  - `acad_process.running = true`
  - `attachable = false`
  - `AutoCAD.Application.26` 返回 `操作无法使用`

当前判断：

- 不是 Python 缺依赖。
- 不是缺 AutoCAD 2027 ProgID。
- 更可能是 AutoCAD 以管理员权限运行、AutoCAD 仍停在启动/许可/欢迎状态、或当前 AutoCAD 实例没有注册到 COM Running Object Table。

下一次人工环境验证：

1. 关闭当前 AutoCAD。
2. 用普通权限重新打开 AutoCAD 2027。
3. 打开测试 DWG，等待命令行可输入。
4. 运行：

```cmd
scripts\current-smoke-test.cmd --execute
```

执行前也可以先运行：

```cmd
scripts\current-com-diagnose.cmd
```

如果返回 `attachable: true`，再运行真实烟测。

如果仍失败，运行：

```cmd
python -m yang_cad_agent.cli task-error-detail 任务ID
```
- 如果发送成功，AutoCAD 命令行应打印 smoke test 文本。

## 批量 accoreconsole dry-run

继续只用测试 DWG：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli batch-task --script .agent\tmp\cad-test\batch-smoke.lsp .agent\tmp\cad-test
```

不要在未确认备份路径前加 `--execute`。
## 2026-06-05 AutoCAD 启动和 COM 复测

新增本地启动入口：
```cmd
scripts\open-autocad-test.cmd
scripts\open-autocad-test.cmd --execute
```

实测结果：
- dry-run 能正确识别 `C:\Program Files\Autodesk\AutoCAD 2027\acad.exe`。
- execute 已启动 AutoCAD，PID 为 `56860`。
- 启动目标为 `.agent\tmp\current-open\S001-current-test.dwg`，不是 `sample` 原图。
- 启动后两次运行 `scripts\current-com-diagnose.cmd`，结果仍为 `attachable: false`。
- `scripts\current-smoke-test.cmd --execute` 按预期返回 `status: blocked` 和 `ACAD_COM_UNAVAILABLE`，未发送 LISP。

当前结论：
- 本地脚本可以主动启动 AutoCAD。
- MCP 仍保持只读/诊断边界，不直接启动或关闭 AutoCAD。
- 自动当前图测试还卡在 AutoCAD COM 不可附着；下一步需要排查 AutoCAD 是否完成初始化、是否有弹窗/许可界面、COM 注册是否正常。
- 不自动关闭 AutoCAD；关闭前必须人工确认没有未保存图纸。
