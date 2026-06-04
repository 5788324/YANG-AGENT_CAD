# 小白操作文档

## 当前图一键烟测脚本

更简单的当前图测试入口是：

```cmd
scripts\current-smoke-test.cmd
```

默认只做 dry-run，会显示 `task_id`、`wrapper_path` 和 `completion_marker`，不会连接 AutoCAD。

如果已经打开 AutoCAD，并且当前打开的是测试 DWG，再运行：

```cmd
scripts\current-smoke-test.cmd --execute
```

这个脚本只运行内置 `current_smoke`，只在 AutoCAD 命令行打印测试文字，不保存 DWG。失败时复制输出里的 `task_id`，让 AI 用 `task_error_detail` 排障。

这份文档给完全不懂代码、CAD 也刚入门的人使用。你只需要知道：默认命令只做检查或预演，不会修改图纸；真正修改图纸的命令会明确要求加 `--execute`。

## accoreconsole 配置修复

如果运行 `scripts\doctor.cmd` 时看到 `ACCORE_CONFIG_LOCKED`，并且提示用户配置文件已经存在，可以让 AI 先确认下面两个路径：

```text
C:\Users\YANG\AppData\Local\Autodesk\AutoCAD 2027\R26.0\chs\acad2027.cfg
C:\Program Files\Autodesk\AutoCAD 2027\acad2027.cfg
```

当前机器的情况是：第一个文件存在，第二个文件缺少，普通权限不能写入 `C:\Program Files`。修复方式是右键运行：

```cmd
scripts\fix-acad-cfg.cmd
```

一定要选“以管理员身份运行”。运行完成后，再执行：

```cmd
scripts\doctor.cmd
```

如果窗口弹开后马上关闭，先让 AI 更新到最新版脚本；新版脚本会停在结果页面，按任意键才会关闭。

doctor 不再提示 `ACCORE_CONFIG_LOCKED` 后，AI 才会继续跑 accoreconsole 的真实测试。

当前机器已经修复成功，并完成了一次只针对测试副本的 accoreconsole 小测试。以后仍然不要直接对原始 `sample` 图纸整批执行，先让 AI 复制副本再测试。

## 先记住三句话

1. 不确定时，只运行 `scripts\doctor.cmd` 和 dry-run 命令。
2. 看到 `--execute` 才代表可能真的修改图纸。
3. 批量修改前必须确认已经备份。

## 打开项目目录

项目目录是：

```text
D:\codex\Yang Agent_CAD
```

在 Windows 文件夹地址栏输入这个路径，或让 AI 帮你打开。

## 第一步：自检

双击或让 AI 运行：

```cmd
scripts\doctor.cmd
```

你看到这些就说明基础环境正常：

- `ok: true`
- 检测到 Python
- 检测到 Git
- 检测到 `accoreconsole`

## 最快试用：个人版一键体检

如果你只想先看这个工具好不好用，运行：

```cmd
scripts\personal-health-check.cmd
```

默认行为：

- 扫描 `sample` 目录。
- 只做 dry-run。
- 不修改 DWG。
- 生成一份给人看的计划报告：

```text
.agent\reports\PERSONAL_HEALTH_PLAN.md
```

如果要换目录，例如只检查测试副本目录：

```cmd
scripts\personal-health-check.cmd .agent\tmp\sample-run
```

真实生成图层、块、文字标注 CSV 报告时，必须明确加：

```cmd
scripts\personal-health-check.cmd .agent\tmp\sample-run --execute
```

不要直接对客户原图目录加 `--execute`。先让 AI 复制测试副本，再执行。

当前测试副本已经验证成功。运行上面的 `--execute` 后，会在 `.agent\tmp\sample-run` 生成：

```text
S001-test.dwg.layer-report.csv
S001-test.dwg.block-report.csv
S001-test.dwg.annotation-report.csv
S001-test.dwg.xref-image-report.csv
S001-test.dwg.title-block-candidate-report.csv
CAD_REPORT_SUMMARY.md
```

这 5 个 CSV 和 1 个总报告都是只读检查结果，不会主动修改 DWG。

## 第二步：运行测试

运行：

```cmd
scripts\test.cmd
```

看到类似下面内容就是正常：

```text
Ran 16 tests
OK
```

## 第三步：查看插件箱

运行：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli toolbox-list
```

现在应该能看到：

```text
current.smoke_test [ok] 当前图烟测
```

这是一个安全插件，只会在 AutoCAD 命令行打印一条测试文字。

## 第四步：当前图插件预演

预演不会改图纸。

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli current-lisp --script toolbox\plugins\current_smoke\main.lsp
```

你应该看到：

- `mode: dry_run`
- `requires_confirmation: true`
- 一条 `(load "...main.lsp")` 命令

这表示 AI 已经准备好要发给 AutoCAD 的命令，但还没有执行。

## 第五步：当前图插件真实执行

只有确认 AutoCAD 已打开、并且当前图纸是测试图纸时，才运行：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli current-lisp --script toolbox\plugins\current_smoke\main.lsp --execute
```

可能结果：

- 成功：AutoCAD 命令行出现 `YANG AGENT CAD: current drawing LISP feed is working.`
- `ACAD_COM_DEPENDENCY_MISSING`：当前 Python 缺少 pywin32，暂时不能通过 COM 投喂。
- `ACAD_COM_UNAVAILABLE`：AutoCAD 没打开，或没有可连接实例。

## 第六步：批量图纸预演

你准备的测试图纸在：

```text
D:\codex\Yang Agent_CAD\sample
```

批量预演命令：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli batch-task --script toolbox\plugins\batch_smoke\main.lsp sample
```

