# 小白操作文档

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
