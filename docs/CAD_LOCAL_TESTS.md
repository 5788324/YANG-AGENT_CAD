# AutoCAD 本地实测清单

本文件用于真实 AutoCAD 环境测试。不要使用客户项目图纸。优先使用空白图纸或专门创建的测试 DWG。

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