注意：不要加 `--execute`。这一步只预演，不会修改 DWG。

## 第七步：生成图层统计报告

这个插件只读取图层和对象数量，不修改 DWG。AI 会先用测试副本运行，不直接碰原始 `sample` 图纸。

先预演：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli batch-task --script toolbox\plugins\batch_layer_report\main.lsp .agent\tmp\sample-run
```

确认只匹配测试副本后，再执行：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli batch-task --script toolbox\plugins\batch_layer_report\main.lsp .agent\tmp\sample-run --execute
```

成功后会生成：

```text
.agent\tmp\sample-run\S001-test.dwg.layer-report.csv
```

这份 CSV 可以用 Excel 打开，里面会看到每个图层的颜色、是否锁定、是否冻结、对象数量。

## 第八步：生成块统计报告

这个插件只读取块参照数量，不修改 DWG。

先预演：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli batch-task --script toolbox\plugins\batch_block_report\main.lsp .agent\tmp\sample-run
```

确认只匹配测试副本后，再执行：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli batch-task --script toolbox\plugins\batch_block_report\main.lsp .agent\tmp\sample-run --execute
```

成功后会生成：

```text
.agent\tmp\sample-run\S001-test.dwg.block-report.csv
```

这份 CSV 可以用 Excel 打开，里面会看到每个普通块的名称、是否外部参照、是否布局块、插入数量。

## 第九步：生成文字标注统计报告

这个插件只读取文字、标注、引线和表格数量，不修改 DWG。

先预演：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli batch-task --script toolbox\plugins\batch_annotation_report\main.lsp .agent\tmp\sample-run
```

确认只匹配测试副本后，再执行：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli batch-task --script toolbox\plugins\batch_annotation_report\main.lsp .agent\tmp\sample-run --execute
```

成功后会生成：

```text
.agent\tmp\sample-run\S001-test.dwg.annotation-report.csv
```

这份 CSV 可以用 Excel 打开，里面会看到单行文字、多行文字、尺寸标注、引线、多重引线、表格的数量。

## 第十步：生成图纸体检总报告

前面三步会生成三份 CSV。这个命令会把它们合成一份更容易读的 Markdown 总报告，不修改 DWG。

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli summarize-reports .agent\tmp\sample-run
```

成功后会生成：

```text
.agent\tmp\sample-run\CAD_REPORT_SUMMARY.md
```

这份报告会汇总图层数量、对象总数、块参照数量、文字/标注数量，并列出对象最多的图层。

## 查看任务记录

运行：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli task-list
```

每次 dry-run 或执行都会生成任务记录。以后出错、回滚、交接都靠它。

## 回滚

批量执行成功备份后，才有回滚。

先预演回滚：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli rollback 任务ID --dry-run
```

确认无误后才执行真实回滚：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli rollback 任务ID
```

## 什么时候必须找 AI 确认

- 要加 `--execute` 时。
- 要批量处理整个文件夹时。
- 要删除、清理、覆盖、保存 DWG 时。
- 看到 `failed` 或 `error_code` 时。
- 不知道任务 ID 是什么时。

## 一键图纸体检

如果只是想快速看一张测试图纸里有哪些图层、块、文字标注，可以让 AI 运行这个命令。它会一次完成三种统计，并生成一份总报告。

先预演：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli health-check .agent\tmp\sample-run
```

确认只匹配测试副本后，再执行：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli health-check .agent\tmp\sample-run --execute
```

成功后会生成：

```text
.agent\tmp\sample-run\CAD_REPORT_SUMMARY.md
```

当前测试副本已经验证成功：图层 12，图层对象 93，普通块参照 1，文字/标注对象 24，外参/图片/底图引用 4，图框标题栏候选 0。以后正式处理客户图纸时，仍然先让 AI 复制副本、dry-run、确认备份，再执行。
## 当前 AutoCAD 图纸 LISP 测试

这个入口用于验证“AutoCAD 已打开的当前图纸”能不能收到 LISP。它不是批量处理，不会通过 MCP 真实执行。

先 dry-run：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli current-lisp --script toolbox\plugins\current_smoke\main.lsp
```

dry-run 会返回：

- `task_id`：本次任务编号。
- `wrapper_path`：AI 自动生成的包装 LISP。
- `completion_marker`：AutoCAD 执行完成后应写入的 `result.json`。
- `command`：实际会发送给 AutoCAD 的命令。

确认 AutoCAD 已打开并且当前有 DWG 后，再执行：

```cmd
set PYTHONPATH=src
C:\Users\YANG\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -m yang_cad_agent.cli current-lisp --script toolbox\plugins\current_smoke\main.lsp --execute
```

结果判断：

- `status: completed`：AutoCAD 执行完并写入完成标记。
- `status: sent_unconfirmed`：命令已发送给 AutoCAD，但 CLI 暂时没看到完成标记。检查 AutoCAD 命令行和 `completion_marker` 路径。
- `status: failed`：执行失败，复制 `task_id` 让 AI 运行 `task_error_detail` 排障。
