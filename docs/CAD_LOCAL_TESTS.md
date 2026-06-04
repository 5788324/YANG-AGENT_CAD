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
- 如果发送成功，AutoCAD 命令行应打印 smoke test 文本。

## 批量 accoreconsole dry-run

继续只用测试 DWG：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli batch-task --script .agent\tmp\cad-test\batch-smoke.lsp .agent\tmp\cad-test
```

不要在未确认备份路径前加 `--execute`。
